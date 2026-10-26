using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using TestService.Api.Emulators.Transactions;
using static TestService.Api.Emulators.Praxis.PraxisResponses;

namespace TestService.Api.Emulators.Praxis;

/// <summary>An input field of an e-wallet payment method, as the gateway lookups describe it.</summary>
public sealed record PraxisInputField(string Key, string Label, int Required, string Format, string? DefaultValue, IReadOnlyDictionary<string, string>? Options = null);

/// <summary>
/// The field rules of the Test E-Wallet method (<c>altbankwire</c>), as the sandbox enforces and
/// describes them: the payment and withdrawal input-field tables, <c>wallet_data</c> validation
/// for <c>direct/apm</c> sale and payout, and the <c>wallet</c> object of a transaction.
/// </summary>
public static class PraxisWalletData
{
    public const string DefaultProcessor = "Test E-Wallet";
    public const string DefaultMethod = "altbankwire";

    /// <summary>The <c>wallet.data</c> IBAN attached by the page buttons (sandbox).</summary>
    public const string TestIban = "XX00TEST00000000000000";

    /// <summary>Accepted <c>customer_country</c> values (the select options of the sandbox).</summary>
    public static readonly IReadOnlyDictionary<string, string> CountryOptions = new Dictionary<string, string>
    {
        ["country_ukraine"] = "Ukriane", // sic: as the sandbox
        ["country_cyprus"] = "Cyprus",
        ["country_usa"] = "United States",
        ["country_canada"] = "Canada",
        ["IN"] = "India"
    };

    /// <summary><c>input_fields_for_payment</c> of the sandbox, in order.</summary>
    public static readonly IReadOnlyList<PraxisInputField> PaymentFields =
    [
        new("name", "Customer Name", 1, "text", ""),
        new("email", "Enter your Email", 1, "text", ""),
        new("phone", "Enter your phone", 1, "text", ""),
        new("customer_country", "Enter your Country", 1, "select", "country_cyprus", CountryOptions),
        new("zip", "Postal code", 0, "text", ""),
        new("dqa_t", "text", 0, "text", "text"),
        new("test", "test_text_field", 1, "text", "12345"),
        new("ip_address", "", 1, "hidden", ""),
        new("QA", "QA", 1, "dynamic", null),
        new("testNumber", "testNumber", 0, "dynamic", null)
    ];

    /// <summary><c>input_fields_for_withdrawal</c> of the sandbox, in order.</summary>
    public static readonly IReadOnlyList<PraxisInputField> WithdrawalFields =
    [
        new("name", "Enter your name", 0, "text", ""),
        new("email", "Enter your Email", 1, "text", ""),
        new("phone", "Enter your phone", 0, "text", ""),
        new("customer_country", "Enter your Country", 0, "text", ""),
        new("ip_address", "", 0, "hidden", ""),
        new("any_field", "any field", 0, "text", "")
    ];

    /// <summary>
    /// The keys a sale must carry. The sandbox marks more fields required in its lookups, but rejects only
    /// these three when they are missing (probe of 2026-10-19), in this order.
    /// </summary>
    private static readonly string[] SaleRequired = ["phone", "ip_address", "QA"];

    /// <summary>
    /// Validation messages of a sale's <c>wallet_data</c>: missing required keys, an invalid
    /// <c>customer_country</c>, then one message per unknown key in request order.
    /// </summary>
    public static List<string> ValidateSale(JsonNode? walletData, string method)
    {
        var data = walletData as JsonObject;
        var errors = SaleRequired.Where(key => IsBlank(data, key)).Select(key => $"[{key}] field is required!").ToList();

        var country = Value(data, "customer_country");
        if (!string.IsNullOrEmpty(country) && !CountryOptions.ContainsKey(country))
        {
            errors.Add($"'{country}' value is not valid for the [customer_country] field!");
        }

        errors.AddRange(UnknownKeys(data, PaymentFields, method));
        return errors;
    }

    /// <summary>Validation messages of a payout's <c>wallet_data</c>: a missing <c>email</c>, then every other key (unknown to the payout).</summary>
    public static List<string> ValidatePayout(JsonNode? walletData, string method)
    {
        var data = walletData as JsonObject;
        var errors = new List<string>();
        if (IsBlank(data, "email"))
        {
            errors.Add("[email] field is required!");
        }
        if (data is not null)
        {
            errors.AddRange(data.Select(p => p.Key).Where(key => key != "email").Select(key => UnknownProperty(method, key)));
        }
        return errors;
    }

    /// <summary>A stable 16-character wallet token derived from the merchant, customer id and e-mail.</summary>
    public static string WalletToken(string merchantId, string cid, string? email) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{merchantId}|wallet|{cid}|{email}")))[..16];

    /// <summary>The stored request <c>wallet_data</c> of a transaction (an empty object when none).</summary>
    public static JsonObject Sent(EmulatedTransaction tx)
    {
        var json = tx.Data(Keys.WalletData);
        if (string.IsNullOrEmpty(json))
        {
            return new JsonObject();
        }
        return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
    }

    /// <summary>Attaches the wallet of an e-wallet page button: <c>data {customer_email, IBAN}</c>.</summary>
    public static void AttachButtonWallet(EmulatedTransaction tx)
    {
        var sent = Sent(tx);
        Attach(tx, new JsonObject
        {
            ["customer_email"] = Value(sent, "email"),
            ["IBAN"] = TestIban
        });
    }

    /// <summary>Attaches the wallet of a page-open outcome: the ten payment keys with the values sent (missing ones null).</summary>
    public static void AttachPaymentWallet(EmulatedTransaction tx) => Attach(tx, Fields(Sent(tx), PaymentFields));

    /// <summary>Attaches the wallet of an approved payout: the six withdrawal keys with the values sent (missing ones null).</summary>
    public static void AttachWithdrawalWallet(EmulatedTransaction tx) => Attach(tx, Fields(Sent(tx), WithdrawalFields));

    /// <summary>The stored <c>wallet</c> object, or null when none is attached.</summary>
    public static JsonNode? Wallet(EmulatedTransaction tx)
    {
        var json = tx.Data(Keys.Wallet);
        return string.IsNullOrEmpty(json) ? null : JsonNode.Parse(json);
    }

    private static void Attach(EmulatedTransaction tx, JsonObject data)
    {
        var wallet = new JsonObject
        {
            ["wallet_token"] = tx.Data(Keys.WalletToken),
            ["account_identifier"] = Value(Sent(tx), "email"),
            ["data"] = data
        };
        tx.ProviderData[Keys.Wallet] = Serialize(wallet);
    }

    private static JsonObject Fields(JsonObject sent, IEnumerable<PraxisInputField> fields)
    {
        var data = new JsonObject();
        foreach (var field in fields)
        {
            data[field.Key] = sent[field.Key]?.DeepClone();
        }
        return data;
    }

    private static IEnumerable<string> UnknownKeys(JsonObject? data, IReadOnlyList<PraxisInputField> known, string method) =>
        data is null
            ? []
            : data.Select(p => p.Key).Where(key => known.All(f => f.Key != key)).Select(key => UnknownProperty(method, key));

    private static string UnknownProperty(string method, string key) => $"Unknown property received within wallet_data for {method}: '{key}'";

    private static bool IsBlank(JsonObject? data, string key) => string.IsNullOrWhiteSpace(Value(data, key));

    private static string? Value(JsonObject? data, string key) =>
        data is null ? null : PraxisSigner.Text(data, key) is { Length: > 0 } text ? text : null;
}
