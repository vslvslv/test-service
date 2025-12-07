using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TestService.Api.Models;

/// <summary>
/// Represents a test environment for organizing test data
/// </summary>
public class Environment
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>
    /// Unique name of the environment (e.g., "dev", "staging", "production")
    /// </summary>
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the environment
    /// </summary>
    [BsonElement("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Description of the environment
    /// </summary>
    [BsonElement("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Environment URL or endpoint
    /// </summary>
    [BsonElement("url")]
    public string? Url { get; set; }

    /// <summary>
    /// Environment color for UI display (e.g., "#00ff00")
    /// </summary>
    [BsonElement("color")]
    public string? Color { get; set; }

    /// <summary>
    /// Whether this environment is active
    /// </summary>
    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Order for display purposes
    /// </summary>
    [BsonElement("order")]
    public int Order { get; set; } = 0;

    /// <summary>
    /// Configuration metadata for this environment
    /// </summary>
    [BsonElement("configuration")]
    public Dictionary<string, string> Configuration { get; set; } = new();

    /// <summary>
    /// Tags for categorization
    /// </summary>
    [BsonElement("tags")]
    public List<string> Tags { get; set; } = new();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("createdBy")]
    public string? CreatedBy { get; set; }
}

/// <summary>
/// DTO for creating an environment
/// </summary>
public class CreateEnvironmentRequest
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Url { get; set; }
    public string? Color { get; set; }
    public int Order { get; set; } = 0;
    public Dictionary<string, string>? Configuration { get; set; }
    public List<string>? Tags { get; set; }
}

/// <summary>
/// DTO for updating an environment
/// </summary>
public class UpdateEnvironmentRequest
{
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Url { get; set; }
    public string? Color { get; set; }
    public bool? IsActive { get; set; }
    public int? Order { get; set; }
    public Dictionary<string, string>? Configuration { get; set; }
    public List<string>? Tags { get; set; }
}

/// <summary>
/// DTO for environment response
/// </summary>
public class EnvironmentResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Url { get; set; }
    public string? Color { get; set; }
    public bool IsActive { get; set; }
    public int Order { get; set; }
    public Dictionary<string, string> Configuration { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public EnvironmentStatistics? Statistics { get; set; }

    public static EnvironmentResponse FromEnvironment(Environment env, EnvironmentStatistics? stats = null)
    {
        return new EnvironmentResponse
        {
            Id = env.Id ?? string.Empty,
            Name = env.Name,
            DisplayName = env.DisplayName,
            Description = env.Description,
            Url = env.Url,
            Color = env.Color,
            IsActive = env.IsActive,
            Order = env.Order,
            Configuration = env.Configuration,
            Tags = env.Tags,
            CreatedAt = env.CreatedAt,
            UpdatedAt = env.UpdatedAt,
            CreatedBy = env.CreatedBy,
            Statistics = stats
        };
    }
}

/// <summary>
/// Statistics for an environment
/// </summary>
public class EnvironmentStatistics
{
    public int TotalEntities { get; set; }
    public int AvailableEntities { get; set; }
    public int ConsumedEntities { get; set; }
    public Dictionary<string, int> EntitiesByType { get; set; } = new();
    public DateTime? LastActivity { get; set; }
}
