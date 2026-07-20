using System.Globalization;
using System.Security.Claims;
using FactoryOS.Identity.Claims;

namespace FactoryOS.Identity.Context;

/// <summary>
/// The scoped, ambient identity of the current execution: the authenticated <see cref="ClaimsPrincipal"/>
/// and the tenant, user and session it carries. It is initialized once per scope (by the host's
/// authentication bridge) and read by the current-principal and current-claims accessors. Following the
/// FactoryOS invariant, the tenant is always in scope once a principal is set.
/// </summary>
public sealed class IdentityContext
{
    private ClaimsPrincipal _principal = new(new ClaimsIdentity());
    private bool _initialized;

    /// <summary>Gets the current principal (an unauthenticated anonymous principal until initialized).</summary>
    public ClaimsPrincipal Principal => _principal;

    /// <summary>Gets a value indicating whether the context has been initialized with a principal.</summary>
    public bool IsInitialized => _initialized;

    /// <summary>Gets a value indicating whether the current principal is authenticated.</summary>
    public bool IsAuthenticated => _principal.Identity?.IsAuthenticated ?? false;

    /// <summary>Gets the current user identifier from the subject claim, or <see langword="null"/>.</summary>
    public Guid? UserId =>
        Guid.TryParse(
            _principal.FindFirst(FactoryClaimTypes.Subject)?.Value,
            CultureInfo.InvariantCulture,
            out var userId)
            ? userId
            : null;

    /// <summary>Gets the current tenant identifier from the tenant claim, or <see langword="null"/>.</summary>
    public Guid? TenantId => ClaimsFactory.GetTenantId(_principal);

    /// <summary>Gets the current session identifier from the session claim, if the token is session-bound.</summary>
    public string? SessionId => _principal.FindFirst(FactoryClaimTypes.Session)?.Value;

    /// <summary>Initializes the context with the authenticated principal. May be called only once.</summary>
    /// <param name="principal">The principal to adopt for the current scope.</param>
    /// <exception cref="InvalidOperationException">Thrown when the context is already initialized.</exception>
    public void Initialize(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (_initialized)
        {
            throw new InvalidOperationException("The identity context has already been initialized.");
        }

        _principal = principal;
        _initialized = true;
    }
}
