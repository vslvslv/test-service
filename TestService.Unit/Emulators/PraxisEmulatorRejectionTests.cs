using System.Text.Json.Nodes;
using TestService.Api.Emulators;
using TestService.Api.Emulators.Praxis;
using TestService.Tests.Praxis;
using static TestService.Unit.Emulators.PraxisEmulatorHarness;

namespace TestService.Unit.Emulators;

/// <summary>Every rejection of the emulator, in the documented order. All are HTTP 200 without a signature header.</summary>
[TestFixture]
public class PraxisEmulatorRejectionTests
{
    private PraxisEmulatorHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = new PraxisEmulatorHarness();

    private async Task<string> SendRawAsync(string path, JsonNode body, string? signature = null)
    {
        var reply = await _harness.SendAsync(path, body, signature: signature);
        Assert.Multiple(() =>
        {
            Assert.That(reply.StatusCode, Is.EqualTo(200));
            Assert.That(reply.Headers.ContainsKey(PraxisSigner.HeaderName), Is.False, "rejections are not signed");
        });
        return reply.Body;
    }

    private JsonObject Sale(string cvv = "568") => PraxisRequestBuilder.Sale(_harness.Merchant, _harness.Now, cvv, 4900, NotificationUrl);

    [TestCase("api/direct-process")]
    [TestCase("direct/refund")]
    [TestCase("agent/find-transaction")]
    [TestCase("agent/manage-transaction")]
    public async Task UnknownMerchant_UserNotFound(string path)
    {
        var body = new JsonObject { ["merchant_id"] = "nobody", ["application_key"] = "x", ["timestamp"] = _harness.Now, ["tid"] = 1 };

        Assert.That(await SendRawAsync(path, body, "sig"), Is.EqualTo("""{"status":401,"description":"User not found"}"""));
    }

    [Test]
    public async Task EmptyOrInvalidBodyOrWrongApplicationKey_UserNotFound()
    {
        var wrongKey = PraxisRequestBuilder.Find(_harness.Merchant, _harness.Now, 1);
        wrongKey["application_key"] = "other";

        Assert.Multiple(async () =>
        {
            Assert.That((await _harness.Emulator.HandleAsync(new EmulatorRequest { Environment = Env, Path = "agent/find-transaction" })).Body,
                Is.EqualTo("""{"status":401,"description":"User not found"}"""));
            Assert.That((await _harness.Emulator.HandleAsync(new EmulatorRequest { Environment = Env, Path = "agent/find-transaction", Body = "{not json" })).Body,
                Is.EqualTo("""{"status":401,"description":"User not found"}"""));
            Assert.That((await _harness.Emulator.HandleAsync(new EmulatorRequest { Environment = Env, Path = "agent/find-transaction", Body = "[1]" })).Body,
                Is.EqualTo("""{"status":401,"description":"User not found"}"""));
            Assert.That(await SendRawAsync("agent/find-transaction", wrongKey), Is.EqualTo("""{"status":401,"description":"User not found"}"""));
        });
    }

    [Test]
    public async Task MerchantOfAnotherEnvironment_UserNotFound()
    {
        var elsewhere = NewMerchant("m-elsewhere", environment: "other");
        await _harness.Merchants.CreateAsync(elsewhere);

        var body = PraxisRequestBuilder.Find(elsewhere, _harness.Now, 1);
        Assert.That(await SendRawAsync("agent/find-transaction", body, PraxisSigner.ForFindTransaction(body, elsewhere.Secret)),
            Is.EqualTo("""{"status":401,"description":"User not found"}"""));
    }

    [TestCase("api/direct-process")]
    [TestCase("direct/refund")]
    [TestCase("agent/find-transaction")]
    [TestCase("agent/manage-transaction")]
    public async Task WrongSignature_ValidationError(string path)
    {
        var body = Sale();
        body["tid"] = 1;

        Assert.That(await SendRawAsync(path, body, "0123abcd"),
            Is.EqualTo("""{"status":1,"description":"Validation Error","errors":{"merchant_id":["Invalid signature"]}}"""));
    }

    [Test]
    public async Task ExpiredTimestamp_OnAgentEndpoints_ValidationErrorWithAge()
    {
        var find = PraxisRequestBuilder.Find(_harness.Merchant, _harness.Now - 130, 1);
        var manage = PraxisRequestBuilder.Manage(_harness.Merchant, _harness.Now - 61, "capture-authorization", 1);
        var atLimit = PraxisRequestBuilder.Find(_harness.Merchant, _harness.Now - 60, 1);
        var noTimestamp = PraxisRequestBuilder.Find(_harness.Merchant, 0, 1);
        noTimestamp.Remove("timestamp");

        Assert.Multiple(async () =>
        {
            Assert.That(await SendRawAsync("agent/find-transaction", find),
                Is.EqualTo("""{"status":1,"description":"Validation Error","errors":{"timestamp":["Timestamp expired: 130s old, should be at most 60s old (required with merchant_id)"]}}"""));
            Assert.That(await SendRawAsync("agent/manage-transaction", manage), Does.Contain("Timestamp expired: 61s old"));
            Assert.That(await SendRawAsync("agent/find-transaction", atLimit), Is.EqualTo("""{"status":401,"description":"Wrong credentials"}"""));
            Assert.That(await SendRawAsync("agent/find-transaction", noTimestamp), Does.Contain("Timestamp cannot be blank."));
        });
    }

    [Test]
    public async Task Sale_MissingCustomerAndDeviceFields_ListsThemInQa2Order()
    {
        var body = Sale();
        body["customer_data"]!.AsObject().Remove("city");
        body["customer_data"]!["address"] = "";
        body.Remove("device_data");

        Assert.That(await SendRawAsync("api/direct-process", body), Is.EqualTo(
            """{"status":400,"description":"City cannot be blank.,Address cannot be blank.,User Agent cannot be blank.,Accept Header cannot be blank.,Language cannot be blank.,Ip Address cannot be blank.,Color Depth cannot be blank.,Pixel Depth cannot be blank.,Pixel Ratio cannot be blank."}"""));
        Assert.That(_harness.Transactions.All, Is.Empty);
    }

    [Test]
    public async Task Sale_CardEncryptedWithAnotherKeyAndExpiredTimestamp_DecryptErrorsThenTimestampLast()
    {
        var timestamp = _harness.Now - 66;
        var body = PraxisRequestBuilder.Sale(_harness.Merchant, timestamp, "568", 4900);
        body["card_data"] = PraxisRequestBuilder.CardData("another-secret", timestamp, "568");

        Assert.That(await SendRawAsync("api/direct-process", body), Is.EqualTo(
            """{"status":400,"description":"Failed to decrypt card_number,Failed to decrypt card_exp,Failed to decrypt cvv,Timestamp expired: 66s old, should be at most 60s old (required with merchant_id)"}"""));
    }

    [Test]
    public async Task DirectProcess_MissingCoreFieldsAndCard_Listed()
    {
        var body = new JsonObject
        {
            ["merchant_id"] = _harness.Merchant.MerchantId,
            ["application_key"] = _harness.Merchant.ApplicationKey,
            ["timestamp"] = _harness.Now,
            ["transaction_type"] = "payout",
            ["amount"] = "12.5"
        };

        Assert.That(await SendRawAsync("api/direct-process", body), Is.EqualTo(
            """{"status":400,"description":"Currency cannot be blank.,Cid cannot be blank.,Order Id cannot be blank.,Card Data cannot be blank.,Amount must be an integer."}"""));
    }

    [TestCase("authorization")]
    [TestCase("credit")]
    public async Task DirectProcess_UnsupportedTransactionType_Status1(string type)
    {
        var body = PraxisRequestBuilder.Sale(_harness.Merchant, _harness.Now, "568", 4900, transactionType: type);

        Assert.That(await SendRawAsync("api/direct-process", body),
            Is.EqualTo($$"""{"status":1,"description":"Gateway does not support the transaction type ({{type}})"}"""));
    }

    [Test]
    public async Task Refund_MissingOrForeignReference()
    {
        var other = NewMerchant("m-2");
        await _harness.Merchants.CreateAsync(other);
        var otherSale = (await _harness.SendAsync("api/direct-process",
            PraxisRequestBuilder.Sale(other, _harness.Now, "568", 4900), other)).Json()!;

        var missing = PraxisRequestBuilder.Refund(_harness.Merchant, _harness.Now, 100, 1);
        missing.Remove("reference_id");
        var expiredMissing = PraxisRequestBuilder.Refund(_harness.Merchant, _harness.Now - 254, 100, 1);
        expiredMissing.Remove("reference_id");

        Assert.Multiple(async () =>
        {
            Assert.That(await SendRawAsync("direct/refund", missing), Is.EqualTo("""{"status":400,"description":"Reference Id cannot be blank."}"""));
            Assert.That(await SendRawAsync("direct/refund", expiredMissing), Is.EqualTo(
                """{"status":400,"description":"Reference Id cannot be blank.,Timestamp expired: 254s old, should be at most 60s old (required with merchant_id)"}"""));
            Assert.That(await SendRawAsync("direct/refund", PraxisRequestBuilder.Refund(_harness.Merchant, _harness.Now, 100, 424242)),
                Is.EqualTo("""{"status":401,"description":"Wrong credentials"}"""));
            Assert.That(await SendRawAsync("direct/refund", PraxisRequestBuilder.Refund(_harness.Merchant, _harness.Now, 100, Tid(otherSale))),
                Is.EqualTo("""{"status":401,"description":"Wrong credentials"}"""));
        });
    }

    [Test]
    public async Task Find_MissingUnknownForeignOrNonNumericTid()
    {
        var other = NewMerchant("m-3");
        await _harness.Merchants.CreateAsync(other);
        var otherTid = Tid((await _harness.SendAsync("api/direct-process",
            PraxisRequestBuilder.Sale(other, _harness.Now, "568", 4900), other)).Json()!);

        var noTid = PraxisRequestBuilder.Find(_harness.Merchant, _harness.Now, 1);
        noTid.Remove("tid");
        var textTid = PraxisRequestBuilder.Find(_harness.Merchant, _harness.Now, 1);
        textTid["tid"] = "abc";

        Assert.Multiple(async () =>
        {
            Assert.That(await SendRawAsync("agent/find-transaction", noTid), Is.EqualTo("""{"status":400,"description":"Tid cannot be blank."}"""));
            Assert.That(await SendRawAsync("agent/find-transaction", textTid), Is.EqualTo("""{"status":401,"description":"Wrong credentials"}"""));
            Assert.That(await SendRawAsync("agent/find-transaction", PraxisRequestBuilder.Find(_harness.Merchant, _harness.Now, 9)),
                Is.EqualTo("""{"status":401,"description":"Wrong credentials"}"""));
            Assert.That(await SendRawAsync("agent/find-transaction", PraxisRequestBuilder.Find(_harness.Merchant, _harness.Now, otherTid)),
                Is.EqualTo("""{"status":401,"description":"Wrong credentials"}"""));
        });
    }

    [TestCase("GET", "api/direct-process")]
    [TestCase("POST", "api/unknown")]
    [TestCase("POST", "pages/3ds/abc")]
    [TestCase("POST", "pages/3ds/1")]
    [TestCase("GET", "pages/3ds/1/submit")]
    public async Task UnknownEndpoint_404(string method, string path)
    {
        var reply = await _harness.Emulator.HandleAsync(new EmulatorRequest { Environment = Env, Path = path, Method = method });

        Assert.Multiple(() =>
        {
            Assert.That(reply.StatusCode, Is.EqualTo(404));
            Assert.That(reply.Json()!["message"]!.GetValue<string>(), Does.StartWith("Unknown Praxis endpoint"));
        });
    }

    [Test]
    public async Task HasEnvironment_OnlyWithAMerchant()
    {
        Assert.Multiple(async () =>
        {
            Assert.That(await _harness.Emulator.HasEnvironmentAsync(Env), Is.True);
            Assert.That(await _harness.Emulator.HasEnvironmentAsync("empty"), Is.False);
            Assert.That(_harness.Emulator.Provider, Is.EqualTo("praxis"));
        });
    }
}
