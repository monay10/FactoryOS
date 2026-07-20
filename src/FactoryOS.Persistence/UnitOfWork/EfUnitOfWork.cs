using FactoryOS.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FactoryOS.Persistence.UnitOfWork;

/// <summary>An Entity Framework Core <see cref="IUnitOfWork"/> that commits a context's pending changes.</summary>
public sealed class EfUnitOfWork : IUnitOfWork
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
