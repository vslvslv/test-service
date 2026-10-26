using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TestService.Api.Emulators.Transactions;

/// <summary>
/// The result a transition writes onto the transaction. <see cref="FinalAmount"/> replaces the
/// transaction amount; <see cref="ProcessedAmount"/> and <see cref="ProcessedCurrency"/> override the
/// processed values of a transition that sets them (approve, pending, authorize), which otherwise are
/// the requested amount and the transaction currency. The <c>Session*</c> values and
/// <see cref="ConversionRate"/> are written by every transition (null when not given): they are the
/// session values a provider shows apart from the transaction's own (for example a verification whose
/// session keeps the processed amount the transaction clears).
/// </summary>
public sealed record StatusChange(string Code, string Details, string SessionStatus)
{
    public long? FinalAmount { get; init; }

    public long? ProcessedAmount { get; init; }

    public string? ProcessedCurrency { get; init; }

    public long? SessionAmount { get; init; }

    public long? SessionProcessedAmount { get; init; }

    public string? SessionProcessedCurrency { get; init; }

    public string? ConversionRate { get; init; }
}

/// <summary>A pending timed follow-up: at <see cref="DueAt"/> the follow-up named <see cref="FollowUp"/> is applied.</summary>
public sealed class TransactionDeadline
{
    [BsonElement("dueAt")]
    public DateTime DueAt { get; set; }

    [BsonElement("followUp")]
    public string FollowUp { get; set; } = string.Empty;
}

/// <summary>One status transition of a transaction.</summary>
public sealed class TransactionHistoryEntry
{
    [BsonElement("at")]
    public DateTime At { get; set; }

    [BsonElement("from")]
    [BsonRepresentation(BsonType.String)]
    public TransactionStatus From { get; set; }

    [BsonElement("to")]
    [BsonRepresentation(BsonType.String)]
    public TransactionStatus To { get; set; }

    [BsonElement("statusCode")]
    public string StatusCode { get; set; } = string.Empty;

    [BsonElement("statusDetails")]
    public string StatusDetails { get; set; } = string.Empty;
}

/// <summary>
/// An emulated provider transaction. It enforces its own status transitions: every status change
/// goes through one method per transition, and an illegal one throws
/// <see cref="InvalidTransactionTransitionException"/>. Provider-specific values (customer, card,
/// session fields) live in <see cref="ProviderData"/>.
/// </summary>
[BsonIgnoreExtraElements]
public class EmulatedTransaction
{
    private static readonly IReadOnlyDictionary<string, TransactionStatus[]> LegalSources = new Dictionary<string, TransactionStatus[]>
    {
        [nameof(AwaitAuthentication)] = [TransactionStatus.Initialized],
        [nameof(MarkPending)] = [TransactionStatus.Initialized],
        [nameof(Approve)] = [TransactionStatus.Initialized, TransactionStatus.Pending, TransactionStatus.Authorized],
        [nameof(Reject)] = [TransactionStatus.Initialized, TransactionStatus.Pending, TransactionStatus.Authorized],
        [nameof(Fail)] = [TransactionStatus.Initialized, TransactionStatus.Pending, TransactionStatus.Authorized],
        [nameof(Authorize)] = [TransactionStatus.Initialized],
        [nameof(PartiallyApprove)] = [TransactionStatus.Initialized],
        [nameof(RequestVerification)] = [TransactionStatus.Initialized, TransactionStatus.Approved],
        [nameof(Cancel)] = [TransactionStatus.Initialized, TransactionStatus.Pending],
        [nameof(SetDeadline)] = [TransactionStatus.Initialized, TransactionStatus.Pending],
        [nameof(SetStatusCheckFollowUp)] = [TransactionStatus.Pending]
    };

    /// <summary>Provider transaction id (Praxis <c>tid</c>); fits in int32.</summary>
    [BsonId]
    public long Tid { get; set; }

    [BsonElement("provider")]
    public string Provider { get; set; } = string.Empty;

    [BsonElement("environment")]
    public string Environment { get; set; } = string.Empty;

    [BsonElement("merchantId")]
    public string MerchantId { get; set; } = string.Empty;

    /// <summary>sale, payout or refund.</summary>
    [BsonElement("transactionType")]
    public string TransactionType { get; set; } = string.Empty;

    [BsonElement("orderId")]
    public string? OrderId { get; set; }

    [BsonElement("currency")]
    public string Currency { get; set; } = string.Empty;

    /// <summary>The amount the merchant asked for, in minor units.</summary>
    [BsonElement("requestedAmount")]
    public long RequestedAmount { get; set; }

    /// <summary>The final amount, in minor units (differs from the requested one for amount-changing test cases).</summary>
    [BsonElement("amount")]
    public long Amount { get; private set; }

    [BsonElement("processedCurrency")]
    public string? ProcessedCurrency { get; private set; }

    [BsonElement("processedAmount")]
    public long? ProcessedAmount { get; private set; }

    /// <summary>Session amount when it differs from <see cref="Amount"/>; null otherwise.</summary>
    [BsonElement("sessionAmount")]
    public long? SessionAmount { get; private set; }

    /// <summary>Session processed amount when it differs from the transaction's; null otherwise.</summary>
    [BsonElement("sessionProcessedAmount")]
    public long? SessionProcessedAmount { get; private set; }

    /// <summary>Session processed currency when it differs from the transaction's; null otherwise.</summary>
    [BsonElement("sessionProcessedCurrency")]
    public string? SessionProcessedCurrency { get; private set; }

    /// <summary>The provider's conversion rate text when it is not 1; null otherwise.</summary>
    [BsonElement("conversionRate")]
    public string? ConversionRate { get; private set; }

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public TransactionStatus Status { get; private set; } = TransactionStatus.Initialized;

    [BsonElement("statusCode")]
    public string StatusCode { get; private set; } = "0";

    [BsonElement("statusDetails")]
    public string StatusDetails { get; private set; } = string.Empty;

    [BsonElement("sessionStatus")]
    public string SessionStatus { get; private set; } = "open";

    /// <summary>Name of the test case that produced the transaction (selects 3DS decisions).</summary>
    [BsonElement("caseName")]
    public string CaseName { get; set; } = string.Empty;

    /// <summary>The transaction goes through the 3DS authentication flow.</summary>
    [BsonElement("is3d")]
    public bool Is3d { get; set; }

    /// <summary>The first response was asynchronous (the customer is redirected).</summary>
    [BsonElement("isAsync")]
    public bool IsAsync { get; set; }

    /// <summary>The first response announced an authentication step (redirect field present, possibly null).</summary>
    [BsonElement("requiresAuthentication")]
    public bool RequiresAuthentication { get; set; }

    [BsonElement("redirectUrl")]
    public string? RedirectUrl { get; set; }

    [BsonElement("notificationUrl")]
    public string? NotificationUrl { get; set; }

    [BsonElement("returnUrl")]
    public string? ReturnUrl { get; set; }

    /// <summary>The transaction a refund refers to.</summary>
    [BsonElement("referenceId")]
    public long? ReferenceId { get; set; }

    [BsonElement("providerData")]
    public Dictionary<string, string?> ProviderData { get; set; } = new();

    /// <summary>Optimistic concurrency version: incremented by every successful save.</summary>
    [BsonElement("version")]
    public int Version { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [BsonElement("history")]
    public List<TransactionHistoryEntry> History { get; private set; } = new();

    [BsonElement("deadline")]
    public TransactionDeadline? Deadline { get; private set; }

    /// <summary>Follow-up applied on the next status check (find-transaction).</summary>
    [BsonElement("onStatusCheck")]
    public string? OnStatusCheck { get; private set; }

    /// <summary>Creates a new transaction in status <c>initialized</c>.</summary>
    public static EmulatedTransaction Open(string provider, string environment, string merchantId, string transactionType,
        string currency, long amount, DateTime now) => new()
    {
        Provider = provider,
        Environment = environment,
        MerchantId = merchantId,
        TransactionType = transactionType,
        Currency = currency,
        RequestedAmount = amount,
        Amount = amount,
        CreatedAt = now,
        UpdatedAt = now
    };

    /// <summary>The value of a provider field, or null.</summary>
    public string? Data(string key) => ProviderData.TryGetValue(key, out var value) ? value : null;

    /// <summary>
    /// Stays <c>initialized</c> while the customer authenticates (or a redirect page is shown); the deadline,
    /// if any, applies when nobody decides. Without a deadline the transaction stays initialized.
    /// </summary>
    public void AwaitAuthentication(StatusChange change, TransactionDeadline? deadline, DateTime now)
    {
        Transition(nameof(AwaitAuthentication), TransactionStatus.Initialized, change, keepsProcessed: false, now);
        Deadline = deadline;
    }

    public void MarkPending(StatusChange change, DateTime now) =>
        Transition(nameof(MarkPending), TransactionStatus.Pending, change, keepsProcessed: true, now);

    public void Approve(StatusChange change, DateTime now) =>
        Transition(nameof(Approve), TransactionStatus.Approved, change, keepsProcessed: true, now);

    public void Reject(StatusChange change, DateTime now) =>
        Transition(nameof(Reject), TransactionStatus.Rejected, change, keepsProcessed: false, now);

    public void Fail(StatusChange change, DateTime now) =>
        Transition(nameof(Fail), TransactionStatus.Error, change, keepsProcessed: false, now);

    public void Authorize(StatusChange change, DateTime now) =>
        Transition(nameof(Authorize), TransactionStatus.Authorized, change, keepsProcessed: true, now);

    public void PartiallyApprove(StatusChange change, DateTime now) =>
        Transition(nameof(PartiallyApprove), TransactionStatus.PartialApproved, change, keepsProcessed: false, now);

    public void RequestVerification(StatusChange change, DateTime now) =>
        Transition(nameof(RequestVerification), TransactionStatus.Verification, change, keepsProcessed: false, now);

    /// <summary>The customer cancelled (e-wallet page); only an open (initialized or pending) transaction can be cancelled.</summary>
    public void Cancel(StatusChange change, DateTime now) =>
        Transition(nameof(Cancel), TransactionStatus.Cancelled, change, keepsProcessed: false, now);

    /// <summary>Schedules a timed follow-up; only an open (initialized or pending) transaction can have one.</summary>
    public void SetDeadline(TransactionDeadline deadline)
    {
        EnsureLegal(nameof(SetDeadline));
        Deadline = deadline;
    }

    /// <summary>Schedules a follow-up for the next status check; only a pending transaction can have one.</summary>
    public void SetStatusCheckFollowUp(string followUp)
    {
        EnsureLegal(nameof(SetStatusCheckFollowUp));
        OnStatusCheck = followUp;
    }

    /// <summary>Removes and returns the deadline's follow-up when it is due (<c>now &gt;= DueAt</c>).</summary>
    public string? TakeDueDeadline(DateTime now)
    {
        if (Deadline is null || now < Deadline.DueAt)
        {
            return null;
        }

        var followUp = Deadline.FollowUp;
        Deadline = null;
        return followUp;
    }

    /// <summary>Removes and returns the status-check follow-up, if any.</summary>
    public string? TakeStatusCheckFollowUp()
    {
        var followUp = OnStatusCheck;
        OnStatusCheck = null;
        return followUp;
    }

    /// <summary>A deep copy (stores and notification snapshots must not share state with the live aggregate).</summary>
    public EmulatedTransaction Clone()
    {
        var copy = (EmulatedTransaction)MemberwiseClone();
        copy.ProviderData = new Dictionary<string, string?>(ProviderData);
        copy.History = History.Select(h => new TransactionHistoryEntry
        {
            At = h.At, From = h.From, To = h.To, StatusCode = h.StatusCode, StatusDetails = h.StatusDetails
        }).ToList();
        copy.Deadline = Deadline is null ? null : new TransactionDeadline { DueAt = Deadline.DueAt, FollowUp = Deadline.FollowUp };
        return copy;
    }

    private void EnsureLegal(string transition)
    {
        if (!LegalSources[transition].Contains(Status))
        {
            throw new InvalidTransactionTransitionException(Status, transition);
        }
    }

    private void Transition(string transition, TransactionStatus to, StatusChange change, bool keepsProcessed, DateTime now)
    {
        EnsureLegal(transition);
        if (transition == nameof(AwaitAuthentication) && (Deadline is not null || History.Count > 0))
        {
            // Authentication is awaited once, right after creation.
            throw new InvalidTransactionTransitionException(Status, transition);
        }

        var from = Status;
        Status = to;
        StatusCode = change.Code;
        StatusDetails = change.Details;
        SessionStatus = change.SessionStatus;
        if (change.FinalAmount is { } finalAmount)
        {
            Amount = finalAmount;
        }

        if (keepsProcessed)
        {
            ProcessedCurrency = change.ProcessedCurrency ?? Currency;
            ProcessedAmount = change.ProcessedAmount ?? RequestedAmount;
        }
        else
        {
            ProcessedCurrency = null;
            ProcessedAmount = null;
        }

        SessionAmount = change.SessionAmount;
        SessionProcessedAmount = change.SessionProcessedAmount;
        SessionProcessedCurrency = change.SessionProcessedCurrency;
        ConversionRate = change.ConversionRate;

        Deadline = null;
        OnStatusCheck = null;
        UpdatedAt = now;
        History.Add(new TransactionHistoryEntry
        {
            At = now, From = from, To = to, StatusCode = change.Code, StatusDetails = change.Details
        });
    }
}
