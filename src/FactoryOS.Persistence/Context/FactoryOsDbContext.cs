using System.Linq.Expressions;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Persistence.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace FactoryOS.Persistence.Context;

/// <summary>
/// The base <see cref="DbContext"/> for FactoryOS. It applies the platform-wide persistence
/// conventions — tenant schema isolation, soft-delete query filters and optimistic-concurrency
/// tokens — so every module context inherits them for free.
/// </summary>
/// <remarks>
/// Derived contexts must call <c>base.OnModelCreating(modelBuilder)</c> <b>last</b>, after mapping
/// their own entities, so the conventions are applied to the complete model.
/// </remarks>
public abstract class FactoryOsDbContext : DbContext
{
    private readonly ITenantSchemaProvider _schemaProvider;

    /// <summary>Initializes a new instance of the <see cref="FactoryOsDbContext"/> class.</summary>
    /// <param name="options">The context options.</param>
    /// <param name="schemaProvider">The tenant schema provider.</param>
    protected FactoryOsDbContext(DbContextOptions options, ITenantSchemaProvider schemaProvider)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(schemaProvider);
        _schemaProvider = schemaProvider;
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        ApplyTenantSchema(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType).HasQueryFilter(BuildIsNotDeletedFilter(clrType));
            }

            if (typeof(IConcurrencyStamped).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType)
                    .Property(nameof(IConcurrencyStamped.ConcurrencyToken))
                    .IsConcurrencyToken();
            }
        }
    }

    private void ApplyTenantSchema(ModelBuilder modelBuilder)
    {
        // Schemas exist on PostgreSQL; providers without schema support (e.g. SQLite in tests) ignore this.
        if (Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
        {
            modelBuilder.HasDefaultSchema(_schemaProvider.Schema);
        }
    }

    private static LambdaExpression BuildIsNotDeletedFilter(Type clrType)
    {
        var parameter = Expression.Parameter(clrType, "e");
        var isDeleted = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
        var body = Expression.Not(isDeleted);
        return Expression.Lambda(body, parameter);
    }
}
