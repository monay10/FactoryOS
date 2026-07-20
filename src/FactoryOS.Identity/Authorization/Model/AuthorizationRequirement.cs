namespace FactoryOS.Identity.Authorization.Model;

/// <summary>The base of an authorization requirement — something a principal must satisfy to be authorized.</summary>
public abstract record AuthorizationRequirement;

/// <summary>Requires the principal to hold a specific permission (wildcard- and hierarchy-aware).</summary>
/// <param name="Permission">The required permission key.</param>
public sealed record PermissionRequirement(string Permission) : AuthorizationRequirement;

/// <summary>Requires the principal to be in a specific role (honouring role inheritance).</summary>
/// <param name="Role">The required role name.</param>
public sealed record RoleRequirement(string Role) : AuthorizationRequirement;

/// <summary>Requires the principal to satisfy a named policy resolved by the policy provider.</summary>
/// <param name="PolicyName">The name of the policy to evaluate.</param>
public sealed record PolicyRequirement(string PolicyName) : AuthorizationRequirement;

/// <summary>The outcome of an authorization evaluation: success, or failure with a reason.</summary>
public sealed class AuthorizationResult
{
    private AuthorizationResult(bool succeeded, string? failureReason)
    {
        Succeeded = succeeded;
        FailureReason = failureReason;
    }

    /// <summary>Gets a value indicating whether authorization succeeded.</summary>
    public bool Succeeded { get; }

    /// <summary>Gets the reason authorization failed, or <see langword="null"/> on success.</summary>
    public string? FailureReason { get; }

    /// <summary>Creates a successful result.</summary>
    /// <returns>A succeeded <see cref="AuthorizationResult"/>.</returns>
    public static AuthorizationResult Success() => new(true, null);

    /// <summary>Creates a failed result.</summary>
    /// <param name="reason">The reason authorization failed.</param>
    /// <returns>A failed <see cref="AuthorizationResult"/>.</returns>
    public static AuthorizationResult Fail(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new AuthorizationResult(false, reason);
    }
}
