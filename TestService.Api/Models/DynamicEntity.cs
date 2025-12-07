using MongoDB.Bson;
using System.Text.Json.Serialization;

namespace TestService.Api.Models;

/// <summary>
/// Generic dynamic entity that can hold any fields based on schema
/// </summary>
public class DynamicEntity
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    /// <summary>
    /// The entity type name (e.g., "Agent", "Customer")
    /// </summary>
    [JsonPropertyName("entityType")]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// The environment this entity belongs to (e.g., "dev", "staging", "production")
    /// </summary>
    [JsonPropertyName("environment")]
    public string? Environment { get; set; }

    /// <summary>
    /// Dynamic fields stored as key-value pairs
    /// </summary>
    [JsonPropertyName("fields")]
    public Dictionary<string, object?> Fields { get; set; } = new();

    /// <summary>
    /// Indicates if this entity has been consumed/fetched (for parallel test execution)
    /// </summary>
    [JsonPropertyName("isConsumed")]
    public bool IsConsumed { get; set; } = false;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Get field value with proper type extraction from JsonElement
    /// </summary>
    public T? GetFieldValue<T>(string fieldName)
    {
        if (!Fields.ContainsKey(fieldName))
            return default;

        var value = Fields[fieldName];
        if (value is System.Text.Json.JsonElement jsonElement)
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
        }
        
        return value is T typedValue ? typedValue : default;
    }

    /// <summary>
    /// Get field value as string
    /// </summary>
    public string? GetFieldString(string fieldName)
    {
        if (!Fields.ContainsKey(fieldName))
            return null;

        var value = Fields[fieldName];
        if (value is System.Text.Json.JsonElement jsonElement)
        {
            return jsonElement.GetString();
        }
        
        return value?.ToString();
    }

    /// <summary>
    /// Convert to BsonDocument for MongoDB storage
    /// </summary>
    public BsonDocument ToBsonDocument()
    {
        var doc = new BsonDocument
        {
            { "entityType", EntityType },
            { "isConsumed", IsConsumed },
            { "createdAt", CreatedAt },
            { "updatedAt", UpdatedAt }
        };

        if (!string.IsNullOrEmpty(Environment))
        {
            doc["environment"] = Environment;
        }

        if (!string.IsNullOrEmpty(Id) && ObjectId.TryParse(Id, out var objectId))
        {
            doc["_id"] = objectId;
        }

        foreach (var field in Fields)
        {
            if (field.Value != null)
            {
                // Handle JsonElement from deserialization
                if (field.Value is System.Text.Json.JsonElement jsonElement)
                {
                    doc[field.Key] = ConvertJsonElementToBsonValue(jsonElement);
                }
                else
                {
                    doc[field.Key] = BsonValue.Create(field.Value);
                }
            }
            else
            {
                doc[field.Key] = BsonNull.Value;
            }
        }

        return doc;
    }

    private static BsonValue ConvertJsonElementToBsonValue(System.Text.Json.JsonElement element)
    {
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => BsonValue.Create(element.GetString()),
            System.Text.Json.JsonValueKind.Number => element.TryGetInt32(out var intValue) 
                ? BsonValue.Create(intValue) 
                : element.TryGetInt64(out var longValue)
                    ? BsonValue.Create(longValue)
                    : BsonValue.Create(element.GetDouble()),
            System.Text.Json.JsonValueKind.True => BsonValue.Create(true),
            System.Text.Json.JsonValueKind.False => BsonValue.Create(false),
            System.Text.Json.JsonValueKind.Null => BsonNull.Value,
            System.Text.Json.JsonValueKind.Array => new BsonArray(
                element.EnumerateArray().Select(ConvertJsonElementToBsonValue)
            ),
            System.Text.Json.JsonValueKind.Object => new BsonDocument(
                element.EnumerateObject().Select(prop => 
                    new BsonElement(prop.Name, ConvertJsonElementToBsonValue(prop.Value))
                )
            ),
            _ => BsonNull.Value
        };
    }

    /// <summary>
    /// Create from BsonDocument retrieved from MongoDB
    /// </summary>
    public static DynamicEntity FromBsonDocument(BsonDocument doc)
    {
        var entity = new DynamicEntity
        {
            // Extract clean ObjectId string
            Id = doc.Contains("_id") ? doc["_id"].AsObjectId.ToString() : null,
            EntityType = doc.GetValue("entityType", "").AsString,
            Environment = doc.Contains("environment") ? doc["environment"].AsString : null,
            IsConsumed = doc.GetValue("isConsumed", false).AsBoolean,
            CreatedAt = doc.GetValue("createdAt", DateTime.UtcNow).ToUniversalTime(),
            UpdatedAt = doc.GetValue("updatedAt", DateTime.UtcNow).ToUniversalTime()
        };

        foreach (var element in doc.Elements)
        {
            if (element.Name != "_id" && element.Name != "entityType" && 
                element.Name != "environment" && element.Name != "isConsumed" && 
                element.Name != "createdAt" && element.Name != "updatedAt")
            {
                entity.Fields[element.Name] = BsonTypeMapper.MapToDotNetValue(element.Value);
            }
        }

        return entity;
    }
}
