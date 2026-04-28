using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TestService.Api.Models;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration.Entities;

[TestFixture]
public class EntityImportExportTests : IntegrationTestBase
{
    private string _entityType = string.Empty;

    protected override async Task OnOneTimeSetUp()
    {
        _entityType = CreateUniqueName("ImportExportTest");

        var schema = new EntitySchemaBuilder()
            .WithEntityName(_entityType)
            .WithField("name", "string", required: true)
            .WithField("value", "number")
            .WithField("description", "string")
            .WithFilterableField("name")
            .Build();

        await ApiHelpers.CreateSchemaAsync(Client, schema);
    }

    protected override async Task OnOneTimeTearDown()
    {
        await ApiHelpers.DeleteSchemaIfExistsAsync(Client, _entityType);
    }

    [Test]
    public async Task Export_AsJson_ReturnsOkAndValidJson()
    {
        var entity = new DynamicEntityBuilder()
            .WithField("name", "ExportTest")
            .WithField("value", 42)
            .WithField("description", "For export")
            .Build();
        await ApiHelpers.CreateEntityAsync(Client, _entityType, entity);

        var response = await Client.GetAsync($"/api/entities/{_entityType}/export?format=json");

        AssertStatusCode(response, HttpStatusCode.OK);
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
        var json = await response.Content.ReadAsStringAsync();
        var list = JsonSerializer.Deserialize<List<DynamicEntity>>(json);
        Assert.That(list, Is.Not.Null);
        Assert.That(list!.Count, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task Export_AsCsv_ReturnsOkAndValidCsv()
    {
        var response = await Client.GetAsync($"/api/entities/{_entityType}/export?format=csv");

        AssertStatusCode(response, HttpStatusCode.OK);
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/csv"));
        var csv = await response.Content.ReadAsStringAsync();
        Assert.That(csv, Does.Contain("name,"));
        Assert.That(csv.Trim().Split('\n').Length, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task Export_ForNonExistentSchema_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/api/entities/NonExistentSchemaType/export?format=json");
        AssertStatusCode(response, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Import_JsonAppend_CreatesEntities()
    {
        var json = """
            [
              { "fields": { "name": "Imported1", "value": 1, "description": "First" } },
              { "fields": { "name": "Imported2", "value": 2, "description": "Second" } }
            ]
            """;
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Add(fileContent, "file", "import.json");

        var response = await Client.PostAsync($"/api/entities/{_entityType}/import?mode=append", content);

        AssertStatusCode(response, HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<EntityImportResult>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Created, Is.EqualTo(2));
        Assert.That(result.Errors.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task Import_JsonWithMissingRequired_ReturnsErrors()
    {
        var json = """
            [
              { "fields": { "name": "HasName" } },
              { "fields": { "value": 99 } }
            ]
            """;
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Add(fileContent, "file", "import.json");

        var response = await Client.PostAsync($"/api/entities/{_entityType}/import?mode=append", content);

        AssertStatusCode(response, HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<EntityImportResult>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Created, Is.EqualTo(1));
        Assert.That(result.Skipped, Is.EqualTo(1));
        Assert.That(result.Errors.Count, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task Import_NoFile_ReturnsBadRequest()
    {
        using var content = new MultipartFormDataContent();
        var response = await Client.PostAsync($"/api/entities/{_entityType}/import?mode=append", content);
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Import_ForNonExistentSchema_ReturnsNotFound()
    {
        var json = "[ { \"fields\": { \"name\": \"X\" } } ]";
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
        content.Add(fileContent, "file", "import.json");

        var response = await Client.PostAsync("/api/entities/NonExistentType/import?mode=append", content);
        AssertStatusCode(response, HttpStatusCode.NotFound);
    }
}
