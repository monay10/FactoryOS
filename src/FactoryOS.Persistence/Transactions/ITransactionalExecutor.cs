namespace FactoryOS.Persistence.Transactions;

/// <summary>Runs a unit of work inside a database transaction, committing on success and rolling back on failure.</summary>
public interface ITransactionalExecutor
{
    /// <summary>Executes an operation transactionally.</summary>
    /// <param name="operation">The operation to run.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the transaction is committed.</returns>
    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

    /// <summary>Executes an operation transactionally and returns its result.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="operation">The operation to run.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The operation's result once the transaction is committed.</returns>
    Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
