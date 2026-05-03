using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using TestService.Api.Controllers;

namespace TestService.Unit;

[TestFixture]
public class SchemasControllerTests
{
    private IEntitySchemaRepository _repo = null!;
    private INotificationService _notifications = null!;
    private SchemasController _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = Substitute.For<IEntitySchemaRepository>();
        _notifications = Substitute.For<INotificationService>();
        _sut = new SchemasController(_repo, _notifications, NullLogger<SchemasController>.Instance);
    }

    // ── GetAll ────────────────────────────────────────────────────────────────

    [Test]
    public async Task GetAll_ReturnsOk_WithSchemasList()
    {
        var schemas = new List<EntitySchema>
        {
            new() { EntityName = "Agent" },
            new() { EntityName = "Customer" }
        };
        _repo.GetAllSchemasAsync().Returns(schemas);

        var result = await _sut.GetAll();

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var returned = (ok!.Value as IEnumerable<EntitySchema>)?.ToList();
        Assert.That(returned, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetAll_Returns500_WhenRepositoryThrows()
    {
        _repo.GetAllSchemasAsync().Returns(Task.FromException<IEnumerable<EntitySchema>>(new Exception("db error")));

        var result = await _sut.GetAll();

        var status = result.Result as ObjectResult;
        Assert.That(status?.StatusCode, Is.EqualTo(500));
    }

    // ── GetByName ─────────────────────────────────────────────────────────────

    [Test]
    public async Task GetByName_ReturnsOk_WhenSchemaExists()
    {
        var schema = new EntitySchema { EntityName = "Agent" };
        _repo.GetSchemaByNameAsync("Agent").Returns(schema);

        var result = await _sut.GetByName("Agent");

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        Assert.That(ok!.Value, Is.SameAs(schema));
    }

    [Test]
    public async Task GetByName_ReturnsNotFound_WhenSchemaDoesNotExist()
    {
        _repo.GetSchemaByNameAsync("Unknown").Returns((EntitySchema?)null);

        var result = await _sut.GetByName("Unknown");

        Assert.That(result.Result, Is.InstanceOf<NotFoundObjectResult>());
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Create_ReturnsCreated_WithNewSchema()
    {
        var input = new EntitySchema { EntityName = "Order" };
        _repo.SchemaExistsAsync("Order").Returns(false);
        _repo.CreateSchemaAsync(input).Returns(input);

        var result = await _sut.Create(input);

        var created = result.Result as CreatedAtActionResult;
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Value, Is.SameAs(input));
    }

    [Test]
    public async Task Create_SendsNotification_AfterCreatingSchema()
    {
        var input = new EntitySchema { EntityName = "Order" };
        _repo.SchemaExistsAsync("Order").Returns(false);
        _repo.CreateSchemaAsync(input).Returns(input);

        await _sut.Create(input);

        await _notifications.Received(1).NotifySchemaCreated("Order", input);
    }

    [Test]
    public async Task Create_ReturnsConflict_WhenSchemaAlreadyExists()
    {
        _repo.SchemaExistsAsync("Agent").Returns(true);

        var result = await _sut.Create(new EntitySchema { EntityName = "Agent" });

        var conflict = result.Result as ConflictObjectResult;
        Assert.That(conflict, Is.Not.Null);
    }

    [Test]
    public async Task Create_ReturnsBadRequest_WhenEntityNameIsEmpty()
    {
        var result = await _sut.Create(new EntitySchema { EntityName = "" });

        Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Create_ReturnsBadRequest_WhenEntityNameIsWhitespace()
    {
        var result = await _sut.Create(new EntitySchema { EntityName = "   " });

        Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Update_ReturnsNoContent_WhenSchemaExists()
    {
        _repo.UpdateSchemaAsync("Agent", Arg.Any<EntitySchema>()).Returns(true);

        var result = await _sut.Update("Agent", new EntitySchema { EntityName = "Agent" });

        Assert.That(result, Is.InstanceOf<NoContentResult>());
    }

    [Test]
    public async Task Update_SendsNotification_AfterUpdatingSchema()
    {
        var schema = new EntitySchema { EntityName = "Agent" };
        _repo.UpdateSchemaAsync("Agent", schema).Returns(true);

        await _sut.Update("Agent", schema);

        await _notifications.Received(1).NotifySchemaUpdated("Agent", schema);
    }

    [Test]
    public async Task Update_ReturnsNotFound_WhenSchemaDoesNotExist()
    {
        _repo.UpdateSchemaAsync("Missing", Arg.Any<EntitySchema>()).Returns(false);

        var result = await _sut.Update("Missing", new EntitySchema { EntityName = "Missing" });

        Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Delete_ReturnsNoContent_WhenSchemaExists()
    {
        _repo.DeleteSchemaAsync("Agent").Returns(true);

        var result = await _sut.Delete("Agent");

        Assert.That(result, Is.InstanceOf<NoContentResult>());
    }

    [Test]
    public async Task Delete_SendsNotification_AfterDeletingSchema()
    {
        _repo.DeleteSchemaAsync("Agent").Returns(true);

        await _sut.Delete("Agent");

        await _notifications.Received(1).NotifySchemaDeleted("Agent");
    }

    [Test]
    public async Task Delete_ReturnsNotFound_WhenSchemaDoesNotExist()
    {
        _repo.DeleteSchemaAsync("Missing").Returns(false);

        var result = await _sut.Delete("Missing");

        Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
    }
}
