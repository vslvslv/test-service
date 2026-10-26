using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TestService.Api.Controllers;
using TestService.Api.Emulators;
using TestService.Api.Emulators.Praxis;
using TestService.Api.Emulators.Transactions;
using TestService.Tests.Praxis;

namespace TestService.Unit.Emulators;

[TestFixture]
public class EmulatorMiddlewareTests
{
    private sealed class FakeEmulator : IProviderEmulator
    {
        public string Provider => "fake";
        public EmulatorRequest? Received { get; private set; }

        public Task<bool> HasEnvironmentAsync(string environment) => Task.FromResult(environment == "known");

        public Task<EmulatorReply> HandleAsync(EmulatorRequest request)
        {
            Received = request;
            return Task.FromResult(new EmulatorReply
            {
                StatusCode = 302,
                ContentType = "text/html",
                Body = "<p>ok</p>",
                Headers = { ["Location"] = "https://merchant.example/return", ["GT-Authentication"] = "sig" }
            });
        }
    }

    private static async Task<(DefaultHttpContext Context, bool NextCalled, string Body)> InvokeAsync(
        string path, FakeEmulator emulator, string method = "GET", string? body = null, string? contentType = null, string query = "")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("svc.test", 5000);
        context.Request.PathBase = "/base";
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = bytes.Length;
            context.Request.ContentType = contentType;
        }
        context.Response.Body = new MemoryStream();

        var nextCalled = false;
        var middleware = new EmulatorMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(context, [emulator]);

        context.Response.Body.Position = 0;
        return (context, nextCalled, await new StreamReader(context.Response.Body).ReadToEndAsync());
    }

    [Test]
    public async Task OtherPath_GoesToNext()
    {
        var (_, nextCalled, _) = await InvokeAsync("/mock/env/x", new FakeEmulator());
        Assert.That(nextCalled, Is.True);
    }

    [TestCase("/emulators", "Provider segment is required")]
    [TestCase("/emulators/unknown/env/x", "Unknown emulator provider 'unknown'.")]
    [TestCase("/emulators/fake", "Environment segment is required")]
    [TestCase("/emulators/fake/empty/api/direct-process", "No fake merchant is registered for environment 'empty'.")]
    public async Task NotRoutable_404WithJsonMessage(string path, string message)
    {
        var emulator = new FakeEmulator();
        var (context, nextCalled, body) = await InvokeAsync(path, emulator, "POST", "{}", "application/json");

        Assert.Multiple(() =>
        {
            Assert.That(nextCalled, Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(404));
            Assert.That(context.Response.ContentType, Does.StartWith("application/json"));
            Assert.That(JsonDocument.Parse(body).RootElement.GetProperty("message").GetString(), Does.StartWith(message));
            Assert.That(emulator.Received, Is.Null, "nothing reaches the emulator");
        });
    }

    [Test]
    public async Task Routable_MapsRequestAndWritesReply()
    {
        var emulator = new FakeEmulator();
        var (context, _, body) = await InvokeAsync("/emulators/FAKE/known/pages/3ds/7/submit", emulator, "post",
            "error_code=906&x=1", "application/x-www-form-urlencoded", "?status=declined");

        var received = emulator.Received!;
        Assert.Multiple(() =>
        {
            Assert.That(received.Environment, Is.EqualTo("known"));
            Assert.That(received.Path, Is.EqualTo("pages/3ds/7/submit"));
            Assert.That(received.Method, Is.EqualTo("POST"));
            Assert.That(received.Body, Is.EqualTo("error_code=906&x=1"));
            Assert.That(received.Form["error_code"], Is.EqualTo("906"));
            Assert.That(received.Query["status"], Is.EqualTo("declined"));
            Assert.That(received.Header("content-type"), Is.EqualTo("application/x-www-form-urlencoded"));
            Assert.That(received.Header("missing"), Is.Null);
            Assert.That(received.BaseUrl, Is.EqualTo("http://svc.test:5000/base"));
            Assert.That(context.Response.StatusCode, Is.EqualTo(302));
            Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo("https://merchant.example/return"));
            Assert.That(context.Response.Headers["GT-Authentication"].ToString(), Is.EqualTo("sig"));
            Assert.That(context.Response.ContentType, Is.EqualTo("text/html"));
            Assert.That(body, Is.EqualTo("<p>ok</p>"));
        });
    }

    [Test]
    public async Task Get_WithoutBody_HasNullBodyAndNoForm()
    {
        var emulator = new FakeEmulator();
        await InvokeAsync("/emulators/fake/known/pages/3ds/7", emulator);

        Assert.Multiple(() =>
        {
            Assert.That(emulator.Received!.Body, Is.Null);
            Assert.That(emulator.Received.Form, Is.Empty);
        });
    }

    [Test]
    public void Reply_JsonOnlyForJsonBodies()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new EmulatorReply { ContentType = "text/html", Body = "<p/>" }.Json(), Is.Null);
            Assert.That(new EmulatorReply { Body = "" }.Json(), Is.Null);
            Assert.That(EmulatorReply.Message(404, "m").Json()!["message"]!.GetValue<string>(), Is.EqualTo("m"));
        });
    }
}

[TestFixture]
public class EmulatorsControllerTests
{
    private InMemoryPraxisMerchantRepository _merchants = null!;
    private InMemoryTransactionRepository _transactions = null!;
    private EmulatorsController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _merchants = new InMemoryPraxisMerchantRepository();
        _transactions = new InMemoryTransactionRepository();
        var emulator = new PraxisEmulator(_merchants, _transactions, new RecordingCallbackDelivery(), new FakeTimeProvider(),
            NullLogger<PraxisEmulator>.Instance);
        _controller = new EmulatorsController(_merchants, _transactions, emulator, NullLogger<EmulatorsController>.Instance);
    }

    private static PraxisMerchantRequest Valid(string merchantId = "m-1", string environment = "qa2") => new()
    {
        Environment = environment,
        MerchantId = merchantId,
        ApplicationKey = "app",
        Secret = "0123456789",
        Gateways = [new PraxisGateway { Id = "gw", Kind = "card" }]
    };

    private static T Value<T>(ActionResult<T> result) where T : class =>
        (result.Result as ObjectResult)?.Value as T ?? throw new AssertionException($"Unexpected result {result.Result}");

    [Test]
    public async Task Create_Valid_201WithSecretLengthAndDefaults()
    {
        var result = await _controller.CreateMerchant(Valid());

        Assert.That(result.Result, Is.InstanceOf<CreatedAtActionResult>());
        var view = Value(result);
        Assert.Multiple(() =>
        {
            Assert.That(view.Id, Is.Not.Null);
            Assert.That(view.SecretLength, Is.EqualTo(10));
            Assert.That(view.TimeScale, Is.EqualTo(1.0));
            Assert.That(view.DropNotifications, Is.False);
            Assert.That(JsonSerializer.Serialize(view), Does.Not.Contain("0123456789"));
        });
    }

    private static IEnumerable<TestCaseData> InvalidRequests()
    {
        yield return new TestCaseData(null, "Request body is required.");
        yield return new TestCaseData(new PraxisMerchantRequest(), "environment is required.");
        PraxisMerchantRequest With(Action<PraxisMerchantRequest> change)
        {
            var request = Valid();
            change(request);
            return request;
        }
        yield return new TestCaseData(With(r => r.MerchantId = " "), "merchantId is required.");
        yield return new TestCaseData(With(r => r.ApplicationKey = null), "applicationKey is required.");
        yield return new TestCaseData(With(r => r.Secret = ""), "secret is required.");
        yield return new TestCaseData(With(r => r.Secret = new string('s', 33)), "secret must be at most 32 characters.");
        yield return new TestCaseData(With(r => r.Gateways = []), "gateways must contain at least one gateway.");
        yield return new TestCaseData(With(r => r.Gateways = null), "gateways must contain at least one gateway.");
        yield return new TestCaseData(With(r => r.Gateways = [new PraxisGateway { Id = "", Kind = "card" }]), "gateways[].id is required.");
        yield return new TestCaseData(With(r => r.Gateways = [new PraxisGateway { Id = "g", Kind = "bank" }]), "gateways[].kind must be 'card' or 'ewallet'.");
        yield return new TestCaseData(With(r => r.TimeScale = -0.1), "timeScale must be a number >= 0.");
        yield return new TestCaseData(With(r => r.TimeScale = double.NaN), "timeScale must be a number >= 0.");
        yield return new TestCaseData(With(r => r.TimeScale = double.PositiveInfinity), "timeScale must be a number >= 0.");
    }

    [TestCaseSource(nameof(InvalidRequests))]
    public async Task Create_Invalid_400(PraxisMerchantRequest? request, string message)
    {
        var result = await _controller.CreateMerchant(request);

        Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        var value = ((BadRequestObjectResult)result.Result!).Value!;
        Assert.That(value.GetType().GetProperty("message")!.GetValue(value), Is.EqualTo(message));
        Assert.That(await _merchants.ListAsync(null), Is.Empty);
    }

    [Test]
    public async Task Create_Duplicate_409_ButOtherEnvironmentIsFine()
    {
        await _controller.CreateMerchant(Valid());

        Assert.Multiple(async () =>
        {
            Assert.That((await _controller.CreateMerchant(Valid())).Result, Is.InstanceOf<ConflictObjectResult>());
            Assert.That((await _controller.CreateMerchant(Valid(environment: "st"))).Result, Is.InstanceOf<CreatedAtActionResult>());
        });
    }

    [Test]
    public async Task Update_WithoutSecret_KeepsStoredSecret()
    {
        var created = Value(await _controller.CreateMerchant(Valid()));
        var update = Valid();
        update.Secret = null;
        update.TimeScale = 0;
        update.DropNotifications = true;
        update.Gateways = [new PraxisGateway { Id = "gw2", Kind = "ewallet", PaymentProcessor = "Test E-Wallet", PaymentMethod = "altbankwire" }];

        var result = await _controller.UpdateMerchant(created.Id!, update);

        var stored = await _merchants.GetByIdAsync(created.Id!);
        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            Assert.That(stored!.Secret, Is.EqualTo("0123456789"));
            Assert.That(stored.TimeScale, Is.EqualTo(0));
            Assert.That(stored.DropNotifications, Is.True);
            Assert.That(stored.Gateways.Single().Kind, Is.EqualTo("ewallet"));
        });
    }

    [Test]
    public async Task Update_UnknownInvalidOrDuplicate()
    {
        var first = Value(await _controller.CreateMerchant(Valid("m-1")));
        await _controller.CreateMerchant(Valid("m-2"));
        var invalid = Valid();
        invalid.Gateways = [];

        Assert.Multiple(async () =>
        {
            Assert.That((await _controller.UpdateMerchant("nope", Valid())).Result, Is.InstanceOf<NotFoundObjectResult>());
            Assert.That((await _controller.UpdateMerchant(first.Id!, invalid)).Result, Is.InstanceOf<BadRequestObjectResult>());
            Assert.That((await _controller.UpdateMerchant(first.Id!, Valid("m-2"))).Result, Is.InstanceOf<ConflictObjectResult>());
        });
    }

    [Test]
    public async Task DeleteAndList()
    {
        var created = Value(await _controller.CreateMerchant(Valid("m-1", "qa2")));
        await _controller.CreateMerchant(Valid("m-2", "st"));

        var qa2 = (IEnumerable<PraxisMerchantView>)((OkObjectResult)(await _controller.GetMerchants("qa2")).Result!).Value!;
        var all = (IEnumerable<PraxisMerchantView>)((OkObjectResult)(await _controller.GetMerchants()).Result!).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(qa2.Select(m => m.MerchantId), Is.EqualTo(new[] { "m-1" }));
            Assert.That(all.Count(), Is.EqualTo(2));
        });

        Assert.Multiple(async () =>
        {
            Assert.That(await _controller.DeleteMerchant(created.Id!), Is.InstanceOf<NoContentResult>());
            Assert.That(await _controller.DeleteMerchant(created.Id!), Is.InstanceOf<NotFoundObjectResult>());
        });
    }

    [Test]
    public async Task GetTransactions_FiltersByEnvironmentOrderAndTid()
    {
        var now = new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc);
        foreach (var (tid, env, order) in new[] { (1L, "qa2", "o-1"), (2L, "qa2", "o-2"), (3L, "st", "o-1") })
        {
            var tx = EmulatedTransaction.Open("praxis", env, "m", "sale", "USD", 100, now.AddSeconds(tid));
            tx.Tid = tid;
            tx.OrderId = order;
            await _transactions.InsertAsync(tx);
        }

        IEnumerable<EmulatedTransaction> Get(ActionResult<IEnumerable<EmulatedTransaction>> r) =>
            (IEnumerable<EmulatedTransaction>)((OkObjectResult)r.Result!).Value!;

        Assert.Multiple(async () =>
        {
            Assert.That(Get(await _controller.GetTransactions("qa2")).Select(t => t.Tid), Is.EqualTo(new[] { 2L, 1L }));
            Assert.That(Get(await _controller.GetTransactions(orderId: "o-1")).Select(t => t.Tid), Is.EqualTo(new[] { 3L, 1L }));
            Assert.That(Get(await _controller.GetTransactions(tid: 3)).Select(t => t.Tid), Is.EqualTo(new[] { 3L }));
        });
    }

    [Test]
    public async Task GetTransactions_AppliesDueDeadlinesBeforeReading()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var controller = new EmulatorsController(harness.Merchants, harness.Transactions, harness.Emulator, NullLogger<EmulatorsController>.Instance);
        var tid = PraxisEmulatorHarness.Tid(await harness.SaleAsync("333", 4900));
        harness.Time.Advance(TimeSpan.FromSeconds(540));

        var listed = (IEnumerable<EmulatedTransaction>)((OkObjectResult)(await controller.GetTransactions(tid: tid)).Result!).Value!;

        Assert.Multiple(() =>
        {
            Assert.That(listed.Single().Status, Is.EqualTo(TransactionStatus.Rejected));
            Assert.That(listed.Single().StatusDetails, Is.EqualTo("Transaction timed out. Old status pending_async. New status declined"));
            Assert.That(harness.Delivery.Jobs, Has.Count.EqualTo(1));
        });
    }

    private sealed class FailingStore : IPraxisMerchantRepository, ITransactionRepository
    {
        private static Exception Boom => new InvalidOperationException("boom");
        public Task<IReadOnlyList<PraxisMerchant>> ListAsync(string? environment) => throw Boom;
        public Task<PraxisMerchant?> GetByIdAsync(string id) => throw Boom;
        public Task<PraxisMerchant?> FindAsync(string environment, string merchantId) => throw Boom;
        public Task<bool> AnyInEnvironmentAsync(string environment) => throw Boom;
        public Task<PraxisMerchant> CreateAsync(PraxisMerchant merchant) => throw Boom;
        public Task<bool> UpdateAsync(PraxisMerchant merchant) => throw Boom;
        public Task<bool> DeleteAsync(string id) => throw Boom;
        public Task<long> NextTidAsync() => throw Boom;
        public Task InsertAsync(EmulatedTransaction transaction) => throw Boom;
        public Task<bool> TrySaveAsync(EmulatedTransaction transaction) => throw Boom;
        public Task<EmulatedTransaction?> GetAsync(long tid) => throw Boom;
        public Task<IReadOnlyList<EmulatedTransaction>> QueryAsync(string? environment, string? orderId, long? tid, int limit = 1000) => throw Boom;
        public Task<IReadOnlyList<EmulatedTransaction>> GetDueAsync(DateTime now, int limit = 100) => throw Boom;
        public Task<EmulatedTransaction?> FindByCardTokenAsync(string environment, string merchantId, string cardToken) => throw Boom;
        public Task<EmulatedTransaction?> FindByWalletTokenAsync(string environment, string merchantId, string walletToken) => throw Boom;
    }

    [Test]
    public async Task StoreFailures_500()
    {
        var store = new FailingStore();
        // The deadline service swallows store failures; the controller does not.
        var emulator = new PraxisEmulator(store, store, new RecordingCallbackDelivery(), new FakeTimeProvider(), NullLogger<PraxisEmulator>.Instance);
        var controller = new EmulatorsController(store, store, emulator, NullLogger<EmulatorsController>.Instance);

        static int? Status(IActionResult? result) => (result as ObjectResult)?.StatusCode;
        Assert.Multiple(async () =>
        {
            Assert.That(Status((await controller.GetMerchants()).Result), Is.EqualTo(500));
            Assert.That(Status((await controller.CreateMerchant(Valid())).Result), Is.EqualTo(500));
            Assert.That(Status((await controller.UpdateMerchant("x", Valid())).Result), Is.EqualTo(500));
            Assert.That(Status(await controller.DeleteMerchant("x")), Is.EqualTo(500));
            Assert.That(Status((await controller.GetTransactions()).Result), Is.EqualTo(500));
        });

        var service = new PraxisDeadlineService(emulator, new FakeTimeProvider(), NullLogger<PraxisDeadlineService>.Instance);
        Assert.That(await service.RunOnceAsync(), Is.EqualTo(0));
    }
}

[TestFixture]
public class PraxisDeadlineServiceTests
{
    [Test]
    public async Task Service_AppliesDueDeadlinesEverySecondUntilStopped()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 1);
        var tid = PraxisEmulatorHarness.Tid(await harness.SaleAsync("333", 4900));
        var service = new PraxisDeadlineService(harness.Emulator, harness.Time, NullLogger<PraxisDeadlineService>.Instance);

        await service.StartAsync(CancellationToken.None);
        harness.Time.Advance(TimeSpan.FromSeconds(540));
        harness.Time.Advance(PraxisDeadlineService.Interval);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (harness.Delivery.Jobs.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
        await service.StopAsync(CancellationToken.None);

        var stored = await harness.Transactions.GetAsync(tid);
        Assert.Multiple(() =>
        {
            Assert.That(stored!.Status, Is.EqualTo(TransactionStatus.Rejected));
            Assert.That(harness.Delivery.Jobs, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task RunOnce_Cancelled_ReturnsZero()
    {
        var harness = new PraxisEmulatorHarness(timeScale: 0);
        await harness.SaleAsync("333", 4900);
        var service = new PraxisDeadlineService(harness.Emulator, harness.Time, NullLogger<PraxisDeadlineService>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(await service.RunOnceAsync(cts.Token), Is.EqualTo(0));
    }
}
