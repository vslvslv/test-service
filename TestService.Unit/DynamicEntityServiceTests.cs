using Microsoft.Extensions.Logging;
using TestService.Api.Services;

namespace TestService.Unit;

[TestFixture]
public class DynamicEntityServiceTests
{
    private IDynamicEntityRepository _repository = null!;
    private IEntitySchemaRepository _schemaRepository = null!;
    private IMessageBusService _messageBus = null!;
    private IEnvironmentService _environmentService = null!;
    private ILogger<DynamicEntityService> _logger = null!;
    private DynamicEntityService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IDynamicEntityRepository>();
        _schemaRepository = Substitute.For<IEntitySchemaRepository>();
        _messageBus = Substitute.For<IMessageBusService>();
        _environmentService = Substitute.For<IEnvironmentService>();
        _logger = Substitute.For<ILogger<DynamicEntityService>>();
        _service = new DynamicEntityService(_repository, _schemaRepository, _messageBus, _environmentService, _logger);
    }

    // ── ValidateEntityAsync ──────────────────────────────────────────────────

    [Test]
    public async Task ValidateEntityAsync_ReturnsFalse_WhenSchemaNotFound()
    {
        _schemaRepository.GetSchemaByNameAsync("unknown").Returns((EntitySchema?)null);

        var entity = new DynamicEntity { Fields = new Dictionary<string, object?>() };
        var result = await _service.ValidateEntityAsync("unknown", entity);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task ValidateEntityAsync_ReturnsFalse_WhenRequiredFieldMissing()
    {
        var schema = BuildSchema("order",
            new FieldDefinition { Name = "orderId", Type = "string", Required = true });
        _schemaRepository.GetSchemaByNameAsync("order").Returns(schema);
        _repository.GetAllAsync("order", Arg.Any<bool>(), Arg.Any<string?>())
            .Returns(Enumerable.Empty<DynamicEntity>());

        var entity = new DynamicEntity
        {
            EntityType = "order",
            Fields = new Dictionary<string, object?>() // orderId missing
        };

        var result = await _service.ValidateEntityAsync("order", entity);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task ValidateEntityAsync_ReturnsTrue_WhenAllRequiredFieldsPresent()
    {
        var schema = BuildSchema("order",
            new FieldDefinition { Name = "orderId", Type = "string", Required = true });
        _schemaRepository.GetSchemaByNameAsync("order").Returns(schema);
        _repository.GetAllAsync("order", Arg.Any<bool>(), Arg.Any<string?>())
            .Returns(Enumerable.Empty<DynamicEntity>());

        var entity = new DynamicEntity
        {
            EntityType = "order",
            Fields = new Dictionary<string, object?> { ["orderId"] = "ORD-001" }
        };

        var result = await _service.ValidateEntityAsync("order", entity);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task ValidateEntityAsync_ReturnsFalse_WhenIndividualUniqueFieldDuplicated()
    {
        var schema = BuildSchema("product",
            new FieldDefinition { Name = "sku", Type = "string", Required = true, IsUnique = true });
        _schemaRepository.GetSchemaByNameAsync("product").Returns(schema);

        var existing = new DynamicEntity
        {
            Id = "existing-1",
            EntityType = "product",
            Fields = new Dictionary<string, object?> { ["sku"] = "SKU-100" }
        };
        _repository.GetAllAsync("product", Arg.Any<bool>(), Arg.Any<string?>())
            .Returns(new[] { existing });

        var newEntity = new DynamicEntity
        {
            EntityType = "product",
            Fields = new Dictionary<string, object?> { ["sku"] = "SKU-100" } // duplicate
        };

        var result = await _service.ValidateEntityAsync("product", newEntity);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task ValidateEntityAsync_ReturnsTrue_WhenUniqueFieldUpdatedToSameValue()
    {
        // Update scenario: entity with same ID should not conflict with itself
        var schema = BuildSchema("product",
            new FieldDefinition { Name = "sku", Type = "string", Required = true, IsUnique = true });
        _schemaRepository.GetSchemaByNameAsync("product").Returns(schema);

        var existing = new DynamicEntity
        {
            Id = "entity-1",
            EntityType = "product",
            Fields = new Dictionary<string, object?> { ["sku"] = "SKU-100" }
        };
        _repository.GetAllAsync("product", Arg.Any<bool>(), Arg.Any<string?>())
            .Returns(new[] { existing });

        var updatedEntity = new DynamicEntity
        {
            Id = "entity-1", // same ID — should not conflict with itself
            EntityType = "product",
            Fields = new Dictionary<string, object?> { ["sku"] = "SKU-100" }
        };

        var result = await _service.ValidateEntityAsync("product", updatedEntity);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task ValidateEntityAsync_ReturnsFalse_WhenCompoundUniqueViolated()
    {
        var schema = new EntitySchema
        {
            EntityName = "slot",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "date", Type = "string", Required = true },
                new() { Name = "room", Type = "string", Required = true }
            },
            UniqueFields = new List<string> { "date", "room" },
            UseCompoundUnique = true
        };
        _schemaRepository.GetSchemaByNameAsync("slot").Returns(schema);

        var existing = new DynamicEntity
        {
            Id = "slot-1",
            EntityType = "slot",
            Fields = new Dictionary<string, object?> { ["date"] = "2025-01-01", ["room"] = "A" }
        };
        _repository.GetAllAsync("slot", Arg.Any<bool>(), Arg.Any<string?>())
            .Returns(new[] { existing });

        var conflict = new DynamicEntity
        {
            EntityType = "slot",
            Fields = new Dictionary<string, object?> { ["date"] = "2025-01-01", ["room"] = "A" }
        };

        var result = await _service.ValidateEntityAsync("slot", conflict);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task ValidateEntityAsync_ReturnsTrue_WhenCompoundUniquePartiallyDiffers()
    {
        var schema = new EntitySchema
        {
            EntityName = "slot",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "date", Type = "string", Required = true },
                new() { Name = "room", Type = "string", Required = true }
            },
            UniqueFields = new List<string> { "date", "room" },
            UseCompoundUnique = true
        };
        _schemaRepository.GetSchemaByNameAsync("slot").Returns(schema);

        var existing = new DynamicEntity
        {
            Id = "slot-1",
            EntityType = "slot",
            Fields = new Dictionary<string, object?> { ["date"] = "2025-01-01", ["room"] = "A" }
        };
        _repository.GetAllAsync("slot", Arg.Any<bool>(), Arg.Any<string?>())
            .Returns(new[] { existing });

        var noConflict = new DynamicEntity
        {
            EntityType = "slot",
            Fields = new Dictionary<string, object?> { ["date"] = "2025-01-01", ["room"] = "B" } // different room
        };

        var result = await _service.ValidateEntityAsync("slot", noConflict);

        Assert.That(result, Is.True);
    }

    // ── GetNextAvailableAsync ────────────────────────────────────────────────

    [Test]
    public async Task GetNextAvailableAsync_ReturnsNull_WhenSchemaNotFound()
    {
        _schemaRepository.GetSchemaByNameAsync("agent").Returns((EntitySchema?)null);

        var result = await _service.GetNextAvailableAsync("agent");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetNextAvailableAsync_ReturnsNull_WhenExcludeOnFetchIsFalse()
    {
        var schema = BuildSchema("agent", new FieldDefinition { Name = "name", Type = "string" });
        schema.ExcludeOnFetch = false;
        _schemaRepository.GetSchemaByNameAsync("agent").Returns(schema);

        var result = await _service.GetNextAvailableAsync("agent");

        Assert.That(result, Is.Null);
        await _repository.DidNotReceive().GetNextAvailableAsync(Arg.Any<string>(), Arg.Any<string?>());
    }

    [Test]
    public async Task GetNextAvailableAsync_PublishesConsumedMessage_WhenEntityFound()
    {
        var schema = BuildSchema("agent");
        schema.ExcludeOnFetch = true;
        _schemaRepository.GetSchemaByNameAsync("agent").Returns(schema);

        var entity = new DynamicEntity { Id = "ent-1", EntityType = "agent" };
        _repository.GetNextAvailableAsync("agent", null).Returns(entity);

        var result = await _service.GetNextAvailableAsync("agent");

        Assert.That(result, Is.SameAs(entity));
        await _messageBus.Received(1).PublishAsync(Arg.Any<object>(), Arg.Is<string>(s => s.Contains("consumed")));
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Test]
    public void CreateAsync_Throws_WhenEnvironmentDoesNotExist()
    {
        var schema = BuildSchema("order", new FieldDefinition { Name = "id", Type = "string", Required = true });
        _schemaRepository.GetSchemaByNameAsync("order").Returns(schema);
        _environmentService.GetByNameAsync("staging").Returns((EnvironmentResponse?)null);

        var entity = new DynamicEntity
        {
            EntityType = "order",
            Environment = "staging",
            Fields = new Dictionary<string, object?> { ["id"] = "ORD-1" }
        };

        Assert.ThrowsAsync<ArgumentException>(async () => await _service.CreateAsync("order", entity));
    }

    [Test]
    public void CreateAsync_Throws_WhenSchemaNotFound()
    {
        _schemaRepository.GetSchemaByNameAsync("unknown").Returns((EntitySchema?)null);

        var entity = new DynamicEntity { Fields = new Dictionary<string, object?>() };

        Assert.ThrowsAsync<ArgumentException>(async () => await _service.CreateAsync("unknown", entity));
    }

    [Test]
    public async Task CreateAsync_PublishesCreatedMessage_OnSuccess()
    {
        var schema = BuildSchema("order",
            new FieldDefinition { Name = "ref", Type = "string", Required = true });
        _schemaRepository.GetSchemaByNameAsync("order").Returns(schema);
        _repository.GetAllAsync("order", Arg.Any<bool>(), Arg.Any<string?>())
            .Returns(Enumerable.Empty<DynamicEntity>());

        var createdEntity = new DynamicEntity { Id = "new-1", EntityType = "order" };
        _repository.CreateAsync(Arg.Any<DynamicEntity>()).Returns(createdEntity);

        var entity = new DynamicEntity
        {
            EntityType = "order",
            Fields = new Dictionary<string, object?> { ["ref"] = "REF-001" }
        };

        var result = await _service.CreateAsync("order", entity);

        Assert.That(result, Is.SameAs(createdEntity));
        await _messageBus.Received(1).PublishAsync(Arg.Any<object>(), Arg.Is<string>(s => s.Contains("created")));
    }

    // ── DeleteAllAsync ───────────────────────────────────────────────────────

    [Test]
    public async Task DeleteAllAsync_ReturnsDeletionCount()
    {
        _repository.DeleteAllAsync("order", null).Returns(7);

        var count = await _service.DeleteAllAsync("order");

        Assert.That(count, Is.EqualTo(7));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static EntitySchema BuildSchema(string entityType, params FieldDefinition[] fields)
    {
        return new EntitySchema
        {
            EntityName = entityType,
            Fields = fields.Length > 0 ? new List<FieldDefinition>(fields) : new List<FieldDefinition>()
        };
    }
}
