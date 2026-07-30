using FactoryOS.Persistence.Context;
using FactoryOS.Persistence.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace FactoryOS.Api.Persistence.Security;

/// <summary>
/// One direct permission grant: a deliberate authorization decision that a principal within a tenant may exercise
/// a permission. The tenant is part of the key rather than a filter, so a grant made in one tenant can never be
/// read in another even by a caller that asks wrongly — the same structural isolation the in-memory store gives.
/// </summary>
public sealed class SecurityGrantRow
{
    /// <summary>The tenant the grant belongs to.</summary>
    public string Tenant { get; set; } = string.Empty;

    /// <summary>The principal the permission is granted to.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>The granted permission, as its normalized <c>resource.action</c> value.</summary>
    public string Permission { get; set; } = string.Empty;
}

/// <summary>
/// The security engine's persistence context — the direct authorization grants persisted to the configured
/// relational database. It inherits the FactoryOS schema-per-tenant and convention pipeline from
/// <see cref="FactoryOsDbContext"/> and maps a single table of grants. Policies and roles are deliberately not
/// persisted here: they are registered from configuration at start-up and rebuilt each time, so only the grants —
/// the runtime admin decisions — need to survive a restart.
/// </summary>
public sealed class SecurityDbContext : FactoryOsDbContext
{
    /// <summary>Initializes a new instance of the <see cref="SecurityDbContext"/> class.</summary>
    /// <param name="options">The context options.</param>
    /// <param name="schemaProvider">The tenant schema provider.</param>
    public SecurityDbContext(DbContextOptions<SecurityDbContext> options, ITenantSchemaProvider schemaProvider)
        : base(options, schemaProvider)
    {
    }

    /// <summary>The direct permission grants.</summary>
    public DbSet<SecurityGrantRow> Grants => Set<SecurityGrantRow>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<SecurityGrantRow>(entity =>
        {
            entity.ToTable("security_grants");

            // The whole grant is its own identity, so a repeated grant is a no-op rather than a duplicate row.
            entity.HasKey(grant => new { grant.Tenant, grant.Subject, grant.Permission });
            entity.Property(grant => grant.Tenant).IsRequired();
            entity.Property(grant => grant.Subject).IsRequired();
            entity.Property(grant => grant.Permission).IsRequired();
        });

        // Base conventions (schema, soft-delete filter, concurrency token) applied last.
        base.OnModelCreating(modelBuilder);
    }
}
