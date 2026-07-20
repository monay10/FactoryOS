using System.Globalization;
using FactoryOS.Identity.Claims;
using FactoryOS.Identity.Context;

namespace FactoryOS.Identity.Authorization.Context;

/// <summary>
/// The principal state an authorization decision is made against: the user and tenant, and the role names
/// and permission keys the principal holds. Built by mapping an authenticated principal's claims.
/// </summary>
public sealed class AuthorizationContext
{
    /// <summary>An anonymous context holding no roles or permissions.</summary>
    public static readonly AuthorizationContext Anonymous = new(null, null, [], []);

    /// <summary>Initializes a new instance of the <see cref="AuthorizationContext"/> class.</summary>
    /// <param name="userId">The user identifier, or <see langword="null"/> when anonymous.</param>
    /// <param name="tenantId">The tenant identifier, or <see langword="null"/> when absent.</param>
    /// <param name="roles">The role names the principal holds.</param>
    /// <param name="permissions">The permission keys the principal holds.</param>
    public AuthorizationContext(
        Guid? userId, Guid? tenantId, IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(permissions);

        UserId = userId;
        TenantId = tenantId;
        Roles = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Permissions = permissions.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Gets the user identifier, or <see langword="null"/> when anonymous.</summary>
    public Guid? UserId { get; }

    /// <summary>Gets the tenant identifier, or <see langword="null"/> when absent.</summary>
    public Guid? TenantId { get; }

    /// <summary>Gets the role names the principal holds.</summary>
    public IReadOnlySet<string> Roles { get; }

    /// <summary>Gets the permission keys the principal holds.</summary>
    public IReadOnlySet<string> Permissions { get; }

    /// <summary>Gets a value indicating whether the context represents an authenticated user.</summary>
    public bool IsAuthenticated => UserId is not null;
}

/// <summary>Exposes the <see cref="AuthorizationContext"/> for the current scope.</summary>
public interface IAuthorizationContextAccessor
{
    /// <summary>Gets the current authorization context, mapped from the current principal's claims.</summary>
    AuthorizationContext Current { get; }
}

/// <summary>
/// Default <see cref="IAuthorizationContextAccessor"/> mapping the scoped <see cref="IdentityContext"/>'s
/// principal claims (subject, tenant, role and permission claims) into an <see cref="AuthorizationContext"/>.
/// </summary>
public sealed class AuthorizationContextAccessor : IAuthorizationContextAccessor
{
    private readonly IdentityContext _identity;

    /// <summary>Initializes a new instance of the <see cref="AuthorizationContextAccessor"/> class.</summary>
    /// <param name="identity">The scoped identity context.</param>
    public AuthorizationContextAccessor(IdentityContext identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        _identity = identity;
    }

    /// <inheritdoc />
    public AuthorizationContext Current
    {
        get
        {
            if (!_identity.IsAuthenticated)
            {
                return AuthorizationContext.Anonymous;
            }

            var principal = _identity.Principal;
            var roles = principal.FindAll(FactoryClaimTypes.Role).Select(claim => claim.Value);
            var permissions = principal.FindAll(FactoryClaimTypes.Permission).Select(claim => claim.Value);
            var userId = Guid.TryParse(
                principal.FindFirst(FactoryClaimTypes.Subject)?.Value,
                CultureInfo.InvariantCulture,
                out var id)
                ? id
                : (Guid?)null;

            return new AuthorizationContext(userId, ClaimsFactory.GetTenantId(principal), roles, permissions);
        }
    }
}
