using System.Net.Http.Json;
using TestService.Api.Models;

namespace TestService.Tests.Infrastructure;

/// <summary>
/// Builder for creating EntitySchema test objects
/// </summary>
public class EntitySchemaBuilder
{
    private string _entityName = "TestEntity";
    private List<FieldDefinition> _fields = new();
    private List<string> _filterableFields = new();
    private List<string> _uniqueFields = new();
    private bool _excludeOnFetch = false;
    private bool _useCompoundUnique = false;

    public EntitySchemaBuilder WithEntityName(string entityName)
    {
        _entityName = entityName;
        return this;
    }

    public EntitySchemaBuilder WithField(string name, string type = "string", bool required = false, string? description = null)
    {
        _fields.Add(new FieldDefinition
        {
            Name = name,
            Type = type,
            Required = required,
            Description = description
        });
        return this;
    }

    public EntitySchemaBuilder WithFields(params (string name, string type, bool required)[] fields)
    {
        foreach (var (name, type, required) in fields)
        {
            WithField(name, type, required);
        }
        return this;
    }

    public EntitySchemaBuilder WithFilterableField(string fieldName)
    {
        _filterableFields.Add(fieldName);
        return this;
    }

    public EntitySchemaBuilder WithFilterableFields(params string[] fieldNames)
    {
        _filterableFields.AddRange(fieldNames);
        return this;
    }

    public EntitySchemaBuilder WithUniqueField(string fieldName)
    {
        _uniqueFields.Add(fieldName);
        return this;
    }

    public EntitySchemaBuilder WithUniqueFields(params string[] fieldNames)
    {
        _uniqueFields.AddRange(fieldNames);
        return this;
    }

    public EntitySchemaBuilder WithCompoundUnique(bool useCompound = true)
    {
        _useCompoundUnique = useCompound;
        return this;
    }

    public EntitySchemaBuilder WithExcludeOnFetch(bool excludeOnFetch = true)
    {
        _excludeOnFetch = excludeOnFetch;
        return this;
    }

    public EntitySchema Build()
    {
        return new EntitySchema
        {
            EntityName = _entityName,
            Fields = _fields,
            FilterableFields = _filterableFields,
            UniqueFields = _uniqueFields,
            UseCompoundUnique = _useCompoundUnique,
            ExcludeOnFetch = _excludeOnFetch
        };
    }

    /// <summary>
    /// Creates a default Agent schema for testing
    /// </summary>
    public static EntitySchema CreateDefaultAgentSchema(bool excludeOnFetch = false)
    {
        return new EntitySchemaBuilder()
            .WithEntityName("Agent")
            .WithField("username", "string", required: true)
            .WithField("password", "string", required: true)
            .WithField("userId", "string", required: true)
            .WithField("firstName", "string")
            .WithField("lastName", "string")
            .WithField("brandId", "string")
            .WithField("labelId", "string")
            .WithField("orientationType", "string")
            .WithField("agentType", "string")
            .WithFilterableFields("username", "brandId", "labelId", "orientationType", "agentType")
            .WithExcludeOnFetch(excludeOnFetch)
            .Build();
    }

    /// <summary>
    /// Creates a minimal test schema
    /// </summary>
    public static EntitySchema CreateMinimalSchema(string entityName = "MinimalEntity")
    {
        return new EntitySchemaBuilder()
            .WithEntityName(entityName)
            .WithField("name", "string", required: true)
            .WithFilterableField("name")
            .Build();
    }
}

/// <summary>
/// Builder for creating DynamicEntity test objects
/// </summary>
public class DynamicEntityBuilder
{
    private Dictionary<string, object?> _fields = new();
    private string? _environment;

    public DynamicEntityBuilder WithField(string name, object? value)
    {
        _fields[name] = value;
        return this;
    }

    public DynamicEntityBuilder WithFields(Dictionary<string, object?> fields)
    {
        foreach (var kvp in fields)
        {
            _fields[kvp.Key] = kvp.Value;
        }
        return this;
    }

    public DynamicEntityBuilder WithEnvironment(string? environment)
    {
        _environment = environment;
        return this;
    }

    public DynamicEntity Build()
    {
        return new DynamicEntity
        {
            Fields = _fields,
            Environment = _environment
        };
    }

    /// <summary>
    /// Creates a default Agent entity for testing
    /// </summary>
    public static DynamicEntity CreateDefaultAgent(string? username = null, string? environment = null)
    {
        return new DynamicEntityBuilder()
            .WithField("username", username ?? $"user_{Guid.NewGuid()}")
            .WithField("password", "Test@123")
            .WithField("userId", Guid.NewGuid().ToString())
            .WithField("firstName", "Test")
            .WithField("lastName", "User")
            .WithField("brandId", "brand123")
            .WithField("labelId", "label456")
            .WithField("orientationType", "vertical")
            .WithField("agentType", "support")
            .WithEnvironment(environment)
            .Build();
    }

    /// <summary>
    /// Creates a minimal entity
    /// </summary>
    public static DynamicEntity CreateMinimalEntity(string name, string? environment = null)
    {
        return new DynamicEntityBuilder()
            .WithField("name", name)
            .WithEnvironment(environment)
            .Build();
    }
}
