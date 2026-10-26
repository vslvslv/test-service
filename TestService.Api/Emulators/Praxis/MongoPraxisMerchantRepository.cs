using MongoDB.Bson;
using MongoDB.Driver;
using TestService.Api.Configuration;

namespace TestService.Api.Emulators.Praxis;

/// <summary>MongoDB store of emulator merchants (collection <c>PraxisMerchants</c>).</summary>
public class MongoPraxisMerchantRepository : IPraxisMerchantRepository
{
    public const string CollectionName = "PraxisMerchants";

    private readonly IMongoCollection<PraxisMerchant> _merchants;
    private readonly ILogger<MongoPraxisMerchantRepository> _logger;

    public MongoPraxisMerchantRepository(MongoDbSettings settings, ILogger<MongoPraxisMerchantRepository> logger)
    {
        _logger = logger;
        var client = new MongoClient(settings.ConnectionString);
        var database = client.GetDatabase(settings.DatabaseName);
        _merchants = database.GetCollection<PraxisMerchant>(CollectionName);

        CreateIndexes();
    }

    private void CreateIndexes()
    {
        try
        {
            _merchants.Indexes.CreateOne(new CreateIndexModel<PraxisMerchant>(
                Builders<PraxisMerchant>.IndexKeys.Ascending(x => x.Environment).Ascending(x => x.MerchantId),
                new CreateIndexOptions { Unique = true }));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not create indexes on {Collection}", CollectionName);
        }
    }

    public async Task<IReadOnlyList<PraxisMerchant>> ListAsync(string? environment)
    {
        var filter = string.IsNullOrWhiteSpace(environment)
            ? Builders<PraxisMerchant>.Filter.Empty
            : Builders<PraxisMerchant>.Filter.Eq(x => x.Environment, environment);
        return await _merchants.Find(filter).SortBy(x => x.Environment).ThenBy(x => x.MerchantId).ToListAsync();
    }

    public async Task<PraxisMerchant?> GetByIdAsync(string id)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return null;
        }
        return await _merchants.Find(x => x.Id == id).FirstOrDefaultAsync();
    }

    public async Task<PraxisMerchant?> FindAsync(string environment, string merchantId) =>
        await _merchants.Find(x => x.Environment == environment && x.MerchantId == merchantId).FirstOrDefaultAsync();

    public async Task<bool> AnyInEnvironmentAsync(string environment) =>
        await _merchants.Find(x => x.Environment == environment).Limit(1).AnyAsync();

    public async Task<PraxisMerchant> CreateAsync(PraxisMerchant merchant)
    {
        merchant.Id = null;
        try
        {
            await _merchants.InsertOneAsync(merchant);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new DuplicateMerchantException(merchant.Environment, merchant.MerchantId);
        }
        return merchant;
    }

    public async Task<bool> UpdateAsync(PraxisMerchant merchant)
    {
        if (merchant.Id is null || !ObjectId.TryParse(merchant.Id, out _))
        {
            return false;
        }
        try
        {
            var result = await _merchants.ReplaceOneAsync(x => x.Id == merchant.Id, merchant);
            return result.MatchedCount > 0;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new DuplicateMerchantException(merchant.Environment, merchant.MerchantId);
        }
    }

    public async Task<bool> DeleteAsync(string id)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return false;
        }
        var result = await _merchants.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0;
    }
}
