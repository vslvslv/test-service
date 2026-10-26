using TestService.Api.Models;
using TestService.Api.Services;

namespace TestService.Tests.Praxis;

/// <summary>Records enqueued callbacks instead of sending them. Also compiled into TestService.Unit.</summary>
public sealed class RecordingCallbackDelivery : ICallbackDelivery
{
    public List<CallbackJob> Jobs { get; } = new();

    public Task EnqueueAsync(CallbackJob job)
    {
        Jobs.Add(job);
        return Task.CompletedTask;
    }
}

/// <summary>A clock that moves only when told to.</summary>
public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}
