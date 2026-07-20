using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FactoryOS.Persistence.Configuration;

/// <summary>
/// Base <see cref="IEntityTypeConfiguration{TEntity}"/> for FactoryOS entities. It applies the conventions every entity
/// shares — the primary key and bounded lengths on the audit-actor columns — then defers the entity-specific mapping to
/// <see cref="ConfigureEntity"/>. The soft-delete query filter, the concurrency token and the platform value
/// conventions are applied by <see cref="Context.FactoryOsDbContext"/> and its conventions, so they are not repeated here.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TId">The entity identifier type.</typeparam>
public abstract class BaseEntityConfiguration<TEntity, TId> : IEntityTypeConfiguration<TEntity>
    where TEntity : Entity<TId>
    where TId : notnull
{
    /// <summary>The maximum length applied to the audit-actor columns.</summary>
    protected const int ActorMaxLength = 256;

    /// <summary>Configures the entity type.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);

        if (typeof(IAuditableEntity).IsAssignableFrom(typeof(TEntity)))
        {
            builder.Property(nameof(IAuditableEntity.CreatedBy)).HasMaxLength(ActorMaxLength);
            builder.Property(nameof(IAuditableEntity.ModifiedBy)).HasMaxLength(ActorMaxLength);
        }

        if (typeof(ISoftDeletable).IsAssignableFrom(typeof(TEntity)))
        {
            builder.Property(nameof(ISoftDeletable.DeletedBy)).HasMaxLength(ActorMaxLength);
        }

        ConfigureEntity(builder);
    }

    /// <summary>Applies the entity-specific mapping (properties, relationships, indexes).</summary>
    /// <param name="builder">The entity type builder.</param>
    protected abstract void ConfigureEntity(EntityTypeBuilder<TEntity> builder);
}

/// <summary>
/// Base <see cref="IEntityTypeConfiguration{TValueObject}"/> for value objects mapped as their own (owned or keyless)
/// type rather than through a value converter. Value objects have no identity, so no key or audit metadata is
/// configured; the derived configuration supplies the columns.
/// </summary>
/// <typeparam name="TValueObject">The value-object type.</typeparam>
public abstract class BaseValueObjectConfiguration<TValueObject> : IEntityTypeConfiguration<TValueObject>
    where TValueObject : class
{
    /// <summary>Configures the value-object type.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<TValueObject> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ConfigureValueObject(builder);
    }

    /// <summary>Applies the value-object-specific mapping (columns, lengths, precision).</summary>
    /// <param name="builder">The entity type builder.</param>
    protected abstract void ConfigureValueObject(EntityTypeBuilder<TValueObject> builder);
}
