using Microsoft.EntityFrameworkCore;

namespace FactoryOS.Persistence.UnitOfWork;

/// <summary>
/// An Entity Framework Core unit of work that commits a context's pending changes. It satisfies both the domain and
/// the shared-kernel <c>IUnitOfWork</c> contracts (identical <see cref="SaveChangesAsync"/> signature) so either
/// abstraction resolves to the same implementation.
/// </summary>
public sealed class EfUnitOfWork : FactoryOS.Domain.Abstractions.IUnitOfWork, FactoryOS.Shared.Abstractions.IUnitOfWork
{
    private readonly DbContext _context;

    /// <summary>Initializes a new instance of the <see cref="EfUnitOfWork"/> class.</summary>
    /// <param name="context">The database context.</param>
    public EfUnitOfWork(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
