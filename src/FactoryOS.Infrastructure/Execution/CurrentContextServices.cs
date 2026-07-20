using FactoryOS.Application.Services;
using FactoryOS.Infrastructure.Configuration;
using FactoryOS.Shared.Guards;
using FactoryOS.Shared.Identifiers;

namespace FactoryOS.Infrastructure.Execution;

/// <summary>
/// The default <see cref="ICurrentUser"/>, reading the authenticated caller from the ambient
/// <see cref="InfrastructureContext"/>. Permission checks honor the FactoryOS <c>resource.action</c> wildcard
/// convention (<c>*</c> grants everything, <c>resource.*</c> grants every action on a resource, an exact grant
/// grants only itself) — matched here independently, as each layer matches the convention on its own.
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    private const string Wildcard = "*";

    private readonly InfrastructureContext _context;

    /// <summary>Initializes a new instance of the <see cref="CurrentUser"/> class.</summary>
    /// <param name="context">The ambient execution context.</param>
    public CurrentUser(InfrastructureContext context)
    {
        _context = Guard.AgainstNull(context);
    }

    /// <inheritdoc />
    public bool IsAuthenticated => _context.IsAuthenticated;

    /// <inheritdoc />
    public UserId? UserId => _context.UserId;

    /// <inheritdoc />
    public string? UserName => _context.UserName;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Permissions => _context.Permissions;

    /// <inheritdoc />
    public bool HasPermission(string permission)
    {
        Guard.AgainstNullOrWhiteSpace(permission);

        foreach (var grant in _context.Permissions)
        {
            if (Grants(grant, permission))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Grants(string grant, string required)
    {
        if (string.IsNullOrWhiteSpace(grant))
        {
            return false;
        }

        if (grant == Wildcard || string.Equals(grant, required, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // A "resource.*" grant covers every action on that resource.
        var star = grant.IndexOf(".*", StringComparison.Ordinal);
        if (star > 0 && star == grant.Length - 2)
        {
            var resource = grant.AsSpan(0, star);
            var dot = required.IndexOf('.', StringComparison.Ordinal);
            return dot == resource.Length
                && required.AsSpan(0, dot).Equals(resource, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}

/// <summary>The default <see cref="ICurrentTenant"/>, reading the resolved tenant from the ambient context.</summary>
public sealed class CurrentTenant : ICurrentTenant
{
    private readonly InfrastructureContext _context;

    /// <summary>Initializes a new instance of the <see cref="CurrentTenant"/> class.</summary>
    /// <param name="context">The ambient execution context.</param>
    public CurrentTenant(InfrastructureContext context)
    {
        _context = Guard.AgainstNull(context);
    }

    /// <inheritdoc />
    public bool HasTenant => !string.IsNullOrWhiteSpace(_context.Tenant);

    /// <inheritdoc />
    public string? Tenant => _context.Tenant;
}

/// <summary>The default <see cref="ICurrentFactory"/>, reading the resolved factory scope from the ambient context.</summary>
public sealed class CurrentFactory : ICurrentFactory
{
    private readonly InfrastructureContext _context;

    /// <summary>Initializes a new instance of the <see cref="CurrentFactory"/> class.</summary>
    /// <param name="context">The ambient execution context.</param>
    public CurrentFactory(InfrastructureContext context)
    {
        _context = Guard.AgainstNull(context);
    }

    /// <inheritdoc />
    public FactoryId? FactoryId => _context.FactoryId;
}

/// <summary>The default <see cref="ICurrentPlant"/>, reading the resolved plant scope from the ambient context.</summary>
public sealed class CurrentPlant : ICurrentPlant
{
    private readonly InfrastructureContext _context;

    /// <summary>Initializes a new instance of the <see cref="CurrentPlant"/> class.</summary>
    /// <param name="context">The ambient execution context.</param>
    public CurrentPlant(InfrastructureContext context)
    {
        _context = Guard.AgainstNull(context);
    }

    /// <inheritdoc />
    public PlantId? PlantId => _context.PlantId;
}

/// <summary>The default <see cref="ICurrentWorkCenter"/>, reading the resolved work-center scope from the context.</summary>
public sealed class CurrentWorkCenter : ICurrentWorkCenter
{
    private readonly InfrastructureContext _context;

    /// <summary>Initializes a new instance of the <see cref="CurrentWorkCenter"/> class.</summary>
    /// <param name="context">The ambient execution context.</param>
    public CurrentWorkCenter(InfrastructureContext context)
    {
        _context = Guard.AgainstNull(context);
    }

    /// <inheritdoc />
    public WorkCenterId? WorkCenterId => _context.WorkCenterId;
}
