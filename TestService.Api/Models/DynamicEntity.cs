using MongoDB.Bson;
using System.Globalization;
using System.Text.Json;
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
        if (value == null)
        {
            return default;
        }

        if (value is JsonElement jsonElement)
        {
            return ReadJsonElementValue<T>(jsonElement);
        }

        if (value is T typedValue)
        {
            return typedValue;
        }

        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        try
        {
            if (targetType == typeof(string))
            {
                return (T?)(object?)value.ToString();
            }

            if (targetType == typeof(Guid))
            {
                return (T?)(object)Guid.Parse(value.ToString()!);
            }

            if (targetType == typeof(DateTime))
            {
                if (value is DateTime dateTime)
                {
                    return (T?)(object)dateTime;
                }

                return (T?)(object)DateTime.Parse(value.ToString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }

            if (TryConvertFromString(targetType, value.ToString(), out var convertedFromString))
            {
                return (T?)convertedFromString;
            }

            if (value is IConvertible)
            {
                return (T?)Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }
        }
        catch
        {
            // Fall through to JSON round-trip for structured values or mixed numeric types.
        }

        var serialized = JsonSerializer.Serialize(value);
        return JsonSerializer.Deserialize<T>(serialized);
    }

    private static T? ReadJsonElementValue<T>(JsonElement jsonElement)
    {
        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        try
        {
            if (targetType == typeof(string))
            {
                return (T?)(object?)jsonElement.ToString();
            }

            if (targetType == typeof(bool) && (jsonElement.ValueKind is JsonValueKind.True or JsonValueKind.False))
            {
                return (T?)(object)jsonElement.GetBoolean();
            }

            if (targetType == typeof(int) && jsonElement.TryGetInt32(out var intValue))
            {
                return (T?)(object)intValue;
            }

            if (targetType == typeof(long) && jsonElement.TryGetInt64(out var longValue))
            {
                return (T?)(object)longValue;
            }

            if (targetType == typeof(decimal) && jsonElement.TryGetDecimal(out var decimalValue))
            {
                return (T?)(object)decimalValue;
            }

            if (targetType == typeof(double) && jsonElement.TryGetDouble(out var doubleValue))
            {
                return (T?)(object)doubleValue;
            }

            if (targetType == typeof(float) && jsonElement.TryGetSingle(out var floatValue))
            {
                return (T?)(object)floatValue;
            }

            if (targetType == typeof(DateTime) && jsonElement.ValueKind == JsonValueKind.String)
            {
                return (T?)(object)DateTime.Parse(jsonElement.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }
        }
        catch
        {
            // Fall back to flexible conversion below.
        }

        if (TryConvertFromString(targetType, jsonElement.ToString(), out var convertedFromString))
        {
            return (T?)convertedFromString;
        }

        return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
    }

    private static bool TryConvertFromString(Type targetType, string? value, out object? convertedValue)
    {
        convertedValue = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (targetType == typeof(int) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            convertedValue = intValue;
            return true;
        }

        if (targetType == typeof(long) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            convertedValue = longValue;
            return true;
        }

        if (targetType == typeof(decimal) && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
        {
            convertedValue = decimalValue;
            return true;
        }

        if (targetType == typeof(double) && double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue))
        {
            convertedValue = doubleValue;
            return true;
        }

        if (targetType == typeof(float) && float.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var floatValue))
        {
            convertedValue = floatValue;
            return true;
        }

        if (targetType == typeof(bool) && bool.TryParse(value, out var boolValue))
        {
            convertedValue = boolValue;
            return true;
        }

        if (targetType == typeof(DateTime) && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTimeValue))
        {
            convertedValue = dateTimeValue;
            return true;
        }

        return false;
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
                entity.Fields[element.Name] = ConvertBsonValueToDotNetValue(element.Value);
            }
        }

        return entity;
    }

    private static object? ConvertBsonValueToDotNetValue(BsonValue value)
    {
        if (value.IsBsonNull)
        {
            return null;
        }

        return value.BsonType switch
        {
            BsonType.Decimal128 => ConvertDecimal128(value.AsDecimal128),
            BsonType.Document => value.AsBsonDocument.Elements.ToDictionary(
                element => element.Name,
                element => ConvertBsonValueToDotNetValue(element.Value)),
            BsonType.Array => value.AsBsonArray.Select(ConvertBsonValueToDotNetValue).ToList(),
            _ => BsonTypeMapper.MapToDotNetValue(value)
        };
    }

    private static object ConvertDecimal128(Decimal128 value)
    {
        var decimalText = value.ToString();

        if (long.TryParse(decimalText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            return longValue;
        }

        if (decimal.TryParse(decimalText, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
        {
            return decimalValue;
        }

        return double.Parse(decimalText, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
    }
}
