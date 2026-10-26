using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TestService.Api.BackgroundServices;
using TestService.Api.Configuration;
using TestService.Api.Emulators.Praxis;
using TestService.Api.Emulators.Transactions;
using TestService.Api.Models;
using TestService.Tests.Infrastructure;
using TestService.Tests.Praxis;

namespace TestService.Tests.Integration;

/// <summary>
/// End-to-end proof of the Praxis emulator through HTTP: merchants registered through
/// <c>/api/emulators/praxis/merchants</c>, signed requests to <c>/emulators/praxis/{env}/...</c>,
/// state in MongoDB, and notifications delivered to this host's own <c>/mock/{env}/notify</c>.
/// </summary>
[TestFixture]
public class PraxisEmulatorTests : IntegrationTestBase
{
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(15);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<MockingSettings>(o => o.AllowedCallbackHosts = ["localhost"]);
            // Deferred: Factory.Server does not exist yet while the host is being configured.
            services.AddHttpClient(CallbackDispatcher.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => Factory.Server.CreateHandler());
        });
    }

    /// <summary>An anonymous client that sees redirects (3DS submit -> return_url) instead of following them.</summary>
    private HttpClient NewEmulatorClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static string NewEnvironment() => $"px-{Guid.NewGuid():N}";

    private static long Unix => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static string NotifyUrl(string environment) => $"http://localhost/mock/{environment}/notify";

    private async Task<PraxisMerchant> RegisterAsync(string environment, double timeScale = 0, string merchantId = "m-1")
    {
        var merchant = new PraxisMerchant
        {
            Environment = environment,
            MerchantId = merchantId,
            ApplicationKey = "app-" + merchantId,
            Secret = "secret-" + merchantId,
            Gateways = [new PraxisGateway { Id = PraxisRequestBuilder.CardGateway, Kind = "card", PaymentProcessor = "Test Card Processor", PaymentMethod = "Credit Card" }],
            TimeScale = timeScale
        };
        var response = await Client.PostAsJsonAsync("/api/emulators/praxis/merchants", new PraxisMerchantRequest
        {
            Environment = merchant.Environment,
            MerchantId = merchant.MerchantId,
            ApplicationKey = merchant.ApplicationKey,
            Secret = merchant.Secret,
            Gateways = merchant.Gateways,
            TimeScale = timeScale
        });
        AssertStatusCode(response, HttpStatusCode.Created);
        merchant.Id = (await response.Content.ReadFromJsonAsync<JsonNode>())!["id"]!.GetValue<string>();
        return merchant;
    }

    private async Task<(HttpResponseMessage Response, JsonNode Body)> SendAsync(string environment, string path, JsonNode body, string secret, string? signature = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/emulators/praxis/{environment}/{path}")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation(PraxisSigner.HeaderName, signature ?? PraxisRequestBuilder.Sign(path, body, secret));
        using var client = NewEmulatorClient();
        var response = await client.SendAsync(request);
        AssertStatusCode(response, HttpStatusCode.OK);
        var text = await response.Content.ReadAsStringAsync();
        return (response, JsonNode.Parse(text)!);
    }

    private async Task<JsonNode> SignedAsync(string environment, string path, JsonNode body, PraxisMerchant merchant)
    {
        var (response, json) = await SendAsync(environment, path, body, merchant.Secret);
        Assert.That(PraxisSigner.Text(json, "status"), Is.EqualTo("0"), json.ToJsonString());
        Assert.That(response.Headers.GetValues(PraxisSigner.HeaderName).Single(),
            Is.EqualTo(PraxisSigner.ForResponse(json, merchant.Secret)), "response signature");
        return json;
    }

    private Task<JsonNode> SaleAsync(string environment, PraxisMerchant merchant, string cvv, long amount, string? notificationUrl = null) =>
        SignedAsync(environment, "api/direct-process", PraxisRequestBuilder.Sale(merchant, Unix, cvv, amount, notificationUrl), merchant);

    private Task<JsonNode> FindAsync(string environment, PraxisMerchant merchant, long tid) =>
        SignedAsync(environment, "agent/find-transaction", PraxisRequestBuilder.Find(merchant, Unix, tid), merchant);

    private static long Tid(JsonNode response) => response["transaction"]!["tid"]!.GetValue<long>();

    private static string Status(JsonNode response) => PraxisSigner.Text(response, "transaction.transaction_status");

    private async Task<List<MockRequestLog>> PollNotificationsAsync(string environment, int expected)
    {
        var deadline = DateTime.UtcNow + PollTimeout;
        while (true)
        {
            var logs = (await Client.GetFromJsonAsync<List<MockRequestLog>>($"/api/mocks/requests?environment={environment}"))!;
            if (logs.Count >= expected)
            {
                return logs.OrderBy(l => l.Timestamp).ToList();
            }
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"Expected {expected} notification(s) at /mock/{environment}/notify within {PollTimeout}, got {logs.Count}.");
            }
            await Task.Delay(100);
        }
    }

    private static void AssertSignedNotification(MockRequestLog received, PraxisMerchant merchant, long tid, string status)
    {
        var body = JsonNode.Parse(received.Body!)!;
        Assert.Multiple(() =>
        {
            Assert.That(received.Method, Is.EqualTo("POST"));
            Assert.That(received.Path, Is.EqualTo("/notify"));
            Assert.That(received.Headers[PraxisSigner.HeaderName], Is.EqualTo(PraxisSigner.ForNotification(body, merchant.Secret)));
            Assert.That(PraxisSigner.Text(body, "merchant_id"), Is.EqualTo(merchant.MerchantId));
            Assert.That(PraxisSigner.Text(body, "transaction.tid"), Is.EqualTo(tid.ToString()));
            Assert.That(PraxisSigner.Text(body, "transaction.transaction_status"), Is.EqualTo(status));
        });
    }

    // ------------------------------------------------------------------ merchants

    [Test]
    public async Task Merchants_Crud_HidesSecretAndKeepsItOnUpdate()
    {
        var environment = NewEnvironment();
        var merchant = await RegisterAsync(environment);

        var listed = (await Client.GetFromJsonAsync<JsonArray>($"/api/emulators/praxis/merchants?environment={environment}"))!;
        var view = listed.Single()!;
        Assert.Multiple(() =>
        {
            Assert.That(view["merchantId"]!.GetValue<string>(), Is.EqualTo("m-1"));
            Assert.That(view["secretLength"]!.GetValue<int>(), Is.EqualTo(merchant.Secret.Length));
            Assert.That(view.AsObject().ContainsKey("secret"), Is.False);
            Assert.That(listed.ToJsonString(), Does.Not.Contain(merchant.Secret));
        });

        // PUT without a secret keeps the stored one: requests signed with it still authenticate.
        var put = await Client.PutAsJsonAsync($"/api/emulators/praxis/merchants/{merchant.Id}", new PraxisMerchantRequest
        {
            Environment = environment, MerchantId = "m-1", ApplicationKey = merchant.ApplicationKey,
            Gateways = merchant.Gateways, TimeScale = 0, DropNotifications = true
        });
        AssertStatusCode(put, HttpStatusCode.OK);
        var sale = await SaleAsync(environment, merchant, "568", 4900);
        Assert.That(Status(sale), Is.EqualTo("approved"));

        var duplicate = await Client.PostAsJsonAsync("/api/emulators/praxis/merchants", new PraxisMerchantRequest
        {
            Environment = environment, MerchantId = "m-1", ApplicationKey = "x", Secret = "y", Gateways = merchant.Gateways
        });
        var invalid = await Client.PostAsJsonAsync("/api/emulators/praxis/merchants", new PraxisMerchantRequest { Environment = environment });
        var unknownPut = await Client.PutAsJsonAsync("/api/emulators/praxis/merchants/000000000000000000000000", new PraxisMerchantRequest());
        Assert.Multiple(() =>
        {
            AssertStatusCode(duplicate, HttpStatusCode.Conflict);
            AssertStatusCode(invalid, HttpStatusCode.BadRequest);
            AssertStatusCode(unknownPut, HttpStatusCode.NotFound);
        });

        AssertStatusCode(await Client.DeleteAsync($"/api/emulators/praxis/merchants/{merchant.Id}"), HttpStatusCode.NoContent);
        AssertStatusCode(await Client.DeleteAsync($"/api/emulators/praxis/merchants/{merchant.Id}"), HttpStatusCode.NotFound);
        AssertStatusCode(await Client.DeleteAsync("/api/emulators/praxis/merchants/not-an-id"), HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Merchants_PutWithAnotherMerchantsId_Conflict()
    {
        var environment = NewEnvironment();
        var first = await RegisterAsync(environment, merchantId: "m-1");
        var second = await RegisterAsync(environment, merchantId: "m-2");

        var put = await Client.PutAsJsonAsync($"/api/emulators/praxis/merchants/{second.Id}", new PraxisMerchantRequest
        {
            Environment = environment, MerchantId = first.MerchantId, ApplicationKey = second.ApplicationKey, Gateways = second.Gateways
        });

        AssertStatusCode(put, HttpStatusCode.Conflict);
    }

    [Test]
    public async Task TransactionRepository_StaleSave_RefusedAndStoreUnchanged()
    {
        var repository = Factory.Services.GetRequiredService<ITransactionRepository>();
        var tx = EmulatedTransaction.Open("praxis", NewEnvironment(), "m", "sale", "USD", 4900, DateTime.UtcNow);
        tx.Tid = await repository.NextTidAsync();
        await repository.InsertAsync(tx);

        var fresh = (await repository.GetAsync(tx.Tid))!;
        var stale = (await repository.GetAsync(tx.Tid))!;
        fresh.Approve(new StatusChange("0", "approved", "successful"), DateTime.UtcNow);
        Assert.That(await repository.TrySaveAsync(fresh), Is.True);

        stale.Reject(new StatusChange("0", "rejected", "failed"), DateTime.UtcNow);
        var staleVersion = stale.Version;
        var saved = await repository.TrySaveAsync(stale);
        var stored = (await repository.GetAsync(tx.Tid))!;

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.False);
            Assert.That(stale.Version, Is.EqualTo(staleVersion), "the stale copy's version is restored");
            Assert.That(stored.Status, Is.EqualTo(TransactionStatus.Approved));
            Assert.That(stored.StatusDetails, Is.EqualTo("approved"));
            Assert.That(stored.Version, Is.EqualTo(fresh.Version));
        });
    }

    [Test]
    public async Task EmulatorApi_WithoutToken_Unauthorized()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        Assert.Multiple(async () =>
        {
            AssertStatusCode(await Client.GetAsync("/api/emulators/praxis/merchants"), HttpStatusCode.Unauthorized);
            AssertStatusCode(await Client.PostAsJsonAsync("/api/emulators/praxis/merchants", new PraxisMerchantRequest()), HttpStatusCode.Unauthorized);
            AssertStatusCode(await Client.GetAsync("/api/emulators/praxis/transactions"), HttpStatusCode.Unauthorized);
        });
    }

    [Test]
    public async Task EnvironmentWithoutMerchant_404JsonAndNothingStored()
    {
        var environment = NewEnvironment();
        var merchant = new PraxisMerchant { Environment = environment, MerchantId = "m", ApplicationKey = "a", Secret = "s" };
        using var emulator = NewEmulatorClient();

        foreach (var path in new[] { "api/direct-process", "agent/find-transaction", "direct/refund", "pages/3ds/1000000" })
        {
            var response = path.StartsWith("pages", StringComparison.Ordinal)
                ? await emulator.GetAsync($"/emulators/praxis/{environment}/{path}")
                : await emulator.PostAsync($"/emulators/praxis/{environment}/{path}",
                    new StringContent(PraxisRequestBuilder.Sale(merchant, Unix, "568", 4900).ToJsonString(), Encoding.UTF8, "application/json"));
            AssertStatusCode(response, HttpStatusCode.NotFound);
            var body = await response.Content.ReadFromJsonAsync<JsonNode>();
            Assert.That(body!["message"]!.GetValue<string>(), Does.Contain(environment));
        }

        var unknownProvider = await emulator.GetAsync($"/emulators/nobody/{environment}/x");
        AssertStatusCode(unknownProvider, HttpStatusCode.NotFound);

        var stored = await Client.GetFromJsonAsync<JsonArray>($"/api/emulators/praxis/transactions?environment={environment}");
        Assert.That(stored, Is.Empty);
    }

    [Test]
    public async Task UnknownMerchantAndWrongSignature_Rejected()
    {
        var environment = NewEnvironment();
        var merchant = await RegisterAsync(environment);
        var stranger = new PraxisMerchant { Environment = environment, MerchantId = "stranger", ApplicationKey = "a", Secret = "s" };

        var (_, unknown) = await SendAsync(environment, "agent/find-transaction", PraxisRequestBuilder.Find(stranger, Unix, 1), stranger.Secret);
        var (response, wrongSignature) = await SendAsync(environment, "agent/find-transaction",
            PraxisRequestBuilder.Find(merchant, Unix, 1), merchant.Secret, signature: "00ff");

        Assert.Multiple(() =>
        {
            Assert.That(unknown.ToJsonString(), Is.EqualTo("""{"status":401,"description":"User not found"}"""));
            Assert.That(wrongSignature.ToJsonString(), Is.EqualTo("""{"status":1,"description":"Validation Error","errors":{"merchant_id":["Invalid signature"]}}"""));
            Assert.That(response.Headers.Contains(PraxisSigner.HeaderName), Is.False);
        });
    }

    // ------------------------------------------------------------------ card flows

    [TestCase("568", "approved", "[TEST] Transaction status: approved")]
    [TestCase("000", "rejected", "[TEST] Transaction status: declined")]
    [TestCase("444", "error", "[TEST] Transaction status: error. Reason: Internal server error")]
    [TestCase("555", "authorized", "[TEST] Transaction status: authorized")]
    [TestCase("801", "rejected", "Insufficient funds")]
    [TestCase("905", "rejected", "Transaction failed. Please contact our support team")]
    public async Task Sale_PerCvv_SignedResponseAndPersisted(string cvv, string status, string details)
    {
        var environment = NewEnvironment();
        var merchant = await RegisterAsync(environment);

        var sale = await SaleAsync(environment, merchant, cvv, 4900);
        var found = await FindAsync(environment, merchant, Tid(sale));
        var orderId = PraxisSigner.Text(sale, "session.order_id");
        var stored = (await Client.GetFromJsonAsync<JsonArray>(
            $"/api/emulators/praxis/transactions?environment={environment}&orderId={orderId}&tid={Tid(sale)}"))!;

        Assert.Multiple(() =>
        {
            Assert.That(Status(sale), Is.EqualTo(status));
            Assert.That(PraxisSigner.Text(sale, "transaction.status_details"), Is.EqualTo(details));
            Assert.That(Status(found), Is.EqualTo(status));
            Assert.That(stored, Has.Count.EqualTo(1));
            Assert.That(stored[0]!["status"]!.GetValue<string>(), Is.EqualTo(status));
            Assert.That(stored[0]!["merchantId"]!.GetValue<string>(), Is.EqualTo(merchant.MerchantId));
        });
    }

    /// <summary>
    /// Acceptance: BillingServer's field set (qa2 registration-request.json), fresh timestamp, test card re-encrypted with
    /// CVV 568 and a fresh signature: approved, signed, example key set, and a signed approved notification arrives.
    /// </summary>
    [Test]
    public async Task Sale_BillingServerFieldSet_ApprovedAndSignedNotificationDelivered()
    {
        var environment = NewEnvironment();
        var merchant = await RegisterAsync(environment);
        var examples = Path.Combine(TestContext.CurrentContext.TestDirectory, "Praxis", "Examples", "qa2", "card-sale-cvv-568-approved");
        var request = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(examples, "registration-request.json")))!.AsObject();
        var example = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(examples, "registration-response.json")))!;
        var timestamp = Unix;
        request["merchant_id"] = merchant.MerchantId;
        request["application_key"] = merchant.ApplicationKey;
        request["timestamp"] = timestamp;
        request["card_data"] = PraxisRequestBuilder.CardData(merchant.Secret, timestamp, "568");
        request["notification_url"] = NotifyUrl(environment);

        var sale = await SignedAsync(environment, "api/direct-process", request, merchant);

        Assert.Multiple(() =>
        {
            Assert.That(Status(sale), Is.EqualTo("approved"));
            Assert.That(KeySet(sale), Is.EqualTo(KeySet(example)));
        });
        var received = (await PollNotificationsAsync(environment, 1)).Single();
        AssertSignedNotification(received, merchant, Tid(sale), "approved");

        var callbacks = (await Client.GetFromJsonAsync<List<MockCallbackLog>>($"/api/mocks/callbacks?source=praxis&correlationId={Tid(sale)}"))!;
        Assert.That(callbacks.Single().ResponseStatusCode, Is.EqualTo(404), "the merchant's reply is only logged");
    }

    [Test]
    public async Task ThreeDs_PageApprove_FindApprovedAndNotificationDelivered()
    {
        using var emulator = NewEmulatorClient();
        var environment = NewEnvironment();
        var merchant = await RegisterAsync(environment, timeScale: 0.1); // 540 s -> 54 s to submit, notification after 1.5 s
        var sale = await SaleAsync(environment, merchant, "333", 4900, NotifyUrl(environment));
        var tid = Tid(sale);
        Assert.That(Status(sale), Is.EqualTo("initialized"));

        var page = await emulator.GetAsync(new Uri(PraxisSigner.Text(sale, "redirect_url")).PathAndQuery);
        AssertStatusCode(page, HttpStatusCode.OK);
        Assert.That(await page.Content.ReadAsStringAsync(), Does.Contain("sandbox_3ds_form"));

        var submit = await emulator.PostAsync($"/emulators/praxis/{environment}/pages/3ds/{tid}/submit?status=approved",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["error_code"] = "903" }));
        AssertStatusCode(submit, HttpStatusCode.Found);
        Assert.That(submit.Headers.Location?.ToString(), Is.EqualTo("https://merchant.example/return"));

        var found = await FindAsync(environment, merchant, tid);
        Assert.Multiple(() =>
        {
            Assert.That(Status(found), Is.EqualTo("approved"));
            Assert.That(PraxisSigner.Text(found, "transaction.status_details"), Is.EqualTo("Approved"));
        });
        AssertSignedNotification((await PollNotificationsAsync(environment, 1)).Single(), merchant, tid, "approved");

        var again = await emulator.PostAsync($"/emulators/praxis/{environment}/pages/3ds/{tid}/submit?status=declined",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["error_code"] = "903" }));
        AssertStatusCode(again, HttpStatusCode.Conflict);
    }

    [Test]
    public async Task ThreeDs_Abandoned_RejectedAfterScaledTimeout()
    {
        var environment = NewEnvironment();
        var merchant = await RegisterAsync(environment, timeScale: 0.002); // 540 s -> about 1.1 s
        var tid = Tid(await SaleAsync(environment, merchant, "333", 4900, NotifyUrl(environment)));

        // The stored transaction flips without any request touching it: the hosted deadline service and the
        // Mongo due-deadline query apply it. Read the store directly (the transactions endpoint applies due deadlines itself).
        var repository = Factory.Services.GetRequiredService<ITransactionRepository>();
        var deadline = DateTime.UtcNow + PollTimeout;
        TransactionStatus? stored = null;
        while (stored != TransactionStatus.Rejected && DateTime.UtcNow < deadline)
        {
            await Task.Delay(200);
            stored = (await repository.GetAsync(tid))!.Status;
        }
        Assert.That(stored, Is.EqualTo(TransactionStatus.Rejected), "applied by the background deadline service");

        var found = await FindAsync(environment, merchant, tid);

        Assert.Multiple(() =>
        {
            Assert.That(Status(found), Is.EqualTo("rejected"));
            Assert.That(PraxisSigner.Text(found, "transaction.status_details"),
                Is.EqualTo("Transaction timed out. Old status pending_async. New status declined"));
        });
        AssertSignedNotification((await PollNotificationsAsync(environment, 1)).Single(), merchant, tid, "rejected");
    }

    [Test]
    public async Task Transactions_TwoSales_NewestFirst()
    {
        var environment = NewEnvironment();
        var merchant = await RegisterAsync(environment);
        var first = Tid(await SaleAsync(environment, merchant, "568", 4900));
        var second = Tid(await SaleAsync(environment, merchant, "000", 4900));

        var stored = (await Client.GetFromJsonAsync<JsonArray>($"/api/emulators/praxis/transactions?environment={environment}"))!;

        Assert.That(stored.Select(t => t!["tid"]!.GetValue<long>()), Is.EqualTo(new[] { second, first }));
    }

    [Test]
    public async Task RefundsAndPayouts_FromSeedSales()
    {
        var environment = NewEnvironment();
        var merchant = await RegisterAsync(environment);
        var seed = await SaleAsync(environment, merchant, "568", 100000);

        var rejected = await SignedAsync(environment, "direct/refund", PraxisRequestBuilder.Refund(merchant, Unix, 4700, Tid(seed)), merchant);
        var pending = await SignedAsync(environment, "direct/refund", PraxisRequestBuilder.Refund(merchant, Unix, 33200, Tid(seed)), merchant);
        var approved = await SignedAsync(environment, "direct/refund", PraxisRequestBuilder.Refund(merchant, Unix, 5000, Tid(seed)), merchant);
        var pendingLater = await FindAsync(environment, merchant, Tid(pending));

        var cardToken = PraxisSigner.Text(seed, "transaction.card.card_token");
        var customerToken = PraxisSigner.Text(seed, "customer.customer_token");
        var x100 = await SignedAsync(environment, "api/direct-process", PraxisRequestBuilder.Payout(merchant, Unix, 1400, cardToken, customerToken), merchant);
        var stillPending = await SignedAsync(environment, "api/direct-process", PraxisRequestBuilder.Payout(merchant, Unix, 33300, cardToken, customerToken), merchant);
        var stillPendingLater = await FindAsync(environment, merchant, Tid(stillPending));

        Assert.Multiple(() =>
        {
            Assert.That(Status(rejected), Is.EqualTo("rejected"));
            Assert.That(Status(pending), Is.EqualTo("pending"));
            Assert.That(Status(approved), Is.EqualTo("approved"));
            Assert.That(Status(pendingLater), Is.EqualTo("rejected"));
            Assert.That(PraxisSigner.Text(pendingLater, "transaction.status_details"),
                Is.EqualTo("[TEST CASE - amount 332] transaction status changed from pending to rejected"));
            Assert.That(Status(x100), Is.EqualTo("approved"));
            Assert.That(PraxisSigner.Text(x100, "transaction.amount"), Is.EqualTo("140000"));
            Assert.That(PraxisSigner.Text(x100, "transaction.card.card_number"), Is.EqualTo("411111******1111"));
            Assert.That(Status(stillPendingLater), Is.EqualTo("pending"));
        });
    }

    [Test]
    public async Task CaptureAndCancel_AuthorizedSales()
    {
        var environment = NewEnvironment();
        var merchant = await RegisterAsync(environment);
        var toCapture = Tid(await SaleAsync(environment, merchant, "555", 5000));
        var toCancel = Tid(await SaleAsync(environment, merchant, "555", 5000));

        var capture = await SignedAsync(environment, "agent/manage-transaction",
            PraxisRequestBuilder.Manage(merchant, Unix, "capture-authorization", toCapture), merchant);
        var cancel = await SignedAsync(environment, "agent/manage-transaction",
            PraxisRequestBuilder.Manage(merchant, Unix, "cancel-authorization", toCancel), merchant);
        var (_, again) = await SendAsync(environment, "agent/manage-transaction",
            PraxisRequestBuilder.Manage(merchant, Unix, "capture-authorization", toCapture), merchant.Secret);

        Assert.Multiple(async () =>
        {
            Assert.That(Status(capture), Is.EqualTo("approved"));
            Assert.That(PraxisSigner.Text(capture, "transaction.status_details"), Is.EqualTo("[TEST] Transaction status: authorized"));
            Assert.That(Status(cancel), Is.EqualTo("rejected"));
            Assert.That(PraxisSigner.Text(cancel, "transaction.status_details"), Is.EqualTo("Transaction status: Cancelled"));
            Assert.That(again.ToJsonString(), Is.EqualTo("""{"status":1,"description":"Generic Error"}"""));
            Assert.That(Status(await FindAsync(environment, merchant, toCapture)), Is.EqualTo("approved"));
        });
    }

    private static SortedSet<string> KeySet(JsonNode? node, string prefix = "")
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        if (node is JsonObject obj)
        {
            foreach (var (key, value) in obj)
            {
                keys.Add(prefix + key);
                keys.UnionWith(KeySet(value, prefix + key + "."));
            }
        }
        return keys;
    }
}
