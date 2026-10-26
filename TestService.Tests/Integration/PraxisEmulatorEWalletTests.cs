using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TestService.Api.BackgroundServices;
using TestService.Api.Configuration;
using TestService.Api.Emulators.Praxis;
using TestService.Api.Models;
using TestService.Tests.Infrastructure;
using TestService.Tests.Praxis;

namespace TestService.Tests.Integration;

/// <summary>
/// End-to-end proof of the Praxis "Test E-Wallet" emulation through HTTP: BillingServer's deposit request,
/// the page chain (wait page -> APM page -> button -> 302 to <c>return_url</c>), find-transaction, the signed
/// notification delivered to this host's own <c>/mock/{env}/notify</c> (state in MongoDB, ts-2 delivery),
/// page-open amounts, payouts with manage-withdrawal-request, the e-wallet refund rejection and the gateway lookups.
/// </summary>
[TestFixture]
public class PraxisEmulatorEWalletTests : IntegrationTestBase
{
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(15);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<MockingSettings>(o => o.AllowedCallbackHosts = ["localhost"]);
            services.AddHttpClient(CallbackDispatcher.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => Factory.Server.CreateHandler());
        });
    }

    private HttpClient NewEmulatorClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static string NewEnvironment() => $"pxw-{Guid.NewGuid():N}";

    private static long Unix => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static string NotifyUrl(string environment) => $"http://localhost/mock/{environment}/notify";

    private static string Qa2(string folder, string file) =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "Praxis", "Examples", "qa2", folder, file);

    private async Task<PraxisMerchant> RegisterAsync(string environment)
    {
        var merchant = new PraxisMerchant
        {
            Environment = environment,
            MerchantId = "w-1",
            ApplicationKey = "app-w-1",
            Secret = "secret-w-1",
            Gateways =
            [
                new PraxisGateway { Id = PraxisRequestBuilder.CardGateway, Kind = "card" },
                new PraxisGateway { Id = PraxisRequestBuilder.EWalletGateway, Kind = "ewallet", PaymentProcessor = "Test E-Wallet", PaymentMethod = "altbankwire" }
            ],
            TimeScale = 0
        };
        var response = await Client.PostAsJsonAsync("/api/emulators/praxis/merchants", new PraxisMerchantRequest
        {
            Environment = merchant.Environment,
            MerchantId = merchant.MerchantId,
            ApplicationKey = merchant.ApplicationKey,
            Secret = merchant.Secret,
            Gateways = merchant.Gateways,
            TimeScale = 0
        });
        AssertStatusCode(response, HttpStatusCode.Created);
        return merchant;
    }

    private async Task<(HttpResponseMessage Response, JsonNode Body)> SendAsync(string environment, string path, JsonNode body, string secret)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/emulators/praxis/{environment}/{path}")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation(PraxisSigner.HeaderName, PraxisRequestBuilder.Sign(path, body, secret));
        using var client = NewEmulatorClient();
        var response = await client.SendAsync(request);
        AssertStatusCode(response, HttpStatusCode.OK);
        return (response, JsonNode.Parse(await response.Content.ReadAsStringAsync())!);
    }

    /// <summary>A signed reply: the GT-Authentication header must verify against the body.</summary>
    private async Task<JsonNode> SignedAsync(string environment, string path, JsonNode body, PraxisMerchant merchant, string expectedStatus = "0")
    {
        var (response, json) = await SendAsync(environment, path, body, merchant.Secret);
        Assert.Multiple(() =>
        {
            Assert.That(PraxisSigner.Text(json, "status"), Is.EqualTo(expectedStatus), json.ToJsonString());
            Assert.That(response.Headers.GetValues(PraxisSigner.HeaderName).Single(),
                Is.EqualTo(PraxisSigner.ForResponse(json, merchant.Secret)), "response signature");
        });
        return json;
    }

    private Task<JsonNode> FindAsync(string environment, PraxisMerchant merchant, long tid) =>
        SignedAsync(environment, "agent/find-transaction", PraxisRequestBuilder.Find(merchant, Unix, tid), merchant);

    private static long Tid(JsonNode response) => response["transaction"]!["tid"]!.GetValue<long>();

    private static string Status(JsonNode response) => PraxisSigner.Text(response, "transaction.transaction_status");

    private static string RelativePath(string url) => new Uri(url).PathAndQuery;

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

    // ------------------------------------------------------------------ acceptance

    /// <summary>
    /// Acceptance: BillingServer's deposit field set (qa2 <c>deposit-request.json</c>) with a fresh timestamp and signature,
    /// merchant with an <c>ewallet</c> gateway and <c>timeScale: 0</c>: wait page -> APM page -> Approve -> 302 to
    /// <c>return_url</c>; find-transaction approved; a signed approved notification with the example's key set.
    /// </summary>
    [Test]
    public async Task Deposit_BillingServerFieldSet_PageApprove_FindApprovedAndSignedNotificationDelivered()
    {
        using var browser = NewEmulatorClient();
        var environment = NewEnvironment();
        var merchant = await RegisterAsync(environment);
        var request = JsonNode.Parse(await File.ReadAllTextAsync(Qa2("ewallet-sale-default-approve", "deposit-request.json")))!.AsObject();
        var exampleResponse = JsonNode.Parse(await File.ReadAllTextAsync(Qa2("ewallet-sale-default-approve", "deposit-response.json")))!;
        var exampleNotification = JsonNode.Parse(await File.ReadAllTextAsync(Qa2("ewallet-sale-default-approve", "notification-1.json")))!;
        request["merchant_id"] = merchant.MerchantId;
        request["application_key"] = merchant.ApplicationKey;
        request["timestamp"] = Unix;
        request["notification_url"] = NotifyUrl(environment);

        var deposit = await SignedAsync(environment, "direct/apm", request, merchant);
        var tid = Tid(deposit);
        Assert.Multiple(() =>
        {
            Assert.That(Status(deposit), Is.EqualTo("initialized"));
            Assert.That(KeySet(deposit), Is.EqualTo(KeySet(exampleResponse)));
        });

        var wait = await browser.GetAsync(RelativePath(PraxisSigner.Text(deposit, "redirect_url")));
        AssertStatusCode(wait, HttpStatusCode.OK);
        var apmPage = $"/emulators/praxis/{environment}/pages/apm/{tid}";
        Assert.That(await wait.Content.ReadAsStringAsync(), Does.Contain(apmPage));

        var page = await browser.GetAsync(apmPage);
        AssertStatusCode(page, HttpStatusCode.OK);
        Assert.That(await page.Content.ReadAsStringAsync(), Does.Contain("setStatus('approved')"));

        var approve = await browser.GetAsync($"{apmPage}/response?status=approved");
        AssertStatusCode(approve, HttpStatusCode.Found);
        Assert.That(approve.Headers.Location?.ToString(), Is.EqualTo("https://merchant.example/return"));

        var found = await FindAsync(environment, merchant, tid);
        Assert.Multiple(() =>
        {
            Assert.That(Status(found), Is.EqualTo("approved"));
            Assert.That(PraxisSigner.Text(found, "transaction.status_details"), Is.EqualTo("[TEST] Transaction status: approved"));
            Assert.That(PraxisSigner.Text(found, "transaction.wallet.data.customer_email"), Is.EqualTo("jane.doe@example.com"));
        });

        var received = (await PollNotificationsAsync(environment, 1)).Single();
        var notification = JsonNode.Parse(received.Body!)!;
        Assert.Multiple(() =>
        {
            Assert.That(received.Method, Is.EqualTo("POST"));
            Assert.That(received.Headers[PraxisSigner.HeaderName], Is.EqualTo(PraxisSigner.ForNotification(notification, merchant.Secret)));
            Assert.That(PraxisSigner.Text(notification, "transaction.tid"), Is.EqualTo(tid.ToString()));
            Assert.That(Status(notification), Is.EqualTo("approved"));
            Assert.That(KeySet(notification), Is.EqualTo(KeySet(exampleNotification)));
        });

        var again = await browser.GetAsync($"{apmPage}/response?status=declined");
        AssertStatusCode(again, HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Sale1078_PageOpen_ApprovedWithChangedAmount()
    {
        using var browser = NewEmulatorClient();
        var environment = NewEnvironment();
        var merchant = await RegisterAsync(environment);
        var sale = await SignedAsync(environment, "direct/apm", PraxisRequestBuilder.Apm(merchant, Unix, "sale", 107800), merchant);
        var tid = Tid(sale);

        var page = await browser.GetAsync($"/emulators/praxis/{environment}/pages/apm/{tid}");
        AssertStatusCode(page, HttpStatusCode.Found);

        var found = await FindAsync(environment, merchant, tid);
        Assert.Multiple(() =>
        {
            Assert.That(Status(found), Is.EqualTo("approved"));
            Assert.That(found["transaction"]!["amount"]!.GetValue<long>(), Is.EqualTo(108000));
            Assert.That(PraxisSigner.Text(found, "transaction.status_details"), Is.EqualTo("[TEST] Change amount less than 0.4%"));
            Assert.That(PraxisSigner.Text(found, "transaction.wallet.data.customer_country"), Is.EqualTo("country_ukraine"));
        });
    }

    [Test]
    public async Task Sale_MissingWalletDataFields_Status400AndNothingStored()
    {
        var environment = NewEnvironment();
        var merchant = await RegisterAsync(environment);
        var body = PraxisRequestBuilder.Apm(merchant, Unix, "sale", 20000,
            walletData: new JsonObject { ["name"] = "Jane Doe", ["email"] = "jane.doe@example.com", ["test"] = "1" });

        var (response, json) = await SendAsync(environment, "direct/apm", body, merchant.Secret);

        Assert.Multiple(() =>
        {
            Assert.That(json.ToJsonString(), Is.EqualTo(
                """{"status":400,"description":"[phone] field is required!,[ip_address] field is required!,[QA] field is required!"}"""));
            Assert.That(response.Headers.Contains(PraxisSigner.HeaderName), Is.False);
        });
        var stored = (await Client.GetFromJsonAsync<JsonArray>($"/api/emulators/praxis/transactions?environment={environment}"))!;
        Assert.That(stored, Is.Empty);
    }

    [Test]
    public async Task Payouts_CancelAndCompleteReplies_WalletTokenPayout()
    {
        using var browser = NewEmulatorClient();
        var environment = NewEnvironment();
        var merchant = await RegisterAsync(environment);

        var approved = await SignedAsync(environment, "direct/apm", PraxisRequestBuilder.Apm(merchant, Unix, "payout", 50000), merchant);
        var pending = await SignedAsync(environment, "direct/apm", PraxisRequestBuilder.Apm(merchant, Unix, "payout", 70000), merchant);
        var redirect = await SignedAsync(environment, "direct/apm", PraxisRequestBuilder.Apm(merchant, Unix, "payout", 30000), merchant);
        Assert.Multiple(() =>
        {
            Assert.That(Status(approved), Is.EqualTo("approved"));
            Assert.That(PraxisSigner.Text(approved, "transaction.status_details"), Is.EqualTo("[TEST] Payout capture successfully"));
            Assert.That(Status(pending), Is.EqualTo("pending"));
            Assert.That(Status(redirect), Is.EqualTo("initialized"));
        });

        var payoutPage = await browser.GetAsync(RelativePath(PraxisSigner.Text(redirect, "redirect_url")));
        AssertStatusCode(payoutPage, HttpStatusCode.OK);
        Assert.That(await payoutPage.Content.ReadAsStringAsync(), Does.Contain("Back to merchant website"));

        var cancel = await SignedAsync(environment, "agent/manage-withdrawal-request",
            PraxisRequestBuilder.ManageWithdrawal(merchant, Unix, "cancel-withdrawal-request", Tid(approved)), merchant, "400");
        var complete = await SignedAsync(environment, "agent/manage-withdrawal-request",
            PraxisRequestBuilder.ManageWithdrawal(merchant, Unix, "complete-withdrawal-request", Tid(approved)), merchant, "400");
        var completePending = await SignedAsync(environment, "agent/manage-withdrawal-request",
            PraxisRequestBuilder.ManageWithdrawal(merchant, Unix, "complete-withdrawal-request", Tid(pending), PraxisRequestBuilder.EWalletGateway), merchant, "400");
        Assert.Multiple(() =>
        {
            Assert.That(PraxisSigner.Text(cancel, "description"), Is.EqualTo("Payout request is already processed"));
            Assert.That(PraxisSigner.Text(complete, "description"), Is.EqualTo("The gateway field is required because the payment method is not Credit Card."));
            Assert.That(PraxisSigner.Text(completePending, "description"),
                Is.EqualTo("Payout request status must be \"requested\" in order to proceed with processing (currently \"pending\")"));
        });

        // A wallet-token payout (BillingServer's withdrawal path) reuses the wallet of an approved sale, not of a later
        // direct/apm payout with the same token (its wallet_data is only the e-mail).
        var sale = await SignedAsync(environment, "direct/apm", PraxisRequestBuilder.Apm(merchant, Unix, "sale", 20000), merchant);
        AssertStatusCode(await browser.GetAsync($"/emulators/praxis/{environment}/pages/apm/{Tid(sale)}/response?status=approved"), HttpStatusCode.Found);
        var found = await FindAsync(environment, merchant, Tid(sale));
        var later = await SignedAsync(environment, "direct/apm", PraxisRequestBuilder.Apm(merchant, Unix, "payout", 5100), merchant);
        Assert.That(PraxisSigner.Text(later, "transaction.wallet.wallet_token"), Is.EqualTo(PraxisSigner.Text(found, "transaction.wallet.wallet_token")));
        var byToken = await SignedAsync(environment, "direct/wallet-token", PraxisRequestBuilder.WalletTokenPayout(merchant, Unix, 5100,
            PraxisSigner.Text(found, "transaction.wallet.wallet_token"), PraxisSigner.Text(found, "customer.customer_token")), merchant);
        Assert.Multiple(() =>
        {
            Assert.That(Status(byToken), Is.EqualTo("approved"));
            Assert.That(PraxisSigner.Text(byToken, "transaction.wallet.wallet_token"), Is.EqualTo(PraxisSigner.Text(found, "transaction.wallet.wallet_token")));
            Assert.That(PraxisSigner.Text(byToken, "transaction.wallet.data.email"), Is.EqualTo("jane.doe@example.com"));
            Assert.That(PraxisSigner.Text(byToken, "transaction.wallet.data.phone"), Is.EqualTo("+10000000000"), "from the sale's wallet_data");
        });
    }

    [Test]
    public async Task Refund_OfAnEWalletSale_SignedRejection()
    {
        using var browser = NewEmulatorClient();
        var environment = NewEnvironment();
        var merchant = await RegisterAsync(environment);
        var sale = await SignedAsync(environment, "direct/apm", PraxisRequestBuilder.Apm(merchant, Unix, "sale", 107800), merchant);
        AssertStatusCode(await browser.GetAsync($"/emulators/praxis/{environment}/pages/apm/{Tid(sale)}"), HttpStatusCode.Found);

        var refund = await SignedAsync(environment, "direct/refund", PraxisRequestBuilder.Refund(merchant, Unix, 500, Tid(sale)), merchant, "1");

        Assert.That(PraxisSigner.Text(refund, "description"), Is.EqualTo("Gateway for reference_id  does not allow refunding"));
    }

    [Test]
    public async Task GatewayLookups_ListBothRegisteredGateways()
    {
        var environment = NewEnvironment();
        var merchant = await RegisterAsync(environment);

        var list = await SignedAsync(environment, "agent/get-available-gateways", PraxisRequestBuilder.GatewayLookup(merchant, Unix), merchant);
        var options = await SignedAsync(environment, "agent/get-gateway-options", PraxisRequestBuilder.GatewayLookup(merchant, Unix, "payout"), merchant);

        Assert.Multiple(() =>
        {
            Assert.That(list["gateway_list"]!.AsArray().Select(g => PraxisSigner.Text(g, "gateway")),
                Is.EqualTo(new[] { PraxisRequestBuilder.CardGateway, PraxisRequestBuilder.EWalletGateway }));
            Assert.That(PraxisSigner.Text(list["gateway_list"]![1], "gateway_type"), Is.EqualTo("apm"));
            Assert.That(options["gateway_options"]!.AsArray().Select(g => PraxisSigner.Text(g, "payment_method")),
                Is.EqualTo(new[] { "Credit Card", "altbankwire" }));
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
