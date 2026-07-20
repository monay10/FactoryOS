using Microsoft.EntityFrameworkCore;

namespace FactoryOS.Persistence.Transactions;

/// <summary>
/// Default <see cref="ITransactionalExecutor"/> over an EF Core context. It reuses an ambient
/// transaction when one is already open, so nested calls compose into a single atomic unit.
/// </summary>
public sealed class EfTransactionalExecutor : ITransactionalExecutor
{
    private readonly DbContext _context;

    /// <summary>Initializes a new instance of the <see cref="EfTransactionalExecutor"/> class.</summary>
    /// <param name="context">The database context.</param>
    public EfTransactionalExecutor(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await ExecuteAsync<object?>(
            async token =>
            {
                await operation(token).ConfigureAwait(false);
                return null;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (_context.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken).ConfigureAwait(false);
        }

        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var result = await operation(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
