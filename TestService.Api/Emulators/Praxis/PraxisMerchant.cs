using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TestService.Api.Emulators.Praxis;

/// <summary>A gateway registered for an emulator merchant.</summary>
public class PraxisGateway
{
    [BsonElement("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary><c>card</c> or <c>ewallet</c>.</summary>
    [BsonElement("kind")]
    public string Kind { get; set; } = string.Empty;

    [BsonElement("paymentProcessor")]
    public string? PaymentProcessor { get; set; }

    [BsonElement("paymentMethod")]
    public string? PaymentMethod { get; set; }
}

/// <summary>
/// An emulator merchant (collection <c>PraxisMerchants</c>, unique environment + merchantId).
/// The secret verifies requests, decrypts card data and signs responses and notifications.
/// </summary>
[BsonIgnoreExtraElements]
public class PraxisMerchant
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("environment")]
    public string Environment { get; set; } = string.Empty;

    [BsonElement("merchantId")]
    public string MerchantId { get; set; } = string.Empty;

    [BsonElement("applicationKey")]
    public string ApplicationKey { get; set; } = string.Empty;

    [BsonElement("secret")]
    public string Secret { get; set; } = string.Empty;

    [BsonElement("gateways")]
    public List<PraxisGateway> Gateways { get; set; } = new();

    [BsonElement("dropNotifications")]
    public bool DropNotifications { get; set; }

    /// <summary>Multiplies every emulator delay for this merchant (0 = immediate).</summary>
    [BsonElement("timeScale")]
    public double TimeScale { get; set; } = 1.0;
}

/// <summary>Body of <c>POST</c>/<c>PUT /api/emulators/praxis/merchants</c>. <c>secret</c> is write-only.</summary>
public class PraxisMerchantRequest
{
    public string? Environment { get; set; }
    public string? MerchantId { get; set; }
    public string? ApplicationKey { get; set; }
    public string? Secret { get; set; }
    public List<PraxisGateway>? Gateways { get; set; }
    public bool? DropNotifications { get; set; }
    public double? TimeScale { get; set; }
}

/// <summary>A merchant as returned by the API: the secret is replaced by its length.</summary>
public class PraxisMerchantView
{
    public string? Id { get; set; }
    public string Environment { get; set; } = string.Empty;
    public string MerchantId { get; set; } = string.Empty;
    public string ApplicationKey { get; set; } = string.Empty;
    public int SecretLength { get; set; }
    public List<PraxisGateway> Gateways { get; set; } = new();
    public bool DropNotifications { get; set; }
    public double TimeScale { get; set; }

    public static PraxisMerchantView From(PraxisMerchant merchant) => new()
    {
        Id = merchant.Id,
        Environment = merchant.Environment,
        MerchantId = merchant.MerchantId,
        ApplicationKey = merchant.ApplicationKey,
        SecretLength = merchant.Secret.Length,
        Gateways = merchant.Gateways,
        DropNotifications = merchant.DropNotifications,
        TimeScale = merchant.TimeScale
    };
}

/// <summary>Thrown when another merchant already has the same environment and merchantId.</summary>
public class DuplicateMerchantException : Exception
{
    public DuplicateMerchantException(string environment, string merchantId)
        : base($"Merchant '{merchantId}' already exists in environment '{environment}'.")
    {
    }
}

public static class PraxisMerchantValidator
{
    public const int MaxSecretLength = 32;

    /// <summary>
    /// Validates <paramref name="request"/> and returns the merchant to store, or an error message.
    /// On update, <paramref name="existing"/> supplies the stored secret when the request has none.
    /// </summary>
    public static (PraxisMerchant? Merchant, string? Error) Validate(PraxisMerchantRequest? request, PraxisMerchant? existing = null)
    {
        if (request is null)
        {
            return (null, "Request body is required.");
        }
        if (string.IsNullOrWhiteSpace(request.Environment))
        {
            return (null, "environment is required.");
        }
        if (string.IsNullOrWhiteSpace(request.MerchantId))
        {
            return (null, "merchantId is required.");
        }
        if (string.IsNullOrWhiteSpace(request.ApplicationKey))
        {
            return (null, "applicationKey is required.");
        }

        var secret = string.IsNullOrEmpty(request.Secret) ? existing?.Secret : request.Secret;
        if (string.IsNullOrEmpty(secret))
        {
            return (null, "secret is required.");
        }
        if (secret.Length > MaxSecretLength)
        {
            // Card data uses the secret, left-padded to 32 bytes, as the AES-256 key.
            return (null, $"secret must be at most {MaxSecretLength} characters.");
        }
        if (request.Gateways is null || request.Gateways.Count == 0)
        {
            return (null, "gateways must contain at least one gateway.");
        }
        foreach (var gateway in request.Gateways)
        {
            if (gateway is null || string.IsNullOrWhiteSpace(gateway.Id))
            {
                return (null, "gateways[].id is required.");
            }
            if (gateway.Kind is not ("card" or "ewallet"))
            {
                return (null, "gateways[].kind must be 'card' or 'ewallet'.");
            }
        }

        var timeScale = request.TimeScale ?? 1.0;
        if (double.IsNaN(timeScale) || double.IsInfinity(timeScale) || timeScale < 0)
        {
            return (null, "timeScale must be a number >= 0.");
        }

        return (new PraxisMerchant
        {
            Id = existing?.Id,
            Environment = request.Environment.Trim(),
            MerchantId = request.MerchantId.Trim(),
            ApplicationKey = request.ApplicationKey,
            Secret = secret,
            Gateways = request.Gateways.Select(g => new PraxisGateway
            {
                Id = g.Id.Trim(), Kind = g.Kind, PaymentProcessor = g.PaymentProcessor, PaymentMethod = g.PaymentMethod
            }).ToList(),
            DropNotifications = request.DropNotifications ?? false,
            TimeScale = timeScale
        }, null);
    }
}

/// <summary>Store of emulator merchants.</summary>
public interface IPraxisMerchantRepository
{
    Task<IReadOnlyList<PraxisMerchant>> ListAsync(string? environment);
    Task<PraxisMerchant?> GetByIdAsync(string id);
    Task<PraxisMerchant?> FindAsync(string environment, string merchantId);
    Task<bool> AnyInEnvironmentAsync(string environment);

    /// <exception cref="DuplicateMerchantException">environment + merchantId already exists.</exception>
    Task<PraxisMerchant> CreateAsync(PraxisMerchant merchant);

    /// <summary>Replaces the merchant with the same id; false when it does not exist.</summary>
    /// <exception cref="DuplicateMerchantException">environment + merchantId belongs to another merchant.</exception>
    Task<bool> UpdateAsync(PraxisMerchant merchant);

    Task<bool> DeleteAsync(string id);
}
