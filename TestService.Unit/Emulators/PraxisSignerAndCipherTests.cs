using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using TestService.Api.Emulators.Praxis;

namespace TestService.Unit.Emulators;

[TestFixture]
public class PraxisSignerAndCipherTests
{
    private const string Secret = "s3cr3t-key";

    /// <summary>The formula, written out independently of the signer: lowercase hex SHA-384 of fields + secret.</summary>
    private static string Expected(string concatenated) =>
        BitConverter.ToString(SHA384.Create().ComputeHash(Encoding.UTF8.GetBytes(concatenated + Secret))).Replace("-", "").ToLowerInvariant();

    [Test]
    public void ForDirectProcess_WithCardData_SignsFieldsAndCardNumberCipher()
    {
        var request = JsonNode.Parse("""
            {"merchant_id":"m1","application_key":"app","timestamp":1792659811,"transaction_type":"sale","cid":"c-1",
             "order_id":"o-1","currency":"USD","amount":4900,"gateway":"gw","notification_url":"https://n","return_url":"https://r",
             "card_data":{"card_number":"CIPHER=="}}
            """);

        Assert.That(PraxisSigner.ForDirectProcess(request, Secret),
            Is.EqualTo(Expected("m1app1792659811salec-1o-1USD4900gwhttps://nhttps://rCIPHER==")));
    }

    [Test]
    public void ForDirectProcess_WithCardToken_UsesTokenInsteadOfCardNumber()
    {
        var request = JsonNode.Parse("""
            {"merchant_id":"m1","application_key":"app","timestamp":"5","transaction_type":"payout","cid":"c","order_id":"o",
             "currency":"EUR","amount":"100","gateway":null,"card_token":"tok","card_data":{"card_number":"ignored"}}
            """);

        Assert.That(PraxisSigner.ForDirectProcess(request, Secret), Is.EqualTo(Expected("m1app5payoutcoEUR100tok")));
    }

    [Test]
    public void ForRefund_MissingWithdrawalRequestId_ConcatenatesEmpty()
    {
        var request = JsonNode.Parse("""
            {"merchant_id":"m","application_key":"a","timestamp":7,"transaction_type":"refund","cid":"c","order_id":"o",
             "currency":"USD","amount":5000,"reference_id":1000001}
            """);

        Assert.Multiple(() =>
        {
            Assert.That(PraxisSigner.ForRefund(request, Secret), Is.EqualTo(Expected("ma7refundcoUSD50001000001")));
            request!["withdrawal_request_id"] = "w9";
            Assert.That(PraxisSigner.ForRefund(request, Secret), Is.EqualTo(Expected("ma7refundcoUSD50001000001w9")));
        });
    }

    [Test]
    public void ForAgentRequests_SignTheirFields()
    {
        var find = JsonNode.Parse("""{"merchant_id":"m","application_key":"a","timestamp":7,"tid":1000002}""");
        var manage = JsonNode.Parse("""{"merchant_id":"m","application_key":"a","timestamp":7,"intent":"capture-authorization","tid":1000002}""");

        Assert.Multiple(() =>
        {
            Assert.That(PraxisSigner.ForFindTransaction(find, Secret), Is.EqualTo(Expected("ma71000002")));
            Assert.That(PraxisSigner.ForManageTransaction(manage, Secret), Is.EqualTo(Expected("ma7capture-authorization1000002")));
        });
    }

    [Test]
    public void ForResponse_NullProcessedAndMissingCustomer_AreEmpty()
    {
        var response = JsonNode.Parse("""
            {"status":0,"timestamp":99,"transaction":{"tid":5,"transaction_status":"rejected","processed_currency":null,"processed_amount":null}}
            """);

        Assert.That(PraxisSigner.ForResponse(response, Secret), Is.EqualTo(Expected("0995rejected")));
    }

    [Test]
    public void ForNotification_SignsItsFields()
    {
        var notification = JsonNode.Parse("""
            {"merchant_id":"m","application_key":"a","timestamp":9,"customer":{"customer_token":"ct"},"session":{"order_id":"o"},
             "transaction":{"tid":5,"currency":"USD","amount":1000,"conversion_rate":"1.000000","processed_currency":"USD","processed_amount":1100}}
            """);

        Assert.That(PraxisSigner.ForNotification(notification, Secret),
            Is.EqualTo(Expected("ma9cto5USD10001.000000USD1100")));
    }

    [Test]
    public void Text_ReadsNestedValuesAndTreatsObjectsAsJson()
    {
        var node = JsonNode.Parse("""{"a":{"b":true,"c":[1]},"n":null}""");

        Assert.Multiple(() =>
        {
            Assert.That(PraxisSigner.Text(node, "a.b"), Is.EqualTo("true"));
            Assert.That(PraxisSigner.Text(node, "a.c"), Is.EqualTo("[1]"));
            Assert.That(PraxisSigner.Text(node, "n"), Is.Empty);
            Assert.That(PraxisSigner.Text(node, "a.b.x"), Is.Empty);
            Assert.That(PraxisSigner.Text(null, "a"), Is.Empty);
        });
    }

    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase("ABC", true)]
    [TestCase(" abc ", true)]
    [TestCase("abd", false)]
    public void Matches_IsCaseInsensitiveAndRejectsMissing(string? provided, bool expected)
    {
        Assert.That(PraxisSigner.Matches(provided, "abc"), Is.EqualTo(expected));
    }

    [Test]
    public void Cipher_RoundTrip_MatchesBillingServerFormula()
    {
        const string timestamp = "1792659811";
        var cipher = PraxisCardCipher.Encrypt("4111111111111111", Secret, timestamp);

        // BillingServer: Aes.Create() defaults (CBC, PKCS7), key = secret.PadLeft(32,'0'), IV = timestamp.PadLeft(16,'0').
        using var aes = Aes.Create();
        aes.Key = Encoding.ASCII.GetBytes(Secret.PadLeft(32, '0'));
        aes.IV = Encoding.ASCII.GetBytes(timestamp.PadLeft(16, '0'));
        using var encryptor = aes.CreateEncryptor();
        var plain = Encoding.UTF8.GetBytes("4111111111111111");
        var expected = Convert.ToBase64String(encryptor.TransformFinalBlock(plain, 0, plain.Length));

        Assert.Multiple(() =>
        {
            Assert.That(cipher, Is.EqualTo(expected));
            Assert.That(PraxisCardCipher.TryDecrypt(cipher, Secret, timestamp, out var decrypted), Is.True);
            Assert.That(decrypted, Is.EqualTo("4111111111111111"));
        });
    }

    [Test]
    public void Cipher_WrongKeyOrIvOrGarbage_FailsToDecrypt()
    {
        var cipher = PraxisCardCipher.Encrypt("12/2030", Secret, "100");

        Assert.Multiple(() =>
        {
            Assert.That(PraxisCardCipher.TryDecrypt(cipher, "other-secret", "100", out _), Is.False);
            Assert.That(PraxisCardCipher.TryDecrypt("not base64!", Secret, "100", out _), Is.False);
            Assert.That(PraxisCardCipher.TryDecrypt("xxxxxxx/xxx9x9xxxxxxxx==", Secret, "100", out _), Is.False);
            Assert.That(PraxisCardCipher.TryDecrypt(null, Secret, "100", out _), Is.False);
            Assert.That(PraxisCardCipher.TryDecrypt(cipher, new string('k', 40), "100", out _), Is.False, "key over 32 bytes");
        });
    }

    [Test]
    public void ForApm_SignsTheDirectProcessFieldsWithoutACardPart()
    {
        var request = JsonNode.Parse("""
            {"merchant_id":"m1","application_key":"app","timestamp":5,"transaction_type":"sale","cid":"c","order_id":"o",
             "currency":"EUR","amount":20000,"gateway":"gw","notification_url":"https://n","return_url":"https://r",
             "wallet_data":{"email":"e"},"card_token":"ignored"}
            """);

        Assert.That(PraxisSigner.ForApm(request, Secret), Is.EqualTo(Expected("m1app5salecoEUR20000gwhttps://nhttps://r")));
    }

    [Test]
    public void ForWalletToken_AppendsTheWalletToken()
    {
        var request = JsonNode.Parse("""
            {"merchant_id":"m1","application_key":"app","timestamp":5,"transaction_type":"payout","cid":"c","order_id":"o",
             "currency":"EUR","amount":100,"gateway":"gw","notification_url":"https://n","return_url":"https://r","wallet_token":"wt"}
            """);

        Assert.That(PraxisSigner.ForWalletToken(request, Secret), Is.EqualTo(Expected("m1app5payoutcoEUR100gwhttps://nhttps://rwt")));
    }

    [Test]
    public void ForGatewayLookupAndManageWithdrawal_SignTheirFields()
    {
        var lookup = JsonNode.Parse("""{"merchant_id":"m","application_key":"a","timestamp":7,"transaction_type":"sale","currency":"EUR"}""");
        var manage = JsonNode.Parse("""{"merchant_id":"m","application_key":"a","timestamp":7,"intent":"cancel-withdrawal-request","withdrawal_request_id":1000003}""");

        Assert.Multiple(() =>
        {
            Assert.That(PraxisSigner.ForGatewayLookup(lookup, Secret), Is.EqualTo(Expected("ma7")));
            Assert.That(PraxisSigner.ForManageWithdrawal(manage, Secret), Is.EqualTo(Expected("ma7cancel-withdrawal-request1000003")));
        });
    }
}
