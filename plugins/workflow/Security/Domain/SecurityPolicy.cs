namespace FactoryOS.Plugins.Workflow.Security.Domain;

/// <summary>
/// An extra condition a rule must satisfy beyond matching the request — the hour it arrived, the network it
/// came from, an attribute of the resource.
/// <para>
/// Constraints are what turn a role-based system into an attribute-based one. "A supervisor may approve" is a
/// role; "a supervisor may approve their own site's orders, during their shift, from the plant network" is the
/// same role with three constraints, and none of them belongs in the role's name.
/// </para>
/// </summary>
/// <param name="Key">The stable constraint key, used to name it in a denial.</param>
public abstract record SecurityConstraint(string Key)
{
    /// <summary>Gets a value indicating whether the constraint holds for a request.</summary>
    /// <param name="context">The request.</param>
    /// <returns><see langword="true"/> when the constraint is satisfied.</returns>
    public abstract bool IsSatisfiedBy(SecurityContext context);

    /// <summary>Describes what the constraint requires, for the text of a denial.</summary>
    /// <returns>The description.</returns>
    public virtual string Describe() => Key;
}

/// <summary>
/// Who a rule applies to. An empty requirement applies to everyone — which is why an <see cref="SecurityEffect
/// .Allow"/> rule with no subject requirement is worth looking at twice.
/// </summary>
/// <param name="Roles">Any of these roles satisfies the requirement.</param>
/// <param name="Subjects">Any of these principals satisfies the requirement.</param>
/// <param name="Claims">Every one of these claims must be present with the given value.</param>
public sealed record SecuritySubjectRequirement(
    IReadOnlyList<string>? Roles = null,
    IReadOnlyList<string>? Subjects = null,
    IReadOnlyList<SecurityClaim>? Claims = null)
{
    /// <summary>A requirement that applies to every principal.</summary>
    public static SecuritySubjectRequirement Everyone { get; } = new();

    /// <summary>Gets a value indicating whether the requirement names nobody in particular.</summary>
    public bool IsUnrestricted =>
        (Roles is null || Roles.Count == 0)
        && (Subjects is null || Subjects.Count == 0)
        && (Claims is null || Claims.Count == 0);

    /// <summary>Creates a requirement satisfied by holding any of a set of roles.</summary>
    /// <param name="roles">The roles.</param>
    /// <returns>The requirement.</returns>
    public static SecuritySubjectRequirement ForRoles(params string[] roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        return new SecuritySubjectRequirement(Roles: roles);
    }

    /// <summary>Creates a requirement satisfied by being one of a set of principals.</summary>
    /// <param name="subjects">The principals.</param>
    /// <returns>The requirement.</returns>
    public static SecuritySubjectRequirement ForSubjects(params string[] subjects)
    {
        ArgumentNullException.ThrowIfNull(subjects);
        return new SecuritySubjectRequirement(Subjects: subjects);
    }

    /// <summary>Creates a requirement satisfied by presenting a claim with a value.</summary>
    /// <param name="type">The claim type.</param>
    /// <param name="value">The value it must carry.</param>
    /// <returns>The requirement.</returns>
    public static SecuritySubjectRequirement ForClaim(string type, string value) =>
        new(Claims: [SecurityClaim.Of(type, value)]);

    /// <summary>
    /// Gets a value indicating whether a principal satisfies the requirement. Roles and subjects are
    /// alternatives — holding either is enough — while every named claim must be present, because a claim
    /// requirement is normally a narrowing ("site=izmir") rather than a choice.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns><see langword="true"/> when the principal satisfies it.</returns>
    public bool IsSatisfiedBy(SecurityPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (Claims is { Count: > 0 }
            && !Claims.All(claim => principal.HasClaim(claim.Type, claim.Value)))
        {
            return false;
        }

        var namesRoles = Roles is { Count: > 0 };
        var namesSubjects = Subjects is { Count: > 0 };
        if (!namesRoles && !namesSubjects)
        {
            return true;
        }

        var byRole = namesRoles && Roles!.Any(principal.HasRole);
        var bySubject = namesSubjects && Subjects!.Any(
            subject => string.Equals(subject, principal.Subject, StringComparison.OrdinalIgnoreCase));

        return byRole || bySubject;
    }
}

/// <summary>
/// One statement of the form "these principals may (or may not) perform this action on this resource, provided
/// these constraints hold".
/// </summary>
/// <param name="Key">The stable rule key, named in the decision it produces.</param>
/// <param name="Effect">Whether the rule grants or refuses.</param>
/// <param name="Permission">The permission the rule speaks about; wildcards widen it.</param>
public sealed record SecurityRule(string Key, SecurityEffect Effect, string Permission)
{
    /// <summary>Gets who the rule applies to.</summary>
    public SecuritySubjectRequirement Subject { get; init; } = SecuritySubjectRequirement.Everyone;

    /// <summary>Gets the constraints that must all hold for the rule to apply.</summary>
    public IReadOnlyList<SecurityConstraint> Constraints { get; init; } = [];

    /// <summary>
    /// Gets the rule's precedence among rules of the same effect; higher decides first. Precedence never lets
    /// an allow outrank a deny — see <see cref="SecurityPolicy"/>.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>Gets what the rule is for.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the permission the rule speaks about, parsed.</summary>
    public SecurityPermission Pattern => SecurityPermission.Parse(Permission);

    /// <summary>Gets a value indicating whether the rule speaks about a request at all.</summary>
    /// <param name="context">The request.</param>
    /// <returns><see langword="true"/> when the rule's permission and subject both match.</returns>
    public bool Matches(SecurityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Pattern.Grants(context.RequestedPermission) && Subject.IsSatisfiedBy(context.Principal);
    }

    /// <summary>Gets the first constraint that does not hold, or <see langword="null"/> when all of them do.</summary>
    /// <param name="context">The request.</param>
    /// <returns>The unsatisfied constraint, or <see langword="null"/>.</returns>
    public SecurityConstraint? FirstUnsatisfied(SecurityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Constraints.FirstOrDefault(constraint => !constraint.IsSatisfiedBy(context));
    }
}

/// <summary>
/// A named set of rules, evaluated together.
/// <para>
/// Two things about how policies combine are not configurable, because making them configurable is how
/// authorization systems end up with a setting that quietly disables them:
/// </para>
/// <list type="number">
///   <item><description><b>An explicit deny always wins.</b> No priority, ordering or policy kind lets an
///   allow outrank a matching deny.</description></item>
///   <item><description><b>Nothing is permitted by default.</b> A request that no rule speaks about is
///   refused.</description></item>
/// </list>
/// </summary>
/// <param name="Key">The stable policy key.</param>
/// <param name="Name">The display name.</param>
/// <param name="Kind">The style of authorization the policy expresses.</param>
public sealed record SecurityPolicy(string Key, string Name, SecurityPolicyKind Kind)
{
    /// <summary>Gets the rules the policy is made of.</summary>
    public IReadOnlyList<SecurityRule> Rules { get; init; } = [];

    /// <summary>
    /// Gets the tenant this policy applies to, or <see langword="null"/> for every tenant. A tenant-specific
    /// policy is <b>configuration</b>, not a branch in the core — the engine never asks which tenant it is
    /// serving, it only asks which policies were configured for the one in scope.
    /// </summary>
    public string? Tenant { get; init; }

    /// <summary>Gets a value indicating whether the policy is evaluated at all.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>Gets what the policy is for.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the policy applies to a tenant.</summary>
    /// <param name="tenant">The tenant in scope.</param>
    /// <returns><see langword="true"/> when the policy applies.</returns>
    public bool AppliesTo(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return IsEnabled && (Tenant is null || string.Equals(Tenant, tenant, StringComparison.Ordinal));
    }

    /// <summary>Creates a policy from a set of rules.</summary>
    /// <param name="key">The policy key.</param>
    /// <param name="name">The display name.</param>
    /// <param name="kind">The authorization style.</param>
    /// <param name="rules">The rules.</param>
    /// <returns>The policy.</returns>
    public static SecurityPolicy Of(
        string key, string name, SecurityPolicyKind kind, params SecurityRule[] rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(rules);
        return new SecurityPolicy(key, name, kind) { Rules = rules };
    }
}
