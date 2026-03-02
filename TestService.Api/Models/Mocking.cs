using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TestService.Api.Models;

public enum PathMatchType
{
    Exact = 0,
    Prefix = 1,
    Regex = 2
}

public enum BodyMatchType
{
    Any = 0,
    Exact = 1,
    Contains = 2,
    Regex = 3
}

public class MockTimes
{
    [BsonElement("unlimited")]
    public bool Unlimited { get; set; } = true;

    [BsonElement("remaining")]
    public int Remaining { get; set; } = 0;
}

public class MockRequestMatcher
{
    [BsonElement("method")]
    public string? Method { get; set; }

    [BsonElement("path")]
    public string Path { get; set; } = "/";

    [BsonElement("pathMatchType")]
    public PathMatchType PathMatchType { get; set; } = PathMatchType.Exact;

    [BsonElement("query")]
    public Dictionary<string, string> Query { get; set; } = new();

    [BsonElement("headers")]
    public Dictionary<string, string> Headers { get; set; } = new();

    [BsonElement("body")]
    public string? Body { get; set; }

    [BsonElement("bodyMatchType")]
    public BodyMatchType BodyMatchType { get; set; } = BodyMatchType.Any;
}

public class MockResponseTemplate
{
    [BsonElement("status")]
    public int Status { get; set; } = 200;

    [BsonElement("headers")]
    public Dictionary<string, string> Headers { get; set; } = new();

    [BsonElement("body")]
    public string? Body { get; set; }

    [BsonElement("delayMs")]
    public int DelayMs { get; set; } = 0;
}

public class MockExpectation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("environment")]
    public string Environment { get; set; } = "default";

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("priority")]
    public int Priority { get; set; } = 0;

    [BsonElement("enabled")]
    public bool Enabled { get; set; } = true;

    [BsonElement("requestMatcher")]
    public MockRequestMatcher RequestMatcher { get; set; } = new();

    [BsonElement("responseTemplate")]
    public MockResponseTemplate ResponseTemplate { get; set; } = new();

    [BsonElement("times")]
    public MockTimes Times { get; set; } = new();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class MockRequestLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("environment")]
    public string Environment { get; set; } = "default";

    [BsonElement("method")]
    public string Method { get; set; } = "GET";

    [BsonElement("path")]
    public string Path { get; set; } = "/";

    [BsonElement("queryString")]
    public string QueryString { get; set; } = string.Empty;

    [BsonElement("headers")]
    public Dictionary<string, string> Headers { get; set; } = new();

    [BsonElement("body")]
    public string? Body { get; set; }

    [BsonElement("matched")]
    public bool Matched { get; set; }

    [BsonElement("matchedExpectationId")]
    public string? MatchedExpectationId { get; set; }

    [BsonElement("matchedExpectationName")]
    public string? MatchedExpectationName { get; set; }

    [BsonElement("responseStatusCode")]
    public int ResponseStatusCode { get; set; }

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class MockVerificationRequest
{
    public string Environment { get; set; } = "default";
    public MockRequestMatcher Matcher { get; set; } = new();
    public int? ExactCount { get; set; }
    public int? MinCount { get; set; }
    public int? MaxCount { get; set; }
}

public class MockVerificationResponse
{
    public bool Success { get; set; }
    public int MatchedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class MockExecutionRequest
{
    public string Environment { get; set; } = "default";
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = "/";
    public Dictionary<string, string> Query { get; set; } = new();
    public Dictionary<string, string> Headers { get; set; } = new();
    public string QueryString { get; set; } = string.Empty;
    public string? Body { get; set; }
}

public class MockExecutionResult
{
    public bool Matched { get; set; }
    public int StatusCode { get; set; } = 404;
    public Dictionary<string, string> Headers { get; set; } = new();
    public string Body { get; set; } = "{\"message\":\"No mock matched request\"}";
    public int DelayMs { get; set; }
    public string? MatchedExpectationId { get; set; }
    public string? MatchedExpectationName { get; set; }
}

public class PostmanImportResult
{
    public int Created { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class DuplicateExpectationRequest
{
    public string TargetEnvironment { get; set; } = string.Empty;
}
