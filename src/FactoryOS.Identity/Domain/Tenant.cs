using FactoryOS.Domain.Primitives;

namespace FactoryOS.Identity.Domain;

/// <summary>A tenant as a security principal boundary. Every user, role and organization belongs to one.</summary>
public sealed class Tenant : AggregateRoot<Guid>
{
    private Tenant(Guid id, string key, string name)
        : base(id)
    {
        Key = key;
        Name = name;
        IsActive = true;
    }

    private Tenant()
    {
        Key = string.Empty;
        Name = string.Empty;
    }

    /// <summary>Gets the stable tenant key (e.g. <c>tenant_001</c>).</summary>
    public string Key { get; private set; }

    /// <summary>Gets the tenant display name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets a value indicating whether the tenant is active.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Creates a new tenant.</summary>
    /// <param name="id">The tenant identifier.</param>
    /// <param name="key">The stable tenant key.</param>
    /// <param name="name">The tenant display name.</param>
    /// <returns>The new tenant.</returns>
    public static Tenant Create(Guid id, string key, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Tenant(id, key, name);
    }

    /// <summary>Deactivates the tenant.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Reactivates the tenant.</summary>
    public void Activate() => IsActive = true;
}
