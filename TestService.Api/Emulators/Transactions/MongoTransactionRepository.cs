using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using TestService.Api.Configuration;

namespace TestService.Api.Emulators.Transactions;

/// <summary>MongoDB store of emulated transactions (collection <c>PraxisTransactions</c>).</summary>
public class MongoTransactionRepository : ITransactionRepository
{
    public const string CollectionName = "PraxisTransactions";
    private const string CounterId = "praxis-tid";

    /// <summary>First tid issued: seven digits, like the sandbox, far below int32.MaxValue.</summary>
    internal const long FirstTid = 1_000_000;

    private readonly IMongoCollection<EmulatedTransaction> _transactions;
    private readonly IMongoCollection<TidCounter> _counters;
    private readonly ILogger<MongoTransactionRepository> _logger;

    public MongoTransactionRepository(MongoDbSettings settings, ILogger<MongoTransactionRepository> logger)
    {
        _logger = logger;
        var client = new MongoClient(settings.ConnectionString);
        var database = client.GetDatabase(settings.DatabaseName);
        _transactions = database.GetCollection<EmulatedTransaction>(CollectionName);
        _counters = database.GetCollection<TidCounter>("EmulatorCounters");

        CreateIndexes();
    }

    private void CreateIndexes()
    {
        try
        {
            _transactions.Indexes.CreateMany(
            [
                new CreateIndexModel<EmulatedTransaction>(Builders<EmulatedTransaction>.IndexKeys
                    .Ascending(x => x.Environment).Ascending(x => x.OrderId)),
                new CreateIndexModel<EmulatedTransaction>(Builders<EmulatedTransaction>.IndexKeys
                    .Ascending("deadline.dueAt"),
                    new CreateIndexOptions { Sparse = true }),
                new CreateIndexModel<EmulatedTransaction>(Builders<EmulatedTransaction>.IndexKeys
                    .Ascending(x => x.Environment).Ascending(x => x.MerchantId).Ascending("providerData.card_token")),
                new CreateIndexModel<EmulatedTransaction>(Builders<EmulatedTransaction>.IndexKeys
                    .Ascending(x => x.Environment).Ascending(x => x.MerchantId).Ascending("providerData.wallet_token"))
            ]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not create indexes on {Collection}", CollectionName);
        }
    }

    public async Task<long> NextTidAsync()
    {
        var counter = await _counters.FindOneAndUpdateAsync(
            Builders<TidCounter>.Filter.Eq(c => c.Id, CounterId),
            Builders<TidCounter>.Update.Inc(c => c.Value, 1L),
            new FindOneAndUpdateOptions<TidCounter> { IsUpsert = true, ReturnDocument = ReturnDocument.After });
        var tid = FirstTid - 1 + counter.Value;
        if (tid > int.MaxValue)
        {
            throw new InvalidOperationException("The emulator tid counter exceeded int32.");
        }
        return tid;
    }

    public Task InsertAsync(EmulatedTransaction transaction) => _transactions.InsertOneAsync(transaction);

    public async Task<bool> TrySaveAsync(EmulatedTransaction transaction)
    {
        var expected = transaction.Version;
        transaction.Version = expected + 1;
        var result = await _transactions.ReplaceOneAsync(
            Builders<EmulatedTransaction>.Filter.Eq(x => x.Tid, transaction.Tid)
            & Builders<EmulatedTransaction>.Filter.Eq(x => x.Version, expected),
            transaction);
        if (result.MatchedCount == 0)
        {
            transaction.Version = expected;
            return false;
        }
        return true;
    }

    public async Task<EmulatedTransaction?> GetAsync(long tid) =>
        await _transactions.Find(x => x.Tid == tid).FirstOrDefaultAsync();

    public async Task<IReadOnlyList<EmulatedTransaction>> QueryAsync(string? environment, string? orderId, long? tid, int limit = 1000)
    {
        var filter = Builders<EmulatedTransaction>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(environment))
        {
            filter &= Builders<EmulatedTransaction>.Filter.Eq(x => x.Environment, environment);
        }
        if (!string.IsNullOrWhiteSpace(orderId))
        {
            filter &= Builders<EmulatedTransaction>.Filter.Eq(x => x.OrderId, orderId);
        }
        if (tid is not null)
        {
            filter &= Builders<EmulatedTransaction>.Filter.Eq(x => x.Tid, tid.Value);
        }

        return await _transactions.Find(filter)
            .SortByDescending(x => x.CreatedAt).ThenByDescending(x => x.Tid)
            .Limit(Math.Clamp(limit, 1, 1000))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<EmulatedTransaction>> GetDueAsync(DateTime now, int limit = 100) =>
        await _transactions.Find(Builders<EmulatedTransaction>.Filter.Lte("deadline.dueAt", now))
            .Limit(limit)
            .ToListAsync();

    public async Task<EmulatedTransaction?> FindByCardTokenAsync(string environment, string merchantId, string cardToken) =>
        await _transactions.Find(Builders<EmulatedTransaction>.Filter.Eq(x => x.Environment, environment)
                & Builders<EmulatedTransaction>.Filter.Eq(x => x.MerchantId, merchantId)
                & Builders<EmulatedTransaction>.Filter.Eq("providerData.card_token", cardToken))
            .SortByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

    public async Task<EmulatedTransaction?> FindByWalletTokenAsync(string environment, string merchantId, string walletToken) =>
        await _transactions.Find(Builders<EmulatedTransaction>.Filter.Eq(x => x.Environment, environment)
                & Builders<EmulatedTransaction>.Filter.Eq(x => x.MerchantId, merchantId)
                & Builders<EmulatedTransaction>.Filter.Eq(x => x.TransactionType, "sale")
                & Builders<EmulatedTransaction>.Filter.Eq("providerData.wallet_token", walletToken))
            .SortByDescending(x => x.CreatedAt).ThenByDescending(x => x.Tid)
            .FirstOrDefaultAsync();

    private sealed class TidCounter
    {
        [BsonId]
        public string Id { get; set; } = string.Empty;

        [BsonElement("value")]
        public long Value { get; set; }
    }
}
