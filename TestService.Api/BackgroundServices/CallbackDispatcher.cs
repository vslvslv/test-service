using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Options;
using TestService.Api.Configuration;
using TestService.Api.Models;
using TestService.Api.Services;

namespace TestService.Api.BackgroundServices;

/// <summary>
/// Sends queued <see cref="CallbackJob"/>s at their due time to allowed hosts and records every
/// attempt in the callback log. No retries; at shutdown, jobs not yet started are logged
/// <c>lost: shutdown</c> and in-flight sends are cancelled and logged the same way.
/// </summary>
public class CallbackDispatcher : BackgroundService
{
    public const string HttpClientName = "MockCallbacks";
    public const int MaxResponseBodyBytes = 64 * 1024;
    public const string HostNotAllowed = "host not allowed";
    public const string InvalidUrl = "invalid url";

    // Task.Delay rejects very long spans; the loop simply re-evaluates after this.
    private static readonly TimeSpan MaxWait = TimeSpan.FromHours(1);

    private readonly CallbackQueue _queue;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<MockingSettings> _settings;
    private readonly TimeProvider _time;
    private readonly ILogger<CallbackDispatcher> _logger;

    // Only touched by the ExecuteAsync loop (and its exit path).
    private readonly PriorityQueue<CallbackJob, (DateTimeOffset DueAt, long Sequence)> _scheduled = new();
    private long _sequence;

    private readonly ConcurrentDictionary<long, Task> _inFlight = new();
    private long _sendId;

    public CallbackDispatcher(
        CallbackQueue queue,
        IHttpClientFactory httpClientFactory,
        IOptions<MockingSettings> settings,
        TimeProvider time,
        ILogger<CallbackDispatcher> logger)
    {
        _queue = queue;
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _time = time;
        _logger = logger;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        // Stop accepting jobs first, so enqueues racing with shutdown are logged as lost.
        _queue.Complete();
        return base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _queue.Reader;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                while (reader.TryRead(out var job))
                {
                    Schedule(job);
                }

                StartDueJobs(stoppingToken);

                if (_scheduled.Count == 0)
                {
                    if (!await reader.WaitToReadAsync(stoppingToken))
                    {
                        break; // writer completed: stopping
                    }
                    continue;
                }

                _scheduled.TryPeek(out _, out var next);
                var delay = next.DueAt - _time.GetUtcNow();
                if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
                if (delay > MaxWait) delay = MaxWait;

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var readTask = reader.WaitToReadAsync(cts.Token).AsTask();
                var delayTask = Task.Delay(delay, _time, cts.Token);
                var completed = await Task.WhenAny(readTask, delayTask);
                cts.Cancel();

                if (completed == readTask && readTask.IsCompletedSuccessfully && !readTask.Result)
                {
                    break; // writer completed: stopping
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal when stopping.
        }

        await ShutdownAsync();
    }

    private void Schedule(CallbackJob job)
    {
        _scheduled.Enqueue(job, (job.DueAt, _sequence++));
    }

    private void StartDueJobs(CancellationToken stoppingToken)
    {
        var now = _time.GetUtcNow();
        while (_scheduled.TryPeek(out var job, out var priority) && priority.DueAt <= now)
        {
            _scheduled.Dequeue();
            var id = Interlocked.Increment(ref _sendId);
            var task = Task.Run(() => SendAsync(job, stoppingToken));
            _inFlight[id] = task;
            _ = task.ContinueWith(_ => _inFlight.TryRemove(id, out Task? _), TaskScheduler.Default);
        }
    }

    private async Task ShutdownAsync()
    {
        _queue.Complete();

        var reader = _queue.Reader;
        while (reader.TryRead(out var job))
        {
            Schedule(job);
        }

        while (_scheduled.TryDequeue(out var job, out _))
        {
            await _queue.RecordAsync(MockCallbackLog.FromJob(job, _time.GetUtcNow(), CallbackQueue.LostShutdown));
            _queue.MarkDone();
        }

        // Already cancelled through the stopping token; each logs itself as lost.
        await Task.WhenAll(_inFlight.Values);
    }

    private async Task SendAsync(CallbackJob job, CancellationToken stoppingToken)
    {
        var log = MockCallbackLog.FromJob(job, _time.GetUtcNow());
        var started = _time.GetTimestamp();
        var timeout = TimeSpan.Zero;
        try
        {
            if (!Uri.TryCreate(job.Url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                log.Error = InvalidUrl;
            }
            else if (!IsHostAllowed(uri.Host))
            {
                log.Error = HostNotAllowed;
            }
            else
            {
                // Clients from IHttpClientFactory need no disposal; the factory owns the handlers.
                var client = _httpClientFactory.CreateClient(HttpClientName);
                timeout = client.Timeout;

                // HttpClient.Timeout stops covering the exchange once headers arrive
                // (ResponseHeadersRead), so the whole send, body included, gets its own deadline,
                // linked to the stopping token so shutdown is never held up by a slow target.
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(timeout);

                using var request = BuildRequest(job, uri);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                log.ResponseStatusCode = (int)response.StatusCode;
                log.ResponseHeaders = ReadHeaders(response);
                log.ResponseBody = await ReadBodyAsync(response.Content, cts.Token);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Keeps the status code if the reply headers had already arrived.
            log.Error = CallbackQueue.LostShutdown;
        }
        catch (OperationCanceledException ex)
        {
            // Timeout (headers or body): logged as an error with no status code.
            log.ResponseStatusCode = null;
            log.Error = $"Timeout: no complete response within {timeout.TotalSeconds:0.###} s";
            _logger.LogWarning(ex, "Callback {Source} {CorrelationId} to {Url} timed out", job.Source, job.CorrelationId, job.Url);
        }
        catch (Exception ex)
        {
            // Connection or I/O error (also while reading the body after the headers arrived): no status code.
            log.ResponseStatusCode = null;
            log.Error = ex.Message;
            _logger.LogWarning(ex, "Callback {Source} {CorrelationId} to {Url} failed", job.Source, job.CorrelationId, job.Url);
        }

        log.DurationMs = (long)_time.GetElapsedTime(started).TotalMilliseconds;
        await _queue.RecordAsync(log);
        _queue.MarkDone();
    }

    private bool IsHostAllowed(string host)
    {
        var allowed = _settings.Value.AllowedCallbackHosts;
        return allowed != null && allowed.Any(h => string.Equals(h?.Trim(), host, StringComparison.OrdinalIgnoreCase));
    }

    private static HttpRequestMessage BuildRequest(CallbackJob job, Uri uri)
    {
        var request = new HttpRequestMessage(new HttpMethod(job.Method), uri);

        // ByteArrayContent (not StringContent) so no Content-Type is added that the caller did not set.
        HttpContent? content = job.Body is null ? null : new ByteArrayContent(Encoding.UTF8.GetBytes(job.Body));

        foreach (var (name, value) in job.Headers ?? new Dictionary<string, string>())
        {
            // Computed from the body by the handler.
            if (string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (request.Headers.TryAddWithoutValidation(name, value))
            {
                continue;
            }

            content ??= new ByteArrayContent([]);
            content.Headers.TryAddWithoutValidation(name, value);
        }

        request.Content = content;
        return request;
    }

    private static Dictionary<string, string> ReadHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            headers[header.Key] = string.Join(", ", header.Value);
        }
        return headers;
    }

    private static async Task<string> ReadBodyAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[MaxResponseBodyBytes];
        var total = 0;
        int read;
        while (total < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken)) > 0)
        {
            total += read;
        }
        return Encoding.UTF8.GetString(buffer, 0, total);
    }
}
