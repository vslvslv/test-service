using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TestService.Api.Emulators.Outcomes;
using TestService.Api.Emulators.Transactions;
using TestService.Api.Models;
using TestService.Api.Services;
using static TestService.Api.Emulators.Praxis.PraxisResponses;

namespace TestService.Api.Emulators.Praxis;

/// <summary>
/// The Praxis emulator: request validation and signature checks, the card endpoints
/// (<c>api/direct-process</c>, <c>direct/refund</c>, <c>agent/find-transaction</c>,
/// <c>agent/manage-transaction</c>), the 3DS page, deadlines and signed notifications sent
/// through <see cref="ICallbackDelivery"/>. Every rejection is HTTP 200 with a Praxis status, as Praxis does.
/// The e-wallet endpoints and pages live in <c>PraxisEmulator.EWallet.cs</c>.
/// </summary>
public partial class PraxisEmulator : IProviderEmulator
{
    public const string ProviderName = "praxis";
    public const int MaxTimestampAgeSeconds = 60;
    private const int MaxSaveAttempts = 5;

    private const string DefaultCardProcessor = PraxisGatewayCatalog.DefaultCardProcessor;
    private const string DefaultCardMethod = PraxisGatewayCatalog.DefaultCardMethod;

    private static readonly (string Path, string Name)[] SaleCustomerFields =
    [
        ("customer_data.city", "City"),
        ("customer_data.address", "Address")
    ];

    private static readonly (string Path, string Name)[] SaleDeviceFields =
    [
        ("device_data.user_agent", "User Agent"),
        ("device_data.accept_header", "Accept Header"),
        ("device_data.language", "Language"),
        ("device_data.ip_address", "Ip Address"),
        ("device_data.color_depth", "Color Depth"),
        ("device_data.pixel_depth", "Pixel Depth"),
        ("device_data.pixel_ratio", "Pixel Ratio")
    ];

    private readonly IPraxisMerchantRepository _merchants;
    private readonly ITransactionRepository _transactions;
    private readonly ICallbackDelivery _delivery;
    private readonly TimeProvider _time;
    private readonly ILogger<PraxisEmulator> _logger;

    public PraxisEmulator(IPraxisMerchantRepository merchants, ITransactionRepository transactions, ICallbackDelivery delivery,
        TimeProvider time, ILogger<PraxisEmulator> logger)
    {
        _merchants = merchants;
        _transactions = transactions;
        _delivery = delivery;
        _time = time;
        _logger = logger;
    }

    public string Provider => ProviderName;

    public Task<bool> HasEnvironmentAsync(string environment) => _merchants.AnyInEnvironmentAsync(environment);

    public async Task<EmulatorReply> HandleAsync(EmulatorRequest request)
    {
        var path = request.Path.Trim('/');
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var isPost = string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase);
        var isGet = string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase);

        switch (path)
        {
            case "api/direct-process" when isPost:
                return await DirectProcessAsync(request);
            case "direct/refund" when isPost:
                return await RefundAsync(request);
            case "agent/find-transaction" when isPost:
                return await FindTransactionAsync(request);
            case "agent/manage-transaction" when isPost:
                return await ManageTransactionAsync(request);
        }

        if (await HandleEWalletAsync(request, path, segments, isPost, isGet) is { } ewallet)
        {
            return ewallet;
        }

        if (segments.Length >= 3 && segments[0] == "pages" && segments[1] == "3ds" && long.TryParse(segments[2], out var tid))
        {
            if (segments.Length == 3 && isGet)
            {
                return await ThreeDsPageAsync(request, tid);
            }
            if (segments.Length == 4 && segments[3] == "submit" && isPost)
            {
                return await ThreeDsSubmitAsync(request, tid);
            }
        }

        return EmulatorReply.Message(StatusCodes.Status404NotFound, $"Unknown Praxis endpoint: {request.Method} {path}");
    }

    /// <summary>Applies every deadline that is due now. Returns how many transactions changed.</summary>
    public async Task<int> ApplyDueDeadlinesAsync(CancellationToken cancellationToken = default)
    {
        var due = await _transactions.GetDueAsync(Now);
        var applied = 0;
        foreach (var transaction in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await MutateAsync(transaction.Tid, merchant: null, action: null);
            if (result.Changed)
            {
                applied++;
            }
        }
        return applied;
    }

    // ------------------------------------------------------------------ endpoints

    private async Task<EmulatorReply> DirectProcessAsync(EmulatorRequest request)
    {
        var (body, merchant, rejection) = await AuthenticateAsync(request, PraxisSigner.ForDirectProcess);
        if (rejection is not null)
        {
            return rejection;
        }

        var type = Text(body, "transaction_type");
        var usesToken = Text(body, "card_token").Length > 0;
        var errors = BlankErrors(body, ("transaction_type", "Transaction Type"), ("currency", "Currency"),
            ("amount", "Amount"), ("cid", "Cid"), ("order_id", "Order Id"));
        if (type == "sale")
        {
            errors.AddRange(BlankErrors(body, SaleCustomerFields));
            errors.AddRange(BlankErrors(body, SaleDeviceFields));
        }
        if (!usesToken && body?["card_data"] is not JsonObject)
        {
            errors.Add("Card Data cannot be blank.");
        }
        AddAmountError(body, errors);

        var timestamp = Text(body, "timestamp");
        var card = new Dictionary<string, string>();
        if (!usesToken && body?["card_data"] is JsonObject)
        {
            foreach (var field in new[] { "card_number", "card_exp", "cvv" })
            {
                if (PraxisCardCipher.TryDecrypt(Text(body, "card_data." + field), merchant!.Secret, timestamp, out var plain))
                {
                    card[field] = plain;
                }
                else
                {
                    errors.Add("Failed to decrypt " + field);
                }
            }
        }
        AddTimestampError(body, errors);

        if (errors.Count > 0)
        {
            return Reply(Error(400, string.Join(",", errors)));
        }
        if (type is not ("sale" or "payout"))
        {
            return Reply(Error(1, $"Gateway does not support the transaction type ({type})"));
        }

        var now = Now;
        var amount = long.Parse(Text(body, "amount"), CultureInfo.InvariantCulture);
        var tx = EmulatedTransaction.Open(ProviderName, request.Environment, merchant!.MerchantId, type, Text(body, "currency"), amount, now);
        tx.Tid = await _transactions.NextTidAsync();
        tx.OrderId = Text(body, "order_id");
        tx.NotificationUrl = NullIfEmpty(Text(body, "notification_url"));
        tx.ReturnUrl = NullIfEmpty(Text(body, "return_url"));
        FillSession(tx, body, merchant);

        if (usesToken)
        {
            await FillFromCardTokenAsync(tx, body, merchant);
        }
        else
        {
            FillCustomer(tx, body, merchant);
            FillCard(tx, merchant, card, Text(body, "customer_data.country"));
        }

        IOutcome outcome;
        if (type == "sale")
        {
            // A sale by card_token carries no CVV, so it falls to the default (declined) row.
            var saleCase = PraxisTestCases.ForSale(
                PraxisTestCases.TriggerCvv(card.GetValueOrDefault("card_number", string.Empty), card.GetValueOrDefault("cvv", string.Empty)),
                amount);
            tx.CaseName = saleCase.Name;
            tx.Is3d = saleCase.Is3d;
            tx.IsAsync = saleCase.Redirects;
            tx.RequiresAuthentication = saleCase.RequiresAuthentication;
            if (saleCase.Redirects)
            {
                tx.RedirectUrl = $"{request.BaseUrl.TrimEnd('/')}/emulators/{ProviderName}/{request.Environment}/pages/3ds/{tx.Tid}";
            }
            outcome = saleCase.Outcome;
        }
        else
        {
            (tx.CaseName, outcome) = PraxisTestCases.ForPayout(amount);
        }

        var context = new OutcomeContext(now, merchant.TimeScale);
        outcome.Apply(tx, context);
        await _transactions.InsertAsync(tx);
        await EnqueueNotificationsAsync(merchant, context);

        return Signed(DirectProcess(tx, UnixNow), merchant);
    }

    private async Task<EmulatorReply> RefundAsync(EmulatorRequest request)
    {
        var (body, merchant, rejection) = await AuthenticateAsync(request, PraxisSigner.ForRefund);
        if (rejection is not null)
        {
            return rejection;
        }

        var errors = BlankErrors(body, ("transaction_type", "Transaction Type"), ("currency", "Currency"),
            ("amount", "Amount"), ("cid", "Cid"), ("order_id", "Order Id"), ("reference_id", "Reference Id"));
        AddAmountError(body, errors);
        AddTimestampError(body, errors);
        if (errors.Count > 0)
        {
            return Reply(Error(400, string.Join(",", errors)));
        }

        var reference = long.TryParse(Text(body, "reference_id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var referenceId)
            ? await LoadOwnedAsync(request.Environment, merchant!, referenceId)
            : null;
        if (reference is null)
        {
            return Reply(Error(401, "Wrong credentials"));
        }
        if (IsEWallet(reference))
        {
            // The Test E-Wallet gateway refunds nothing; the reply is signed, as captured (two spaces, as Praxis).
            return Signed(Error(1, "Gateway for reference_id  does not allow refunding"), merchant!);
        }

        var now = Now;
        var amount = long.Parse(Text(body, "amount"), CultureInfo.InvariantCulture);
        var tx = EmulatedTransaction.Open(ProviderName, request.Environment, merchant!.MerchantId, "refund", Text(body, "currency"), amount, now);
        tx.Tid = await _transactions.NextTidAsync();
        tx.OrderId = Text(body, "order_id");
        tx.ReferenceId = reference.Tid;
        tx.NotificationUrl = NullIfEmpty(Text(body, "notification_url"));
        tx.ReturnUrl = NullIfEmpty(Text(body, "return_url"));
        foreach (var (key, value) in reference.ProviderData)
        {
            tx.ProviderData[key] = value;
        }
        FillSession(tx, body, merchant, fallbackGateway: reference.Data(Keys.Gateway));
        tx.ProviderData[Keys.WithdrawalRequestId] = NullIfEmpty(Text(body, "withdrawal_request_id"));

        var (caseName, outcome) = PraxisTestCases.ForRefund(amount);
        tx.CaseName = caseName;
        var context = new OutcomeContext(now, merchant.TimeScale);
        outcome.Apply(tx, context);
        await _transactions.InsertAsync(tx);
        await EnqueueNotificationsAsync(merchant, context);

        return Signed(Refund(tx, UnixNow), merchant);
    }

    private async Task<EmulatorReply> FindTransactionAsync(EmulatorRequest request)
    {
        var (body, merchant, rejection) = await AuthenticateAgentAsync(request, PraxisSigner.ForFindTransaction);
        if (rejection is not null)
        {
            return rejection;
        }

        if (!TryReadTid(body, out var tid, out var tidRejection))
        {
            return tidRejection!;
        }
        if (await LoadOwnedAsync(request.Environment, merchant!, tid) is null)
        {
            return Reply(Error(401, "Wrong credentials"));
        }

        var result = await MutateAsync(tid, merchant, (tx, context) =>
        {
            var followUp = PraxisTestCases.FollowUp(tx.TakeStatusCheckFollowUp());
            followUp?.Apply(tx, context);
            return followUp is not null;
        });
        if (result.Transaction is null)
        {
            return Reply(Error(401, "Wrong credentials"));
        }

        return Signed(FindTransaction(result.Transaction, UnixNow), merchant!);
    }

    private async Task<EmulatorReply> ManageTransactionAsync(EmulatorRequest request)
    {
        var (body, merchant, rejection) = await AuthenticateAgentAsync(request, PraxisSigner.ForManageTransaction);
        if (rejection is not null)
        {
            return rejection;
        }

        if (!TryReadTid(body, out var tid, out var tidRejection))
        {
            return tidRejection!;
        }
        var intent = Text(body, "intent");
        if (intent.Length == 0)
        {
            return Reply(Error(400, "Intent cannot be blank."));
        }
        if (await LoadOwnedAsync(request.Environment, merchant!, tid) is null)
        {
            return Reply(Error(401, "Wrong credentials"));
        }

        Func<EmulatedTransaction, IOutcome>? select = intent switch
        {
            "capture-authorization" => tx => PraxisTestCases.ForCapture(tx.RequestedAmount),
            "cancel-authorization" => _ => PraxisTestCases.ForCancel(),
            _ => null
        };
        if (select is null)
        {
            return Reply(Error(1, "Generic Error"));
        }

        var result = await MutateAsync(tid, merchant, (tx, context) =>
        {
            if (tx.Status != TransactionStatus.Authorized)
            {
                // Capture and cancel apply only to an authorized sale.
                throw new InvalidTransactionTransitionException(tx.Status, intent);
            }
            select(tx).Apply(tx, context);
            return true;
        });
        if (result.Transaction is null)
        {
            return Reply(Error(401, "Wrong credentials"));
        }
        if (result.Refused || result.SaveConflict)
        {
            return Reply(Error(1, "Generic Error"));
        }

        return Signed(ManageTransaction(result.Transaction, UnixNow), merchant!);
    }

    private async Task<EmulatorReply> ThreeDsPageAsync(EmulatorRequest request, long tid)
    {
        var result = await MutateAsync(tid, merchant: null, action: null, environment: request.Environment);
        if (result.Transaction is null)
        {
            return EmulatorReply.Message(StatusCodes.Status404NotFound, $"Unknown transaction {tid}");
        }
        if (!AwaitsThreeDsDecision(result.Transaction))
        {
            return EmulatorReply.Message(StatusCodes.Status409Conflict, $"Transaction {tid} is not awaiting 3DS authentication");
        }

        var submitUrl = $"{request.BaseUrl.TrimEnd('/')}/emulators/{ProviderName}/{request.Environment}/pages/3ds/{tid}/submit";
        return new EmulatorReply
        {
            ContentType = "text/html; charset=UTF-8",
            Body = PraxisSimulationPages.ThreeDsPage(submitUrl)
        };
    }

    private async Task<EmulatorReply> ThreeDsSubmitAsync(EmulatorRequest request, long tid)
    {
        request.Query.TryGetValue("status", out var status);
        request.Form.TryGetValue("error_code", out var errorCode);

        var result = await MutateAsync(tid, merchant: null, (tx, context) =>
        {
            if (!AwaitsThreeDsDecision(tx))
            {
                throw new InvalidTransactionTransitionException(tx.Status, "ThreeDsDecision");
            }
            var outcome = PraxisTestCases.ThreeDsDecision(tx.CaseName, status, errorCode)
                          ?? throw new ArgumentException($"Unknown 3DS status '{status}'.");
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
            return EmulatorReply.Message(StatusCodes.Status409Conflict, $"Transaction {tid} is not awaiting 3DS authentication");
        }

        var returnUrl = result.Transaction.ReturnUrl;
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return new EmulatorReply { ContentType = "text/html; charset=UTF-8", Body = PraxisSimulationPages.Done(status ?? string.Empty) };
        }
        return new EmulatorReply
        {
            StatusCode = StatusCodes.Status302Found,
            ContentType = "text/html; charset=UTF-8",
            Headers = { ["Location"] = returnUrl }
        };
    }

    // ------------------------------------------------------------------ authentication and validation

    private async Task<(JsonNode? Body, PraxisMerchant? Merchant, EmulatorReply? Rejection)> AuthenticateAsync(
        EmulatorRequest request, Func<JsonNode?, string, string> sign)
    {
        var body = Parse(request.Body);
        var merchantId = Text(body, "merchant_id");
        var merchant = merchantId.Length == 0 ? null : await _merchants.FindAsync(request.Environment, merchantId);
        if (merchant is null || !string.Equals(merchant.ApplicationKey, Text(body, "application_key"), StringComparison.Ordinal))
        {
            return (body, null, Reply(Error(401, "User not found")));
        }
        if (!PraxisSigner.Matches(request.Header(PraxisSigner.HeaderName), sign(body, merchant.Secret)))
        {
            return (body, merchant, Reply(ValidationError("merchant_id", "Invalid signature")));
        }
        return (body, merchant, null);
    }

    /// <summary><c>agent/*</c>: like <see cref="AuthenticateAsync"/>, and an expired timestamp is a <c>status 1</c> validation error.</summary>
    private async Task<(JsonNode? Body, PraxisMerchant? Merchant, EmulatorReply? Rejection)> AuthenticateAgentAsync(
        EmulatorRequest request, Func<JsonNode?, string, string> sign)
    {
        var authenticated = await AuthenticateAsync(request, sign);
        if (authenticated.Rejection is null && TimestampError(authenticated.Body) is { } timestampError)
        {
            return (authenticated.Body, authenticated.Merchant, Reply(ValidationError("timestamp", timestampError)));
        }
        return authenticated;
    }

    private string? TimestampError(JsonNode? body)
    {
        if (!long.TryParse(Text(body, "timestamp"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestamp))
        {
            return "Timestamp cannot be blank.";
        }
        var age = UnixNow - timestamp;
        return age > MaxTimestampAgeSeconds
            ? $"Timestamp expired: {age}s old, should be at most {MaxTimestampAgeSeconds}s old (required with merchant_id)"
            : null;
    }

    private void AddTimestampError(JsonNode? body, List<string> errors)
    {
        if (TimestampError(body) is { } error)
        {
            errors.Add(error);
        }
    }

    private static void AddAmountError(JsonNode? body, List<string> errors)
    {
        var amount = Text(body, "amount");
        if (amount.Length > 0 && !long.TryParse(amount, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            errors.Add("Amount must be an integer.");
        }
    }

    private static List<string> BlankErrors(JsonNode? body, params (string Path, string Name)[] fields) =>
        fields.Where(f => string.IsNullOrWhiteSpace(Text(body, f.Path))).Select(f => $"{f.Name} cannot be blank.").ToList();

    private static bool TryReadTid(JsonNode? body, out long tid, out EmulatorReply? rejection)
    {
        rejection = null;
        var text = Text(body, "tid");
        if (text.Length == 0)
        {
            tid = 0;
            rejection = Reply(Error(400, "Tid cannot be blank."));
            return false;
        }
        if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out tid))
        {
            rejection = Reply(Error(401, "Wrong credentials"));
            return false;
        }
        return true;
    }

    /// <summary>The transaction if it exists and belongs to this merchant in this environment.</summary>
    private async Task<EmulatedTransaction?> LoadOwnedAsync(string environment, PraxisMerchant merchant, long tid)
    {
        var tx = await _transactions.GetAsync(tid);
        return tx is not null && tx.Provider == ProviderName && tx.Environment == environment && tx.MerchantId == merchant.MerchantId
            ? tx
            : null;
    }

    private static bool AwaitsThreeDsDecision(EmulatedTransaction tx) =>
        tx.Status == TransactionStatus.Initialized && tx.Deadline?.FollowUp == PraxisTestCases.ThreeDsAbandoned;

    // ------------------------------------------------------------------ state changes

    /// <summary>
    /// <c>SaveConflict</c>: the change could not be saved after <see cref="MaxSaveAttempts"/> conflicting saves;
    /// <c>Transaction</c> is then the stored (unchanged) transaction.
    /// </summary>
    private sealed record MutationResult(EmulatedTransaction? Transaction, bool Changed, bool Refused, string? InvalidArgument,
        bool SaveConflict = false);

    private static string SaveConflictMessage(long tid) =>
        $"Transaction {tid}: the decision could not be saved because of concurrent updates; try again";

    /// <summary>
    /// Loads the transaction, applies a due deadline, then <paramref name="action"/> (true when it changed something),
    /// and saves with optimistic concurrency, retrying on a conflict. Notifications are enqueued only after a successful save.
    /// A refused transition (<see cref="InvalidTransactionTransitionException"/>) is reported, not thrown.
    /// </summary>
    private async Task<MutationResult> MutateAsync(long tid, PraxisMerchant? merchant,
        Func<EmulatedTransaction, OutcomeContext, bool>? action, string? environment = null)
    {
        for (var attempt = 1; ; attempt++)
        {
            var tx = await _transactions.GetAsync(tid);
            if (tx is null || tx.Provider != ProviderName || (environment is not null && tx.Environment != environment))
            {
                return new MutationResult(null, false, false, null);
            }

            merchant ??= await _merchants.FindAsync(tx.Environment, tx.MerchantId);
            var timeScale = merchant?.TimeScale ?? 1.0;
            var contexts = new List<OutcomeContext>();
            var changed = ApplyDueDeadlines(tx, timeScale, contexts);

            var refused = false;
            string? invalidArgument = null;
            if (action is not null)
            {
                var context = new OutcomeContext(Now, timeScale);
                contexts.Add(context);
                try
                {
                    changed |= action(tx, context);
                }
                catch (InvalidTransactionTransitionException)
                {
                    refused = true;
                }
                catch (ArgumentException ex)
                {
                    invalidArgument = ex.Message;
                }
            }

            if (!changed)
            {
                return new MutationResult(tx, false, refused, invalidArgument);
            }
            if (refused || invalidArgument is not null)
            {
                // Persist only what the due deadline changed: reload and re-apply it alone.
                return await SaveDeadlinesOnlyAsync(tid, merchant, refused, invalidArgument);
            }

            if (await _transactions.TrySaveAsync(tx))
            {
                foreach (var context in contexts)
                {
                    await EnqueueNotificationsAsync(merchant, context);
                }
                return new MutationResult(tx, true, false, null);
            }

            if (attempt >= MaxSaveAttempts)
            {
                _logger.LogWarning("Praxis transaction {Tid}: gave up after {Attempts} conflicting saves", tid, attempt);
                return new MutationResult(await _transactions.GetAsync(tid), false, Refused: false, null, SaveConflict: true);
            }
        }
    }

    private async Task<MutationResult> SaveDeadlinesOnlyAsync(long tid, PraxisMerchant? merchant, bool refused, string? invalidArgument)
    {
        var result = await MutateAsync(tid, merchant, action: null);
        return result with { Refused = refused, InvalidArgument = invalidArgument };
    }

    /// <summary>Applies the deadline follow-ups that are due, each at its own due time.</summary>
    private bool ApplyDueDeadlines(EmulatedTransaction tx, double timeScale, List<OutcomeContext> contexts)
    {
        var changed = false;
        var now = Now;
        while (tx.Deadline is { } deadline && now >= deadline.DueAt)
        {
            var followUpName = tx.TakeDueDeadline(now);
            changed = true;
            var context = new OutcomeContext(deadline.DueAt, timeScale);
            contexts.Add(context);
            var followUp = PraxisTestCases.FollowUp(followUpName);
            if (followUp is null)
            {
                _logger.LogWarning("Praxis transaction {Tid}: unknown follow-up {FollowUp}", tx.Tid, followUpName);
                continue;
            }
            followUp.Apply(tx, context);
        }
        return changed;
    }

    private async Task EnqueueNotificationsAsync(PraxisMerchant? merchant, OutcomeContext context)
    {
        if (merchant is null || merchant.DropNotifications)
        {
            return;
        }

        var now = _time.GetUtcNow();
        foreach (var notification in context.Notifications)
        {
            var snapshot = notification.Snapshot;
            if (string.IsNullOrWhiteSpace(snapshot.NotificationUrl))
            {
                continue;
            }

            var dueAt = new DateTimeOffset(DateTime.SpecifyKind(context.Now, DateTimeKind.Utc)) + notification.Delay;
            if (dueAt < now)
            {
                dueAt = now;
            }

            var body = Notification(snapshot, merchant, dueAt.ToUnixTimeSeconds());
            await _delivery.EnqueueAsync(new CallbackJob
            {
                Source = ProviderName,
                Environment = snapshot.Environment,
                CorrelationId = snapshot.Tid.ToString(CultureInfo.InvariantCulture),
                Url = snapshot.NotificationUrl,
                Method = "POST",
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Content-Type"] = "application/json",
                    [PraxisSigner.HeaderName] = PraxisSigner.ForNotification(body, merchant.Secret)
                },
                Body = Serialize(body),
                DueAt = dueAt
            });
        }
    }

    // ------------------------------------------------------------------ transaction data

    private static void FillSession(EmulatedTransaction tx, JsonNode? body, PraxisMerchant merchant, string? fallbackGateway = null,
        string defaultProcessor = DefaultCardProcessor, string defaultMethod = DefaultCardMethod)
    {
        var gatewayId = NullIfEmpty(Text(body, "gateway")) ?? fallbackGateway;
        var gateway = merchant.Gateways.FirstOrDefault(g => g.Id == gatewayId);
        tx.ProviderData[Keys.Gateway] = gatewayId;
        tx.ProviderData[Keys.PaymentProcessor] = NullIfEmpty(gateway?.PaymentProcessor) ?? tx.Data(Keys.PaymentProcessor) ?? defaultProcessor;
        tx.ProviderData[Keys.PaymentMethod] = NullIfEmpty(gateway?.PaymentMethod) ?? tx.Data(Keys.PaymentMethod) ?? defaultMethod;
        tx.ProviderData[Keys.Cid] = NullIfEmpty(Text(body, "cid"));
        tx.ProviderData[Keys.AuthToken] = Guid.NewGuid().ToString("N");
        tx.ProviderData[Keys.Variable1] = NullIfEmpty(Text(body, "variable1"));
        tx.ProviderData[Keys.Variable2] = NullIfEmpty(Text(body, "variable2"));
        tx.ProviderData[Keys.Variable3] = NullIfEmpty(Text(body, "variable3"));
        tx.ProviderData[Keys.WithdrawalRequestId] = null;
    }

    private static void FillCustomer(EmulatedTransaction tx, JsonNode? body, PraxisMerchant merchant)
    {
        tx.ProviderData[Keys.CustomerToken] = NullIfEmpty(Text(body, "customer_token"))
                                              ?? Token(merchant.MerchantId, "customer", Text(body, "cid"));
        tx.ProviderData[Keys.Country] = NullIfEmpty(Text(body, "customer_data.country"));
        tx.ProviderData[Keys.FirstName] = NullIfEmpty(Text(body, "customer_data.first_name"));
        tx.ProviderData[Keys.LastName] = NullIfEmpty(Text(body, "customer_data.last_name"));
    }

    private static void FillCard(EmulatedTransaction tx, PraxisMerchant merchant, IReadOnlyDictionary<string, string> card, string country)
    {
        var number = card["card_number"];
        tx.ProviderData[Keys.CardToken] = Token(merchant.MerchantId, "card", number);
        tx.ProviderData[Keys.CardType] = number.Length == 0 ? null : number[0] switch
        {
            '4' => "VISA",
            '5' => "MASTERCARD",
            '3' => "AMEX",
            _ => "UNKNOWN"
        };
        tx.ProviderData[Keys.CardNumber] = number.Length >= 10 ? $"{number[..6]}******{number[^4..]}" : new string('*', number.Length);
        tx.ProviderData[Keys.CardExp] = card["card_exp"];
        tx.ProviderData[Keys.CardIssuerName] = "EMULATOR TEST BANK";
        tx.ProviderData[Keys.CardIssuerCountry] = NullIfEmpty(country);
    }

    /// <summary>A payout by <c>card_token</c>: card and customer come from the merchant's earlier transaction with that token.</summary>
    private async Task FillFromCardTokenAsync(EmulatedTransaction tx, JsonNode? body, PraxisMerchant merchant)
    {
        var cardToken = Text(body, "card_token");
        var source = cardToken.Length == 0 ? null : await _transactions.FindByCardTokenAsync(tx.Environment, merchant.MerchantId, cardToken);
        foreach (var key in new[] { Keys.Country, Keys.FirstName, Keys.LastName, Keys.CardType, Keys.CardNumber, Keys.CardExp,
                     Keys.CardIssuerName, Keys.CardIssuerCountry, Keys.CustomerToken })
        {
            tx.ProviderData[key] = source?.Data(key);
        }
        tx.ProviderData[Keys.CardToken] = NullIfEmpty(cardToken);
        tx.ProviderData[Keys.CustomerToken] = NullIfEmpty(Text(body, "customer_token")) ?? tx.Data(Keys.CustomerToken);
        if (body?["customer_data"] is JsonObject)
        {
            FillCustomer(tx, body, merchant);
        }
    }

    // ------------------------------------------------------------------ helpers

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private long UnixNow => _time.GetUtcNow().ToUnixTimeSeconds();

    private static string Text(JsonNode? body, string path) => PraxisSigner.Text(body, path);

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static JsonNode? Parse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }
        try
        {
            return JsonNode.Parse(body) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A stable 32-character token derived from the merchant and a value (customer tokens, card tokens).</summary>
    private static string Token(string merchantId, string kind, string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{merchantId}|{kind}|{value}")))[..32];

    private static EmulatorReply Reply(JsonNode body) => new() { Body = Serialize(body) };

    private static EmulatorReply Signed(JsonNode body, PraxisMerchant merchant) => new()
    {
        Body = Serialize(body),
        Headers = { [PraxisSigner.HeaderName] = PraxisSigner.ForResponse(body, merchant.Secret) }
    };
}
