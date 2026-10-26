using System.Globalization;
using System.Text.Json.Nodes;
using TestService.Api.Emulators.Outcomes;
using TestService.Api.Emulators.Transactions;
using static TestService.Api.Emulators.Praxis.PraxisResponses;

namespace TestService.Api.Emulators.Praxis;

/// <summary>
/// The Praxis "Test E-Wallet" endpoints: <c>direct/apm</c> (sale and payout), <c>direct/wallet-token</c> (payout),
/// the gateway lookups, <c>agent/manage-withdrawal-request</c>, and the e-wallet simulation pages
/// (<c>pages/apm-request/{tid}</c> "Please wait" -> <c>pages/apm/{tid}</c> -> <c>pages/apm/{tid}/response?status=</c>,
/// and <c>pages/payout/{tid}</c>). <c>agent/find-transaction</c> and notifications are shared with the card flows.
/// </summary>
public partial class PraxisEmulator
{
    private const string ApmRequestPage = "apm-request";
    private const string ApmPage = "apm";
    private const string PayoutPage = "payout";

    private async Task<EmulatorReply?> HandleEWalletAsync(EmulatorRequest request, string path, string[] segments, bool isPost, bool isGet)
    {
        switch (path)
        {
            case "direct/apm" when isPost:
                return await ApmAsync(request, byWalletToken: false);
            case "direct/wallet-token" when isPost:
                return await ApmAsync(request, byWalletToken: true);
            case "agent/get-available-gateways" when isPost:
                return await GatewayLookupAsync(request, GatewayList);
            case "agent/get-gateway-options" when isPost:
                return await GatewayLookupAsync(request, GatewayOptions);
            case "agent/manage-withdrawal-request" when isPost:
                return await ManageWithdrawalAsync(request);
        }

        if (!isGet || segments.Length < 3 || segments[0] != "pages" || !long.TryParse(segments[2], NumberStyles.None, CultureInfo.InvariantCulture, out var tid))
        {
            return null;
        }
        return (segments[1], segments.Length) switch
        {
            (ApmRequestPage, 3) => await ApmRequestPageAsync(request, tid),
            (ApmPage, 3) => await ApmPageAsync(request, tid),
            (ApmPage, 4) when segments[3] == "response" => await ApmResponseAsync(request, tid),
            (PayoutPage, 3) => await PayoutPageAsync(request, tid),
            _ => null
        };
    }

    // ------------------------------------------------------------------ endpoints

    /// <summary><c>direct/apm</c> sale or payout, or (<paramref name="byWalletToken"/>) a <c>direct/wallet-token</c> payout.</summary>
    private async Task<EmulatorReply> ApmAsync(EmulatorRequest request, bool byWalletToken)
    {
        var (body, merchant, rejection) = await AuthenticateAsync(request,
            byWalletToken ? PraxisSigner.ForWalletToken : PraxisSigner.ForApm);
        if (rejection is not null)
        {
            return rejection;
        }

        var type = Text(body, "transaction_type");
        var gateway = merchant!.Gateways.FirstOrDefault(g => g.Id == Text(body, "gateway"));
        var method = NullIfEmpty(gateway?.PaymentMethod) ?? PraxisWalletData.DefaultMethod;

        var errors = BlankErrors(body, ("transaction_type", "Transaction Type"), ("currency", "Currency"),
            ("amount", "Amount"), ("cid", "Cid"), ("order_id", "Order Id"));
        AddAmountError(body, errors);
        if (!byWalletToken)
        {
            if (type == "sale")
            {
                errors.AddRange(PraxisWalletData.ValidateSale(body?["wallet_data"], method));
            }
            else if (type == "payout")
            {
                errors.AddRange(PraxisWalletData.ValidatePayout(body?["wallet_data"], method));
            }
        }
        AddTimestampError(body, errors);

        if (errors.Count > 0)
        {
            return Reply(Error(400, string.Join(",", errors)));
        }
        if (byWalletToken ? type != "payout" : type is not ("sale" or "payout"))
        {
            return Reply(Error(1, $"Gateway does not support the transaction type ({type})"));
        }

        var now = Now;
        var amount = long.Parse(Text(body, "amount"), CultureInfo.InvariantCulture);
        var tx = EmulatedTransaction.Open(ProviderName, request.Environment, merchant.MerchantId, type, Text(body, "currency"), amount, now);
        tx.Tid = await _transactions.NextTidAsync();
        tx.OrderId = Text(body, "order_id");
        tx.NotificationUrl = NullIfEmpty(Text(body, "notification_url"));
        tx.ReturnUrl = NullIfEmpty(Text(body, "return_url"));
        tx.ProviderData[Keys.Channel] = EWalletChannel;
        FillSession(tx, body, merchant, defaultProcessor: PraxisWalletData.DefaultProcessor, defaultMethod: PraxisWalletData.DefaultMethod);

        if (byWalletToken)
        {
            await FillFromWalletTokenAsync(tx, body, merchant);
        }
        else
        {
            FillCustomer(tx, body, merchant);
            var walletData = body?["wallet_data"] as JsonObject;
            tx.ProviderData[Keys.WalletData] = walletData is null ? null : Serialize(walletData);
            tx.ProviderData[Keys.WalletToken] = PraxisWalletData.WalletToken(merchant.MerchantId, Text(body, "cid"),
                walletData is null ? null : PraxisSigner.Text(walletData, "email"));
        }

        IOutcome outcome;
        if (type == "sale")
        {
            tx.CaseName = PraxisEWalletCases.SaleCaseName;
            tx.RequiresAuthentication = true;
            tx.RedirectUrl = $"{PagesBase(request)}/{ApmRequestPage}/{tx.Tid}";
            outcome = PraxisEWalletCases.ForSale();
        }
        else
        {
            (tx.CaseName, outcome, var redirects) = PraxisEWalletCases.ForPayout(amount);
            if (redirects)
            {
                tx.RequiresAuthentication = true;
                tx.RedirectUrl = $"{PagesBase(request)}/{PayoutPage}/{tx.Tid}";
            }
        }

        var context = new OutcomeContext(now, merchant.TimeScale);
        outcome.Apply(tx, context);
        await _transactions.InsertAsync(tx);
        await EnqueueNotificationsAsync(merchant, context);

        return Signed(DirectProcess(tx, UnixNow), merchant);
    }

    private async Task<EmulatorReply> GatewayLookupAsync(EmulatorRequest request, Func<PraxisMerchant, long, JsonObject> render)
    {
        var (_, merchant, rejection) = await AuthenticateAgentAsync(request, PraxisSigner.ForGatewayLookup);
        if (rejection is not null)
        {
            return rejection;
        }

        // Filters (currency, country, gateway, transaction_type) are ignored: the sandbox answered the same list for sale and payout.
        return Signed(render(merchant!, UnixNow), merchant!);
    }

    /// <summary>
    /// <c>agent/manage-withdrawal-request</c>. Emulated payouts are processed at once and never "requested",
    /// so cancel and complete only ever answer the sandbox's refusals; nothing changes.
    /// </summary>
    private async Task<EmulatorReply> ManageWithdrawalAsync(EmulatorRequest request)
    {
        var (body, merchant, rejection) = await AuthenticateAgentAsync(request, PraxisSigner.ForManageWithdrawal);
        if (rejection is not null)
        {
            return rejection;
        }

        var intent = Text(body, "intent");
        if (intent is not ("cancel-withdrawal-request" or "complete-withdrawal-request"))
        {
            return Reply(Error(1, "Generic Error"));
        }

        var id = Text(body, "withdrawal_request_id");
        if (id.Length == 0)
        {
            return intent == "cancel-withdrawal-request"
                ? Reply(Error(400, "Withdrawal Request Id cannot be blank."))
                : Reply(Error(1, "Generic Error"));
        }

        var payout = long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tid)
            ? await LoadOwnedAsync(request.Environment, merchant!, tid)
            : null;
        if (payout is null || !IsEWallet(payout) || payout.TransactionType != "payout")
        {
            return Reply(Error(401, "Wrong credentials"));
        }

        if (intent == "cancel-withdrawal-request")
        {
            return Signed(Error(400, "Payout request is already processed"), merchant!);
        }
        if (Text(body, "gateway").Length == 0)
        {
            return Signed(Error(400, "The gateway field is required because the payment method is not Credit Card."), merchant!);
        }
        return Signed(Error(400,
            $"Payout request status must be \"requested\" in order to proceed with processing (currently \"{payout.Status.ToWire()}\")"), merchant!);
    }

    // ------------------------------------------------------------------ pages

    /// <summary>The "Please wait" page of a sale's <c>redirect_url</c>; its script loads the APM page.</summary>
    private async Task<EmulatorReply> ApmRequestPageAsync(EmulatorRequest request, long tid)
    {
        if (await LoadEWalletAsync(request.Environment, tid, "sale") is null)
        {
            return EmulatorReply.Message(StatusCodes.Status404NotFound, $"Unknown transaction {tid}");
        }
        return Html(PraxisSimulationPages.WaitPage($"{PagesBase(request)}/{ApmPage}/{tid}"));
    }

    /// <summary>
    /// The APM page: for the page-open trigger amounts it applies the outcome and redirects to <c>return_url</c>;
    /// otherwise it shows the five buttons, and from then on an unanswered page times out from <c>pending</c>
    /// instead of <c>pending_async</c>. 404 unknown tid, 409 when the sale no longer awaits its page.
    /// </summary>
    private async Task<EmulatorReply> ApmPageAsync(EmulatorRequest request, long tid)
    {
        if (await LoadEWalletAsync(request.Environment, tid, "sale") is null)
        {
            return EmulatorReply.Message(StatusCodes.Status404NotFound, $"Unknown transaction {tid}");
        }

        var applied = false;
        var result = await MutateAsync(tid, merchant: null, (tx, context) =>
        {
            if (!AwaitsApmPage(tx))
            {
                throw new InvalidTransactionTransitionException(tx.Status, "ApmPage");
            }
            var outcome = PraxisEWalletCases.PageOpen(tx.RequestedAmount);
            if (outcome is not null)
            {
                applied = true;
                outcome.Apply(tx, context);
                return true;
            }
            if (tx.Deadline is { FollowUp: PraxisEWalletCases.PageAbandoned } deadline)
            {
                tx.SetDeadline(new TransactionDeadline { DueAt = deadline.DueAt, FollowUp = PraxisEWalletCases.PageOpenedAbandoned });
                return true;
            }
            return false;
        }, environment: request.Environment);

        if (result.Transaction is null)
        {
            return EmulatorReply.Message(StatusCodes.Status404NotFound, $"Unknown transaction {tid}");
        }
        if (result.SaveConflict)
        {
            return EmulatorReply.Message(StatusCodes.Status409Conflict, SaveConflictMessage(tid));
        }
        if (result.Refused)
        {
            return EmulatorReply.Message(StatusCodes.Status409Conflict, $"Transaction {tid} is not awaiting its payment page");
        }
        return applied
            ? BackToMerchant(result.Transaction, result.Transaction.Status.ToWire())
            : Html(PraxisSimulationPages.ApmPage($"{PagesBase(request)}/{ApmPage}/{tid}/response"));
    }

    /// <summary>A page button: applies its outcome and redirects to <c>return_url</c>. 404 / 409 / 400 (unknown status).</summary>
    private async Task<EmulatorReply> ApmResponseAsync(EmulatorRequest request, long tid)
    {
        if (await LoadEWalletAsync(request.Environment, tid, "sale") is null)
        {
            return EmulatorReply.Message(StatusCodes.Status404NotFound, $"Unknown transaction {tid}");
        }

        request.Query.TryGetValue("status", out var status);
        var result = await MutateAsync(tid, merchant: null, (tx, context) =>
        {
            if (!AwaitsApmPage(tx))
            {
                throw new InvalidTransactionTransitionException(tx.Status, "ApmButton");
            }
            var outcome = PraxisEWalletCases.Button(tx.RequestedAmount, status)
                          ?? throw new ArgumentException($"Unknown e-wallet page status '{status}'.");
            outcome.Apply(tx, context);
            return true;
        }, environment: request.Environment);

        if (result.Transaction is null)
        {
            return EmulatorReply.Message(StatusCodes.Status404NotFound, $"Unknown transaction {tid}");
        }
        if (result.InvalidArgument is not null)
        {
            return EmulatorReply.Message(StatusCodes.Status400BadRequest, result.InvalidArgument);
        }
        if (result.SaveConflict)
        {
            return EmulatorReply.Message(StatusCodes.Status409Conflict, SaveConflictMessage(tid));
        }
        if (result.Refused)
        {
            return EmulatorReply.Message(StatusCodes.Status409Conflict, $"Transaction {tid} is not awaiting its payment page");
        }
        return BackToMerchant(result.Transaction, status ?? string.Empty);
    }

    /// <summary>The payout page of payout 300: a link back to the merchant; it changes nothing (sandbox).</summary>
    private async Task<EmulatorReply> PayoutPageAsync(EmulatorRequest request, long tid)
    {
        var payout = await LoadEWalletAsync(request.Environment, tid, "payout");
        return payout is null
            ? EmulatorReply.Message(StatusCodes.Status404NotFound, $"Unknown transaction {tid}")
            : Html(PraxisSimulationPages.PayoutPage(payout.ReturnUrl));
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// A sale waits for its page while initialized, until its deadline (none when the merchant's time scale is 0).
    /// </summary>
    private static bool AwaitsApmPage(EmulatedTransaction tx) =>
        IsEWallet(tx) && tx.TransactionType == "sale" && tx.Status == TransactionStatus.Initialized
        && (tx.Deadline is null || tx.Deadline.FollowUp is PraxisEWalletCases.PageAbandoned or PraxisEWalletCases.PageOpenedAbandoned);

    private async Task<EmulatedTransaction?> LoadEWalletAsync(string environment, long tid, string transactionType)
    {
        var tx = await _transactions.GetAsync(tid);
        return tx is not null && tx.Provider == ProviderName && tx.Environment == environment && IsEWallet(tx)
               && tx.TransactionType == transactionType
            ? tx
            : null;
    }

    /// <summary>A <c>direct/wallet-token</c> payout: wallet data and customer come from the merchant's newest sale with that token.</summary>
    private async Task FillFromWalletTokenAsync(EmulatedTransaction tx, JsonNode? body, PraxisMerchant merchant)
    {
        var walletToken = Text(body, "wallet_token");
        var source = walletToken.Length == 0 ? null : await _transactions.FindByWalletTokenAsync(tx.Environment, merchant.MerchantId, walletToken);
        foreach (var key in new[] { Keys.Country, Keys.FirstName, Keys.LastName, Keys.CustomerToken, Keys.WalletData })
        {
            tx.ProviderData[key] = source?.Data(key);
        }
        tx.ProviderData[Keys.WalletToken] = NullIfEmpty(walletToken);
        tx.ProviderData[Keys.CustomerToken] = NullIfEmpty(Text(body, "customer_token")) ?? tx.Data(Keys.CustomerToken);
        if (body?["customer_data"] is JsonObject)
        {
            FillCustomer(tx, body, merchant);
        }
    }

    private static string PagesBase(EmulatorRequest request) =>
        $"{request.BaseUrl.TrimEnd('/')}/emulators/{ProviderName}/{request.Environment}/pages";

    private static EmulatorReply Html(string body) => new() { ContentType = "text/html; charset=UTF-8", Body = body };

    /// <summary>302 to the transaction's <c>return_url</c>, or a plain "done" page without one.</summary>
    private static EmulatorReply BackToMerchant(EmulatedTransaction tx, string status) =>
        string.IsNullOrWhiteSpace(tx.ReturnUrl)
            ? Html(PraxisSimulationPages.ApmDone(status))
            : new EmulatorReply
            {
                StatusCode = StatusCodes.Status302Found,
                ContentType = "text/html; charset=UTF-8",
                Headers = { ["Location"] = tx.ReturnUrl }
            };
}
