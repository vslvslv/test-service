using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TestService.Api.Models;

/// <summary>
/// Represents an activity/action performed in the system
/// </summary>
public class Activity
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// When the activity occurred
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Type of resource affected (entity, schema, user, environment)
    /// </summary>
    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Action performed (created, updated, deleted, consumed, reset, etc.)
    /// </summary>
    [BsonElement("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Entity type (e.g., "test-agent", "product") - for entity operations
    /// </summary>
    [BsonElement("entityType")]
    public string? EntityType { get; set; }

    /// <summary>
    /// Specific entity ID - for entity operations
    /// </summary>
    [BsonElement("entityId")]
    public string? EntityId { get; set; }

    /// <summary>
    /// User who performed the action
    /// </summary>
    [BsonElement("user")]
    public string User { get; set; } = string.Empty;

    /// <summary>
    /// Environment context (e.g., "dev", "qa", "staging")
    /// </summary>
    [BsonElement("environment")]
    public string? Environment { get; set; }

    /// <summary>
    /// Additional context about the activity
    /// </summary>
    [BsonElement("details")]
    public ActivityDetails? Details { get; set; }

    /// <summary>
    /// Human-readable description of the activity
    /// </summary>
    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Additional details about an activity
/// </summary>
public class ActivityDetails
{
    /// <summary>
    /// Number of items affected (for bulk operations)
    /// </summary>
    [BsonElement("count")]
    public int? Count { get; set; }

    /// <summary>
    /// Fields that were modified (for update operations)
    /// </summary>
    [BsonElement("fields")]
    public List<string>? Fields { get; set; }

    /// <summary>
    /// Previous value (for updates)
    /// </summary>
    [BsonElement("oldValue")]
    public string? OldValue { get; set; }

    /// <summary>
    /// New value (for updates)
    /// </summary>
    [BsonElement("newValue")]
    public string? NewValue { get; set; }

    /// <summary>
    /// IP address of the requester
    /// </summary>
    [BsonElement("ipAddress")]
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string
    /// </summary>
    [BsonElement("userAgent")]
    public string? UserAgent { get; set; }
}

/// <summary>
/// Constants for activity types
/// </summary>
public static class ActivityType
{
    public const string Entity = "entity";
    public const string Schema = "schema";
    public const string User = "user";
    public const string Environment = "environment";
    public const string System = "system";
}

/// <summary>
/// Constants for activity actions
/// </summary>
public static class ActivityAction
{
    public const string Created = "created";
    public const string Updated = "updated";
    public const string Deleted = "deleted";
    public const string Consumed = "consumed";
    public const string Reset = "reset";
    public const string BulkReset = "bulk-reset";
    public const string LoggedIn = "logged-in";
    public const string LoggedOut = "logged-out";
}
