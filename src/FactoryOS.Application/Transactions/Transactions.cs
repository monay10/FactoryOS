namespace FactoryOS.Application.Transactions;

/// <summary>An ambient transaction that can be committed or rolled back, and is released on disposal.</summary>
public interface ITransaction : IAsyncDisposable
{
    /// <summary>Commits the transaction.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the transaction is committed.</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Rolls the transaction back.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the transaction is rolled back.</returns>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

/// <summary>Begins transactions over the current unit of work.</summary>
public interface ITransactionManager
{
    /// <summary>Begins a new transaction.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The started transaction.</returns>
    Task<ITransaction> BeginAsync(CancellationToken cancellationToken = default);
}
