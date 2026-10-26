using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using TestService.Api.BackgroundServices;

namespace TestService.Unit;

[TestFixture]
public class CallbackDispatcherTests
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    private FakeTimeProvider _time = null!;
    private IMockRepository _repository = null!;
    private ConcurrentQueue<MockCallbackLog> _logs = null!;
    private StubHandler _handler = null!;
    private IHttpClientFactory _httpClientFactory = null!;
    private MockingSettings _settings = null!;
    private TimeSpan _clientTimeout;
    private CallbackQueue _queue = null!;
    private CallbackDispatcher _dispatcher = null!;
    private bool _started;

    [SetUp]
    public void SetUp()
    {
        _time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero));
        _logs = new ConcurrentQueue<MockCallbackLog>();
        _repository = Substitute.For<IMockRepository>();
        _repository.CreateCallbackLogAsync(Arg.Do<MockCallbackLog>(l => _logs.Enqueue(l))).Returns(Task.CompletedTask);

        _handler = new StubHandler(_time);
        _clientTimeout = TimeSpan.FromSeconds(30);
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _httpClientFactory.CreateClient(CallbackDispatcher.HttpClientName)
            .Returns(_ => new HttpClient(_handler, disposeHandler: false) { Timeout = _clientTimeout });

        _settings = new MockingSettings { AllowedCallbackHosts = ["localhost"] };

        _queue = new CallbackQueue(_repository, _time, NullLogger<CallbackQueue>.Instance);
        _dispatcher = new CallbackDispatcher(_queue, _httpClientFactory, Options.Create(_settings), _time,
            NullLogger<CallbackDispatcher>.Instance);
        _started = false;
    }

    [TearDown]
    public async Task TearDown()
    {
        _handler.Release();
        if (_started)
        {
            await _dispatcher.StopAsync(CancellationToken.None);
        }
        _dispatcher.Dispose();
        _handler.Dispose();
    }

    private async Task StartAsync()
    {
        await _dispatcher.StartAsync(CancellationToken.None);
        _started = true;
    }

    private CallbackJob Job(string url = "http://localhost/mock/env/notify", TimeSpan? dueIn = null, string correlationId = "tx-1") => new()
    {
        Source = "praxis",
        Environment = "env",
        CorrelationId = correlationId,
        Url = url,
        Method = "POST",
        Headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["GT-Authentication"] = "abc123"
        },
        Body = "{\"status\":0}",
        DueAt = _time.GetUtcNow() + (dueIn ?? TimeSpan.Zero)
    };

    private async Task<List<MockCallbackLog>> WaitForLogsAsync(int count)
    {
        var deadline = DateTime.UtcNow + WaitTimeout;
        while (_logs.Count < count)
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"Expected {count} callback log(s) but got {_logs.Count}.");
            }
            await Task.Delay(10);
        }
        return _logs.ToList();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string because)
    {
        var deadline = DateTime.UtcNow + WaitTimeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"Timed out waiting: {because}");
            }
            await Task.Delay(10);
        }
    }

    // ── Sent ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Enqueue_AllowedHostDueLater_SendsAtDueTimeUnchangedAndLogsResponse()
    {
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json"),
            Headers = { { "X-Reply", "yes" } }
        };
        await StartAsync();

        var job = Job(dueIn: TimeSpan.FromMilliseconds(200));
        await _queue.EnqueueAsync(job);

        await Task.Delay(150);
        Assert.That(_handler.Requests, Is.Empty, "must not be sent before its due time");

        _time.Advance(TimeSpan.FromMilliseconds(200));
        var logs = await WaitForLogsAsync(1);

        Assert.That(_handler.Requests, Has.Count.EqualTo(1));
        var request = _handler.Requests.Single();
        Assert.Multiple(() =>
        {
            Assert.That(request.Url, Is.EqualTo(job.Url));
            Assert.That(request.Method, Is.EqualTo("POST"));
            Assert.That(request.Body, Is.EqualTo(job.Body));
            Assert.That(request.Headers["Content-Type"], Is.EqualTo("application/json"));
            Assert.That(request.Headers["GT-Authentication"], Is.EqualTo("abc123"));
            Assert.That(request.At, Is.GreaterThanOrEqualTo(job.DueAt));
        });

        var log = logs.Single();
        Assert.Multiple(() =>
        {
            Assert.That(log.Source, Is.EqualTo("praxis"));
            Assert.That(log.Environment, Is.EqualTo("env"));
            Assert.That(log.CorrelationId, Is.EqualTo("tx-1"));
            Assert.That(log.Url, Is.EqualTo(job.Url));
            Assert.That(log.Method, Is.EqualTo("POST"));
            Assert.That(log.RequestHeaders["GT-Authentication"], Is.EqualTo("abc123"));
            Assert.That(log.RequestBody, Is.EqualTo(job.Body));
            Assert.That(log.ResponseStatusCode, Is.EqualTo(202));
            Assert.That(log.ResponseBody, Is.EqualTo("{\"ok\":true}"));
            Assert.That(log.ResponseHeaders["X-Reply"], Is.EqualTo("yes"));
            Assert.That(log.ResponseHeaders["Content-Type"], Does.StartWith("application/json"));
            Assert.That(log.Error, Is.Null);
            Assert.That(log.DueAt, Is.EqualTo(job.DueAt.UtcDateTime));
            Assert.That(log.SentAt, Is.GreaterThanOrEqualTo(log.DueAt));
            Assert.That(log.SentAt.Kind, Is.EqualTo(DateTimeKind.Utc));
        });
        Assert.That(_queue.PendingCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Enqueue_BodyWithoutContentType_SendsNoContentType()
    {
        await StartAsync();
        var job = Job();
        job.Headers.Remove("Content-Type");

        await _queue.EnqueueAsync(job);
        await WaitForLogsAsync(1);

        Assert.That(_handler.Requests.Single().Headers.ContainsKey("Content-Type"), Is.False);
        Assert.That(_handler.Requests.Single().Body, Is.EqualTo(job.Body));
    }

    // ── Ordering ────────────────────────────────────────────────────────────

    [Test]
    public async Task Enqueue_LaterJobFirst_JobDueNowIsSentFirst()
    {
        await StartAsync();

        await _queue.EnqueueAsync(Job(dueIn: TimeSpan.FromSeconds(5), correlationId: "A"));
        await _queue.EnqueueAsync(Job(correlationId: "B"));

        var first = await WaitForLogsAsync(1);
        Assert.That(first.Single().CorrelationId, Is.EqualTo("B"));
        await Task.Delay(100);
        Assert.That(_handler.Requests, Has.Count.EqualTo(1));

        _time.Advance(TimeSpan.FromSeconds(5));
        var all = await WaitForLogsAsync(2);
        Assert.That(all.Select(l => l.CorrelationId), Is.EqualTo(new[] { "B", "A" }));
        Assert.That(all[1].SentAt, Is.GreaterThanOrEqualTo(all[1].DueAt));
    }

    // ── Host allow-list ─────────────────────────────────────────────────────

    [Test]
    public async Task Enqueue_HostNotAllowed_NothingSentAndRefusalLogged()
    {
        await StartAsync();

        await _queue.EnqueueAsync(Job(url: "http://merchant.example.com/notify"));
        var log = (await WaitForLogsAsync(1)).Single();

        Assert.That(_handler.Requests, Is.Empty);
        Assert.That(log.Error, Is.EqualTo(CallbackDispatcher.HostNotAllowed));
        Assert.That(log.ResponseStatusCode, Is.Null);
        Assert.That(log.SentAt, Is.Not.EqualTo(default(DateTime)));
    }

    [Test]
    public async Task Enqueue_EmptyAllowList_NothingSent()
    {
        _settings.AllowedCallbackHosts.Clear();
        await StartAsync();

        await _queue.EnqueueAsync(Job());
        var log = (await WaitForLogsAsync(1)).Single();

        Assert.That(_handler.Requests, Is.Empty);
        Assert.That(log.Error, Is.EqualTo(CallbackDispatcher.HostNotAllowed));
    }

    [Test]
    public async Task Enqueue_HostDiffersOnlyInCase_IsAllowed()
    {
        _settings.AllowedCallbackHosts = ["LocalHost"];
        await StartAsync();

        await _queue.EnqueueAsync(Job(url: "http://LOCALHOST:5000/mock/env/notify"));
        var log = (await WaitForLogsAsync(1)).Single();

        Assert.That(_handler.Requests, Has.Count.EqualTo(1));
        Assert.That(log.Error, Is.Null);
        Assert.That(log.ResponseStatusCode, Is.EqualTo(200));
    }

    [Test]
    public async Task Enqueue_SubdomainOfAllowedHost_IsNotAllowed()
    {
        await StartAsync();

        await _queue.EnqueueAsync(Job(url: "http://localhost.evil.example/notify"));
        var log = (await WaitForLogsAsync(1)).Single();

        Assert.That(_handler.Requests, Is.Empty);
        Assert.That(log.Error, Is.EqualTo(CallbackDispatcher.HostNotAllowed));
    }

    [Test]
    public async Task Enqueue_InvalidUrl_NothingSentAndErrorLogged()
    {
        await StartAsync();

        await _queue.EnqueueAsync(Job(url: "not a url"));
        var log = (await WaitForLogsAsync(1)).Single();

        Assert.That(_handler.Requests, Is.Empty);
        Assert.That(log.Error, Is.EqualTo(CallbackDispatcher.InvalidUrl));
    }

    // ── Target down ─────────────────────────────────────────────────────────

    [Test]
    public async Task Enqueue_ConnectionRefused_LogsErrorWithoutStatusCode()
    {
        _handler.Respond = _ => throw new HttpRequestException("No connection could be made because the target machine actively refused it.");
        await StartAsync();

        await _queue.EnqueueAsync(Job());
        var log = (await WaitForLogsAsync(1)).Single();

        Assert.That(log.Error, Does.Contain("actively refused"));
        Assert.That(log.ResponseStatusCode, Is.Null);
        Assert.That(_queue.PendingCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Enqueue_TargetTimesOut_LogsTimeoutWithoutStatusCode()
    {
        _clientTimeout = TimeSpan.FromMilliseconds(100);
        _handler.Hang = true;
        await StartAsync();

        await _queue.EnqueueAsync(Job());
        var log = (await WaitForLogsAsync(1)).Single();

        Assert.That(log.Error, Does.Contain("Timeout"));
        Assert.That(log.ResponseStatusCode, Is.Null);
    }

    [Test]
    public async Task Enqueue_TargetStallsBodyAfterHeaders_LogsTimeoutWithoutStatusCode()
    {
        _clientTimeout = TimeSpan.FromMilliseconds(200);
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StalledStream()) };
        await StartAsync();

        await _queue.EnqueueAsync(Job());
        var log = (await WaitForLogsAsync(1)).Single();

        Assert.That(log.Error, Does.Contain("Timeout"));
        Assert.That(log.ResponseStatusCode, Is.Null);
        Assert.That(_queue.PendingCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Enqueue_BodyReadFailsAfterHeaders_LogsErrorWithoutStatusCode()
    {
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new FailingStream()) };
        await StartAsync();

        await _queue.EnqueueAsync(Job());
        var log = (await WaitForLogsAsync(1)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(log.Error, Does.Contain("connection reset while reading the body"));
            Assert.That(log.ResponseStatusCode, Is.Null);
            Assert.That(_queue.PendingCount, Is.EqualTo(0));
        });
    }

    // ── Duration ────────────────────────────────────────────────────────────

    [Test]
    public async Task Enqueue_Sent_LogsTheDurationOfTheExchange()
    {
        _handler.Elapse = TimeSpan.FromMilliseconds(250);
        await StartAsync();

        await _queue.EnqueueAsync(Job());
        var log = (await WaitForLogsAsync(1)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(log.ResponseStatusCode, Is.EqualTo(200));
            Assert.That(log.DurationMs, Is.EqualTo(250));
        });
    }

    // ── Response body truncation ────────────────────────────────────────────

    [Test]
    public async Task Enqueue_LargeResponseBody_LogsFirst64KB()
    {
        var big = new string('x', 100 * 1024);
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(big) };
        await StartAsync();

        await _queue.EnqueueAsync(Job());
        var log = (await WaitForLogsAsync(1)).Single();

        Assert.That(log.ResponseBody, Has.Length.EqualTo(CallbackDispatcher.MaxResponseBodyBytes));
        Assert.That(log.ResponseBody, Is.EqualTo(big[..CallbackDispatcher.MaxResponseBodyBytes]));
    }

    // ── Queue full ──────────────────────────────────────────────────────────

    [Test]
    public async Task Enqueue_1001stPendingJob_IsDroppedEvenWhenJobsAreInTheScheduler()
    {
        await StartAsync();

        for (var i = 0; i < CallbackQueue.Capacity; i++)
        {
            await _queue.EnqueueAsync(Job(dueIn: TimeSpan.FromHours(1), correlationId: $"job-{i}"));
        }
        // The dispatcher drains the channel into its scheduler; the bound must still hold.
        await WaitUntilAsync(() => _queue.Reader.Count == 0, "dispatcher drains the channel");

        await _queue.EnqueueAsync(Job(dueIn: TimeSpan.FromHours(1), correlationId: "overflow"));

        var log = (await WaitForLogsAsync(1)).Single();
        Assert.That(log.CorrelationId, Is.EqualTo("overflow"));
        Assert.That(log.Error, Is.EqualTo(CallbackQueue.DroppedQueueFull));
        Assert.That(_queue.PendingCount, Is.EqualTo(CallbackQueue.Capacity));
        Assert.That(_handler.Requests, Is.Empty);
    }

    [Test]
    public async Task Enqueue_QueueFullWithoutDispatcher_DropsAndKeepsCounterBounded()
    {
        for (var i = 0; i < CallbackQueue.Capacity + 2; i++)
        {
            await _queue.EnqueueAsync(Job());
        }

        var logs = _logs.ToList();
        Assert.That(logs, Has.Count.EqualTo(2));
        Assert.That(logs.Select(l => l.Error), Is.All.EqualTo(CallbackQueue.DroppedQueueFull));
        Assert.That(_queue.PendingCount, Is.EqualTo(CallbackQueue.Capacity));
    }

    // ── Shutdown ────────────────────────────────────────────────────────────

    [Test]
    public async Task Stop_WithPendingJobs_LogsEachLostAndRejectsLaterEnqueues()
    {
        await StartAsync();

        await _queue.EnqueueAsync(Job(dueIn: TimeSpan.FromMinutes(5), correlationId: "p1"));
        await _queue.EnqueueAsync(Job(dueIn: TimeSpan.FromMinutes(6), correlationId: "p2"));
        await WaitUntilAsync(() => _queue.Reader.Count == 0, "dispatcher moves jobs to its scheduler");
        await _queue.EnqueueAsync(Job(dueIn: TimeSpan.FromMinutes(7), correlationId: "p3"));

        await _dispatcher.StopAsync(CancellationToken.None);
        _started = false;

        var logs = _logs.ToList();
        Assert.That(logs, Has.Count.EqualTo(3));
        Assert.That(logs.Select(l => l.CorrelationId), Is.EquivalentTo(new[] { "p1", "p2", "p3" }));
        Assert.That(logs.Select(l => l.Error), Is.All.EqualTo(CallbackQueue.LostShutdown));
        Assert.That(logs.Select(l => l.ResponseStatusCode), Is.All.Null);
        Assert.That(_queue.PendingCount, Is.EqualTo(0));
        Assert.That(_handler.Requests, Is.Empty);

        await _queue.EnqueueAsync(Job(correlationId: "late"));
        var late = _logs.Last();
        Assert.That(late.CorrelationId, Is.EqualTo("late"));
        Assert.That(late.Error, Is.EqualTo(CallbackQueue.LostShutdown));
        Assert.That(_queue.PendingCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Stop_WithSendInFlight_CancelsItAndLogsLost()
    {
        _handler.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await StartAsync();

        await _queue.EnqueueAsync(Job());
        await WaitUntilAsync(() => _handler.Requests.Count == 1, "send starts");

        // The gate is never released: stop must cancel the send rather than wait for it.
        var stop = _dispatcher.StopAsync(CancellationToken.None);
        Assert.That(await Task.WhenAny(stop, Task.Delay(WaitTimeout)), Is.SameAs(stop), "stop must not wait for the target");
        await stop;
        _started = false;

        var log = _logs.Single();
        Assert.That(log.Error, Is.EqualTo(CallbackQueue.LostShutdown));
        Assert.That(log.ResponseStatusCode, Is.Null);
        Assert.That(_queue.PendingCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Stop_WhileReadingTheBody_LogsLostAndKeepsTheStatusCode()
    {
        var reading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StreamContent(new StalledStream(reading)) };
        await StartAsync();

        await _queue.EnqueueAsync(Job());
        await reading.Task.WaitAsync(WaitTimeout);

        await _dispatcher.StopAsync(CancellationToken.None);
        _started = false;

        var log = _logs.Single();
        Assert.Multiple(() =>
        {
            Assert.That(log.Error, Is.EqualTo(CallbackQueue.LostShutdown));
            Assert.That(log.ResponseStatusCode, Is.EqualTo(202), "the reply headers had arrived");
            Assert.That(_queue.PendingCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task Enqueue_FarFutureDueAt_DispatcherKeepsRunning()
    {
        await StartAsync();

        await _queue.EnqueueAsync(Job(dueIn: TimeSpan.FromDays(100), correlationId: "far"));
        await WaitUntilAsync(() => _queue.Reader.Count == 0, "far job scheduled");
        await _queue.EnqueueAsync(Job(correlationId: "now"));

        var sent = (await WaitForLogsAsync(1)).Single();
        Assert.That(sent.CorrelationId, Is.EqualTo("now"));
        Assert.That(sent.ResponseStatusCode, Is.EqualTo(200));
        Assert.That(_dispatcher.ExecuteTask!.IsCompleted, Is.False, "dispatcher loop must still be running");

        await _dispatcher.StopAsync(CancellationToken.None);
        _started = false;

        var far = _logs.Single(l => l.CorrelationId == "far");
        Assert.That(far.Error, Is.EqualTo(CallbackQueue.LostShutdown));
        Assert.That(_handler.Requests, Has.Count.EqualTo(1));
    }

    // ── Robustness ──────────────────────────────────────────────────────────

    [Test]
    public async Task LogWriteFailure_DoesNotStopTheDispatcher()
    {
        var calls = 0;
        var written = new ConcurrentQueue<MockCallbackLog>();
        _repository.CreateCallbackLogAsync(Arg.Any<MockCallbackLog>()).Returns(callInfo =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw new InvalidOperationException("mongo down");
            }
            written.Enqueue(callInfo.Arg<MockCallbackLog>());
            return Task.CompletedTask;
        });
        await StartAsync();

        await _queue.EnqueueAsync(Job(correlationId: "first"));
        await WaitUntilAsync(() => Volatile.Read(ref calls) == 1, "first log write attempted");
        await _queue.EnqueueAsync(Job(correlationId: "second"));
        await WaitUntilAsync(() => written.Count == 1, "second log written");

        Assert.That(written.Single().CorrelationId, Is.EqualTo("second"));
        Assert.That(_handler.Requests, Has.Count.EqualTo(2));
        await WaitUntilAsync(() => _queue.PendingCount == 0, "both jobs finished");
    }

    // ── Configuration layering ──────────────────────────────────────────────

    [TestCase("Development", new[] { "localhost" })]
    [TestCase("Docker", new[] { "localhost" })]
    [TestCase("Production", new string[0])]
    public void AppSettings_AllowedCallbackHosts_BindPerEnvironment(string environment, string[] expected)
    {
        var apiDir = FindApiProjectDirectory();
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(apiDir, "appsettings.json"))
            .AddJsonFile(Path.Combine(apiDir, $"appsettings.{environment}.json"))
            .Build();

        var settings = new MockingSettings();
        configuration.GetSection("Mocking").Bind(settings);

        Assert.That(settings.AllowedCallbackHosts, Is.EqualTo(expected));
    }

    [Test]
    public void AppSettings_BaseOnly_AllowsNothing()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(FindApiProjectDirectory(), "appsettings.json"))
            .Build();

        var settings = new MockingSettings();
        configuration.GetSection("Mocking").Bind(settings);

        Assert.That(settings.AllowedCallbackHosts, Is.Empty);
    }

    private static string FindApiProjectDirectory()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "TestService.Api", "appsettings.json");
            if (File.Exists(candidate))
            {
                return Path.GetDirectoryName(candidate)!;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("TestService.Api project directory not found above the test output directory.");
    }

    /// <summary>A response body that never yields data until cancelled; <paramref name="reading"/> is set on the first read.</summary>
    private sealed class StalledStream(TaskCompletionSource? reading = null) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            reading?.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <summary>A response body whose read fails like a connection reset after the headers arrived.</summary>
    private sealed class FailingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("connection reset while reading the body");
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("connection reset while reading the body"));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    private sealed record CapturedRequest(string Url, string Method, Dictionary<string, string> Headers, string? Body, DateTimeOffset At);

    private sealed class StubHandler(FakeTimeProvider time) : HttpMessageHandler
    {
        public ConcurrentQueue<CapturedRequest> Requests { get; } = new();
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        public bool Hang { get; set; }
        public TaskCompletionSource? Gate { get; set; }

        /// <summary>Fake time that passes while the target answers.</summary>
        public TimeSpan Elapse { get; set; }

        public void Release() => Gate?.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in request.Headers)
            {
                headers[header.Key] = string.Join(", ", header.Value);
            }
            string? body = null;
            if (request.Content != null)
            {
                foreach (var header in request.Content.Headers)
                {
                    headers[header.Key] = string.Join(", ", header.Value);
                }
                body = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            Requests.Enqueue(new CapturedRequest(request.RequestUri!.ToString(), request.Method.Method, headers, body, time.GetUtcNow()));

            if (Hang)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            if (Gate != null)
            {
                await Gate.Task.WaitAsync(cancellationToken);
            }
            if (Elapse > TimeSpan.Zero)
            {
                time.Advance(Elapse);
            }
            return Respond(request);
        }
    }
}
