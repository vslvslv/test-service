using System.Text.Json.Nodes;
using TestService.Api.Emulators;
using TestService.Api.Emulators.Praxis;
using TestService.Api.Emulators.Transactions;
using TestService.Tests.Praxis;
using static TestService.Unit.Emulators.PraxisEmulatorHarness;

namespace TestService.Unit.Emulators;

/// <summary>
/// Every row of the Test E-Wallet case table through <see cref="PraxisEmulator"/> (fake clock, in-memory stores):
/// sale page chain and buttons, page-open amounts, payouts, wallet-token payouts, wallet_data rules,
/// manage-withdrawal-request, e-wallet refunds, gateway lookups and the page edge cases.
/// </summary>
[TestFixture]
public class PraxisEWalletCaseTests
{
    private const string TimedOut = "Transaction timed out. Old status pending. New status declined";
    private const string TimedOutPendingAsync = "Transaction timed out. Old status pending_async. New status declined";
    private const string PayoutSuccess = "[TEST] Payout capture successfully";

    private static readonly string[] PaymentKeys = ["name", "email", "phone", "customer_country", "zip", "dqa_t", "test", "ip_address", "QA", "testNumber"];
    private static readonly string[] WithdrawalKeys = ["name", "email", "phone", "customer_country", "ip_address", "any_field"];

    private static async Task<JsonNode> SaleAsync(PraxisEmulatorHarness harness, long amount, string? returnUrl = ReturnUrl)
    {
        var reply = await harness.ApmAsync("sale", amount, returnUrl: returnUrl);
        var response = reply.Json()!;
        Assert.That(Str(response, "status"), Is.EqualTo("0"), reply.Body);
        return response;
    }

    private static async Task<JsonNode> PayoutAsync(PraxisEmulatorHarness harness, long amount)
    {
        var reply = await harness.ApmAsync("payout", amount);
        var response = reply.Json()!;
        Assert.Multiple(() =>
        {
            Assert.That(Str(response, "status"), Is.EqualTo("0"), reply.Body);
            Assert.That(reply.Headers[PraxisSigner.HeaderName], Is.EqualTo(PraxisSigner.ForResponse(response, harness.Merchant.Secret)));
        });
        return response;
    }

    private static List<string> Keys(JsonNode? node) => node is JsonObject obj ? obj.Select(p => p.Key).ToList() : [];

    private static async Task<int> StoredCountAsync(PraxisEmulatorHarness harness) =>
        (await harness.Transactions.QueryAsync(Env, null, null)).Count;

    // ------------------------------------------------------------------ sale: first reply and page chain

    [Test]
    public async Task Sale_FirstReply_InitializedWithRedirectToWaitPage()
    {
        var harness = new PraxisEmulatorHarness();
        var reply = await harness.ApmAsync("sale", 20000);
        var response = reply.Json()!;
        var tid = Tid(response);
        var expected = $"http://localhost/emulators/praxis/{Env}/pages/apm-request/{tid}";
        var transaction = response["transaction"]!.AsObject();

        Assert.Multiple(() =>
        {
            Assert.That(Str(response, "status"), Is.EqualTo("0"));
            Assert.That(reply.Headers[PraxisSigner.HeaderName], Is.EqualTo(PraxisSigner.ForResponse(response, harness.Merchant.Secret)));
            Assert.That(Str(response, "transaction.transaction_status"), Is.EqualTo("initialized"));
            Assert.That(Str(response, "transaction.status_code"), Is.EqualTo("0"));
            Assert.That(Str(response, "transaction.status_details"), Is.EqualTo("No status details"));
            Assert.That(Str(response, "redirect_url"), Is.EqualTo(expected));
            Assert.That(Str(response, "transaction.redirect_url"), Is.EqualTo(expected));
            Assert.That(transaction.ContainsKey("is_async") && transaction["is_async"] is null, Is.True, "is_async: null");
            Assert.That(transaction.ContainsKey("card") && transaction["card"] is null, Is.True, "card: null");
            Assert.That(transaction["wallet"], Is.Null);
            Assert.That(transaction["cascade_level"], Is.Null);
            Assert.That(transaction["processed_amount"], Is.Null);
            Assert.That(Str(response, "transaction.payment_method"), Is.EqualTo("altbankwire"));
            Assert.That(Str(response, "transaction.payment_processor"), Is.EqualTo("Test E-Wallet"));
            Assert.That(Str(response, "transaction.currency"), Is.EqualTo("EUR"));
            Assert.That(Str(response, "session.processed_amount"), Is.EqualTo("0"));
            Assert.That(Str(response, "session.processed_currency"), Is.EqualTo("EUR"));
            Assert.That(Str(response, "session.session_status"), Is.EqualTo("open"));
            Assert.That(Str(response, "session.intent"), Is.EqualTo("payment"));
            Assert.That(Str(response, "customer.country"), Is.EqualTo("BG"));
            Assert.That(harness.Delivery.Jobs, Is.Empty);
        });
    }

    [Test]
    public async Task WaitPage_ScriptLoadsTheApmPage()
    {
        var harness = new PraxisEmulatorHarness();
        var tid = Tid(await SaleAsync(harness, 20000));

        var page = await harness.GetPageAsync($"pages/apm-request/{tid}");

        Assert.Multiple(() =>
        {
            Assert.That(page.StatusCode, Is.EqualTo(200));
            Assert.That(page.ContentType, Does.StartWith("text/html"));
            Assert.That(page.Body, Does.Contain("Please wait"));
            Assert.That(page.Body, Does.Contain($"\"http://emulator.test/emulators/praxis/{Env}/pages/apm/{tid}\""));
        });
    }

    [Test]
    public async Task ApmPage_OtherAmount_ShowsFiveButtonsAndChangesNothing()
    {
        var harness = new PraxisEmulatorHarness();
        var tid = Tid(await SaleAsync(harness, 20000));

        var page = await harness.GetPageAsync($"pages/apm/{tid}");
        var found = await harness.FindAsync(tid);

        Assert.Multiple(() =>
        {
            Assert.That(page.StatusCode, Is.EqualTo(200));
            Assert.That(page.Body, Does.Contain($"\"http://emulator.test/emulators/praxis/{Env}/pages/apm/{tid}/response\""));
            foreach (var status in new[] { "approved", "pending", "declined", "cancelled", "verification" })
            {
                Assert.That(page.Body, Does.Contain($"setStatus('{status}')"));
            }
            Assert.That(Str(found, "transaction.transaction_status"), Is.EqualTo("initialized"));
        });
    }

    // ------------------------------------------------------------------ page-open amounts

    // amount, status, code, details, final amount, processed amount, wallet with the ten payment keys
    private static readonly object?[][] PageOpenRows =
    [
        [48700L, "rejected", "NoMoneyCode", "test Insufficient funds msg APM!", 48700L, null, false],
        [99900L, "rejected", "0", "[TEST] Insufficient funds [TEST CASE - amount 999]", 99900L, null, false],
        [107800L, "approved", "0", "[TEST] Change amount less than 0.4%", 108000L, 108000L, true],
        [107900L, "approved", "0", "[TEST] Change amount between 0.4% and 10%", 115400L, 115400L, true],
        [33400L, "verification", "0", "No status details", 26720L, null, true]
    ];

    [TestCaseSource(nameof(PageOpenRows))]
    public async Task PageOpen_TriggerAmount_AppliesOutcomeAndRedirectsToReturnUrl(long amount, string status, string code, string details,
        long finalAmount, long? processedAmount, bool paymentWallet)
    {
        var harness = new PraxisEmulatorHarness();
        var tid = Tid(await SaleAsync(harness, amount));

        var page = await harness.GetPageAsync($"pages/apm/{tid}");
        var found = await harness.FindAsync(tid);

        Assert.Multiple(() =>
        {
            Assert.That(page.StatusCode, Is.EqualTo(302));
            Assert.That(page.Headers["Location"], Is.EqualTo(ReturnUrl));
            Assert.That(Str(found, "transaction.transaction_status"), Is.EqualTo(status));
            Assert.That(Str(found, "transaction.status_code"), Is.EqualTo(code));
            Assert.That(Str(found, "transaction.status_details"), Is.EqualTo(details));
            Assert.That(found["transaction"]!["amount"]!.GetValue<long>(), Is.EqualTo(finalAmount));
            Assert.That(found["transaction"]!["processed_amount"]?.GetValue<long>(), Is.EqualTo(processedAmount));
            Assert.That(Str(found, "transaction.is_3d"), Is.EqualTo("0"));
            Assert.That(found["transaction"]!.AsObject().ContainsKey("is_async"), Is.False);
            if (paymentWallet)
            {
                var wallet = found["transaction"]!["wallet"]!;
                Assert.That(Keys(wallet["data"]), Is.EqualTo(PaymentKeys));
                Assert.That(Str(wallet, "data.name"), Is.EqualTo("Jane Doe"));
                Assert.That(Str(wallet, "data.QA"), Is.EqualTo("qa"));
                Assert.That(wallet["data"]!["dqa_t"], Is.Null);
                Assert.That(Str(wallet, "account_identifier"), Is.EqualTo("jane.doe@example.com"));
                Assert.That(Str(wallet, "wallet_token"), Has.Length.EqualTo(16));
            }
            else
            {
                Assert.That(found["transaction"]!["wallet"], Is.Null);
            }
        });
    }

    [Test]
    public async Task PageOpen_Rejection_NotifiesTwelveSecondsLater()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var tid = Tid(await SaleAsync(harness, 48700));

        await harness.GetPageAsync($"pages/apm/{tid}");

        var job = harness.Delivery.Jobs.Single();
        var notification = JsonNode.Parse(job.Body!)!;
        Assert.Multiple(() =>
        {
            Assert.That(job.DueAt, Is.EqualTo(harness.Time.GetUtcNow() + TimeSpan.FromSeconds(12)));
            Assert.That(Str(notification, "transaction.transaction_status"), Is.EqualTo("rejected"));
            Assert.That(Str(notification, "transaction.is_async"), Is.EqualTo("0"));
            Assert.That(Str(notification, "transaction.cascade_level"), Is.EqualTo("0"));
            Assert.That(Str(notification, "session.processed_amount"), Is.EqualTo("0"), "qa2: 0 unless approved");
            Assert.That(job.Headers[PraxisSigner.HeaderName], Is.EqualTo(PraxisSigner.ForNotification(notification, harness.Merchant.Secret)));
        });
    }

    [Test]
    public async Task PageOpen_334_ThreeNotificationsOfCopies_TransactionStaysInVerification()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var tid = Tid(await SaleAsync(harness, 33400));
        var opened = harness.Time.GetUtcNow();

        await harness.GetPageAsync($"pages/apm/{tid}");

        var jobs = harness.Delivery.Jobs;
        var bodies = jobs.Select(j => JsonNode.Parse(j.Body!)!).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(jobs.Select(j => j.DueAt - opened), Is.EqualTo(new[] { TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(17), TimeSpan.FromSeconds(22) }));
            Assert.That(bodies.Select(b => Str(b, "transaction.transaction_status")), Is.EqualTo(new[] { "approved", "rejected", "approved" }));
            Assert.That(bodies.Select(b => b["transaction"]!["amount"]!.GetValue<long>()), Is.EqualTo(new[] { 13360L, 33400L, 13360L }));
            Assert.That(bodies.All(b => b["transaction"]!["wallet"] is JsonObject), Is.True, "copies carry the wallet");
            Assert.That(jobs.All(j => j.Headers[PraxisSigner.HeaderName] == PraxisSigner.ForNotification(JsonNode.Parse(j.Body!), harness.Merchant.Secret)), Is.True);
        });
        var found = await harness.FindAsync(tid);
        Assert.Multiple(() =>
        {
            Assert.That(Str(found, "transaction.transaction_status"), Is.EqualTo("verification"));
            Assert.That(found["transaction"]!["amount"]!.GetValue<long>(), Is.EqualTo(26720));
            Assert.That(Str(found, "session.amount"), Is.EqualTo("13360"), "sandbox: the session keeps the copies' amount");
            Assert.That(Str(found, "session.processed_amount"), Is.EqualTo("13360"));
            Assert.That(harness.Delivery.Jobs, Has.Count.EqualTo(3), "no notification of the verification itself");
        });
    }

    // ------------------------------------------------------------------ buttons

    // amount, button, status, code, details, final amount, processed amount, processed currency, wallet, notifications, session status
    // (Pending times out at once with timeScale 0, like every emulator timeout: see Button_Pending_TimeScaleZero_TimesOutAtOnce.)
    private static readonly object?[][] ButtonRows =
    [
        [20000L, "approved", "approved", "0", "[TEST] Transaction status: approved", 20000L, 20000L, "EUR", true, 1, "successful"],
        [20000L, "declined", "rejected", "0", "[TEST] Transaction status: declined. Reason: Insufficient funds", 20000L, null, null, false, 1, "failed"],
        [20000L, "cancelled", "cancelled", "0", "[TEST] Transaction status: cancelled", 20000L, null, null, false, 1, "failed"],
        [20000L, "verification", "verification", "0", "[TEST] Transaction status: verification", 20000L, null, null, false, 1, "open"],
        [1100L, "approved", "approved", "11", "[TEST CASE - amount 11] Changing final amount to 10", 1000L, 1000L, "EUR", true, 1, "successful"],
        [1200L, "approved", "approved", "12", "[TEST CASE - amount 12] Changing final currency", 1068L, 1200L, "USD", true, 1, "successful"],
        [1300L, "approved", "verification", "13", "[TEST CASE - amount 13] Changing final currency and amount", 1625L, null, null, true, 1, "open"],
        [1400L, "approved", "verification", "14", "[TEST CASE - amount 14] Changing final amount to x100", 140000L, null, null, true, 1, "open"],
        [1900L, "approved", "approved", "19", "[TEST CASE - amount 19] Changing final amount to 20", 2000L, 2000L, "EUR", true, 1, "successful"],
        [4600L, "approved", "verification", "46", "[TEST CASE - amount 46] Changing final amount to x2", 9200L, null, null, true, 1, "open"],
        [1100L, "declined", "rejected", "0", "[TEST] Transaction status: declined. Reason: Insufficient funds", 1100L, null, null, false, 1, "failed"]
    ];

    [TestCaseSource(nameof(ButtonRows))]
    public async Task Button_CaseTableRow(long amount, string button, string status, string code, string details, long finalAmount,
        long? processedAmount, string? processedCurrency, bool wallet, int notifications, string sessionStatus)
    {
        var harness = new PraxisEmulatorHarness();
        var tid = Tid(await SaleAsync(harness, amount));

        var pressed = await harness.GetPageAsync($"pages/apm/{tid}/response", button);
        var found = await harness.FindAsync(tid);

        Assert.Multiple(() =>
        {
            Assert.That(pressed.StatusCode, Is.EqualTo(302));
            Assert.That(pressed.Headers["Location"], Is.EqualTo(ReturnUrl));
            Assert.That(Str(found, "transaction.transaction_status"), Is.EqualTo(status));
            Assert.That(Str(found, "transaction.status_code"), Is.EqualTo(code));
            Assert.That(Str(found, "transaction.status_details"), Is.EqualTo(details));
            Assert.That(found["transaction"]!["amount"]!.GetValue<long>(), Is.EqualTo(finalAmount));
            Assert.That(found["transaction"]!["processed_amount"]?.GetValue<long>(), Is.EqualTo(processedAmount));
            Assert.That(Str(found, "transaction.processed_currency"), Is.EqualTo(processedCurrency));
            Assert.That(Str(found, "session.session_status"), Is.EqualTo(sessionStatus));
            Assert.That(harness.Delivery.Jobs, Has.Count.EqualTo(notifications));
            if (wallet)
            {
                Assert.That(Keys(found["transaction"]!["wallet"]!["data"]), Is.EqualTo(new[] { "customer_email", "IBAN" }));
                Assert.That(Str(found, "transaction.wallet.data.customer_email"), Is.EqualTo("jane.doe@example.com"));
                Assert.That(Str(found, "transaction.wallet.data.IBAN"), Is.EqualTo("XX00TEST00000000000000"));
            }
            else
            {
                Assert.That(found["transaction"]!["wallet"], Is.Null);
            }
        });
    }

    [TestCase(1300L, "2600", "AUD", "0.624810")]
    [TestCase(1400L, "140000", "EUR", "1.000000")]
    [TestCase(4600L, "9200", "EUR", "1.000000")]
    public async Task Button_VerificationAmounts_SessionShowsTheProcessedValues(long amount, string processed, string currency, string rate)
    {
        var harness = new PraxisEmulatorHarness();
        var tid = Tid(await SaleAsync(harness, amount));
        await harness.GetPageAsync($"pages/apm/{tid}/response", "approved");

        var found = await harness.FindAsync(tid);

        Assert.Multiple(() =>
        {
            Assert.That(Str(found, "transaction.transaction_status"), Is.EqualTo("verification"));
            Assert.That(found["transaction"]!["processed_amount"], Is.Null);
            Assert.That(Str(found, "session.processed_amount"), Is.EqualTo(processed));
            Assert.That(Str(found, "session.processed_currency"), Is.EqualTo(currency));
            Assert.That(Str(found, "session.conversion_rate"), Is.EqualTo(rate));
            Assert.That(Str(found, "transaction.conversion_rate"), Is.EqualTo(rate));
        });
    }

    [Test]
    public async Task Button_Amount12_SessionShowsTheProcessedCurrency()
    {
        var harness = new PraxisEmulatorHarness();
        var tid = Tid(await SaleAsync(harness, 1200));
        await harness.GetPageAsync($"pages/apm/{tid}/response", "approved");

        var found = await harness.FindAsync(tid);
        var notification = JsonNode.Parse(harness.Delivery.Jobs.Single().Body!)!;

        Assert.Multiple(() =>
        {
            Assert.That(Str(found, "session.processed_currency"), Is.EqualTo("USD"));
            Assert.That(Str(found, "session.currency"), Is.EqualTo("EUR"));
            Assert.That(Str(found, "session.conversion_rate"), Is.EqualTo("0.890315"));
            Assert.That(Str(notification, "transaction.processed_currency"), Is.EqualTo("USD"));
            Assert.That(harness.Delivery.Jobs.Single().Headers[PraxisSigner.HeaderName],
                Is.EqualTo(PraxisSigner.ForNotification(notification, harness.Merchant.Secret)));
        });
    }

    [Test]
    public async Task Button_Pending_TimesOutAfter450ScaledSecondsWithNotification()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var tid = Tid(await SaleAsync(harness, 20000));
        await harness.GetPageAsync($"pages/apm/{tid}/response", "pending");

        harness.Time.Advance(TimeSpan.FromSeconds(449));
        var pending = await harness.FindAsync(tid);
        Assert.Multiple(() =>
        {
            Assert.That(Str(pending, "transaction.transaction_status"), Is.EqualTo("pending"));
            Assert.That(Str(pending, "transaction.status_details"), Is.EqualTo("[TEST] Transaction status: pending. Reason: Awaiting authorization"));
            Assert.That(pending["transaction"]!["processed_amount"]!.GetValue<long>(), Is.EqualTo(20000));
            Assert.That(Str(pending, "session.session_status"), Is.EqualTo("open"));
            Assert.That(pending["transaction"]!["wallet"], Is.Null);
            Assert.That(harness.Delivery.Jobs, Is.Empty, "no notification for pending");
        });

        harness.Time.Advance(TimeSpan.FromSeconds(1));
        var found = await harness.FindAsync(tid);
        Assert.Multiple(() =>
        {
            Assert.That(Str(found, "transaction.transaction_status"), Is.EqualTo("rejected"));
            Assert.That(Str(found, "transaction.status_code"), Is.EqualTo("0"));
            Assert.That(Str(found, "transaction.status_details"), Is.EqualTo(TimedOut));
            Assert.That(harness.Delivery.Jobs.Single().DueAt, Is.EqualTo(harness.Time.GetUtcNow() + TimeSpan.FromSeconds(12)));
        });
    }

    [Test]
    public async Task Button_Pending_TimeScaleZero_TimesOutAtOnce()
    {
        var harness = new PraxisEmulatorHarness();
        var tid = Tid(await SaleAsync(harness, 20000));

        var pressed = await harness.GetPageAsync($"pages/apm/{tid}/response", "pending");
        var found = await harness.FindAsync(tid);

        Assert.Multiple(() =>
        {
            Assert.That(pressed.StatusCode, Is.EqualTo(302));
            Assert.That(Str(found, "transaction.transaction_status"), Is.EqualTo("rejected"));
            Assert.That(Str(found, "transaction.status_details"), Is.EqualTo(TimedOut));
            Assert.That(harness.Delivery.Jobs, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task PageNeverOpened_RejectedAt450ScaledSecondsFromPendingAsync()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var tid = Tid(await SaleAsync(harness, 20000));
        await harness.GetPageAsync($"pages/apm-request/{tid}");

        harness.Time.Advance(TimeSpan.FromSeconds(450));
        var found = await harness.FindAsync(tid);
        var notification = JsonNode.Parse(harness.Delivery.Jobs.Single().Body!)!;

        Assert.Multiple(() =>
        {
            Assert.That(Str(found, "transaction.transaction_status"), Is.EqualTo("rejected"));
            Assert.That(Str(found, "transaction.status_details"), Is.EqualTo(TimedOutPendingAsync), "qa2: the page was never opened");
            Assert.That(Str(notification, "transaction.status_details"), Is.EqualTo(TimedOutPendingAsync));
            Assert.That(Str(notification, "session.processed_amount"), Is.EqualTo("0"));
        });
    }

    [Test]
    public async Task PageNeverSubmitted_RejectedAt450ScaledSeconds_AndThePageAnswers409()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var tid = Tid(await SaleAsync(harness, 20000));

        harness.Time.Advance(TimeSpan.FromSeconds(449));
        Assert.That((await harness.GetPageAsync($"pages/apm/{tid}")).StatusCode, Is.EqualTo(200));
        Assert.That((await harness.Transactions.GetAsync(tid))!.Deadline!.FollowUp, Is.EqualTo(PraxisEWalletCases.PageOpenedAbandoned));

        harness.Time.Advance(TimeSpan.FromSeconds(1));
        Assert.That(await harness.Emulator.ApplyDueDeadlinesAsync(), Is.EqualTo(1));
        var found = await harness.FindAsync(tid);
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(Str(found, "transaction.transaction_status"), Is.EqualTo("rejected"));
            Assert.That(Str(found, "transaction.status_details"), Is.EqualTo(TimedOut), "sandbox: the buttons page was shown");
            Assert.That(Str(found, "session.session_status"), Is.EqualTo("failed"));
            Assert.That(harness.Delivery.Jobs, Has.Count.EqualTo(1));
            Assert.That((await harness.GetPageAsync($"pages/apm/{tid}")).StatusCode, Is.EqualTo(409));
            Assert.That((await harness.GetPageAsync($"pages/apm/{tid}/response", "approved")).StatusCode, Is.EqualTo(409));
        });
    }

    [Test]
    public async Task TimeScaleZero_PageWaitsWithoutDeadline()
    {
        var harness = new PraxisEmulatorHarness();
        var tid = Tid(await SaleAsync(harness, 20000));

        harness.Time.Advance(TimeSpan.FromHours(2));

        await Assert.MultipleAsync(async () =>
        {
            Assert.That((await harness.Transactions.GetAsync(tid))!.Deadline, Is.Null);
            Assert.That(Str(await harness.FindAsync(tid), "transaction.transaction_status"), Is.EqualTo("initialized"));
            Assert.That((await harness.GetPageAsync($"pages/apm/{tid}/response", "approved")).StatusCode, Is.EqualTo(302));
        });
    }

    [Test]
    public async Task DropNotifications_ApprovedSaleSendsNothing_FindStillApproved()
    {
        var harness = new PraxisEmulatorHarness();
        var dropping = NewMerchant("m-drop");
        dropping.DropNotifications = true;
        var merchant = await harness.Merchants.CreateAsync(dropping);
        var tid = Tid((await harness.ApmAsync("sale", 20000, merchant: merchant)).Json()!);

        await harness.GetPageAsync($"pages/apm/{tid}/response", "approved");
        var found = await harness.FindAsync(tid, merchant);

        Assert.Multiple(() =>
        {
            Assert.That(Str(found, "transaction.transaction_status"), Is.EqualTo("approved"));
            Assert.That(harness.Delivery.Jobs, Is.Empty);
        });
    }

    // ------------------------------------------------------------------ page edge cases

    [TestCase("pages/apm-request/999")]
    [TestCase("pages/apm/999")]
    [TestCase("pages/apm/999/response")]
    [TestCase("pages/payout/999")]
    public async Task Pages_UnknownTid_404(string path)
    {
        var harness = new PraxisEmulatorHarness();

        var page = await harness.GetPageAsync(path, "approved");

        Assert.That(page.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public async Task Pages_TidOfAnotherEnvironment_404()
    {
        var harness = new PraxisEmulatorHarness();
        var sale = Tid(await SaleAsync(harness, 20000));
        var payout = Tid(await PayoutAsync(harness, 30000));

        async Task<int> GetInOtherEnvironmentAsync(string path, string? status = null) => (await harness.Emulator.HandleAsync(new EmulatorRequest
        {
            Environment = "other",
            Path = path,
            Method = "GET",
            Query = status is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["status"] = status },
            BaseUrl = "http://emulator.test"
        })).StatusCode;

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await GetInOtherEnvironmentAsync($"pages/apm-request/{sale}"), Is.EqualTo(404));
            Assert.That(await GetInOtherEnvironmentAsync($"pages/apm/{sale}"), Is.EqualTo(404));
            Assert.That(await GetInOtherEnvironmentAsync($"pages/apm/{sale}/response", "approved"), Is.EqualTo(404));
            Assert.That(await GetInOtherEnvironmentAsync($"pages/payout/{payout}"), Is.EqualTo(404));
            Assert.That(Str(await harness.FindAsync(sale), "transaction.transaction_status"), Is.EqualTo("initialized"), "nothing changed");
        });
    }

    [Test]
    public async Task Pages_CardTransactionOrWrongType_404()
    {
        var harness = new PraxisEmulatorHarness();
        var card = Tid(await harness.SaleAsync("333", 4900));
        var payout = Tid(await PayoutAsync(harness, 5100));
        var sale = Tid(await SaleAsync(harness, 20000));

        await Assert.MultipleAsync(async () =>
        {
            Assert.That((await harness.GetPageAsync($"pages/apm/{card}")).StatusCode, Is.EqualTo(404));
            Assert.That((await harness.GetPageAsync($"pages/apm-request/{payout}")).StatusCode, Is.EqualTo(404));
            Assert.That((await harness.GetPageAsync($"pages/apm/{payout}/response", "approved")).StatusCode, Is.EqualTo(404));
            Assert.That((await harness.GetPageAsync($"pages/payout/{sale}")).StatusCode, Is.EqualTo(404));
            Assert.That((await harness.GetPageAsync($"pages/3ds/{sale}")).StatusCode, Is.EqualTo(409), "an e-wallet sale is not awaiting 3DS");
            Assert.That((await harness.GetPageAsync($"pages/apm/{sale}/other")).StatusCode, Is.EqualTo(404));
        });
    }

    [Test]
    public async Task Response_UnknownStatus_400_ThenSecondButton_409()
    {
        var harness = new PraxisEmulatorHarness();
        var tid = Tid(await SaleAsync(harness, 20000));

        var unknown = await harness.GetPageAsync($"pages/apm/{tid}/response", "maybe");
        var missing = await harness.GetPageAsync($"pages/apm/{tid}/response");
        var first = await harness.GetPageAsync($"pages/apm/{tid}/response", "declined");
        var second = await harness.GetPageAsync($"pages/apm/{tid}/response", "approved");
        var reopened = await harness.GetPageAsync($"pages/apm/{tid}");

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(unknown.StatusCode, Is.EqualTo(400));
            Assert.That(missing.StatusCode, Is.EqualTo(400));
            Assert.That(first.StatusCode, Is.EqualTo(302));
            Assert.That(second.StatusCode, Is.EqualTo(409));
            Assert.That(reopened.StatusCode, Is.EqualTo(409));
            Assert.That(Str(await harness.FindAsync(tid), "transaction.transaction_status"), Is.EqualTo("rejected"));
        });
    }

    [Test]
    public async Task NoReturnUrl_ButtonAndPageOpenAnswerADonePage()
    {
        var harness = new PraxisEmulatorHarness();
        var button = Tid(await SaleAsync(harness, 20000, returnUrl: null));
        var pageOpen = Tid(await SaleAsync(harness, 107800, returnUrl: null));

        var pressed = await harness.GetPageAsync($"pages/apm/{button}/response", "approved");
        var opened = await harness.GetPageAsync($"pages/apm/{pageOpen}");

        Assert.Multiple(() =>
        {
            Assert.That(pressed.StatusCode, Is.EqualTo(200));
            Assert.That(pressed.Body, Does.Contain("approved"));
            Assert.That(opened.StatusCode, Is.EqualTo(200));
            Assert.That(opened.Body, Does.Contain("E-wallet result"));
        });
    }

    // ------------------------------------------------------------------ find-transaction shapes

    [Test]
    public async Task Find_ShapeFollowsTheState()
    {
        var harness = new PraxisEmulatorHarness();
        var sale = Tid(await SaleAsync(harness, 20000));
        var waiting = (await harness.FindAsync(sale))["transaction"]!.AsObject();
        await harness.GetPageAsync($"pages/apm/{sale}/response", "approved");
        var decided = (await harness.FindAsync(sale))["transaction"]!.AsObject();
        var payout = (await harness.FindAsync(Tid(await PayoutAsync(harness, 5100))))["transaction"]!.AsObject();

        Assert.Multiple(() =>
        {
            Assert.That(waiting.ContainsKey("is_async") && waiting.ContainsKey("redirect_url") && !waiting.ContainsKey("is_3d"), Is.True);
            Assert.That(waiting["cascade_level"], Is.Null);
            Assert.That(!decided.ContainsKey("is_async") && !decided.ContainsKey("redirect_url") && decided.ContainsKey("is_3d"), Is.True);
            Assert.That(decided["cascade_level"]!.GetValue<int>(), Is.EqualTo(0));
            Assert.That(payout.ContainsKey("is_async") && !payout.ContainsKey("redirect_url") && !payout.ContainsKey("is_3d"), Is.True);
        });
    }

    // ------------------------------------------------------------------ payouts

    // amount, status, code, details, final amount, processed amount, withdrawal wallet
    private static readonly object?[][] PayoutRows =
    [
        [1100L, "approved", "11", PayoutSuccess, 1000L, 1100L, true],
        [1200L, "approved", "12", PayoutSuccess, 1200L, 1200L, true],
        [1300L, "approved", "13", PayoutSuccess, 2600L, 1300L, true],
        [1400L, "approved", "14", PayoutSuccess, 140000L, 1400L, true],
        [1900L, "approved", "19", PayoutSuccess, 2000L, 1900L, true],
        [4600L, "approved", "46", PayoutSuccess, 9200L, 4600L, true],
        [5000L, "error", "200", "[TEST] Incorrect processor response for payout [TEST CASE - amount 50]", 5000L, null, false],
        [20000L, "rejected", "NoMoneyCode", "test Insufficient payout funds msg APM!", 20000L, null, false],
        [30000L, "initialized", "300", "[TEST] Redirect for payout initiated [TEST CASE - amount 300]", 30000L, null, false],
        [33200L, "pending", "333", "[TEST] Payout capture pending [TEST CASE - amount 332.00]", 33200L, 33200L, false],
        [33300L, "pending", "333", "[TEST] Payout capture pending [TEST CASE - amount 333.00]", 33300L, 33300L, false],
        [50000L, "approved", "0", PayoutSuccess, 50000L, 50000L, true],
        [60000L, "rejected", "600", "[TEST] Payout capture failed [TEST CASE - amount 600]", 60000L, null, false],
        [70000L, "pending", "50", "[TEST] Payout capture error [TEST CASE - amount 700]", 70000L, 70000L, false],
        [80000L, "approved", "0", PayoutSuccess, 80000L, 80000L, true],
        [5100L, "approved", "0", PayoutSuccess, 5100L, 5100L, true]
    ];

    [TestCaseSource(nameof(PayoutRows))]
    public async Task Payout_CaseTableRow_ImmediateAndUnchangedLater(long amount, string status, string code, string details,
        long finalAmount, long? processedAmount, bool wallet)
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var response = await PayoutAsync(harness, amount);
        harness.Time.Advance(TimeSpan.FromMinutes(40));
        var found = await harness.FindAsync(Tid(response));

        Assert.Multiple(() =>
        {
            foreach (var body in new[] { response, found })
            {
                Assert.That(Str(body, "transaction.transaction_type"), Is.EqualTo("payout"));
                Assert.That(Str(body, "transaction.transaction_status"), Is.EqualTo(status));
                Assert.That(Str(body, "transaction.status_code"), Is.EqualTo(code));
                Assert.That(Str(body, "transaction.status_details"), Is.EqualTo(details));
                Assert.That(body["transaction"]!["amount"]!.GetValue<long>(), Is.EqualTo(finalAmount));
                Assert.That(body["transaction"]!["processed_amount"]?.GetValue<long>(), Is.EqualTo(processedAmount));
                Assert.That(Str(body, "session.intent"), Is.EqualTo("withdrawal"));
                Assert.That(body["transaction"]!.AsObject().ContainsKey("redirect_url"), Is.EqualTo(amount == 30000));
                if (wallet)
                {
                    Assert.That(Keys(body["transaction"]!["wallet"]!["data"]), Is.EqualTo(WithdrawalKeys));
                    Assert.That(Str(body, "transaction.wallet.data.email"), Is.EqualTo("jane.doe@example.com"));
                }
                else
                {
                    Assert.That(body["transaction"]!["wallet"], Is.Null);
                }
            }
            Assert.That(Str(response, "session.processed_amount"), Is.EqualTo(status == "approved" ? processedAmount!.Value.ToString() : "0"));
            Assert.That(harness.Delivery.Jobs, Is.Empty, "payouts never notify");
        });
    }

    [Test]
    public async Task Payout300_RedirectsToThePayoutPage_WhichChangesNothing()
    {
        var harness = new PraxisEmulatorHarness();
        var response = await PayoutAsync(harness, 30000);
        var tid = Tid(response);
        var expected = $"http://localhost/emulators/praxis/{Env}/pages/payout/{tid}";

        var page = await harness.GetPageAsync($"pages/payout/{tid}");
        var found = await harness.FindAsync(tid);

        Assert.Multiple(() =>
        {
            Assert.That(Str(response, "redirect_url"), Is.EqualTo(expected));
            Assert.That(Str(response, "transaction.redirect_url"), Is.EqualTo(expected));
            Assert.That(page.StatusCode, Is.EqualTo(200));
            Assert.That(page.Body, Does.Contain("Test E-Wallet 3DS page"));
            Assert.That(page.Body, Does.Contain($"href=\"{ReturnUrl}\""));
            Assert.That(Str(found, "transaction.transaction_status"), Is.EqualTo("initialized"));
            Assert.That(Str(found, "transaction.redirect_url"), Is.EqualTo(expected));
        });
    }

    [TestCase("javascript:alert(1)")]
    [TestCase("relative/path")]
    public async Task Payout300_PayoutPage_NonHttpReturnUrl_LinksToHash(string returnUrl)
    {
        var harness = new PraxisEmulatorHarness();
        var tid = Tid((await harness.ApmAsync("payout", 30000, returnUrl: returnUrl)).Json()!);

        var page = await harness.GetPageAsync($"pages/payout/{tid}");

        Assert.Multiple(() =>
        {
            Assert.That(page.StatusCode, Is.EqualTo(200));
            Assert.That(page.Body, Does.Contain("href=\"#\""));
            Assert.That(page.Body, Does.Not.Contain("javascript:"));
        });
    }

    [Test]
    public async Task OtherPayout_NoRedirectUrl_TopLevelKeyNull()
    {
        var harness = new PraxisEmulatorHarness();
        var response = await PayoutAsync(harness, 5100);

        Assert.Multiple(() =>
        {
            Assert.That(response.AsObject().ContainsKey("redirect_url"), Is.True);
            Assert.That(response["redirect_url"], Is.Null);
        });
    }

    // ------------------------------------------------------------------ wallet-token payouts

    [Test]
    public async Task WalletTokenPayout_UsesTheWalletAndCustomerOfTheApprovedSale()
    {
        var harness = new PraxisEmulatorHarness();
        var saleTid = Tid(await SaleAsync(harness, 20000));
        await harness.GetPageAsync($"pages/apm/{saleTid}/response", "approved");
        var sale = await harness.FindAsync(saleTid);
        var walletToken = Str(sale, "transaction.wallet.wallet_token");
        // A later direct/apm payout carries the same token but only {email}: it is never the source.
        var payout = await PayoutAsync(harness, 5100);
        Assert.That(Str(payout, "transaction.wallet.wallet_token"), Is.EqualTo(walletToken));

        var reply = await harness.SendAsync("direct/wallet-token", PraxisRequestBuilder.WalletTokenPayout(harness.Merchant, harness.Now, 1100,
            walletToken, Str(sale, "customer.customer_token")));
        var response = reply.Json()!;

        Assert.Multiple(() =>
        {
            Assert.That(Str(response, "status"), Is.EqualTo("0"), reply.Body);
            Assert.That(reply.Headers[PraxisSigner.HeaderName], Is.EqualTo(PraxisSigner.ForResponse(response, harness.Merchant.Secret)));
            Assert.That(Str(response, "transaction.transaction_status"), Is.EqualTo("approved"));
            Assert.That(Str(response, "transaction.status_code"), Is.EqualTo("11"));
            Assert.That(response["transaction"]!["amount"]!.GetValue<long>(), Is.EqualTo(1000));
            Assert.That(Str(response, "transaction.wallet.wallet_token"), Is.EqualTo(walletToken));
            Assert.That(Str(response, "transaction.wallet.account_identifier"), Is.EqualTo("jane.doe@example.com"));
            Assert.That(Keys(response["transaction"]!["wallet"]!["data"]), Is.EqualTo(WithdrawalKeys));
            Assert.That(Str(response, "transaction.wallet.data.phone"), Is.EqualTo("+10000000000"));
            Assert.That(Str(response, "customer.customer_token"), Is.EqualTo(Str(sale, "customer.customer_token")));
            Assert.That(Str(response, "customer.country"), Is.EqualTo("BG"));
        });
    }

    [Test]
    public async Task WalletTokenPayout_TwoSalesWithTheToken_TheLaterTidWins()
    {
        var harness = new PraxisEmulatorHarness();
        var first = await SaleAsync(harness, 20000);
        var secondData = PraxisRequestBuilder.SaleWalletData();
        secondData["phone"] = "+19999999999";
        var second = (await harness.ApmAsync("sale", 20000, walletData: secondData)).Json()!;
        Assert.That(Tid(second), Is.GreaterThan(Tid(first)));
        var walletToken = PraxisWalletData.WalletToken(harness.Merchant.MerchantId, "cid-1", "jane.doe@example.com");

        var response = (await harness.SendAsync("direct/wallet-token",
            PraxisRequestBuilder.WalletTokenPayout(harness.Merchant, harness.Now, 5100, walletToken, null))).Json()!;

        Assert.That(Str(response, "transaction.wallet.data.phone"), Is.EqualTo("+19999999999"), "same CreatedAt: the higher tid is the newer sale");
    }

    [Test]
    public async Task WalletTokenPayout_UnknownToken_PaysOutWithAnEmptyWallet()
    {
        var harness = new PraxisEmulatorHarness();
        var body = PraxisRequestBuilder.WalletTokenPayout(harness.Merchant, harness.Now, 5100, "0000000000000000", null);
        body["customer_data"] = new JsonObject { ["country"] = "SE", ["first_name"] = "Ann" };

        var response = (await harness.SendAsync("direct/wallet-token", body)).Json()!;

        Assert.Multiple(() =>
        {
            Assert.That(Str(response, "transaction.transaction_status"), Is.EqualTo("approved"));
            Assert.That(Str(response, "transaction.wallet.wallet_token"), Is.EqualTo("0000000000000000"));
            Assert.That(Str(response, "transaction.wallet.account_identifier"), Is.Null);
            Assert.That(response["transaction"]!["wallet"]!["data"]!.AsObject().All(p => p.Value is null), Is.True);
            Assert.That(Str(response, "customer.country"), Is.EqualTo("SE"));
        });
    }

    [Test]
    public async Task WalletTokenPayout_SaleType_Status1_AndSignatureCoversTheToken()
    {
        var harness = new PraxisEmulatorHarness();
        var body = PraxisRequestBuilder.WalletTokenPayout(harness.Merchant, harness.Now, 5100, "t", null);
        body["transaction_type"] = "sale";
        var unsigned = PraxisRequestBuilder.WalletTokenPayout(harness.Merchant, harness.Now, 5100, "t", null);

        var wrongType = await harness.SendAsync("direct/wallet-token", body);
        var wrongSignature = await harness.SendAsync("direct/wallet-token", unsigned, signature: PraxisSigner.ForApm(unsigned, harness.Merchant.Secret));

        Assert.Multiple(() =>
        {
            Assert.That(wrongType.Body, Is.EqualTo("""{"status":1,"description":"Gateway does not support the transaction type (sale)"}"""));
            Assert.That(Str(wrongSignature.Json()!, "description"), Is.EqualTo("Validation Error"));
        });
    }

    // ------------------------------------------------------------------ wallet_data and request validation

    private static IEnumerable<TestCaseData> WalletDataRows()
    {
        yield return new TestCaseData("sale", new JsonObject { ["name"] = "Jane Doe", ["email"] = "e@example.com", ["test"] = "1" },
            "[phone] field is required!,[ip_address] field is required!,[QA] field is required!").SetName("Sale_MissingPhoneIpQa");
        var bulgaria = PraxisRequestBuilder.SaleWalletData();
        bulgaria["customer_country"] = "country_bulgaria";
        yield return new TestCaseData("sale", bulgaria, "'country_bulgaria' value is not valid for the [customer_country] field!").SetName("Sale_UnknownCountry");
        var unknown = PraxisRequestBuilder.SaleWalletData();
        unknown["iban"] = "X";
        yield return new TestCaseData("sale", unknown, "Unknown property received within wallet_data for altbankwire: 'iban'").SetName("Sale_UnknownKey");
        var blankPhone = PraxisRequestBuilder.SaleWalletData();
        blankPhone["phone"] = "";
        blankPhone["customer_country"] = "country_mars";
        blankPhone["x"] = 1;
        yield return new TestCaseData("sale", blankPhone,
            "[phone] field is required!,'country_mars' value is not valid for the [customer_country] field!,Unknown property received within wallet_data for altbankwire: 'x'")
            .SetName("Sale_RequiredThenCountryThenUnknown");
        yield return new TestCaseData("sale", null,
            "[phone] field is required!,[ip_address] field is required!,[QA] field is required!").SetName("Sale_NoWalletData");
        yield return new TestCaseData("payout", new JsonObject { ["test"] = "1" },
            "[email] field is required!,Unknown property received within wallet_data for altbankwire: 'test'").SetName("Payout_TestOnly");
        yield return new TestCaseData("payout", new JsonObject { ["email"] = "e@example.com", ["test"] = "1" },
            "Unknown property received within wallet_data for altbankwire: 'test'").SetName("Payout_EmailAndTest");
        yield return new TestCaseData("payout", new JsonObject { ["full_name"] = "J", ["bank_account"] = "1", ["iban"] = "X" },
            "[email] field is required!,Unknown property received within wallet_data for altbankwire: 'full_name'," +
            "Unknown property received within wallet_data for altbankwire: 'bank_account',Unknown property received within wallet_data for altbankwire: 'iban'")
            .SetName("Payout_BankFields");
    }

    [TestCaseSource(nameof(WalletDataRows))]
    public async Task WalletData_Invalid_Status400AndNothingStored(string type, JsonObject? walletData, string description)
    {
        var harness = new PraxisEmulatorHarness();
        var body = PraxisRequestBuilder.Apm(harness.Merchant, harness.Now, type, 20000, NotificationUrl);
        body["wallet_data"] = walletData;

        var reply = await harness.SendAsync("direct/apm", body);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(reply.Json()!["status"]!.GetValue<int>(), Is.EqualTo(400));
            Assert.That(Str(reply.Json()!, "description"), Is.EqualTo(description));
            Assert.That(reply.Headers.ContainsKey(PraxisSigner.HeaderName), Is.False);
            Assert.That(await StoredCountAsync(harness), Is.Zero);
        });
    }

    [Test]
    public async Task WalletData_MethodNameComesFromTheGateway()
    {
        var harness = new PraxisEmulatorHarness();
        var merchant = NewMerchant("m-method");
        merchant.Gateways = [new PraxisGateway { Id = PraxisRequestBuilder.EWalletGateway, Kind = "ewallet", PaymentMethod = "skrill" }];
        merchant = await harness.Merchants.CreateAsync(merchant);
        var body = PraxisRequestBuilder.Apm(merchant, harness.Now, "payout", 5100, walletData: new JsonObject { ["email"] = "e", ["x"] = "1" });

        var reply = await harness.SendAsync("direct/apm", body, merchant);

        Assert.That(Str(reply.Json()!, "description"), Is.EqualTo("Unknown property received within wallet_data for skrill: 'x'"));
    }

    [Test]
    public async Task UnregisteredGateway_DefaultsToTestEWallet()
    {
        var harness = new PraxisEmulatorHarness();
        var body = PraxisRequestBuilder.Apm(harness.Merchant, harness.Now, "sale", 20000);
        body["gateway"] = "unregistered";

        var response = (await harness.SendAsync("direct/apm", body)).Json()!;

        Assert.Multiple(() =>
        {
            Assert.That(Str(response, "transaction.payment_processor"), Is.EqualTo("Test E-Wallet"));
            Assert.That(Str(response, "transaction.payment_method"), Is.EqualTo("altbankwire"));
        });
    }

    [Test]
    public async Task Apm_BlanksAmountWalletDataAndExpiredTimestamp_OneStatus400InOrder()
    {
        var harness = new PraxisEmulatorHarness();
        var body = PraxisRequestBuilder.Apm(harness.Merchant, harness.Now - 300, "sale", 20000);
        body["currency"] = "";
        body["amount"] = "12.5";
        body.Remove("order_id");
        ((JsonObject)body["wallet_data"]!).Remove("QA");

        var description = Str((await harness.SendAsync("direct/apm", body)).Json()!, "description");

        Assert.That(description, Is.EqualTo("Currency cannot be blank.,Order Id cannot be blank.,Amount must be an integer.,[QA] field is required!," +
                                            "Timestamp expired: 300s old, should be at most 60s old (required with merchant_id)"));
    }

    [TestCase("authorization")]
    [TestCase("refund")]
    public async Task Apm_OtherType_Status1(string type)
    {
        var harness = new PraxisEmulatorHarness();
        var body = PraxisRequestBuilder.Apm(harness.Merchant, harness.Now, type, 20000);

        var reply = await harness.SendAsync("direct/apm", body);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(reply.Body, Is.EqualTo($$"""{"status":1,"description":"Gateway does not support the transaction type ({{type}})"}"""));
            Assert.That(await StoredCountAsync(harness), Is.Zero);
        });
    }

    [TestCase("direct/apm")]
    [TestCase("direct/wallet-token")]
    [TestCase("agent/get-available-gateways")]
    [TestCase("agent/get-gateway-options")]
    [TestCase("agent/manage-withdrawal-request")]
    public async Task EWalletEndpoints_UnknownMerchantAndWrongSignature_Rejected(string path)
    {
        var harness = new PraxisEmulatorHarness();
        var unknown = new JsonObject { ["merchant_id"] = "nobody", ["application_key"] = "x", ["timestamp"] = harness.Now };
        var signed = PraxisRequestBuilder.GatewayLookup(harness.Merchant, harness.Now);

        var userNotFound = await harness.SendAsync(path, unknown, signature: "sig");
        var wrongSignature = await harness.SendAsync(path, signed, signature: "0000");

        Assert.Multiple(() =>
        {
            Assert.That(userNotFound.Body, Is.EqualTo("""{"status":401,"description":"User not found"}"""));
            Assert.That(wrongSignature.Body, Is.EqualTo("""{"status":1,"description":"Validation Error","errors":{"merchant_id":["Invalid signature"]}}"""));
        });
    }

    // ------------------------------------------------------------------ gateway lookups

    [Test]
    public async Task GetAvailableGateways_OneEntryPerGateway_WithBillingServerKeys()
    {
        var harness = new PraxisEmulatorHarness();

        var reply = await harness.SendAsync("agent/get-available-gateways", PraxisRequestBuilder.GatewayLookup(harness.Merchant, harness.Now));
        var response = reply.Json()!;
        var list = response["gateway_list"]!.AsArray();
        var card = list[0]!;
        var ewallet = list[1]!;
        var input = ewallet["payment_information"]!["input_fields"]!.AsArray();

        Assert.Multiple(() =>
        {
            Assert.That(Keys(response), Is.EqualTo(new[] { "status", "description", "gateway_list", "timestamp", "version" }));
            Assert.That(reply.Headers[PraxisSigner.HeaderName], Is.EqualTo(PraxisSigner.ForResponse(response, harness.Merchant.Secret)));
            Assert.That(list, Has.Count.EqualTo(2));
            Assert.That(Keys(card), Is.EqualTo(new[] { "gateway", "gateway_type", "payment_method", "display_name", "system_name", "image", "bdcc", "payment_information" }));
            Assert.That(Keys(card["payment_information"]), Is.EqualTo(new[]
            {
                "deposit_supported", "authorization_supported", "payout_supported", "mit_supported", "input_fields", "card_types",
                "processing_currencies", "fees", "spread", "countries_supported"
            }));
            Assert.That(Str(card, "gateway"), Is.EqualTo(PraxisRequestBuilder.CardGateway));
            Assert.That(Str(card, "gateway_type"), Is.EqualTo("card"));
            Assert.That(Str(card, "display_name"), Is.EqualTo("Test Card Processor"));
            Assert.That(Str(card, "system_name"), Is.EqualTo("Test Card Processor - m-1"));
            Assert.That(card["payment_information"]!["input_fields"]!.AsArray(), Is.Empty);
            Assert.That(Keys(card["payment_information"]!["processing_currencies"]), Is.EqualTo(new[] { "EUR", "USD", "GBP", "BRL", "MXN", "JPY", "PLN", "VND" }));
            Assert.That(Keys(card["payment_information"]!["processing_currencies"]!["EUR"]),
                Is.EqualTo(new[] { "currency", "min_amount", "max_amount", "is_bdcc_enabled", "bdcc" }));
            Assert.That(Str(ewallet, "gateway"), Is.EqualTo(PraxisRequestBuilder.EWalletGateway));
            Assert.That(Str(ewallet, "gateway_type"), Is.EqualTo("apm"));
            Assert.That(Str(ewallet, "payment_method"), Is.EqualTo("altbankwire"));
            Assert.That(Str(ewallet, "image"), Is.EqualTo(null));
            Assert.That(input.Select(f => Str(f!, "key")), Is.EqualTo(PaymentKeys));
            Assert.That(Keys(input[3]), Is.EqualTo(new[] { "key", "label", "default_value", "required", "format", "min", "max", "options" }));
            Assert.That(input[3]!["options"]!.GetValue<string>(), Does.Contain("\"country_ukraine\":\"Ukriane\""));
            Assert.That(input[8]!["default_value"], Is.Null);
            Assert.That(ewallet["payment_information"]!["countries_supported"]!.AsArray(), Has.Count.EqualTo(270));
            Assert.That(Keys(ewallet["payment_information"]!["processing_currencies"]), Is.EqualTo(new[] { "EUR", "PLN", "BRL", "USD", "JPY" }));
        });
    }

    [Test]
    public async Task GetGatewayOptions_OneEntryPerGateway_WithBillingServerKeys()
    {
        var harness = new PraxisEmulatorHarness();

        var reply = await harness.SendAsync("agent/get-gateway-options", PraxisRequestBuilder.GatewayLookup(harness.Merchant, harness.Now, "payout"));
        var response = reply.Json()!;
        var options = response["gateway_options"]!.AsArray();
        var card = options[0]!;
        var ewallet = options[1]!;
        var payment = ewallet["input_fields"]!["input_fields_for_payment"]!.AsArray();
        var withdrawal = ewallet["input_fields"]!["input_fields_for_withdrawal"]!.AsArray();

        Assert.Multiple(() =>
        {
            Assert.That(Keys(response), Is.EqualTo(new[] { "status", "description", "gateway_options", "version", "timestamp" }));
            Assert.That(reply.Headers.ContainsKey(PraxisSigner.HeaderName), Is.True);
            Assert.That(Keys(card), Is.EqualTo(new[]
            {
                "payment_method", "payment_processor", "gateway", "system_name", "image", "status", "deposit_supported",
                "authorization_supported", "payout_supported", "refund_supported", "is_3d", "input_fields"
            }));
            Assert.That(Str(card, "is_3d"), Is.EqualTo("no"));
            Assert.That(Str(card, "refund_supported"), Is.EqualTo("1"));
            Assert.That(card["input_fields"], Is.Null);
            Assert.That(Str(ewallet, "payment_processor"), Is.EqualTo("Test E-Wallet"));
            Assert.That(Str(ewallet, "system_name"), Is.EqualTo("Test E-Wallet - m-1"));
            Assert.That(Str(ewallet, "refund_supported"), Is.EqualTo("0"));
            Assert.That(ewallet["is_3d"], Is.Null);
            Assert.That(payment.Select(f => Str(f!, "key")), Is.EqualTo(PaymentKeys));
            Assert.That(withdrawal.Select(f => Str(f!, "key")), Is.EqualTo(WithdrawalKeys));
            Assert.That(Keys(payment[0]), Is.EqualTo(new[] { "key", "label", "required", "format", "min", "default_value", "max" }));
            Assert.That(payment[3]!["options"]!["country_cyprus"]!.GetValue<string>(), Is.EqualTo("Cyprus"));
            Assert.That(payment[8]!["default_value"]!.GetValue<string>(), Is.Empty);
        });
    }

    [Test]
    public async Task GatewayLookup_DefaultsAndExpiredTimestamp()
    {
        var harness = new PraxisEmulatorHarness();
        var merchant = NewMerchant("m-defaults");
        merchant.Gateways = [new PraxisGateway { Id = "c", Kind = "card" }, new PraxisGateway { Id = "w", Kind = "ewallet" }];
        merchant = await harness.Merchants.CreateAsync(merchant);

        var options = (await harness.SendAsync("agent/get-gateway-options", PraxisRequestBuilder.GatewayLookup(merchant, harness.Now), merchant)).Json()!;
        var expired = (await harness.SendAsync("agent/get-available-gateways", PraxisRequestBuilder.GatewayLookup(merchant, harness.Now - 120), merchant)).Json()!;

        Assert.Multiple(() =>
        {
            Assert.That(Str(options["gateway_options"]![0]!, "payment_method"), Is.EqualTo("Credit Card"));
            Assert.That(Str(options["gateway_options"]![0]!, "payment_processor"), Is.EqualTo("Test Card Processor"));
            Assert.That(Str(options["gateway_options"]![1]!, "payment_method"), Is.EqualTo("altbankwire"));
            Assert.That(Str(options["gateway_options"]![1]!, "payment_processor"), Is.EqualTo("Test E-Wallet"));
            Assert.That(Str(expired, "status"), Is.EqualTo("1"));
            Assert.That(Str(expired, "description"), Is.EqualTo("Validation Error"));
        });
    }

    // ------------------------------------------------------------------ manage-withdrawal-request

    private static async Task<EmulatorReply> ManageWithdrawalAsync(PraxisEmulatorHarness harness, string intent, long? id, string? gateway = null) =>
        await harness.SendAsync("agent/manage-withdrawal-request", PraxisRequestBuilder.ManageWithdrawal(harness.Merchant, harness.Now, intent, id, gateway));

    [Test]
    public async Task ManageWithdrawal_EveryReply()
    {
        var harness = new PraxisEmulatorHarness();
        var approved = Tid(await PayoutAsync(harness, 50000));
        var pending = Tid(await PayoutAsync(harness, 70000));
        var sale = Tid(await SaleAsync(harness, 20000));
        var otherMerchant = await harness.Merchants.CreateAsync(NewMerchant("m-other"));
        var foreign = Tid((await harness.ApmAsync("payout", 5100, merchant: otherMerchant)).Json()!);
        var cardSale = await harness.SaleAsync("568", 4900);
        var cardPayout = Tid((await harness.SendAsync("api/direct-process", PraxisRequestBuilder.Payout(harness.Merchant, harness.Now, 5000,
            Str(cardSale, "transaction.card.card_token"), Str(cardSale, "customer.customer_token")))).Json()!);

        var cancel = await ManageWithdrawalAsync(harness, "cancel-withdrawal-request", approved);
        var completeNoGateway = await ManageWithdrawalAsync(harness, "complete-withdrawal-request", approved);
        var completePending = await ManageWithdrawalAsync(harness, "complete-withdrawal-request", pending, PraxisRequestBuilder.EWalletGateway);
        var completeApproved = await ManageWithdrawalAsync(harness, "complete-withdrawal-request", approved, PraxisRequestBuilder.EWalletGateway);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(cancel.Body, Is.EqualTo("""{"status":400,"description":"Payout request is already processed"}"""));
            Assert.That(completeNoGateway.Body,
                Is.EqualTo("""{"status":400,"description":"The gateway field is required because the payment method is not Credit Card."}"""));
            Assert.That(Str(completePending.Json()!, "description"),
                Is.EqualTo("Payout request status must be \"requested\" in order to proceed with processing (currently \"pending\")"));
            Assert.That(Str(completeApproved.Json()!, "description"),
                Is.EqualTo("Payout request status must be \"requested\" in order to proceed with processing (currently \"approved\")"));
            foreach (var signed in new[] { cancel, completeNoGateway, completePending, completeApproved })
            {
                Assert.That(signed.Headers[PraxisSigner.HeaderName], Is.EqualTo(PraxisSigner.ForResponse(signed.Json(), harness.Merchant.Secret)));
            }

            Assert.That((await ManageWithdrawalAsync(harness, "cancel-withdrawal-request", null)).Body,
                Is.EqualTo("""{"status":400,"description":"Withdrawal Request Id cannot be blank."}"""));
            Assert.That((await ManageWithdrawalAsync(harness, "complete-withdrawal-request", null)).Body, Is.EqualTo("""{"status":1,"description":"Generic Error"}"""));
            Assert.That((await ManageWithdrawalAsync(harness, "approve-withdrawal-request", approved)).Body, Is.EqualTo("""{"status":1,"description":"Generic Error"}"""));
            Assert.That((await ManageWithdrawalAsync(harness, "cancel-withdrawal-request", 424242)).Body, Is.EqualTo("""{"status":401,"description":"Wrong credentials"}"""));
            Assert.That((await ManageWithdrawalAsync(harness, "cancel-withdrawal-request", sale)).Body, Is.EqualTo("""{"status":401,"description":"Wrong credentials"}"""));
            Assert.That((await ManageWithdrawalAsync(harness, "cancel-withdrawal-request", foreign)).Body, Is.EqualTo("""{"status":401,"description":"Wrong credentials"}"""));
            var cardRefused = await ManageWithdrawalAsync(harness, "cancel-withdrawal-request", cardPayout);
            Assert.That(cardRefused.Body, Is.EqualTo("""{"status":401,"description":"Wrong credentials"}"""), "a card payout is not a withdrawal request");
            Assert.That(cardRefused.Headers.ContainsKey(PraxisSigner.HeaderName), Is.False);
            Assert.That(Str(await harness.FindAsync(pending), "transaction.transaction_status"), Is.EqualTo("pending"), "nothing changes");
        });
    }

    [Test]
    public async Task ManageWithdrawal_ExpiredTimestamp_Status1ValidationError()
    {
        var harness = new PraxisEmulatorHarness();
        var payout = Tid(await PayoutAsync(harness, 50000));

        var reply = await harness.SendAsync("agent/manage-withdrawal-request",
            PraxisRequestBuilder.ManageWithdrawal(harness.Merchant, harness.Now - 130, "cancel-withdrawal-request", payout));

        Assert.Multiple(() =>
        {
            Assert.That(reply.Body, Is.EqualTo(
                """{"status":1,"description":"Validation Error","errors":{"timestamp":["Timestamp expired: 130s old, should be at most 60s old (required with merchant_id)"]}}"""));
            Assert.That(reply.Headers.ContainsKey(PraxisSigner.HeaderName), Is.False);
        });
    }

    [Test]
    public async Task ManageWithdrawal_NonNumericId_WrongCredentials()
    {
        var harness = new PraxisEmulatorHarness();
        var body = PraxisRequestBuilder.ManageWithdrawal(harness.Merchant, harness.Now, "cancel-withdrawal-request", null);
        body["withdrawal_request_id"] = "abc";

        var reply = await harness.SendAsync("agent/manage-withdrawal-request", body);

        Assert.That(reply.Body, Is.EqualTo("""{"status":401,"description":"Wrong credentials"}"""));
    }

    // ------------------------------------------------------------------ refund

    [TestCase(4700L)]
    [TestCase(4800L)]
    [TestCase(500L)]
    public async Task Refund_OfAnEWalletSale_SignedStatus1AndNothingStored(long amount)
    {
        var harness = new PraxisEmulatorHarness();
        var tid = Tid(await SaleAsync(harness, 107800));
        await harness.GetPageAsync($"pages/apm/{tid}");

        var reply = await harness.SendAsync("direct/refund", PraxisRequestBuilder.Refund(harness.Merchant, harness.Now, amount, tid));

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(reply.Body, Is.EqualTo("""{"status":1,"description":"Gateway for reference_id  does not allow refunding"}"""));
            Assert.That(reply.Headers[PraxisSigner.HeaderName], Is.EqualTo(PraxisSigner.ForResponse(reply.Json(), harness.Merchant.Secret)));
            Assert.That(await StoredCountAsync(harness), Is.EqualTo(1));
        });
    }

    // ------------------------------------------------------------------ case table data

    [Test]
    public void FollowUps_ReachableFromTheCardTable()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PraxisTestCases.FollowUp(PraxisEWalletCases.PageAbandoned), Is.Not.Null);
            Assert.That(PraxisTestCases.FollowUp(PraxisEWalletCases.PageOpenedAbandoned), Is.Not.Null);
            Assert.That(PraxisTestCases.FollowUp(PraxisEWalletCases.PendingTimeout), Is.Not.Null);
            Assert.That(PraxisTestCases.FollowUp("nothing"), Is.Null);
            Assert.That(PraxisEWalletCases.Button(20000, "maybe"), Is.Null);
            Assert.That(PraxisEWalletCases.PageOpen(20000), Is.Null);
        });
    }

    [Test]
    public void WalletToken_IsStableSixteenHexCharacters()
    {
        var token = PraxisWalletData.WalletToken("m", "c", "e@example.com");

        Assert.Multiple(() =>
        {
            Assert.That(token, Does.Match("^[0-9a-f]{16}$"));
            Assert.That(PraxisWalletData.WalletToken("m", "c", "e@example.com"), Is.EqualTo(token));
            Assert.That(PraxisWalletData.WalletToken("m", "c", "other@example.com"), Is.Not.EqualTo(token));
        });
    }

    [Test]
    public void Wallet_NoneStored_IsNull_AndSentDefaultsToEmpty()
    {
        var tx = EmulatedTransaction.Open("praxis", Env, "m", "sale", "EUR", 1, DateTime.UtcNow);

        Assert.Multiple(() =>
        {
            Assert.That(PraxisWalletData.Wallet(tx), Is.Null);
            Assert.That(PraxisWalletData.Sent(tx), Is.Empty);
        });
    }
}
