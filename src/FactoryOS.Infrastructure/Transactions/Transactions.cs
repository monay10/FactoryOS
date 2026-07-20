using FactoryOS.Application.Transactions;
using FactoryOS.Shared.Abstractions;
using FactoryOS.Shared.Guards;

namespace FactoryOS.Infrastructure.Transactions;

/// <summary>
/// The default <see cref="ITransaction"/>. It bounds a unit of work: committing flushes the pending changes through
/// <see cref="IUnitOfWork.SaveChangesAsync"/>, while rolling back simply leaves them unflushed — the scoped unit of
/// work is discarded at the end of the request, so unsaved changes never reach the store.
/// </summary>
public sealed class Transaction : ITransaction
{
    private readonly IUnitOfWork _unitOfWork;
    private bool _completed;

    /// <summary>Initializes a new instance of the <see cref="Transaction"/> class.</summary>
    /// <param name="unitOfWork">The unit of work this transaction commits.</param>
    public Transaction(IUnitOfWork unitOfWork)
    {
        _unitOfWork = Guard.AgainstNull(unitOfWork);
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _completed = true;
    }

    /// <inheritdoc />
    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _completed = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        // Nothing to release: the pending changes are only ever persisted through an explicit commit.
        _completed = true;
        return ValueTask.CompletedTask;
    }

    /// <summary>Gets a value indicating whether the transaction was committed, rolled back or disposed.</summary>
    public bool IsCompleted => _completed;
}

/// <summary>
/// The default <see cref="ITransactionManager"/>, beginning transactions over the ambient unit of work. The unit of
/// work is an optional dependency — resolved through <see cref="IEnumerable{T}"/> so a host without a persistence
/// layer still composes — and a transaction can only be started once one is registered.
/// </summary>
public sealed class TransactionManager : ITransactionManager
{
    private readonly IUnitOfWork? _unitOfWork;

    /// <summary>Initializes a new instance of the <see cref="TransactionManager"/> class.</summary>
    /// <param name="unitOfWorks">The registered units of work; at most one is expected.</param>
    public TransactionManager(IEnumerable<IUnitOfWork> unitOfWorks)
    {
        Guard.AgainstNull(unitOfWorks);
        _unitOfWork = unitOfWorks.FirstOrDefault();
    }

    /// <inheritdoc />
    public Task<ITransaction> BeginAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_unitOfWork is null)
        {
            throw new InvalidOperationException(
                "No IUnitOfWork is registered; a persistence layer must register one before a transaction can begin.");
        }

        return Task.FromResult<ITransaction>(new Transaction(_unitOfWork));
    }
}
