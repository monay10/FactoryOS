using FactoryOS.Plugins.Workflow.Security.Domain;

namespace FactoryOS.Plugins.Workflow.Security.Policies;

/// <summary>
/// Builders for the seven authorization styles the platform supports.
/// <para>
/// They all produce the <b>same</b> thing — a policy made of rules with constraints — because they are not
/// seven mechanisms, they are seven shapes of one. Naming them separately is what makes a policy file readable
/// ("this is the time-based one") without splitting the evaluator into seven code paths that would each need
/// their own proof that deny still beats allow.
/// </para>
/// </summary>
public static class PolicyLibrary
{
    /// <summary>
    /// Role-based: named roles may perform a permission.
    /// </summary>
    /// <param name="key">The policy key.</param>
    /// <param name="name">The display name.</param>
    /// <param name="permission">The permission granted; wildcards widen it.</param>
    /// <param name="roles">The roles that hold it.</param>
    /// <returns>The policy.</returns>
    public static SecurityPolicy RoleBased(
        string key, string name, string permission, params string[] roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        return SecurityPolicy.Of(
            key,
            name,
            SecurityPolicyKind.RoleBased,
            new SecurityRule($"{key}:allow", SecurityEffect.Allow, permission)
            {
                Subject = SecuritySubjectRequirement.ForRoles(roles),
                Description = $"{string.Join(", ", roles)} may {permission}.",
            });
    }

    /// <summary>
    /// Attribute-based: a permission applies only while the named constraints hold.
    /// </summary>
    /// <param name="key">The policy key.</param>
    /// <param name="name">The display name.</param>
    /// <param name="permission">The permission.</param>
    /// <param name="subject">Who it applies to.</param>
    /// <param name="constraints">The constraints that must all hold.</param>
    /// <returns>The policy.</returns>
    public static SecurityPolicy AttributeBased(
        string key,
        string name,
        string permission,
        SecuritySubjectRequirement subject,
        params SecurityConstraint[] constraints)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(constraints);
        return SecurityPolicy.Of(
            key,
            name,
            SecurityPolicyKind.AttributeBased,
            new SecurityRule($"{key}:allow", SecurityEffect.Allow, permission)
            {
                Subject = subject,
                Constraints = constraints,
                Description = $"May {permission} while {string.Join(" and ", constraints.Select(c => c.Describe()))}.",
            });
    }

    /// <summary>
    /// Claim-based: whoever presents a claim with a value may perform a permission.
    /// </summary>
    /// <param name="key">The policy key.</param>
    /// <param name="name">The display name.</param>
    /// <param name="permission">The permission.</param>
    /// <param name="claimType">The claim type.</param>
    /// <param name="claimValue">The value it must carry.</param>
    /// <returns>The policy.</returns>
    public static SecurityPolicy ClaimBased(
        string key, string name, string permission, string claimType, string claimValue) =>
        SecurityPolicy.Of(
            key,
            name,
            SecurityPolicyKind.ClaimBased,
            new SecurityRule($"{key}:allow", SecurityEffect.Allow, permission)
            {
                Subject = SecuritySubjectRequirement.ForClaim(claimType, claimValue),
                Description = $"Holders of {claimType}={claimValue} may {permission}.",
            });

    /// <summary>
    /// Resource-based: a principal may act on the instances it owns.
    /// </summary>
    /// <param name="key">The policy key.</param>
    /// <param name="name">The display name.</param>
    /// <param name="permission">The permission.</param>
    /// <returns>The policy.</returns>
    public static SecurityPolicy ResourceBased(string key, string name, string permission) =>
        SecurityPolicy.Of(
            key,
            name,
            SecurityPolicyKind.ResourceBased,
            new SecurityRule($"{key}:allow", SecurityEffect.Allow, permission)
            {
                Constraints = [new ResourceOwnerConstraint($"{key}:owner")],
                Description = $"An owner may {permission} on what they own.",
            });

    /// <summary>
    /// Tenant-based: a policy that exists only for one tenant.
    /// <para>
    /// This is <b>configuration</b>, not a branch in the core. The engine never asks which tenant it is
    /// serving; it asks the repository for the policies configured for the tenant in scope, and a tenant with
    /// none configured simply has none. Onboarding a factory adds a policy, never a code path.
    /// </para>
    /// </summary>
    /// <param name="key">The policy key.</param>
    /// <param name="name">The display name.</param>
    /// <param name="tenant">The tenant it belongs to.</param>
    /// <param name="permission">The permission.</param>
    /// <param name="roles">The roles that hold it.</param>
    /// <returns>The policy.</returns>
    public static SecurityPolicy TenantBased(
        string key, string name, string tenant, string permission, params string[] roles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentNullException.ThrowIfNull(roles);
        return SecurityPolicy.Of(
            key,
            name,
            SecurityPolicyKind.TenantBased,
            new SecurityRule($"{key}:allow", SecurityEffect.Allow, permission)
            {
                Subject = roles.Length == 0
                    ? SecuritySubjectRequirement.Everyone
                    : SecuritySubjectRequirement.ForRoles(roles),
                Description = $"In {tenant}, may {permission}.",
            }) with
        {
            Tenant = tenant,
        };
    }

    /// <summary>
    /// Time-based: a permission applies only inside a window of the working week.
    /// </summary>
    /// <param name="key">The policy key.</param>
    /// <param name="name">The display name.</param>
    /// <param name="permission">The permission.</param>
    /// <param name="window">The window it applies in.</param>
    /// <param name="roles">The roles it applies to; none means everyone.</param>
    /// <returns>The policy.</returns>
    public static SecurityPolicy TimeBased(
        string key,
        string name,
        string permission,
        TimeWindowConstraint window,
        params string[] roles)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(roles);
        return SecurityPolicy.Of(
            key,
            name,
            SecurityPolicyKind.TimeBased,
            new SecurityRule($"{key}:allow", SecurityEffect.Allow, permission)
            {
                Subject = roles.Length == 0
                    ? SecuritySubjectRequirement.Everyone
                    : SecuritySubjectRequirement.ForRoles(roles),
                Constraints = [window],
                Description = $"May {permission} {window.Describe()}.",
            });
    }

    /// <summary>
    /// Network-based: a permission applies only to requests from named networks.
    /// </summary>
    /// <param name="key">The policy key.</param>
    /// <param name="name">The display name.</param>
    /// <param name="permission">The permission.</param>
    /// <param name="ranges">The permitted networks, in CIDR notation or as single addresses.</param>
    /// <returns>The policy.</returns>
    public static SecurityPolicy IpBased(
        string key, string name, string permission, params string[] ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var constraint = new IpRangeConstraint($"{key}:network", ranges);
        return SecurityPolicy.Of(
            key,
            name,
            SecurityPolicyKind.IpBased,
            new SecurityRule($"{key}:allow", SecurityEffect.Allow, permission)
            {
                Constraints = [constraint],
                Description = $"May {permission} {constraint.Describe()}.",
            });
    }

    /// <summary>
    /// Builds a policy that refuses a permission outright, whatever else grants it.
    /// <para>
    /// A deny needs no constraints and no priority to win — it wins because it is a deny. This exists so that
    /// "nobody exports the audit trail from outside the plant network, no matter what role they hold" is one
    /// line rather than an audit of every grant that might otherwise reach it.
    /// </para>
    /// </summary>
    /// <param name="key">The policy key.</param>
    /// <param name="name">The display name.</param>
    /// <param name="permission">The permission refused.</param>
    /// <param name="subject">Who it is refused to; none means everyone.</param>
    /// <param name="reason">Why.</param>
    /// <returns>The policy.</returns>
    public static SecurityPolicy Prohibition(
        string key,
        string name,
        string permission,
        SecuritySubjectRequirement? subject = null,
        string? reason = null) =>
        SecurityPolicy.Of(
            key,
            name,
            SecurityPolicyKind.RoleBased,
            new SecurityRule($"{key}:deny", SecurityEffect.Deny, permission)
            {
                Subject = subject ?? SecuritySubjectRequirement.Everyone,
                Description = reason ?? $"{permission} is refused.",
            });
}
