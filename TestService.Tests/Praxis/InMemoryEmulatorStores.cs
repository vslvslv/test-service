using TestService.Api.Emulators.Praxis;
using TestService.Api.Emulators.Transactions;

namespace TestService.Tests.Praxis;

/// <summary>
/// In-memory <see cref="ITransactionRepository"/>: stores deep copies and enforces the optimistic
/// <c>Version</c> like the Mongo store. Also compiled into TestService.Unit as a linked file.
/// </summary>
public class InMemoryTransactionRepository : ITransactionRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<long, EmulatedTransaction> _store = new();
    private long _nextTid = 1_000_000;

    /// <summary>Number of saves refused because of a version conflict.</summary>
    public int Conflicts { get; private set; }

    /// <summary>Runs once before the next <see cref="TrySaveAsync"/> (for example to simulate a concurrent writer).</summary>
    public Action<InMemoryTransactionRepository>? BeforeNextSave { get; set; }

    public IReadOnlyList<EmulatedTransaction> All
    {
        get
        {
            lock (_gate)
            {
                return _store.Values.Select(t => t.Clone()).ToList();
            }
        }
    }

    public Task<long> NextTidAsync()
    {
        lock (_gate)
        {
            return Task.FromResult(_nextTid++);
        }
    }

    public Task InsertAsync(EmulatedTransaction transaction)
    {
        lock (_gate)
        {
            if (!_store.TryAdd(transaction.Tid, transaction.Clone()))
            {
                throw new InvalidOperationException($"Duplicate tid {transaction.Tid}");
            }
        }
        return Task.CompletedTask;
    }

    public Task<bool> TrySaveAsync(EmulatedTransaction transaction)
    {
        var hook = BeforeNextSave;
        BeforeNextSave = null;
        hook?.Invoke(this);

        lock (_gate)
        {
            if (!_store.TryGetValue(transaction.Tid, out var stored) || stored.Version != transaction.Version)
            {
                Conflicts++;
                return Task.FromResult(false);
            }
            transaction.Version++;
            _store[transaction.Tid] = transaction.Clone();
            return Task.FromResult(true);
        }
    }

    /// <summary>Bumps the stored version, as a concurrent writer would.</summary>
    public void Touch(long tid)
    {
        lock (_gate)
        {
            _store[tid].Version++;
        }
    }

    public Task<EmulatedTransaction?> GetAsync(long tid)
    {
        lock (_gate)
        {
            return Task.FromResult(_store.TryGetValue(tid, out var tx) ? tx.Clone() : null);
        }
    }

    public Task<IReadOnlyList<EmulatedTransaction>> QueryAsync(string? environment, string? orderId, long? tid, int limit = 1000)
    {
        lock (_gate)
        {
            IReadOnlyList<EmulatedTransaction> result = _store.Values
                .Where(t => string.IsNullOrWhiteSpace(environment) || t.Environment == environment)
                .Where(t => string.IsNullOrWhiteSpace(orderId) || t.OrderId == orderId)
                .Where(t => tid is null || t.Tid == tid)
                .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Tid)
                .Take(Math.Clamp(limit, 1, 1000))
                .Select(t => t.Clone())
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyList<EmulatedTransaction>> GetDueAsync(DateTime now, int limit = 100)
    {
        lock (_gate)
        {
            IReadOnlyList<EmulatedTransaction> result = _store.Values
                .Where(t => t.Deadline is not null && t.Deadline.DueAt <= now)
                .Take(limit)
                .Select(t => t.Clone())
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task<EmulatedTransaction?> FindByCardTokenAsync(string environment, string merchantId, string cardToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_store.Values
                .Where(t => t.Environment == environment && t.MerchantId == merchantId && t.Data("card_token") == cardToken)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => t.Clone())
                .FirstOrDefault());
        }
    }

    public Task<EmulatedTransaction?> FindByWalletTokenAsync(string environment, string merchantId, string walletToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_store.Values
                .Where(t => t.Environment == environment && t.MerchantId == merchantId && t.TransactionType == "sale"
                            && t.Data("wallet_token") == walletToken)
                .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Tid)
                .Select(t => t.Clone())
                .FirstOrDefault());
        }
    }
}

/// <summary>In-memory <see cref="IPraxisMerchantRepository"/> with the unique environment + merchantId rule.</summary>
public class InMemoryPraxisMerchantRepository : IPraxisMerchantRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<string, PraxisMerchant> _store = new();

    public Task<IReadOnlyList<PraxisMerchant>> ListAsync(string? environment)
    {
        lock (_gate)
        {
            IReadOnlyList<PraxisMerchant> result = _store.Values
                .Where(m => string.IsNullOrWhiteSpace(environment) || m.Environment == environment)
                .OrderBy(m => m.Environment).ThenBy(m => m.MerchantId)
                .Select(Copy)
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task<PraxisMerchant?> GetByIdAsync(string id)
    {
        lock (_gate)
        {
            return Task.FromResult(_store.TryGetValue(id, out var merchant) ? Copy(merchant) : null);
        }
    }

    public Task<PraxisMerchant?> FindAsync(string environment, string merchantId)
    {
        lock (_gate)
        {
            var merchant = _store.Values.FirstOrDefault(m => m.Environment == environment && m.MerchantId == merchantId);
            return Task.FromResult(merchant is null ? null : Copy(merchant));
        }
    }

    public Task<bool> AnyInEnvironmentAsync(string environment)
    {
        lock (_gate)
        {
            return Task.FromResult(_store.Values.Any(m => m.Environment == environment));
        }
    }

    public Task<PraxisMerchant> CreateAsync(PraxisMerchant merchant)
    {
        lock (_gate)
        {
            if (_store.Values.Any(m => m.Environment == merchant.Environment && m.MerchantId == merchant.MerchantId))
            {
                throw new DuplicateMerchantException(merchant.Environment, merchant.MerchantId);
            }
            merchant.Id = Guid.NewGuid().ToString("N")[..24];
            _store[merchant.Id] = Copy(merchant);
            return Task.FromResult(merchant);
        }
    }

    public Task<bool> UpdateAsync(PraxisMerchant merchant)
    {
        lock (_gate)
        {
            if (merchant.Id is null || !_store.ContainsKey(merchant.Id))
            {
                return Task.FromResult(false);
            }
            if (_store.Values.Any(m => m.Id != merchant.Id && m.Environment == merchant.Environment && m.MerchantId == merchant.MerchantId))
            {
                throw new DuplicateMerchantException(merchant.Environment, merchant.MerchantId);
            }
            _store[merchant.Id] = Copy(merchant);
            return Task.FromResult(true);
        }
    }

    public Task<bool> DeleteAsync(string id)
    {
        lock (_gate)
        {
            return Task.FromResult(_store.Remove(id));
        }
    }

    private static PraxisMerchant Copy(PraxisMerchant m) => new()
    {
        Id = m.Id,
        Environment = m.Environment,
        MerchantId = m.MerchantId,
        ApplicationKey = m.ApplicationKey,
        Secret = m.Secret,
        Gateways = m.Gateways.Select(g => new PraxisGateway
        {
            Id = g.Id, Kind = g.Kind, PaymentProcessor = g.PaymentProcessor, PaymentMethod = g.PaymentMethod
        }).ToList(),
        DropNotifications = m.DropNotifications,
        TimeScale = m.TimeScale
    };
}
