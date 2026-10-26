using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TestService.Api.Emulators;
using TestService.Api.Emulators.Praxis;
using TestService.Tests.Praxis;

namespace TestService.Unit.Emulators;

/// <summary>A <see cref="PraxisEmulator"/> over in-memory stores, a fake clock and a recording delivery.</summary>
public sealed class PraxisEmulatorHarness
{
    public const string Env = "unit";
    public const string NotificationUrl = "http://localhost/mock/unit/notify";
    public const string ReturnUrl = "https://merchant.example/return";

    public PraxisEmulatorHarness(double timeScale = 0)
    {
        Emulator = new PraxisEmulator(Merchants, Transactions, Delivery, Time, NullLogger<PraxisEmulator>.Instance);
        Merchant = Merchants.CreateAsync(NewMerchant("m-1", timeScale)).GetAwaiter().GetResult();
    }

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.Zero));
    public InMemoryPraxisMerchantRepository Merchants { get; } = new();
    public InMemoryTransactionRepository Transactions { get; } = new();
    public RecordingCallbackDelivery Delivery { get; } = new();
    public PraxisEmulator Emulator { get; }
    public PraxisMerchant Merchant { get; }

    public long Now => Time.GetUtcNow().ToUnixTimeSeconds();

    public static PraxisMerchant NewMerchant(string merchantId, double timeScale = 0, string environment = Env) => new()
    {
        Environment = environment,
        MerchantId = merchantId,
        ApplicationKey = "app-" + merchantId,
        Secret = "secret-" + merchantId,
        Gateways =
        [
            new PraxisGateway { Id = PraxisRequestBuilder.CardGateway, Kind = "card", PaymentProcessor = "Test Card Processor", PaymentMethod = "Credit Card" },
            new PraxisGateway { Id = PraxisRequestBuilder.EWalletGateway, Kind = "ewallet", PaymentProcessor = "Test E-Wallet", PaymentMethod = "altbankwire" }
        ],
        TimeScale = timeScale
    };

    public Task<EmulatorReply> SendAsync(string path, JsonNode body, PraxisMerchant? merchant = null, string? signature = null) =>
        Emulator.HandleAsync(PraxisRequestBuilder.Request(Env, path, body, (merchant ?? Merchant).Secret, signature));

    public async Task<JsonNode> SaleAsync(string cvv, long amount, string? notificationUrl = NotificationUrl, string? returnUrl = ReturnUrl)
    {
        var reply = await SendAsync("api/direct-process",
            PraxisRequestBuilder.Sale(Merchant, Now, cvv, amount, notificationUrl, returnUrl));
        return reply.Json()!;
    }

    public async Task<JsonNode> FindAsync(long tid, PraxisMerchant? merchant = null)
    {
        var reply = await SendAsync("agent/find-transaction", PraxisRequestBuilder.Find(merchant ?? Merchant, Now, tid), merchant);
        return reply.Json()!;
    }

    public async Task<JsonNode> ManageAsync(string intent, long tid)
    {
        var reply = await SendAsync("agent/manage-transaction", PraxisRequestBuilder.Manage(Merchant, Now, intent, tid));
        return reply.Json()!;
    }

    public async Task<JsonNode> RefundAsync(long amount, long referenceId)
    {
        var reply = await SendAsync("direct/refund", PraxisRequestBuilder.Refund(Merchant, Now, amount, referenceId));
        return reply.Json()!;
    }

    public Task<EmulatorReply> PageAsync(long tid) => Emulator.HandleAsync(new EmulatorRequest
    {
        Environment = Env, Path = $"pages/3ds/{tid}", Method = "GET", BaseUrl = "http://emulator.test"
    });

    public Task<EmulatorReply> SubmitAsync(long tid, string status, string? errorCode = "903") => Emulator.HandleAsync(new EmulatorRequest
    {
        Environment = Env,
        Path = $"pages/3ds/{tid}/submit",
        Method = "POST",
        Query = new Dictionary<string, string> { ["status"] = status },
        Form = errorCode is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["error_code"] = errorCode },
        BaseUrl = "http://emulator.test"
    });

    /// <summary>A `direct/apm` request (sale or payout) as the sandbox captures send it.</summary>
    public Task<EmulatorReply> ApmAsync(string transactionType, long amount, JsonObject? walletData = null,
        string? notificationUrl = NotificationUrl, string? returnUrl = ReturnUrl, PraxisMerchant? merchant = null) =>
        SendAsync("direct/apm", PraxisRequestBuilder.Apm(merchant ?? Merchant, Now, transactionType, amount, notificationUrl, returnUrl, walletData), merchant);

    /// <summary>A GET of an emulator page (`pages/...`).</summary>
    public Task<EmulatorReply> GetPageAsync(string path, string? status = null) => Emulator.HandleAsync(new EmulatorRequest
    {
        Environment = Env,
        Path = path,
        Method = "GET",
        Query = status is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["status"] = status },
        BaseUrl = "http://emulator.test"
    });

    public static long Tid(JsonNode response) => response["transaction"]!["tid"]!.GetValue<long>();

    public static string? Str(JsonNode response, string path) => PraxisSigner.Text(response, path) is { Length: > 0 } text ? text : null;
}
