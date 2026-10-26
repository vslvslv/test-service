using System.Text.Json.Nodes;
using TestService.Api.Emulators.Praxis;
using TestService.Api.Emulators.Transactions;
using TestService.Tests.Praxis;
using static TestService.Unit.Emulators.PraxisEmulatorHarness;

namespace TestService.Unit.Emulators;

/// <summary>Every row of the case table through <see cref="PraxisEmulator"/>, with a fake clock.</summary>
[TestFixture]
public class PraxisEmulatorCaseTests
{
    private const string Approved = "[TEST] Transaction status: approved";
    private const string Declined = "[TEST] Transaction status: declined";
    private const string AuthRequired = "[TEST] Transaction status: pending_async. Reason: 3DS authentication required";

    // cvv, amount, status, code, details, final amount, processed amount (null = none), notifications
    private static readonly object?[][] SaleRows =
    [
        ["568", 4900L, "approved", "0", Approved, 4900L, 4900L, 1],
        ["000", 4900L, "rejected", "0", Declined, 4900L, null, 1],
        ["123", 4900L, "rejected", "0", Declined, 4900L, null, 1],
        ["353", 4900L, "partial_approved", "0", "[TEST] Transaction status: partial_approved. Reason: Notification with partial approved", 4900L, null, 1],
        ["444", 4900L, "error", "0", "[TEST] Transaction status: error. Reason: Internal server error", 4900L, null, 0],
        ["555", 4900L, "authorized", "0", "[TEST] Transaction status: authorized", 4900L, 4900L, 0],
        ["801", 4900L, "rejected", "GE-006", "Insufficient funds", 4900L, null, 0],
        ["900", 4900L, "rejected", "0", "Card has been lost", 4900L, null, 0],
        ["901", 4900L, "rejected", "0", "Card has been stolen", 4900L, null, 0],
        ["902", 4900L, "rejected", "0", "Declined due to fraud", 4900L, null, 0],
        ["903", 4900L, "rejected", "0", "Do not honor", 4900L, null, 0],
        ["904", 4900L, "rejected", "0", "Transaction declined", 4900L, null, 0],
        ["905", 4900L, "rejected", "GE-007", "Transaction failed. Please contact our support team", 4900L, null, 0],
        ["333", 4900L, "initialized", "530", AuthRequired, 4900L, null, 0],
        ["300", 4900L, "initialized", "530", AuthRequired, 4900L, null, 0],
        ["333", 33400L, "initialized", "530", AuthRequired, 33400L, null, 0],
        ["333", 33500L, "initialized", "530", AuthRequired, 33500L, null, 0],
        ["454", 49800L, "initialized", "530", AuthRequired, 49800L, null, 0],
        ["454", 4900L, "rejected", "0", Declined, 4900L, null, 1],
        ["588", 71300L, "initialized", "0", AuthRequired, 71300L, null, 0],
        ["588", 4900L, "rejected", "0", Declined, 4900L, null, 1],
        ["568", 1100L, "approved", "11", "[TEST CASE - amount 11] Changing final amount to 10", 1000L, 1100L, 0],
        ["568", 1200L, "approved", "12", "[TEST CASE - amount 12] Changing final currency", 1200L, 1200L, 0],
        ["568", 1300L, "approved", "13", "[TEST CASE - amount 13] Changing final currency and amount", 2600L, 1300L, 0],
        ["568", 1400L, "approved", "14", "[TEST CASE - amount 14] Changing final currency to x100", 140000L, 1400L, 0],
        ["568", 1900L, "approved", "19", "[TEST CASE - amount 19] Changing final amount to 20", 2000L, 1900L, 0],
        ["568", 4600L, "approved", "46", "[TEST CASE - amount 46] Changing final amount to x2", 9200L, 4600L, 0],
        ["568", 33900L, "approved", "0", Approved, 33900L, 33900L, 0],
        ["801", 2020L, "approved", "0", Approved, 2020L, 2020L, 0],
        ["801", 3000L, "pending", "0", "[TEST] Transaction status: pending", 3000L, 3000L, 0]
    ];

    [TestCaseSource(nameof(SaleRows))]
    public async Task Sale_CaseTableRow_FirstResponse(string cvv, long amount, string status, string code, string details,
        long finalAmount, long? processedAmount, int notifications)
    {
        var harness = new PraxisEmulatorHarness();

        var reply = await harness.SendAsync("api/direct-process", PraxisRequestBuilder.Sale(harness.Merchant, harness.Now, cvv, amount, NotificationUrl));
        var response = reply.Json()!;

        Assert.Multiple(() =>
        {
            Assert.That(reply.StatusCode, Is.EqualTo(200));
            Assert.That(Str(response, "status"), Is.EqualTo("0"));
            Assert.That(Str(response, "transaction.transaction_status"), Is.EqualTo(status));
            Assert.That(Str(response, "transaction.status_code"), Is.EqualTo(code));
            Assert.That(Str(response, "transaction.status_details"), Is.EqualTo(details));
            Assert.That(response["transaction"]!["amount"]!.GetValue<long>(), Is.EqualTo(finalAmount));
            Assert.That(response["transaction"]!["processed_amount"]?.GetValue<long>(), Is.EqualTo(processedAmount));
            Assert.That(harness.Delivery.Jobs, Has.Count.EqualTo(notifications));
            Assert.That(reply.Headers[PraxisSigner.HeaderName], Is.EqualTo(PraxisSigner.ForResponse(response, harness.Merchant.Secret)));
            Assert.That(Str(response, "transaction.card.card_number"), Is.EqualTo("411111******1111"));
            Assert.That(Str(response, "transaction.card.card_exp"), Is.EqualTo("12/2030"));
            Assert.That(Str(response, "transaction.card.card_type"), Is.EqualTo("VISA"));
        });
    }

    [Test]
    public async Task Sale_ByCardTokenWithoutCardData_TakesTheDefaultDecline()
    {
        var harness = new PraxisEmulatorHarness();
        var seed = await harness.SaleAsync("568", 4900);
        harness.Delivery.Jobs.Clear();
        var body = PraxisRequestBuilder.Sale(harness.Merchant, harness.Now, "568", 4900, NotificationUrl);
        body.Remove("card_data");
        body["card_token"] = Str(seed, "transaction.card.card_token");
        body["customer_token"] = Str(seed, "customer.customer_token");

        var reply = await harness.SendAsync("api/direct-process", body);
        var response = reply.Json()!;
        var stored = await harness.Transactions.GetAsync(Tid(response));

        Assert.Multiple(() =>
        {
            Assert.That(Str(response, "status"), Is.EqualTo("0"), reply.Body);
            Assert.That(Str(response, "transaction.transaction_status"), Is.EqualTo("rejected"));
            Assert.That(Str(response, "transaction.status_details"), Is.EqualTo(Declined));
            Assert.That(Str(response, "transaction.card.card_number"), Is.EqualTo("411111******1111"), "card from the seed sale");
            Assert.That(stored!.CaseName, Is.EqualTo("declined-other"));
            Assert.That(harness.Delivery.Jobs, Has.Count.EqualTo(1));
        });
    }

    // AMEX sends the trigger CVV with a fourth digit 1 (568 -> 5681) [doc]; other cards do not.
    [TestCase("378282246310005", "5681", "approved", "AMEX")]
    [TestCase("378282246310005", "3331", "initialized", "AMEX")]
    [TestCase("4111111111111111", "5681", "rejected", "VISA")]
    public async Task Sale_FourDigitCvv_SelectsTheCaseOnlyForAmex(string cardNumber, string cvv, string status, string cardType)
    {
        var harness = new PraxisEmulatorHarness();
        var body = PraxisRequestBuilder.Sale(harness.Merchant, harness.Now, cvv, 4900, NotificationUrl);
        body["card_data"] = PraxisRequestBuilder.CardData(harness.Merchant.Secret, harness.Now, cvv, cardNumber);

        var reply = await harness.SendAsync("api/direct-process", body);
        var response = reply.Json()!;

        Assert.Multiple(() =>
        {
            Assert.That(Str(response, "status"), Is.EqualTo("0"), reply.Body);
            Assert.That(Str(response, "transaction.transaction_status"), Is.EqualTo(status));
            Assert.That(Str(response, "transaction.card.card_type"), Is.EqualTo(cardType));
        });
    }

    [Test]
    public async Task Sale_3ds_HasRedirectUrlAndAsyncFlag()
    {
        var harness = new PraxisEmulatorHarness();
        var response = await harness.SaleAsync("333", 4900);
        var tid = Tid(response);

        var expected = $"http://localhost/emulators/praxis/{Env}/pages/3ds/{tid}";
        Assert.Multiple(() =>
        {
            Assert.That(Str(response, "redirect_url"), Is.EqualTo(expected));
            Assert.That(Str(response, "transaction.redirect_url"), Is.EqualTo(expected));
            Assert.That(Str(response, "transaction.is_async"), Is.EqualTo("1"));
            Assert.That(Str(response, "session.session_status"), Is.EqualTo("open"));
        });
    }

    [Test]
    public async Task Sale_588With713_HasNullRedirectKeyAndTimesOut()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var response = await harness.SaleAsync("588", 71300);

        Assert.Multiple(() =>
        {
            Assert.That(response["transaction"]!.AsObject().ContainsKey("redirect_url"), Is.True);
            Assert.That(response["redirect_url"], Is.Null);
            Assert.That(Str(response, "transaction.is_async"), Is.EqualTo("0"));
        });

        harness.Time.Advance(TimeSpan.FromSeconds(539));
        Assert.That(Str(await harness.FindAsync(Tid(response)), "transaction.transaction_status"), Is.EqualTo("initialized"));

        harness.Time.Advance(TimeSpan.FromSeconds(1));
        var found = await harness.FindAsync(Tid(response));
        Assert.Multiple(() =>
        {
            Assert.That(Str(found, "transaction.transaction_status"), Is.EqualTo("rejected"));
            Assert.That(Str(found, "transaction.status_code"), Is.EqualTo("0"));
            Assert.That(Str(found, "transaction.status_details"), Is.EqualTo("Transaction timed out. Old status pending_async. New status declined"));
            Assert.That(Str(found, "transaction.is_3d"), Is.EqualTo("0"));
            Assert.That(harness.Delivery.Jobs, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Sale_Amount30Cvv801_ApprovedOnNextStatusCheck()
    {
        var harness = new PraxisEmulatorHarness();
        var tid = Tid(await harness.SaleAsync("801", 3000));

        var first = await harness.FindAsync(tid);
        var second = await harness.FindAsync(tid);

        Assert.Multiple(() =>
        {
            Assert.That(Str(first, "transaction.transaction_status"), Is.EqualTo("approved"));
            Assert.That(Str(first, "transaction.status_code"), Is.EqualTo("30.00"));
            Assert.That(Str(first, "transaction.status_details"), Is.EqualTo("[TEST CASE - amount 30.00] transaction status changed from pending to approved"));
            Assert.That(Str(second, "transaction.transaction_status"), Is.EqualTo("approved"));
            Assert.That(harness.Delivery.Jobs, Is.Empty);
        });
    }

    [Test]
    public async Task Sale_Approved_NotificationIsSignedAndDelayedByScaledFifteenSeconds()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 0.5);
        var start = harness.Time.GetUtcNow();
        var tid = Tid(await harness.SaleAsync("568", 4900));

        var job = harness.Delivery.Jobs.Single();
        var body = JsonNode.Parse(job.Body!)!;
        Assert.Multiple(() =>
        {
            Assert.That(job.Source, Is.EqualTo("praxis"));
            Assert.That(job.Environment, Is.EqualTo(Env));
            Assert.That(job.CorrelationId, Is.EqualTo(tid.ToString()));
            Assert.That(job.Url, Is.EqualTo(NotificationUrl));
            Assert.That(job.Method, Is.EqualTo("POST"));
            Assert.That(job.DueAt, Is.EqualTo(start + TimeSpan.FromSeconds(7.5)));
            Assert.That(job.Headers["Content-Type"], Is.EqualTo("application/json"));
            Assert.That(job.Headers[PraxisSigner.HeaderName], Is.EqualTo(PraxisSigner.ForNotification(body, harness.Merchant.Secret)));
            Assert.That(Str(body, "merchant_id"), Is.EqualTo(harness.Merchant.MerchantId));
            Assert.That(Str(body, "transaction.transaction_status"), Is.EqualTo("approved"));
            Assert.That(body.AsObject().ContainsKey("status"), Is.False);
            Assert.That(Str(body, "timestamp"), Is.EqualTo((start + TimeSpan.FromSeconds(7.5)).ToUnixTimeSeconds().ToString()));
        });
    }

    [Test]
    public async Task Sale_DropNotificationsOrNoUrl_SendsNothing()
    {
        var harness = new PraxisEmulatorHarness();
        await harness.SaleAsync("568", 4900, notificationUrl: null);
        Assert.That(harness.Delivery.Jobs, Is.Empty);

        var dropping = PraxisEmulatorHarness.NewMerchant("m-drop");
        dropping.DropNotifications = true;
        await harness.Merchants.CreateAsync(dropping);
        await harness.SendAsync("api/direct-process", PraxisRequestBuilder.Sale(dropping, harness.Now, "568", 4900, NotificationUrl), dropping);
        Assert.That(harness.Delivery.Jobs, Is.Empty);
    }

    // ------------------------------------------------------------------ 3DS page decisions

    [Test]
    public async Task ThreeDs_Approve_FindReturnsApprovedAndNotifies()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var tid = Tid(await harness.SaleAsync("333", 4900));

        var page = await harness.PageAsync(tid);
        Assert.Multiple(() =>
        {
            Assert.That(page.StatusCode, Is.EqualTo(200));
            Assert.That(page.ContentType, Does.StartWith("text/html"));
            Assert.That(page.Body, Does.Contain("sandbox_3ds_form").And.Contain("error_code").And.Contain("Verification"));
            Assert.That(page.Body, Does.Contain($"http://emulator.test/emulators/praxis/{Env}/pages/3ds/{tid}/submit"));
        });

        var submit = await harness.SubmitAsync(tid, "approved");
        Assert.Multiple(() =>
        {
            Assert.That(submit.StatusCode, Is.EqualTo(302));
            Assert.That(submit.Headers["Location"], Is.EqualTo(ReturnUrl));
        });

        var found = await harness.FindAsync(tid);
        var job = harness.Delivery.Jobs.Single();
        Assert.Multiple(() =>
        {
            Assert.That(Str(found, "transaction.transaction_status"), Is.EqualTo("approved"));
            Assert.That(Str(found, "transaction.status_code"), Is.EqualTo("530"));
            Assert.That(Str(found, "transaction.status_details"), Is.EqualTo("Approved"));
            Assert.That(Str(found, "transaction.processed_amount"), Is.EqualTo("4900"));
            Assert.That(Str(found, "transaction.is_3d"), Is.EqualTo("1"));
            Assert.That(job.DueAt, Is.EqualTo(harness.Time.GetUtcNow() + TimeSpan.FromSeconds(15)));
            Assert.That(Str(JsonNode.Parse(job.Body!)!, "transaction.transaction_status"), Is.EqualTo("approved"));
        });

        // A second decision is refused, and the page is no longer offered.
        Assert.That((await harness.SubmitAsync(tid, "declined")).StatusCode, Is.EqualTo(409));
        Assert.That((await harness.PageAsync(tid)).StatusCode, Is.EqualTo(409));
    }

    [TestCase("903", "GE-006", "Do not honor")]
    [TestCase("906", "GE-006", ". Reason: Insufficient funds")]
    [TestCase("904", "904", "No status details")]
    [TestCase("900", "900", "Card has been lost")]
    [TestCase("901", "901", "Card has been  stolen")]
    [TestCase("902", "902", "Declined due to fraud")]
    [TestCase("912", "912", "Declined due to suspected-fraud")]
    [TestCase(null, "GE-006", "Do not honor")]
    [TestCase("777", "GE-006", "Do not honor")]
    public async Task ThreeDs_Decline_RejectsWithTheChosenCode(string? errorCode, string code, string details)
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var tid = Tid(await harness.SaleAsync("333", 4900));

        await harness.SubmitAsync(tid, "declined", errorCode);
        var found = await harness.FindAsync(tid);

        Assert.Multiple(() =>
        {
            Assert.That(Str(found, "transaction.transaction_status"), Is.EqualTo("rejected"));
            Assert.That(Str(found, "transaction.status_code"), Is.EqualTo(code));
            Assert.That(Str(found, "transaction.status_details"), Is.EqualTo(details));
            Assert.That(Str(found, "session.session_status"), Is.EqualTo("failed"));
            Assert.That(harness.Delivery.Jobs, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void ThreeDsPage_SubmitUrl_IsAJavaScriptStringNotHtmlEncoded()
    {
        var page = PraxisSimulationPages.ThreeDsPage("http://emulator.test/pages/3ds/1/submit?a=1&b='x'");

        Assert.Multiple(() =>
        {
            Assert.That(page, Does.Contain(@"""http://emulator.test/pages/3ds/1/submit?a=1\u0026b=\u0027x\u0027"" + '?status=' + status"));
            Assert.That(page, Does.Not.Contain("&amp;"));
        });
    }

    [Test]
    public async Task ThreeDs_Pending_ThenRejectedAfterScaledTimeout()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var tid = Tid(await harness.SaleAsync("333", 4900));

        await harness.SubmitAsync(tid, "pending");
        var pending = await harness.FindAsync(tid);
        harness.Time.Advance(TimeSpan.FromSeconds(540));
        var timedOut = await harness.FindAsync(tid);

        Assert.Multiple(() =>
        {
            Assert.That(Str(pending, "transaction.transaction_status"), Is.EqualTo("pending"));
            Assert.That(Str(pending, "transaction.status_code"), Is.EqualTo("1"));
            Assert.That(Str(pending, "transaction.status_details"), Is.EqualTo("Transaction in progress."));
            Assert.That(Str(pending, "transaction.processed_amount"), Is.EqualTo("4900"));
            Assert.That(Str(timedOut, "transaction.transaction_status"), Is.EqualTo("rejected"));
            Assert.That(Str(timedOut, "transaction.status_code"), Is.EqualTo("1"));
            Assert.That(Str(timedOut, "transaction.status_details"), Is.EqualTo("Transaction timed out. Old status pending. New status declined"));
            Assert.That(timedOut["transaction"]!["processed_amount"], Is.Null);
            Assert.That(harness.Delivery.Jobs.Select(j => Str(JsonNode.Parse(j.Body!)!, "transaction.transaction_status")),
                Is.EqualTo(new[] { "pending", "rejected" }));
            // As the qa2 card notifications: the session shows 0 unless approved.
            Assert.That(harness.Delivery.Jobs.Select(j => Str(JsonNode.Parse(j.Body!)!, "session.processed_amount")),
                Is.EqualTo(new[] { "0", "0" }));
            Assert.That(Str(JsonNode.Parse(harness.Delivery.Jobs[0].Body!)!, "transaction.processed_amount"), Is.EqualTo("4900"));
        });
    }

    [Test]
    public async Task ThreeDs_Verification_SetsVerification()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var tid = Tid(await harness.SaleAsync("300", 4900));

        await harness.SubmitAsync(tid, "verification");
        var found = await harness.FindAsync(tid);

        Assert.Multiple(() =>
        {
            Assert.That(Str(found, "transaction.transaction_status"), Is.EqualTo("verification"));
            Assert.That(Str(found, "transaction.status_details"), Is.EqualTo("Transaction set to verification status"));
            Assert.That(found["transaction"]!["processed_amount"], Is.Null);
            Assert.That(harness.Delivery.Jobs, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task ThreeDs_Abandoned_RejectedAfterScaledTimeoutAndNotified()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 0.1);
        var tid = Tid(await harness.SaleAsync("333", 4900));

        harness.Time.Advance(TimeSpan.FromSeconds(54));
        var found = await harness.FindAsync(tid);

        Assert.Multiple(() =>
        {
            Assert.That(Str(found, "transaction.transaction_status"), Is.EqualTo("rejected"));
            Assert.That(Str(found, "transaction.status_code"), Is.EqualTo("530"));
            Assert.That(Str(found, "transaction.status_details"), Is.EqualTo("Transaction timed out. Old status pending_async. New status declined"));
            Assert.That(harness.Delivery.Jobs, Has.Count.EqualTo(1));
        });
        Assert.That((await harness.SubmitAsync(tid, "approved")).StatusCode, Is.EqualTo(409), "page submitted after the timeout");
    }

    [Test]
    public async Task ThreeDs_SubmitAsTheFirstActionAfterTheTimeout_409AndTimedOutOnce()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var tid = Tid(await harness.SaleAsync("333", 4900));
        harness.Time.Advance(TimeSpan.FromSeconds(540));

        var submit = await harness.SubmitAsync(tid, "approved");
        var stored = await harness.Transactions.GetAsync(tid);

        Assert.Multiple(() =>
        {
            Assert.That(submit.StatusCode, Is.EqualTo(409));
            Assert.That(stored!.Status, Is.EqualTo(TransactionStatus.Rejected));
            Assert.That(stored.StatusDetails, Is.EqualTo("Transaction timed out. Old status pending_async. New status declined"));
            Assert.That(harness.Delivery.Jobs, Has.Count.EqualTo(1));
            Assert.That(Str(JsonNode.Parse(harness.Delivery.Jobs[0].Body!)!, "transaction.transaction_status"), Is.EqualTo("rejected"));
        });
    }

    [Test]
    public async Task ThreeDs_PageAsTheFirstActionAfterTheTimeout_409AndTimedOutOnce()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var tid = Tid(await harness.SaleAsync("333", 4900));
        harness.Time.Advance(TimeSpan.FromSeconds(540));

        var page = await harness.PageAsync(tid);
        var stored = await harness.Transactions.GetAsync(tid);

        Assert.Multiple(() =>
        {
            Assert.That(page.StatusCode, Is.EqualTo(409));
            Assert.That(stored!.Status, Is.EqualTo(TransactionStatus.Rejected));
            Assert.That(harness.Delivery.Jobs, Has.Count.EqualTo(1));
            Assert.That(Str(JsonNode.Parse(harness.Delivery.Jobs[0].Body!)!, "transaction.transaction_status"), Is.EqualTo("rejected"));
        });
    }

    [Test]
    public async Task ThreeDs_DuplicateStatus334_TwoNotificationsFiveSecondsApart()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var tid = Tid(await harness.SaleAsync("333", 33400));

        await harness.SubmitAsync(tid, "approved");
        var found = await harness.FindAsync(tid);
        var jobs = harness.Delivery.Jobs;

        Assert.Multiple(() =>
        {
            Assert.That(Str(found, "transaction.transaction_status"), Is.EqualTo("verification"));
            Assert.That(Str(found, "transaction.status_details"), Is.EqualTo(Declined));
            Assert.That(Str(found, "session.session_status"), Is.EqualTo("failed"));
            Assert.That(jobs, Has.Count.EqualTo(2));
            Assert.That(Str(JsonNode.Parse(jobs[0].Body!)!, "transaction.transaction_status"), Is.EqualTo("approved"));
            Assert.That(Str(JsonNode.Parse(jobs[1].Body!)!, "transaction.transaction_status"), Is.EqualTo("verification"));
            Assert.That(jobs[1].DueAt - jobs[0].DueAt, Is.EqualTo(TimeSpan.FromSeconds(5)));
        });
    }

    [Test]
    public async Task ThreeDs_DuplicateAmount335_VerificationWithAmount35000()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var tid = Tid(await harness.SaleAsync("333", 33500));

        await harness.SubmitAsync(tid, "approved");
        var found = await harness.FindAsync(tid);
        var jobs = harness.Delivery.Jobs;

        Assert.Multiple(() =>
        {
            Assert.That(Str(found, "transaction.transaction_status"), Is.EqualTo("verification"));
            Assert.That(Str(found, "transaction.amount"), Is.EqualTo("35000"));
            Assert.That(Str(found, "transaction.status_details"), Is.EqualTo(Approved));
            Assert.That(Str(JsonNode.Parse(jobs[0].Body!)!, "transaction.amount"), Is.EqualTo("33500"));
            Assert.That(Str(JsonNode.Parse(jobs[1].Body!)!, "transaction.amount"), Is.EqualTo("35000"));
        });
    }

    [Test]
    public async Task ThreeDs_PartialAtPsp498_ApprovedWith40000()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var tid = Tid(await harness.SaleAsync("454", 49800));

        await harness.SubmitAsync(tid, "approved");
        var found = await harness.FindAsync(tid);

        Assert.Multiple(() =>
        {
            Assert.That(Str(found, "transaction.transaction_status"), Is.EqualTo("approved"));
            Assert.That(Str(found, "transaction.amount"), Is.EqualTo("40000"));
            Assert.That(Str(found, "transaction.processed_amount"), Is.EqualTo("40000"));
            Assert.That(Str(found, "session.processed_amount"), Is.EqualTo("40000"));
            Assert.That(Str(found, "transaction.status_details"), Is.EqualTo("TEST amount 498, 3D partial approved with amount 400"));
        });
    }

    [Test]
    public async Task ThreeDs_AbandonAppliedLate_NotificationDueNow()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var tid = Tid(await harness.SaleAsync("333", 4900));

        harness.Time.Advance(TimeSpan.FromSeconds(600));
        await harness.FindAsync(tid);

        var job = harness.Delivery.Jobs.Single();
        var now = harness.Time.GetUtcNow();
        Assert.Multiple(() =>
        {
            Assert.That(job.DueAt, Is.EqualTo(now), "dueAt + 15 s is in the past, so it is clamped to now");
            Assert.That(Str(JsonNode.Parse(job.Body!)!, "timestamp"), Is.EqualTo(now.ToUnixTimeSeconds().ToString()));
        });
    }

    [Test]
    public async Task ThreeDs_AbandonAppliedAtDueTime_NotificationFifteenSecondsAfterDueTime()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var start = harness.Time.GetUtcNow();
        var tid = Tid(await harness.SaleAsync("333", 4900));

        harness.Time.Advance(TimeSpan.FromSeconds(540));
        await harness.FindAsync(tid);

        var dueAt = start + TimeSpan.FromSeconds(540);
        var job = harness.Delivery.Jobs.Single();
        Assert.Multiple(() =>
        {
            Assert.That(job.DueAt, Is.EqualTo(dueAt + TimeSpan.FromSeconds(15)));
            Assert.That(Str(JsonNode.Parse(job.Body!)!, "timestamp"), Is.EqualTo((dueAt + TimeSpan.FromSeconds(15)).ToUnixTimeSeconds().ToString()));
        });
    }

    [Test]
    public async Task Manage_NotAuthorized_GenericErrorAndStatusUnchanged()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var seed = await harness.SaleAsync("568", 4900);
        var payout = (await harness.SendAsync("api/direct-process", PraxisRequestBuilder.Payout(harness.Merchant, harness.Now, 33200,
            Str(seed, "transaction.card.card_token"), Str(seed, "customer.customer_token")))).Json()!;
        var threeDsTid = Tid(await harness.SaleAsync("333", 4900));

        var capture = await harness.ManageAsync("capture-authorization", Tid(payout));
        var cancel = await harness.ManageAsync("cancel-authorization", threeDsTid);

        Assert.Multiple(async () =>
        {
            Assert.That(capture.ToJsonString(), Is.EqualTo("""{"status":1,"description":"Generic Error"}"""));
            Assert.That(cancel.ToJsonString(), Is.EqualTo("""{"status":1,"description":"Generic Error"}"""));
            Assert.That(Str(await harness.FindAsync(Tid(payout)), "transaction.transaction_status"), Is.EqualTo("pending"));
            Assert.That(Str(await harness.FindAsync(threeDsTid), "transaction.transaction_status"), Is.EqualTo("initialized"));
        });
    }

    [Test]
    public async Task ThreeDs_PageEdgeCases()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var tid = Tid(await harness.SaleAsync("333", 4900, returnUrl: null));
        var plainTid = Tid(await harness.SaleAsync("568", 4900));

        Assert.Multiple(async () =>
        {
            Assert.That((await harness.PageAsync(999)).StatusCode, Is.EqualTo(404));
            Assert.That((await harness.SubmitAsync(999, "approved")).StatusCode, Is.EqualTo(404));
            Assert.That((await harness.PageAsync(plainTid)).StatusCode, Is.EqualTo(409));
            Assert.That((await harness.SubmitAsync(tid, "bogus")).StatusCode, Is.EqualTo(400));
            var done = await harness.SubmitAsync(tid, "approved");
            Assert.That(done.StatusCode, Is.EqualTo(200), "no return_url: a confirmation page");
            Assert.That(done.Body, Does.Contain("approved"));
        });
    }

    [Test]
    public async Task ThreeDs_PageOfAnotherEnvironment_IsNotFound()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var tid = Tid(await harness.SaleAsync("333", 4900));

        var reply = await harness.Emulator.HandleAsync(new TestService.Api.Emulators.EmulatorRequest
        {
            Environment = "other", Path = $"pages/3ds/{tid}", Method = "GET"
        });

        Assert.That(reply.StatusCode, Is.EqualTo(404));
    }

    // ------------------------------------------------------------------ deadlines in the background

    [Test]
    public async Task ApplyDueDeadlines_RejectsAbandonedOnceAndLaterReadsDoNotNotifyAgain()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var tid = Tid(await harness.SaleAsync("333", 4900));

        Assert.That(await harness.Emulator.ApplyDueDeadlinesAsync(), Is.EqualTo(0));
        harness.Time.Advance(TimeSpan.FromSeconds(540));
        Assert.That(await harness.Emulator.ApplyDueDeadlinesAsync(), Is.EqualTo(1));
        Assert.That(await harness.Emulator.ApplyDueDeadlinesAsync(), Is.EqualTo(0));

        var found = await harness.FindAsync(tid);
        Assert.Multiple(() =>
        {
            Assert.That(Str(found, "transaction.transaction_status"), Is.EqualTo("rejected"));
            Assert.That(harness.Delivery.Jobs, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task ConcurrentSave_IsRetriedAgainstTheNewVersion()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var tid = Tid(await harness.SaleAsync("333", 4900));
        harness.Transactions.BeforeNextSave = store => store.Touch(tid);

        var submit = await harness.SubmitAsync(tid, "approved");

        Assert.Multiple(async () =>
        {
            Assert.That(submit.StatusCode, Is.EqualTo(302));
            Assert.That(harness.Transactions.Conflicts, Is.EqualTo(1));
            Assert.That(Str(await harness.FindAsync(tid), "transaction.transaction_status"), Is.EqualTo("approved"));
            Assert.That(harness.Delivery.Jobs, Has.Count.EqualTo(1), "notifications are enqueued only for the save that won");
        });
    }

    [Test]
    public async Task ConcurrentSave_GivesUpAfterRepeatedConflicts()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var tid = Tid(await harness.SaleAsync("333", 4900));
        void Conflict(InMemoryTransactionRepository store)
        {
            store.Touch(tid);
            store.BeforeNextSave = Conflict;
        }
        harness.Transactions.BeforeNextSave = Conflict;

        var submit = await harness.SubmitAsync(tid, "approved");
        harness.Transactions.BeforeNextSave = null;
        var stored = await harness.Transactions.GetAsync(tid);

        Assert.Multiple(() =>
        {
            Assert.That(submit.StatusCode, Is.EqualTo(409));
            Assert.That(submit.Json()!["message"]!.GetValue<string>(),
                Is.EqualTo($"Transaction {tid}: the decision could not be saved because of concurrent updates; try again"));
            Assert.That(harness.Transactions.Conflicts, Is.EqualTo(5));
            Assert.That(harness.Delivery.Jobs, Is.Empty);
            Assert.That(stored!.Status, Is.EqualTo(TransactionStatus.Initialized), "still awaiting 3DS");
            Assert.That(stored.Deadline!.FollowUp, Is.EqualTo(PraxisTestCases.ThreeDsAbandoned));
        });
    }

    // ------------------------------------------------------------------ payouts, refunds, capture

    [TestCase(1400L, "approved", "14", "[TEST CASE - amount 14] Changing final currency to x100", 140000L)]
    [TestCase(4600L, "approved", "46", "[TEST CASE - amount 46] Changing final amount to x2", 9200L)]
    [TestCase(4700L, "rejected", "0", Declined, 4700L)]
    [TestCase(4800L, "error", "0", "[TEST] Transaction status: error. Reason: Internal server error", 4800L)]
    [TestCase(33200L, "pending", "0", "[TEST] Transaction status: pending. Reason: Awaiting authorization", 33200L)]
    [TestCase(33300L, "pending", "0", "[TEST] Transaction status: pending. Reason: Awaiting authorization", 33300L)]
    [TestCase(5000L, "approved", "0", Approved, 5000L)]
    public async Task Payout_CaseTableRow(long amount, string status, string code, string details, long finalAmount)
    {
        var harness = new PraxisEmulatorHarness();
        var seed = await harness.SaleAsync("568", 4900);
        harness.Delivery.Jobs.Clear();

        var reply = await harness.SendAsync("api/direct-process", PraxisRequestBuilder.Payout(harness.Merchant, harness.Now, amount,
            Str(seed, "transaction.card.card_token"), Str(seed, "customer.customer_token")));
        var response = reply.Json()!;
        var later = await harness.FindAsync(Tid(response));

        Assert.Multiple(() =>
        {
            Assert.That(Str(response, "transaction.transaction_type"), Is.EqualTo("payout"));
            Assert.That(Str(response, "transaction.transaction_status"), Is.EqualTo(status));
            Assert.That(Str(response, "transaction.status_code"), Is.EqualTo(code));
            Assert.That(Str(response, "transaction.status_details"), Is.EqualTo(details));
            Assert.That(response["transaction"]!["amount"]!.GetValue<long>(), Is.EqualTo(finalAmount));
            Assert.That(Str(response, "session.intent"), Is.EqualTo("withdrawal"));
            Assert.That(response["transaction"]!["card"]!["card_token"], Is.Null);
            Assert.That(Str(response, "transaction.card.card_number"), Is.EqualTo("411111******1111"));
            Assert.That(Str(response, "customer.customer_token"), Is.EqualTo(Str(seed, "customer.customer_token")));
            Assert.That(Str(later, "transaction.transaction_status"), Is.EqualTo(status), "payouts keep their status");
            Assert.That(harness.Delivery.Jobs, Is.Empty);
        });
    }

    [TestCase(4700L, "rejected", Declined, "rejected", Declined)]
    [TestCase(4800L, "error", "[TEST] Transaction status: error. Reason: Internal server error", "error", "[TEST] Transaction status: error. Reason: Internal server error")]
    [TestCase(33200L, "pending", "[TEST] Transaction status: pending. Reason: Awaiting authorization", "rejected", "[TEST CASE - amount 332] transaction status changed from pending to rejected")]
    [TestCase(33300L, "pending", "[TEST] Transaction status: pending. Reason: Awaiting authorization", "approved", "[TEST CASE - amount 333] transaction status changed from pending to approved")]
    [TestCase(5000L, "approved", Approved, "approved", Approved)]
    public async Task Refund_CaseTableRow(long amount, string status, string details, string laterStatus, string laterDetails)
    {
        var harness = new PraxisEmulatorHarness();
        var seedTid = Tid(await harness.SaleAsync("568", 100000));

        var refund = await harness.RefundAsync(amount, seedTid);
        var later = await harness.FindAsync(Tid(refund));

        Assert.Multiple(() =>
        {
            Assert.That(Str(refund, "status"), Is.EqualTo("0"));
            Assert.That(Str(refund, "transaction.transaction_type"), Is.EqualTo("refund"));
            Assert.That(Str(refund, "transaction.transaction_status"), Is.EqualTo(status));
            Assert.That(Str(refund, "transaction.status_details"), Is.EqualTo(details));
            Assert.That(Str(refund, "transaction.reference_id"), Is.EqualTo(seedTid.ToString()));
            Assert.That(refund["transaction"]!["is_async"], Is.Null);
            Assert.That(Str(refund, "transaction.card.card_number"), Is.EqualTo("411111******1111"));
            Assert.That(Str(later, "transaction.transaction_status"), Is.EqualTo(laterStatus));
            Assert.That(Str(later, "transaction.status_details"), Is.EqualTo(laterDetails));
        });
    }

    [TestCase(4700L, "rejected", null)]
    [TestCase(4800L, "error", null)]
    [TestCase(5000L, "approved", "5000")]
    public async Task Capture_AuthorizedSale(long amount, string status, string? processed)
    {
        var harness = new PraxisEmulatorHarness();
        var tid = Tid(await harness.SaleAsync("555", amount));

        var capture = await harness.ManageAsync("capture-authorization", tid);
        var found = await harness.FindAsync(tid);

        Assert.Multiple(() =>
        {
            Assert.That(Str(capture, "status"), Is.EqualTo("0"));
            Assert.That(capture.AsObject().Select(p => p.Key), Is.EqualTo(new[] { "status", "description", "transaction", "version", "timestamp" }));
            Assert.That(Str(capture, "transaction.transaction_status"), Is.EqualTo(status));
            Assert.That(Str(capture, "transaction.status_details"), Is.EqualTo("[TEST] Transaction status: authorized"));
            Assert.That(Str(capture, "transaction.processed_amount"), Is.EqualTo(processed));
            Assert.That(Str(found, "transaction.transaction_status"), Is.EqualTo(status));
        });
    }

    [Test]
    public async Task Cancel_AuthorizedSale_Rejected()
    {
        var harness = new PraxisEmulatorHarness();
        var tid = Tid(await harness.SaleAsync("555", 5000));

        var cancel = await harness.ManageAsync("cancel-authorization", tid);

        Assert.Multiple(() =>
        {
            Assert.That(Str(cancel, "transaction.transaction_status"), Is.EqualTo("rejected"));
            Assert.That(Str(cancel, "transaction.status_details"), Is.EqualTo("Transaction status: Cancelled"));
        });
    }

    [Test]
    public async Task Manage_RefusedOrUnknown_GenericErrorOrBlank()
    {
        var harness = new PraxisEmulatorHarness();
        var approvedTid = Tid(await harness.SaleAsync("568", 5000));

        var captureApproved = await harness.ManageAsync("capture-authorization", approvedTid);
        var unknownIntent = await harness.ManageAsync("refund-everything", approvedTid);
        var blankIntent = await harness.ManageAsync("", approvedTid);
        var noTid = (await harness.SendAsync("agent/manage-transaction", new JsonObject
        {
            ["merchant_id"] = harness.Merchant.MerchantId, ["application_key"] = harness.Merchant.ApplicationKey,
            ["timestamp"] = harness.Now, ["intent"] = "capture-authorization"
        })).Json()!;
        var unknownTid = await harness.ManageAsync("capture-authorization", 42);

        Assert.Multiple(() =>
        {
            Assert.That(captureApproved.ToJsonString(), Is.EqualTo("""{"status":1,"description":"Generic Error"}"""));
            Assert.That(unknownIntent.ToJsonString(), Is.EqualTo("""{"status":1,"description":"Generic Error"}"""));
            Assert.That(blankIntent.ToJsonString(), Is.EqualTo("""{"status":400,"description":"Intent cannot be blank."}"""));
            Assert.That(noTid.ToJsonString(), Is.EqualTo("""{"status":400,"description":"Tid cannot be blank."}"""));
            Assert.That(unknownTid.ToJsonString(), Is.EqualTo("""{"status":401,"description":"Wrong credentials"}"""));
        });
    }
}
