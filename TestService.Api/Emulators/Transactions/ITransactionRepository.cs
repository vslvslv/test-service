namespace TestService.Api.Emulators.Transactions;

/// <summary>Persistent store of emulated transactions (state across requests and restarts).</summary>
public interface ITransactionRepository
{
    /// <summary>The next transaction id from the counter document (unique across environments; fits in int32).</summary>
    Task<long> NextTidAsync();

    Task InsertAsync(EmulatedTransaction transaction);

    /// <summary>
    /// Saves <paramref name="transaction"/> if nobody saved it since it was read (same <c>Version</c>),
    /// and increments its <c>Version</c>. Returns false on a conflict; the transaction is then unchanged in the store.
    /// </summary>
    Task<bool> TrySaveAsync(EmulatedTransaction transaction);

    Task<EmulatedTransaction?> GetAsync(long tid);

    /// <summary>Transactions matching every given filter, newest first, at most <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<EmulatedTransaction>> QueryAsync(string? environment, string? orderId, long? tid, int limit = 1000);

    /// <summary>Transactions whose deadline is due at <paramref name="now"/>.</summary>
    Task<IReadOnlyList<EmulatedTransaction>> GetDueAsync(DateTime now, int limit = 100);

    /// <summary>The newest transaction of the merchant that carries <paramref name="cardToken"/>.</summary>
    Task<EmulatedTransaction?> FindByCardTokenAsync(string environment, string merchantId, string cardToken);

    /// <summary>
    /// The merchant's newest e-wallet sale (latest <c>CreatedAt</c>, then highest tid) that carries <paramref name="walletToken"/>;
    /// payouts are never a source (their <c>wallet_data</c> is only the e-mail).
    /// </summary>
    Task<EmulatedTransaction?> FindByWalletTokenAsync(string environment, string merchantId, string walletToken);
}
