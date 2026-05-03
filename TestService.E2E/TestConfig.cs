namespace TestService.E2E;

public static class TestConfig
{
    public static string BaseUrl =>
        Environment.GetEnvironmentVariable("BASE_URL") ?? "http://localhost:3000";

    public static string ApiUrl =>
        Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:5000";

    public static string Username =>
        Environment.GetEnvironmentVariable("E2E_USER") ?? "admin";

    public static string Password =>
        Environment.GetEnvironmentVariable("E2E_PASSWORD") ?? "Admin@123";

    public static string AuthStatePath => ".auth/user.json";
}
