using TestService.Api.Models;
using TestService.Api.Services;

namespace TestService.Tests.Infrastructure;

/// <summary>
/// In-process no-op replacement for IMessageBusService. The integration suite
/// runs without RabbitMQ; the real bus would block API requests on a
/// non-existent broker. No test asserts on published messages, so swallowing
/// them is safe.
/// </summary>
internal sealed class FakeMessageBusService : IMessageBusService
{
    public Task PublishAsync<T>(T message, string routingKey = "") => Task.CompletedTask;

    public void StartConsuming(Func<TestData, Task> messageHandler)
    {
    }

    public void StopConsuming()
    {
    }
}
