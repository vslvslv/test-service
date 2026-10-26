using TestService.Api.Models;

namespace TestService.Api.Services;

/// <summary>
/// Schedules a fully built outbound request (for example a provider notification) to be sent
/// at its due time to an allowed host. Every attempt is recorded in the callback log.
/// </summary>
public interface ICallbackDelivery
{
    /// <summary>
    /// Enqueues <paramref name="job"/>. Never throws for a full queue or a stopping host:
    /// those jobs are logged as <c>dropped: queue full</c> or <c>lost: shutdown</c>.
    /// </summary>
    Task EnqueueAsync(CallbackJob job);
}
