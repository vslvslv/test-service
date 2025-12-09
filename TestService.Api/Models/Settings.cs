using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace TestService.Api.Models;

/// <summary>
/// Application settings stored in database
/// </summary>
public class AppSettings
{
    [BsonId]
    [BsonElement("_id")]
    [JsonPropertyName("id")]
    public string Id { get; set; } = "app_settings";

    [JsonPropertyName("dataRetention")]
    public DataRetentionSettings DataRetention { get; set; } = new();

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedBy")]
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Data retention configuration
/// </summary>
public class DataRetentionSettings
{
    /// <summary>
    /// Number of days to keep schemas before deletion (null = never delete)
    /// </summary>
    [JsonPropertyName("schemaRetentionDays")]
    public int? SchemaRetentionDays { get; set; }

    /// <summary>
    /// Number of days to keep entities before deletion (null = never delete)
    /// </summary>
    [JsonPropertyName("entityRetentionDays")]
    public int? EntityRetentionDays { get; set; }

    /// <summary>
    /// Whether automatic cleanup is enabled
    /// </summary>
    [JsonPropertyName("autoCleanupEnabled")]
    public bool AutoCleanupEnabled { get; set; } = true;
}

/// <summary>
/// API Key for external integrations
/// </summary>
public class ApiKey
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The actual API key value (should be hashed in production)
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Hashed version of the key for secure storage
    /// </summary>
    [BsonElement("keyHash")]
    [JsonIgnore]
    public string? KeyHash { get; set; }

    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("createdBy")]
    public string? CreatedBy { get; set; }

    [JsonPropertyName("lastUsed")]
    public DateTime? LastUsed { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Check if the API key is expired
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
}

/// <summary>
/// Request to create a new API key
/// </summary>
public class CreateApiKeyRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Number of days until expiration (null = never expires)
    /// </summary>
    [JsonPropertyName("expirationDays")]
    public int? ExpirationDays { get; set; }
}
