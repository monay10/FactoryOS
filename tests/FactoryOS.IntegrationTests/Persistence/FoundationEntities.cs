using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Primitives;
using FactoryOS.Persistence.Configuration;
using FactoryOS.Persistence.Context;
using FactoryOS.Persistence.Multitenancy;
using FactoryOS.Persistence.ValueConversion;
using FactoryOS.Shared.Identifiers;
using FactoryOS.Shared.Primitives;
using FactoryOS.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FactoryOS.IntegrationTests.Persistence;

/// <summary>A strongly-typed enumeration persisted by the foundation tests.</summary>
public sealed class GizmoStatus : Enumeration
{
    public static readonly GizmoStatus Active = new(1, "Active");
    public static readonly GizmoStatus Retired = new(2, "Retired");

    private GizmoStatus(int id, string name)
        : base(id, name)
    {
    }
}

/// <summary>
/// A foundation test aggregate keyed by a strongly-typed identifier and carrying value objects, so the tests exercise
/// the value converters, the model conventions and the base entity configuration end-to-end.
/// </summary>
public sealed class Gizmo : AggregateRoot<MachineId>, IAuditableEntity
{
    private Gizmo(MachineId id, string name, Money price, Percentage efficiency, DateRange window, GizmoStatus status)
        : base(id)
    {
        Name = name;
        Price = price;
        Efficiency = efficiency;
        Window = window;
        Status = status;
    }

    private Gizmo()
    {
        Name = string.Empty;
        Price = Money.Zero("USD");
        Efficiency = Percentage.Zero;
        Window = DateRange.Between(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        Status = GizmoStatus.Active;
    }

    public string Name { get; private set; }

    public Money Price { get; private set; }

    public Percentage Efficiency { get; private set; }

    public DateRange Window { get; private set; }

    public GizmoStatus Status { get; private set; }

    public DateTimeOffset CreatedOnUtc { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedOnUtc { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static Gizmo Create(
        MachineId id, string name, Money price, Percentage efficiency, DateRange window, GizmoStatus status) =>
        new(id, name, price, efficiency, window, status);

    public void ApplyCreated(DateTimeOffset whenUtc, string? actor)
    {
        CreatedOnUtc = whenUtc;
        CreatedBy = actor;
    }

    public void ApplyModified(DateTimeOffset whenUtc, string? actor)
    {
        ModifiedOnUtc = whenUtc;
        ModifiedBy = actor;
    }
}

/// <summary>The <see cref="Gizmo"/> mapping, built on <see cref="BaseEntityConfiguration{TEntity, TId}"/>.</summary>
public sealed class GizmoConfiguration : BaseEntityConfiguration<Gizmo, MachineId>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Gizmo> builder)
    {
        builder.Property(gizmo => gizmo.Name).IsRequired().HasMaxLength(200);
        builder.Property(gizmo => gizmo.Price).HasConversion(new MoneyConverter()).IsRequired();
        builder.Property(gizmo => gizmo.Efficiency).HasConversion(new PercentageConverter());
        builder.Property(gizmo => gizmo.Window).HasConversion(new DateRangeConverter());
        builder.Property(gizmo => gizmo.Status).HasConversion(new EnumerationConverter<GizmoStatus>());
        builder.HasIndex(gizmo => gizmo.Name);
    }
}

/// <summary>A concrete <see cref="FactoryOsDbContext"/> that discovers its configurations from the assembly.</summary>
public sealed class FoundationTestDbContext : FactoryOsDbContext
{
    public FoundationTestDbContext(DbContextOptions<FoundationTestDbContext> options, ITenantSchemaProvider schema)
        : base(options, schema)
    {
    }

    public DbSet<Gizmo> Gizmos => Set<Gizmo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFrom<GizmoConfiguration>();
        base.OnModelCreating(modelBuilder);
    }
}
