using System.Text.Json;
using System.Text.Json.Serialization;

namespace TestService.Api.Emulators.Transactions;

/// <summary>Status of an emulated provider transaction; JSON uses the wire name (for example <c>partial_approved</c>).</summary>
[JsonConverter(typeof(TransactionStatusJsonConverter))]
public enum TransactionStatus
{
    Initialized,
    Pending,
    Approved,
    Rejected,
    Error,
    Authorized,
    PartialApproved,
    Verification,
    Cancelled
}

public static class TransactionStatusNames
{
    /// <summary>The wire name used by providers (for example <c>partial_approved</c>).</summary>
    public static string ToWire(this TransactionStatus status) => status switch
    {
        TransactionStatus.Initialized => "initialized",
        TransactionStatus.Pending => "pending",
        TransactionStatus.Approved => "approved",
        TransactionStatus.Rejected => "rejected",
        TransactionStatus.Error => "error",
        TransactionStatus.Authorized => "authorized",
        TransactionStatus.PartialApproved => "partial_approved",
        TransactionStatus.Verification => "verification",
        TransactionStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}

/// <summary>Reads and writes <see cref="TransactionStatus"/> as its wire name.</summary>
public sealed class TransactionStatusJsonConverter : JsonConverter<TransactionStatus>
{
    public override TransactionStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var name = reader.GetString();
        foreach (var status in Enum.GetValues<TransactionStatus>())
        {
            if (status.ToWire() == name)
            {
                return status;
            }
        }
        throw new JsonException($"Unknown transaction status '{name}'.");
    }

    public override void Write(Utf8JsonWriter writer, TransactionStatus value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToWire());
}

/// <summary>Thrown when a transition is not allowed from the transaction's current status.</summary>
public class InvalidTransactionTransitionException : InvalidOperationException
{
    public InvalidTransactionTransitionException(TransactionStatus from, string transition)
        : base($"Transition '{transition}' is not allowed from status '{from.ToWire()}'.")
    {
        From = from;
        Transition = transition;
    }

    public TransactionStatus From { get; }

    public string Transition { get; }
}
