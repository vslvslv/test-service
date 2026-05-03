using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TestService.Api.Services;

namespace TestService.Unit;

[TestFixture]
public class EntityImportExportServiceTests
{
    private IDynamicEntityService _entityService = null!;
    private IEntitySchemaRepository _schemaRepository = null!;
    private IActivityService _activityService = null!;
    private ILogger<EntityImportExportService> _logger = null!;
    private EntityImportExportService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _entityService = Substitute.For<IDynamicEntityService>();
        _schemaRepository = Substitute.For<IEntitySchemaRepository>();
        _activityService = Substitute.For<IActivityService>();
        _logger = Substitute.For<ILogger<EntityImportExportService>>();
        _service = new EntityImportExportService(_entityService, _schemaRepository, _activityService, _logger);
    }

    // ── ExportAsJsonAsync ────────────────────────────────────────────────────

    [Test]
    public async Task ExportAsJsonAsync_ReturnsValidJsonArray()
    {
        var entities = new[]
        {
            new DynamicEntity { Id = "1", EntityType = "product", Fields = new Dictionary<string, object?> { ["name"] = "Widget" } },
            new DynamicEntity { Id = "2", EntityType = "product", Fields = new Dictionary<string, object?> { ["name"] = "Gadget" } }
        };
        _entityService.GetAllAsync("product", null).Returns(entities);

        var bytes = await _service.ExportAsJsonAsync("product");

        var json = Encoding.UTF8.GetString(bytes);
        using var doc = JsonDocument.Parse(json);
        Assert.That(doc.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(doc.RootElement.GetArrayLength(), Is.EqualTo(2));
    }

    [Test]
    public async Task ExportAsJsonAsync_ReturnsEmptyArrayWhenNoEntities()
    {
        _entityService.GetAllAsync("product", null).Returns(Enumerable.Empty<DynamicEntity>());

        var bytes = await _service.ExportAsJsonAsync("product");

        var json = Encoding.UTF8.GetString(bytes);
        Assert.That(json, Is.EqualTo("[]"));
    }

    // ── ExportAsCsvAsync ─────────────────────────────────────────────────────

    [Test]
    public async Task ExportAsCsvAsync_ThrowsArgumentException_WhenSchemaNotFound()
    {
        _schemaRepository.GetSchemaByNameAsync("unknown").Returns((EntitySchema?)null);

        Assert.ThrowsAsync<ArgumentException>(async () => await _service.ExportAsCsvAsync("unknown"));
    }

    [Test]
    public async Task ExportAsCsvAsync_ReturnsCsvWithHeaderRow()
    {
        var schema = new EntitySchema
        {
            EntityName = "product",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "sku", Type = "string" },
                new() { Name = "price", Type = "number" }
            }
        };
        _schemaRepository.GetSchemaByNameAsync("product").Returns(schema);
        _entityService.GetAllAsync("product", null).Returns(new[]
        {
            new DynamicEntity
            {
                EntityType = "product",
                Fields = new Dictionary<string, object?> { ["sku"] = "SKU-1", ["price"] = 9.99 }
            }
        });

        var bytes = await _service.ExportAsCsvAsync("product");

        var csv = Encoding.UTF8.GetString(bytes);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines[0].Trim(), Is.EqualTo("sku,price"));
    }

    [Test]
    public async Task ExportAsCsvAsync_EscapesCommaInValues()
    {
        var schema = new EntitySchema
        {
            EntityName = "item",
            Fields = new List<FieldDefinition> { new() { Name = "desc", Type = "string" } }
        };
        _schemaRepository.GetSchemaByNameAsync("item").Returns(schema);
        _entityService.GetAllAsync("item", null).Returns(new[]
        {
            new DynamicEntity
            {
                EntityType = "item",
                Fields = new Dictionary<string, object?> { ["desc"] = "red, green, blue" }
            }
        });

        var bytes = await _service.ExportAsCsvAsync("item");

        var csv = Encoding.UTF8.GetString(bytes);
        Assert.That(csv, Does.Contain("\"red, green, blue\""));
    }

    // ── ImportAsync: JSON ────────────────────────────────────────────────────

    [Test]
    public async Task ImportAsync_Json_ReturnsError_WhenSchemaNotFound()
    {
        _schemaRepository.GetSchemaByNameAsync("ghost").Returns((EntitySchema?)null);

        var stream = ToStream("[{}]");
        var result = await _service.ImportAsync("ghost", stream, "data.json", null, "create");

        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0].Message, Does.Contain("not found"));
    }

    [Test]
    public async Task ImportAsync_Json_ReturnsError_WhenFileTooLarge()
    {
        var schema = SimpleSchema("large");
        _schemaRepository.GetSchemaByNameAsync("large").Returns(schema);

        // 6 MB of zeros
        var bytes = new byte[6 * 1024 * 1024];
        var stream = new MemoryStream(bytes);
        var result = await _service.ImportAsync("large", stream, "data.json", null, "create");

        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0].Message, Does.Contain("size"));
    }

    [Test]
    public async Task ImportAsync_Json_CreatesEntities_ForValidRows()
    {
        var schema = SimpleSchema("user", required: true);
        _schemaRepository.GetSchemaByNameAsync("user").Returns(schema);
        _entityService.ValidateEntityAsync("user", Arg.Any<DynamicEntity>()).Returns(true);
        _entityService.CreateAsync("user", Arg.Any<DynamicEntity>())
            .Returns(Task.FromResult(new DynamicEntity()));

        var json = """[{"name":"Alice"},{"name":"Bob"}]""";
        var result = await _service.ImportAsync("user", ToStream(json), "data.json", null, "create");

        Assert.That(result.Created, Is.EqualTo(2));
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public async Task ImportAsync_Json_SkipsRow_WhenRequiredFieldMissing()
    {
        var schema = SimpleSchema("user", required: true);
        _schemaRepository.GetSchemaByNameAsync("user").Returns(schema);

        var json = """[{"other":"value"}]"""; // "name" is required but absent
        var result = await _service.ImportAsync("user", ToStream(json), "data.json", null, "create");

        Assert.That(result.Skipped, Is.EqualTo(1));
        Assert.That(result.Errors[0].Message, Does.Contain("Required field"));
    }

    [Test]
    public async Task ImportAsync_Json_SkipsRow_WhenValidationFails()
    {
        var schema = SimpleSchema("user");
        _schemaRepository.GetSchemaByNameAsync("user").Returns(schema);
        _entityService.ValidateEntityAsync("user", Arg.Any<DynamicEntity>()).Returns(false);

        var json = """[{"name":"Alice"}]""";
        var result = await _service.ImportAsync("user", ToStream(json), "data.json", null, "create");

        Assert.That(result.Skipped, Is.EqualTo(1));
    }

    [Test]
    public async Task ImportAsync_Json_ReturnsError_WhenRowCountExceedsMax()
    {
        var schema = SimpleSchema("bulk");
        _schemaRepository.GetSchemaByNameAsync("bulk").Returns(schema);

        // 5001 rows
        var rows = Enumerable.Range(1, 5001).Select(i => $"{{\"name\":\"item{i}\"}}");
        var json = "[" + string.Join(",", rows) + "]";

        var result = await _service.ImportAsync("bulk", ToStream(json), "data.json", null, "create");

        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0].Message, Does.Contain("Row count"));
    }

    // ── ImportAsync: CSV ─────────────────────────────────────────────────────

    [Test]
    public async Task ImportAsync_Csv_CreatesEntities_ForValidRows()
    {
        var schema = SimpleSchema("item");
        _schemaRepository.GetSchemaByNameAsync("item").Returns(schema);
        _entityService.ValidateEntityAsync("item", Arg.Any<DynamicEntity>()).Returns(true);
        _entityService.CreateAsync("item", Arg.Any<DynamicEntity>())
            .Returns(Task.FromResult(new DynamicEntity()));

        var csv = "name\nWidget\nGadget\n";
        var result = await _service.ImportAsync("item", ToStream(csv), "data.csv", null, "create");

        Assert.That(result.Created, Is.EqualTo(2));
    }

    [Test]
    public async Task ImportAsync_Csv_ReturnsError_WhenInvalidJson()
    {
        var schema = SimpleSchema("item");
        _schemaRepository.GetSchemaByNameAsync("item").Returns(schema);

        var result = await _service.ImportAsync("item", ToStream("not valid json"), "data.json", null, "create");

        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0].Message, Does.Contain("Invalid JSON"));
    }

    // ── ImportAsync: upsert mode ─────────────────────────────────────────────

    [Test]
    public async Task ImportAsync_Upsert_UpdatesExistingEntity_WhenUniqueKeyMatches()
    {
        var schema = new EntitySchema
        {
            EntityName = "product",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "sku", Type = "string", Required = true, IsUnique = true }
            }
        };
        _schemaRepository.GetSchemaByNameAsync("product").Returns(schema);
        _entityService.ValidateEntityAsync("product", Arg.Any<DynamicEntity>()).Returns(true);

        var existing = new DynamicEntity
        {
            Id = "existing-1",
            EntityType = "product",
            Fields = new Dictionary<string, object?> { ["sku"] = "SKU-100" }
        };
        _entityService.GetAllAsync("product", "dev").Returns(new[] { existing });
        _entityService.UpdateAsync("product", "existing-1", Arg.Any<DynamicEntity>()).Returns(true);

        var json = """[{"sku":"SKU-100"}]""";
        var result = await _service.ImportAsync("product", ToStream(json), "data.json", "dev", "upsert");

        Assert.That(result.Updated, Is.EqualTo(1));
        Assert.That(result.Created, Is.EqualTo(0));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Stream ToStream(string text) =>
        new MemoryStream(Encoding.UTF8.GetBytes(text));

    private static EntitySchema SimpleSchema(string entityType, bool required = false)
    {
        return new EntitySchema
        {
            EntityName = entityType,
            Fields = new List<FieldDefinition>
            {
                new() { Name = "name", Type = "string", Required = required }
            }
        };
    }
}
