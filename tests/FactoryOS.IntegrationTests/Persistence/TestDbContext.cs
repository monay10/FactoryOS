using FactoryOS.Persistence.Context;
using FactoryOS.Persistence.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace FactoryOS.IntegrationTests.Persistence;

/// <summary>A concrete <see cref="FactoryOsDbContext"/> used by the persistence integration tests.</summary>
public sealed class TestDbContext : FactoryOsDbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options, ITenantSchemaProvider schemaProvider)
        : base(options, schemaProvider)
    {
    }

    public DbSet<Widget> Widgets => Set<Widget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Widget>(entity =>
        {
            entity.HasKey(widget => widget.Id);
            entity.Property(widget => widget.Name).IsRequired();
        });

        // Base conventions (schema, soft-delete filter, concurrency token) applied last.
        base.OnModelCreating(modelBuilder);
    }
}
