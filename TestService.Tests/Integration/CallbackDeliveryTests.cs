using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TestService.Api.BackgroundServices;
using TestService.Api.Configuration;
using TestService.Api.Models;
using TestService.Api.Services;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration;

/// <summary>
/// End-to-end proof of outbound callback delivery: jobs are enqueued through
/// <see cref="ICallbackDelivery"/>, sent to this test host's own <c>/mock/*</c> route
/// (through the in-memory TestServer handler) and read back through <c>/api/mocks/callbacks</c>.
/// </summary>
[TestFixture]
public class CallbackDeliveryTests : IntegrationTestBase
{
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(10);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<MockingSettings>(o => o.AllowedCallbackHosts = ["localhost"]);
            // Deferred: Factory.Server does not exist yet while the host is being configured.
            services.AddHttpClient(CallbackDispatcher.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => Factory.Server.CreateHandler());
        });
    }

    private ICallbackDelivery Delivery => Factory.Services.GetRequiredService<ICallbackDelivery>();

    private static CallbackJob Job(string environment, string correlationId, string url, string source = "praxis", TimeSpan? dueIn = null) => new()
    {
        Source = source,
        Environment = environment,
        CorrelationId = correlationId,
        Url = url,
        Method = "POST",
        Headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["GT-Authentication"] = "sig-" + correlationId
        },
        Body = "{\"transaction\":{\"tid\":\"" + correlationId + "\"}}",
        DueAt = DateTimeOffset.UtcNow + (dueIn ?? TimeSpan.Zero)
    };

    private async Task<List<MockCallbackLog>> GetCallbacksAsync(string query)
    {
        var response = await Client.GetAsync($"/api/mocks/callbacks?{query}");
        AssertStatusCode(response, HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<MockCallbackLog>>())!;
    }

    private async Task<List<MockCallbackLog>> PollCallbacksAsync(string query, int expectedCount)
    {
        var deadline = DateTime.UtcNow + PollTimeout;
        while (true)
        {
            var logs = await GetCallbacksAsync(query);
            if (logs.Count >= expectedCount)
            {
                return logs;
            }
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"Expected {expectedCount} callback log(s) for '{query}' within {PollTimeout}, got {logs.Count}.");
            }
            await Task.Delay(100);
        }
    }

    [Test]
    public async Task Enqueue_AllowedHost_SendsAfterDueTimeAndLogsTargetReply()
    {
        var environment = $"cb-{Guid.NewGuid():N}";
        var correlationId = Guid.NewGuid().ToString("N");
        var job = Job(environment, correlationId, $"http://localhost/mock/{environment}/notify",
            dueIn: TimeSpan.FromMilliseconds(200));

        await Delivery.EnqueueAsync(job);

        var logs = await PollCallbacksAsync($"environment={environment}&correlationId={correlationId}", 1);
        var log = logs.Single();
        Assert.Multiple(() =>
        {
            Assert.That(log.Source, Is.EqualTo("praxis"));
            Assert.That(log.Url, Is.EqualTo(job.Url));
            Assert.That(log.Method, Is.EqualTo("POST"));
            Assert.That(log.RequestBody, Is.EqualTo(job.Body));
            Assert.That(log.RequestHeaders["GT-Authentication"], Is.EqualTo("sig-" + correlationId));
            Assert.That(log.Error, Is.Null);
            Assert.That(log.ResponseStatusCode, Is.EqualTo(404));
            Assert.That(log.ResponseBody, Is.EqualTo("{\"message\":\"No mock matched request\"}"));
            Assert.That(log.ResponseHeaders.Keys, Has.Some.EqualTo("Content-Type").IgnoreCase);
            Assert.That(log.SentAt, Is.GreaterThanOrEqualTo(log.DueAt));
            Assert.That(log.DueAt, Is.EqualTo(job.DueAt.UtcDateTime).Within(TimeSpan.FromMilliseconds(1)));
        });

        // The target (the /mock middleware) received the request unchanged.
        var requests = await Client.GetFromJsonAsync<List<MockRequestLog>>($"/api/mocks/requests?environment={environment}");
        Assert.That(requests, Has.Count.EqualTo(1));
        var received = requests!.Single();
        Assert.Multiple(() =>
        {
            Assert.That(received.Method, Is.EqualTo("POST"));
            Assert.That(received.Path, Is.EqualTo("/notify"));
            Assert.That(received.Body, Is.EqualTo(job.Body));
            Assert.That(received.Headers["GT-Authentication"], Is.EqualTo("sig-" + correlationId));
            Assert.That(received.Headers["Content-Type"], Is.EqualTo("application/json"));
        });
    }

    [Test]
    public async Task Enqueue_HostNotAllowed_NothingSentAndRefusalLogged()
    {
        var environment = $"cb-{Guid.NewGuid():N}";
        var correlationId = Guid.NewGuid().ToString("N");

        await Delivery.EnqueueAsync(Job(environment, correlationId, $"http://merchant.example.com/mock/{environment}/notify"));

        var log = (await PollCallbacksAsync($"environment={environment}&correlationId={correlationId}", 1)).Single();
        Assert.That(log.Error, Is.EqualTo("host not allowed"));
        Assert.That(log.ResponseStatusCode, Is.Null);

        var requests = await Client.GetFromJsonAsync<List<MockRequestLog>>($"/api/mocks/requests?environment={environment}");
        Assert.That(requests, Is.Empty);
    }

    [Test]
    public async Task GetCallbacks_FiltersBySourceAndCorrelationId_LimitsAndSortsNewestFirst()
    {
        var environment = $"cb-{Guid.NewGuid():N}";
        // Refused jobs are logged without any network I/O, which keeps this test fast.
        const string blocked = "http://blocked.example/notify";
        await Delivery.EnqueueAsync(Job(environment, "a", blocked, source: "praxis"));
        await PollCallbacksAsync($"environment={environment}", 1);
        await Task.Delay(20); // distinct sentAt values (Mongo stores milliseconds)
        await Delivery.EnqueueAsync(Job(environment, "b", blocked, source: "praxis"));
        await PollCallbacksAsync($"environment={environment}", 2);
        await Task.Delay(20);
        await Delivery.EnqueueAsync(Job(environment, "c", blocked, source: "other"));

        var all = await PollCallbacksAsync($"environment={environment}", 3);
        Assert.That(all, Has.Count.EqualTo(3));
        Assert.That(all.Select(l => l.SentAt), Is.Ordered.Descending);

        var praxis = await GetCallbacksAsync($"environment={environment}&source=praxis");
        Assert.That(praxis.Select(l => l.CorrelationId), Is.EqualTo(new[] { "b", "a" }));

        var byCorrelation = await GetCallbacksAsync($"environment={environment}&correlationId=c");
        Assert.That(byCorrelation.Single().Source, Is.EqualTo("other"));

        var limited = await GetCallbacksAsync($"environment={environment}&limit=1");
        Assert.That(limited, Has.Count.EqualTo(1));
        Assert.That(limited.Single().CorrelationId, Is.EqualTo("c"));
    }

    [Test]
    public async Task DeleteCallbacks_ByEnvironment_RemovesOnlyThatEnvironment()
    {
        var e = $"cb-e-{Guid.NewGuid():N}";
        var f = $"cb-f-{Guid.NewGuid():N}";
        await Delivery.EnqueueAsync(Job(e, "e1", "http://blocked.example/notify"));
        await Delivery.EnqueueAsync(Job(e, "e2", "http://blocked.example/notify"));
        await Delivery.EnqueueAsync(Job(f, "f1", "http://blocked.example/notify"));
        await PollCallbacksAsync($"environment={e}", 2);
        await PollCallbacksAsync($"environment={f}", 1);

        var response = await Client.DeleteAsync($"/api/mocks/callbacks?environment={e}");
        AssertStatusCode(response, HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, long>>();
        Assert.That(payload!["deletedCount"], Is.EqualTo(2));

        Assert.That(await GetCallbacksAsync($"environment={e}"), Is.Empty);
        Assert.That(await GetCallbacksAsync($"environment={f}"), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetCallbacks_LimitOutOfRange_IsClamped()
    {
        var environment = $"cb-{Guid.NewGuid():N}";
        await Delivery.EnqueueAsync(Job(environment, "x1", "http://blocked.example/notify"));
        await Delivery.EnqueueAsync(Job(environment, "x2", "http://blocked.example/notify"));
        await PollCallbacksAsync($"environment={environment}", 2);

        var zero = await GetCallbacksAsync($"environment={environment}&limit=0");
        Assert.That(zero, Has.Count.EqualTo(1), "limit=0 is clamped to 1");

        var huge = await GetCallbacksAsync("limit=5000");
        Assert.That(huge, Has.Count.GreaterThanOrEqualTo(2).And.Count.LessThanOrEqualTo(1000));
    }

    [Test]
    public async Task GetCallbacks_LimitAboveMax_ReturnsAtMost1000()
    {
        // Seeded through the repository: the delivery queue itself holds at most 1000 pending jobs.
        var environment = $"cb-max-{Guid.NewGuid():N}";
        var repository = Factory.Services.GetRequiredService<IMockRepository>();
        var now = DateTime.UtcNow;
        // Chunked so the driver's connection wait queue is not overrun.
        foreach (var chunk in Enumerable.Range(0, 1001).Chunk(50))
        {
            await Task.WhenAll(chunk.Select(i => repository.CreateCallbackLogAsync(new MockCallbackLog
            {
                Source = "praxis",
                Environment = environment,
                CorrelationId = i.ToString(),
                Url = "http://blocked.example/notify",
                Error = CallbackDispatcher.HostNotAllowed,
                DueAt = now,
                SentAt = now
            })));
        }

        var logs = await GetCallbacksAsync($"environment={environment}&limit=5000");
        Assert.That(logs, Has.Count.EqualTo(1000));
    }

    [Test]
    public async Task DeleteCallbacks_WithoutEnvironment_DeletesAll()
    {
        var e = $"cb-e-{Guid.NewGuid():N}";
        var f = $"cb-f-{Guid.NewGuid():N}";
        await Delivery.EnqueueAsync(Job(e, "e1", "http://blocked.example/notify"));
        await Delivery.EnqueueAsync(Job(f, "f1", "http://blocked.example/notify"));
        await PollCallbacksAsync($"environment={e}", 1);
        await PollCallbacksAsync($"environment={f}", 1);

        var response = await Client.DeleteAsync("/api/mocks/callbacks");
        AssertStatusCode(response, HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, long>>();
        Assert.That(payload!["deletedCount"], Is.GreaterThanOrEqualTo(2));

        Assert.That(await GetCallbacksAsync($"environment={e}"), Is.Empty);
        Assert.That(await GetCallbacksAsync($"environment={f}"), Is.Empty);
    }

    [Test]
    public async Task CallbackEndpoints_WithoutToken_ReturnUnauthorized()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var get = await Client.GetAsync("/api/mocks/callbacks?environment=e&correlationId=42");
        var delete = await Client.DeleteAsync("/api/mocks/callbacks?environment=e");

        AssertStatusCode(get, HttpStatusCode.Unauthorized);
        AssertStatusCode(delete, HttpStatusCode.Unauthorized);
    }

    [Test]
    public void MockCallbacksHttpClient_HasThirtySecondTimeout()
    {
        var factory = Factory.Services.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient(CallbackDispatcher.HttpClientName);

        Assert.That(client.Timeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
    }
}
