namespace TestService.Api.Configuration;

public class MockingSettings
{
    /// <summary>
    /// Hosts that outbound mock callbacks may be sent to (exact, case-insensitive host match).
    /// Empty means nothing is sent.
    /// </summary>
    public List<string> AllowedCallbackHosts { get; set; } = new();
}
