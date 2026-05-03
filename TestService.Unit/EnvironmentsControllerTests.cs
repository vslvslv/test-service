using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using TestService.Api.Controllers;
using TestService.Api.Models;

namespace TestService.Unit;

[TestFixture]
public class EnvironmentsControllerTests
{
    private IEnvironmentService _service = null!;
    private EnvironmentsController _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _service = Substitute.For<IEnvironmentService>();
        _sut = BuildSut();
    }

    // ── GetAll ────────────────────────────────────────────────────────────────

    [Test]
    public async Task GetAll_ReturnsOk_WithEnvironmentsList()
    {
        var environments = new List<EnvironmentResponse>
        {
            new() { Id = "1", Name = "dev" },
            new() { Id = "2", Name = "staging" }
        };
        _service.GetAllAsync(false, false).Returns(environments);

        var result = await _sut.GetAll();

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var returned = (ok!.Value as IEnumerable<EnvironmentResponse>)?.ToList();
        Assert.That(returned, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetAll_PassesIncludeInactive_ToService()
    {
        _service.GetAllAsync(Arg.Any<bool>(), Arg.Any<bool>()).Returns(Enumerable.Empty<EnvironmentResponse>());

        await _sut.GetAll(includeInactive: true);

        await _service.Received(1).GetAllAsync(true, false);
    }

    [Test]
    public async Task GetAll_Returns500_WhenServiceThrows()
    {
        _service.GetAllAsync(Arg.Any<bool>(), Arg.Any<bool>()).Returns(Task.FromException<IEnumerable<EnvironmentResponse>>(new Exception("db error")));

        var result = await _sut.GetAll();

        var status = result.Result as ObjectResult;
        Assert.That(status?.StatusCode, Is.EqualTo(500));
    }

    // ── GetById ───────────────────────────────────────────────────────────────

    [Test]
    public async Task GetById_ReturnsOk_WhenEnvironmentExists()
    {
        var env = new EnvironmentResponse { Id = "abc", Name = "dev" };
        _service.GetByIdAsync("abc", false).Returns(env);

        var result = await _sut.GetById("abc");

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        Assert.That(ok!.Value, Is.SameAs(env));
    }

    [Test]
    public async Task GetById_ReturnsNotFound_WhenEnvironmentDoesNotExist()
    {
        _service.GetByIdAsync("missing", false).Returns((EnvironmentResponse?)null);

        var result = await _sut.GetById("missing");

        Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
    }

    // ── GetByName ─────────────────────────────────────────────────────────────

    [Test]
    public async Task GetByName_ReturnsOk_WhenEnvironmentExists()
    {
        var env = new EnvironmentResponse { Id = "1", Name = "dev" };
        _service.GetByNameAsync("dev", false).Returns(env);

        var result = await _sut.GetByName("dev");

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        Assert.That(ok!.Value, Is.SameAs(env));
    }

    [Test]
    public async Task GetByName_ReturnsNotFound_WhenEnvironmentDoesNotExist()
    {
        _service.GetByNameAsync("unknown", false).Returns((EnvironmentResponse?)null);

        var result = await _sut.GetByName("unknown");

        Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Create_ReturnsCreated_WithNewEnvironment()
    {
        var created = new EnvironmentResponse { Id = "new-id", Name = "qa" };
        _service.CreateAsync(Arg.Any<CreateEnvironmentRequest>(), Arg.Any<string?>()).Returns(created);

        var result = await _sut.Create(new CreateEnvironmentRequest { Name = "qa" });

        var createdResult = result.Result as CreatedAtActionResult;
        Assert.That(createdResult, Is.Not.Null);
        Assert.That(createdResult!.Value, Is.SameAs(created));
    }

    [Test]
    public async Task Create_ReturnsConflict_WhenEnvironmentNameAlreadyExists()
    {
        _service.CreateAsync(Arg.Any<CreateEnvironmentRequest>(), Arg.Any<string?>())
                .Returns(Task.FromException<EnvironmentResponse>(new InvalidOperationException("duplicate")));

        var result = await _sut.Create(new CreateEnvironmentRequest { Name = "dev" });

        Assert.That(result.Result, Is.InstanceOf<ConflictObjectResult>());
    }

    [Test]
    public async Task Create_ReturnsBadRequest_WhenNameIsInvalid()
    {
        _service.CreateAsync(Arg.Any<CreateEnvironmentRequest>(), Arg.Any<string?>())
                .Returns(Task.FromException<EnvironmentResponse>(new ArgumentException("Invalid name format")));

        var result = await _sut.Create(new CreateEnvironmentRequest { Name = "INVALID NAME" });

        Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Delete_ReturnsNoContent_WhenEnvironmentExists()
    {
        _service.DeleteAsync("abc").Returns(true);

        var result = await _sut.Delete("abc");

        Assert.That(result, Is.InstanceOf<NoContentResult>());
    }

    [Test]
    public async Task Delete_ReturnsNotFound_WhenEnvironmentDoesNotExist()
    {
        _service.DeleteAsync("missing").Returns(false);

        var result = await _sut.Delete("missing");

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task Delete_ReturnsBadRequest_WhenEnvironmentHasEntities()
    {
        _service.DeleteAsync("has-entities")
                .Returns(Task.FromException<bool>(new InvalidOperationException("Cannot delete environment with entities")));

        var result = await _sut.Delete("has-entities");

        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
    }

    // ── Activate / Deactivate ─────────────────────────────────────────────────

    [Test]
    public async Task Activate_ReturnsNoContent_WhenEnvironmentExists()
    {
        _service.UpdateAsync("abc", Arg.Is<UpdateEnvironmentRequest>(r => r.IsActive == true)).Returns(true);

        var result = await _sut.Activate("abc");

        Assert.That(result, Is.InstanceOf<NoContentResult>());
    }

    [Test]
    public async Task Activate_ReturnsNotFound_WhenEnvironmentDoesNotExist()
    {
        _service.UpdateAsync("missing", Arg.Any<UpdateEnvironmentRequest>()).Returns(false);

        var result = await _sut.Activate("missing");

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task Deactivate_ReturnsNoContent_WhenEnvironmentExists()
    {
        _service.UpdateAsync("abc", Arg.Is<UpdateEnvironmentRequest>(r => r.IsActive == false)).Returns(true);

        var result = await _sut.Deactivate("abc");

        Assert.That(result, Is.InstanceOf<NoContentResult>());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private EnvironmentsController BuildSut(string username = "admin")
    {
        var claims = new[] { new Claim(ClaimTypes.Name, username) };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);

        var controller = new EnvironmentsController(_service, NullLogger<EnvironmentsController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return controller;
    }
}
