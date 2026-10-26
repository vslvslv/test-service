using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TestService.Api.Models;

/// <summary>
/// A fully built outbound request to send at <see cref="DueAt"/>. Delivery sends it unchanged.
/// </summary>
public class CallbackJob
{
    /// <summary>Who produced the job, for example <c>praxis</c>.</summary>
    public string Source { get; set; } = string.Empty;

    public string Environment { get; set; } = "default";

    /// <summary>Caller's correlation id, for example the emulated transaction id.</summary>
    public string? CorrelationId { get; set; }

    public string Url { get; set; } = string.Empty;

    public string Method { get; set; } = "POST";

    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? Body { get; set; }

    public DateTimeOffset DueAt { get; set; }
}

/// <summary>
/// One delivery attempt (sent, refused, failed, dropped or lost), stored in <c>MockCallbackLogs</c>.
/// </summary>
[BsonIgnoreExtraElements]
public class MockCallbackLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("source")]
    public string Source { get; set; } = string.Empty;

    [BsonElement("environment")]
    public string Environment { get; set; } = "default";

    [BsonElement("correlationId")]
    public string? CorrelationId { get; set; }

    [BsonElement("url")]
    public string Url { get; set; } = string.Empty;

    [BsonElement("method")]
    public string Method { get; set; } = "POST";

    [BsonElement("requestHeaders")]
    public Dictionary<string, string> RequestHeaders { get; set; } = new();

    [BsonElement("requestBody")]
    public string? RequestBody { get; set; }

    [BsonElement("responseStatusCode")]
    public int? ResponseStatusCode { get; set; }

    [BsonElement("responseHeaders")]
    public Dictionary<string, string> ResponseHeaders { get; set; } = new();

    /// <summary>First 64 KB of the response body.</summary>
    [BsonElement("responseBody")]
    public string? ResponseBody { get; set; }

    [BsonElement("error")]
    public string? Error { get; set; }

    [BsonElement("dueAt")]
    public DateTime DueAt { get; set; }

    /// <summary>When the attempt was recorded: send start for real sends, log time otherwise.</summary>
    [BsonElement("sentAt")]
    public DateTime SentAt { get; set; }

    [BsonElement("durationMs")]
    public long DurationMs { get; set; }

    public static MockCallbackLog FromJob(CallbackJob job, DateTimeOffset sentAt, string? error = null) => new()
    {
        Source = job.Source,
        Environment = job.Environment,
        CorrelationId = job.CorrelationId,
        Url = job.Url,
        Method = job.Method,
        RequestHeaders = new Dictionary<string, string>(job.Headers ?? new Dictionary<string, string>()),
        RequestBody = job.Body,
        DueAt = job.DueAt.UtcDateTime,
        SentAt = sentAt.UtcDateTime,
        Error = error
    };
}
