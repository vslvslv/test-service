using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TestService.Api.Emulators.Praxis;

/// <summary>
/// Praxis <c>GT-Authentication</c> signatures: lowercase hex SHA-384 over the UTF-8 concatenation
/// of the fields followed by the merchant secret. A null or missing field is an empty string;
/// a number is its JSON text.
/// </summary>
public static class PraxisSigner
{
    public const string HeaderName = "GT-Authentication";

    private static readonly string[] ApmFields =
    [
        "merchant_id", "application_key", "timestamp", "transaction_type", "cid", "order_id",
        "currency", "amount", "gateway", "notification_url", "return_url"
    ];

    public static string Sign(IEnumerable<string?> parts, string secret) =>
        Convert.ToHexStringLower(SHA384.HashData(Encoding.UTF8.GetBytes(string.Concat(parts) + secret)));

    /// <summary>Signature of an <c>api/direct-process</c> sale or payout request.</summary>
    public static string ForDirectProcess(JsonNode? request, string secret)
    {
        var cardToken = Text(request, "card_token");
        var cardPart = cardToken.Length > 0 ? cardToken : Text(request, "card_data.card_number");
        return Sign(Fields(request, "merchant_id", "application_key", "timestamp", "transaction_type", "cid", "order_id",
            "currency", "amount", "gateway", "notification_url", "return_url").Append(cardPart), secret);
    }

    /// <summary>Signature of a <c>direct/apm</c> sale or payout request (the <c>direct-process</c> fields without the card part).</summary>
    public static string ForApm(JsonNode? request, string secret) =>
        Sign(Fields(request, ApmFields), secret);

    /// <summary>Signature of a <c>direct/wallet-token</c> payout request: the <c>direct/apm</c> fields plus <c>wallet_token</c>.</summary>
    public static string ForWalletToken(JsonNode? request, string secret) =>
        Sign(Fields(request, [.. ApmFields, "wallet_token"]), secret);

    /// <summary>Signature of an <c>agent/get-available-gateways</c> or <c>agent/get-gateway-options</c> request.</summary>
    public static string ForGatewayLookup(JsonNode? request, string secret) =>
        Sign(Fields(request, "merchant_id", "application_key", "timestamp"), secret);

    /// <summary>Signature of an <c>agent/manage-withdrawal-request</c> request.</summary>
    public static string ForManageWithdrawal(JsonNode? request, string secret) =>
        Sign(Fields(request, "merchant_id", "application_key", "timestamp", "intent", "withdrawal_request_id"), secret);

    /// <summary>Signature of a <c>direct/refund</c> request.</summary>
    public static string ForRefund(JsonNode? request, string secret) =>
        Sign(Fields(request, "merchant_id", "application_key", "timestamp", "transaction_type", "cid", "order_id",
            "currency", "amount", "reference_id", "withdrawal_request_id"), secret);

    /// <summary>Signature of an <c>agent/find-transaction</c> request.</summary>
    public static string ForFindTransaction(JsonNode? request, string secret) =>
        Sign(Fields(request, "merchant_id", "application_key", "timestamp", "tid"), secret);

    /// <summary>Signature of an <c>agent/manage-transaction</c> request.</summary>
    public static string ForManageTransaction(JsonNode? request, string secret) =>
        Sign(Fields(request, "merchant_id", "application_key", "timestamp", "intent", "tid"), secret);

    /// <summary>Signature of a response body (header <c>GT-Authentication</c> of every successful response).</summary>
    public static string ForResponse(JsonNode? response, string secret) =>
        Sign(Fields(response, "status", "timestamp", "customer.customer_token", "transaction.tid",
            "transaction.transaction_status", "transaction.processed_currency", "transaction.processed_amount"), secret);

    /// <summary>Signature of a notification body.</summary>
    public static string ForNotification(JsonNode? notification, string secret) =>
        Sign(Fields(notification, "merchant_id", "application_key", "timestamp", "customer.customer_token",
            "session.order_id", "transaction.tid", "transaction.currency", "transaction.amount",
            "transaction.conversion_rate", "transaction.processed_currency", "transaction.processed_amount"), secret);

    /// <summary>Constant-time, case-insensitive comparison of a provided signature with the expected one.</summary>
    public static bool Matches(string? provided, string expected)
    {
        if (string.IsNullOrEmpty(provided))
        {
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(provided.Trim().ToLowerInvariant()),
            Encoding.ASCII.GetBytes(expected));
    }

    /// <summary>The text of the field at a dotted path: a string's value, a number's JSON text, "" when null or missing.</summary>
    public static string Text(JsonNode? root, string path)
    {
        var node = root;
        foreach (var part in path.Split('.'))
        {
            if (node is not JsonObject obj || !obj.TryGetPropertyValue(part, out node))
            {
                return string.Empty;
            }
        }

        if (node is null)
        {
            return string.Empty;
        }
        if (node is JsonValue value)
        {
            return value.GetValueKind() switch
            {
                JsonValueKind.String => value.GetValue<string>(),
                JsonValueKind.Null => string.Empty,
                _ => value.ToJsonString()
            };
        }
        return node.ToJsonString();
    }

    private static IEnumerable<string> Fields(JsonNode? root, params string[] paths) => paths.Select(p => Text(root, p));
}
