using System.Threading.Channels;
using TestService.Api.Models;

namespace TestService.Api.Services;

/// <summary>
/// Bounded hand-off between callers of <see cref="ICallbackDelivery"/> and the
/// <see cref="BackgroundServices.CallbackDispatcher"/>. "Pending" means enqueued and not yet
/// logged; the pending counter enforces the bound across jobs already moved into the scheduler.
/// </summary>
public class CallbackQueue : ICallbackDelivery
{
    public const int Capacity = 1000;
    public const string DroppedQueueFull = "dropped: queue full";
    public const string LostShutdown = "lost: shutdown";

    private readonly Channel<CallbackJob> _channel;
    private readonly IMockRepository _repository;
    private readonly TimeProvider _time;
    private readonly ILogger<CallbackQueue> _logger;
    private int _pending;
    private volatile bool _completed;

    public CallbackQueue(IMockRepository repository, TimeProvider time, ILogger<CallbackQueue> logger)
    {
        _repository = repository;
        _time = time;
        _logger = logger;
        _channel = Channel.CreateBounded<CallbackJob>(new BoundedChannelOptions(Capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <summary>Jobs enqueued and not yet logged.</summary>
    public int PendingCount => Volatile.Read(ref _pending);

    internal ChannelReader<CallbackJob> Reader => _channel.Reader;

    public async Task EnqueueAsync(CallbackJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (_completed)
        {
            await RecordAsync(MockCallbackLog.FromJob(job, _time.GetUtcNow(), LostShutdown));
            return;
        }

        if (Interlocked.Increment(ref _pending) > Capacity)
        {
            Interlocked.Decrement(ref _pending);
            await RecordAsync(MockCallbackLog.FromJob(job, _time.GetUtcNow(), DroppedQueueFull));
            return;
        }

        if (_channel.Writer.TryWrite(job))
        {
            return;
        }

        Interlocked.Decrement(ref _pending);
        // TryWrite fails either because the writer is completed (host stopping) or, defensively,
        // because the channel is full; the counter normally prevents the latter.
        var error = _completed ? LostShutdown : DroppedQueueFull;
        await RecordAsync(MockCallbackLog.FromJob(job, _time.GetUtcNow(), error));
    }

    /// <summary>Stops accepting jobs; later enqueues are logged <c>lost: shutdown</c>.</summary>
    internal void Complete()
    {
        _completed = true;
        _channel.Writer.TryComplete();
    }

    /// <summary>Marks one pending job as finished (its attempt has been logged).</summary>
    internal void MarkDone() => Interlocked.Decrement(ref _pending);

    /// <summary>Writes one attempt to the callback log; never throws.</summary>
    internal async Task RecordAsync(MockCallbackLog log)
    {
        try
        {
            await _repository.CreateCallbackLogAsync(log);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write callback log for {Source} {CorrelationId} to {Url}",
                log.Source, log.CorrelationId, log.Url);
        }
    }
}
