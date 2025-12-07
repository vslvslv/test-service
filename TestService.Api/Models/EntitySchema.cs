using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TestService.Api.Models;

/// <summary>
/// Defines the structure of a dynamic entity type
/// </summary>
public class EntitySchema
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>
    /// Name of the entity type (e.g., "Agent", "Customer", "Product")
    /// </summary>
    [BsonElement("entityName")]
    public string EntityName { get; set; } = string.Empty;

    /// <summary>
    /// List of field definitions for this entity
    /// </summary>
    [BsonElement("fields")]
    public List<FieldDefinition> Fields { get; set; } = new();

    /// <summary>
    /// List of fields that can be used for filtering
    /// </summary>
    [BsonElement("filterableFields")]
    public List<string> FilterableFields { get; set; } = new();

    /// <summary>
    /// If true, entities of this type will be marked as consumed when fetched
    /// and excluded from subsequent queries (useful for parallel test execution)
    /// </summary>
    [BsonElement("excludeOnFetch")]
    public bool ExcludeOnFetch { get; set; } = false;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Defines a single field in an entity schema
/// </summary>
public class FieldDefinition
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("type")]
    public string Type { get; set; } = "string"; // string, number, boolean, datetime

    [BsonElement("required")]
    public bool Required { get; set; } = false;

    [BsonElement("description")]
    public string? Description { get; set; }
}
