using System.Security.Claims;
using FactoryOS.Identity.Context;

namespace FactoryOS.Identity.Execution;

/// <summary>Exposes the authenticated principal and its core identifiers for the current scope.</summary>
public interface ICurrentPrincipalAccessor
{
    /// <summary>Gets the current principal.</summary>
    ClaimsPrincipal Principal { get; }

    /// <summary>Gets a value indicating whether the current principal is authenticated.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Gets the current user identifier, or <see langword="null"/> when unauthenticated.</summary>
    Guid? UserId { get; }

    /// <summary>Gets the current tenant identifier, or <see langword="null"/> when absent.</summary>
    Guid? TenantId { get; }

    /// <summary>Gets the current session identifier, or <see langword="null"/> when the token is not session-bound.</summary>
    string? SessionId { get; }
}

/// <summary>Default <see cref="ICurrentPrincipalAccessor"/> reading from the scoped <see cref="IdentityContext"/>.</summary>
public sealed class CurrentPrincipalAccessor : ICurrentPrincipalAccessor
{
    private readonly IdentityContext _context;

    /// <summary>Initializes a new instance of the <see cref="CurrentPrincipalAccessor"/> class.</summary>
    /// <param name="context">The scoped identity context.</param>
    public CurrentPrincipalAccessor(IdentityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public ClaimsPrincipal Principal => _context.Principal;

    /// <inheritdoc />
    public bool IsAuthenticated => _context.IsAuthenticated;

    /// <inheritdoc />
    public Guid? UserId => _context.UserId;

    /// <inheritdoc />
    public Guid? TenantId => _context.TenantId;

    /// <inheritdoc />
    public string? SessionId => _context.SessionId;
}
