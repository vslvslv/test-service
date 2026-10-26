using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TestService.Api.BackgroundServices;
using TestService.Api.Configuration;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration;

/// <summary>
/// Checks Program.cs's callback wiring through the real host, without the overrides that
/// <see cref="CallbackDeliveryTests"/> applies.
/// </summary>
[TestFixture]
public class CallbackDeliveryConfigurationTests : IntegrationTestBase
{
    protected override bool AuthenticateAsAdminByDefault => false;

    [Test]
    public void MockingSettings_BindFromDevelopmentAppSettings()
    {
        var settings = Factory.Services.GetRequiredService<IOptions<MockingSettings>>().Value;

        Assert.That(settings.AllowedCallbackHosts, Is.EqualTo(new[] { "localhost" }));
    }

    [Test]
    public void MockCallbacksPrimaryHandler_DoesNotFollowRedirects()
    {
        var handlerFactory = Factory.Services.GetRequiredService<IHttpMessageHandlerFactory>();
        HttpMessageHandler? handler = handlerFactory.CreateHandler(CallbackDispatcher.HttpClientName);
        while (handler is DelegatingHandler delegating)
        {
            handler = delegating.InnerHandler;
        }

        Assert.That(handler, Is.InstanceOf<SocketsHttpHandler>());
        Assert.That(((SocketsHttpHandler)handler!).AllowAutoRedirect, Is.False);
    }
}
