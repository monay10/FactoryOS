using FactoryOS.Domain.Primitives;

namespace FactoryOS.Identity.Domain;

/// <summary>An organizational unit within a tenant (plant, line, department), optionally nested.</summary>
public sealed class Organization : AggregateRoot<Guid>
{
    private Organization(Guid id, Guid tenantId, string name, Guid? parentId)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        ParentId = parentId;
    }

    private Organization() => Name = string.Empty;

    /// <summary>Gets the owning tenant identifier.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Gets the organization name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets the parent organization identifier, if this unit is nested.</summary>
    public Guid? ParentId { get; private set; }

    /// <summary>Creates a new organization.</summary>
    /// <param name="id">The organization identifier.</param>
    /// <param name="tenantId">The owning tenant.</param>
    /// <param name="name">The organization name.</param>
    /// <param name="parentId">The optional parent organization.</param>
    /// <returns>The new organization.</returns>
    public static Organization Create(Guid id, Guid tenantId, string name, Guid? parentId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Organization(id, tenantId, name, parentId);
    }
}
