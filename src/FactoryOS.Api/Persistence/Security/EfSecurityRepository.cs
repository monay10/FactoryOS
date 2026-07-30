using System.Collections.Concurrent;
using FactoryOS.Plugins.Workflow.Security.Domain;
using FactoryOS.Plugins.Workflow.Security.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactoryOS.Api.Persistence.Security;

/// <summary>
/// An <see cref="ISecurityRepository"/> that persists the direct authorization grants to the configured relational
/// database, while keeping policies and roles in memory. The split is deliberate and mirrors the plugin runtime's
/// (installs persist, packages are rediscovered): policies and roles are registered from configuration at start-up
/// and rebuilt every time, so only the grants — the deliberate, runtime admin decisions whose loss on restart is a
/// real security incident — are durable.
/// <para>
/// The repository is a singleton (the engine resolves it as one), so grants are read and written through short-lived
/// contexts from an <see cref="IDbContextFactory{TContext}"/> rather than a captured scoped context. The tenant is
/// part of every key, so a grant is structurally unreachable from another tenant.
/// </para>
/// </summary>
public sealed class EfSecurityRepository : ISecurityRepository
{
    private readonly IDbContextFactory<SecurityDbContext> _factory;
    private readonly ConcurrentDictionary<string, SecurityPolicy> _policies = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SecurityRole> _roles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Initializes the repository and ensures the grants schema exists.</summary>
    /// <param name="factory">The context factory the repository opens a context from per operation.</param>
    public EfSecurityRepository(IDbContextFactory<SecurityDbContext> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;

        // Create the grants table on first construction. Idempotent, so a second repository over the same database
        // is a no-op. Real Postgres migrations are a later concern; this makes the repository self-sufficient.
        using var context = _factory.CreateDbContext();
        context.Database.EnsureCreated();
    }

    /// <inheritdoc />
    public void RegisterPolicy(SecurityPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policies[policy.Key] = policy;
    }

    /// <inheritdoc />
    public void RegisterRole(SecurityRole role)
    {
        ArgumentNullException.ThrowIfNull(role);
        _roles[role.Key] = role;
    }

    /// <inheritdoc />
    public void Grant(string tenant, string subject, string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        // Parsed on the way in, so a malformed grant is refused where somebody can still fix it rather than silently
        // never matching anything at the moment it is relied on.
        var value = SecurityPermission.Parse(permission).Value;

        using var context = _factory.CreateDbContext();
        if (context.Grants.Find(tenant, subject, value) is not null)
        {
            return;
        }

        context.Grants.Add(new SecurityGrantRow { Tenant = tenant, Subject = subject, Permission = value });
        context.SaveChanges();
    }

    /// <inheritdoc />
    public bool Revoke(string tenant, string subject, string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        var value = SecurityPermission.Parse(permission).Value;

        using var context = _factory.CreateDbContext();
        var row = context.Grants.Find(tenant, subject, value);
        if (row is null)
        {
            return false;
        }

        context.Grants.Remove(row);
        context.SaveChanges();
        return true;
    }

    /// <inheritdoc />
    public IReadOnlyList<SecurityPolicy> PoliciesFor(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return _policies.Values
            .Where(policy => policy.AppliesTo(tenant))
            .OrderBy(policy => policy.Key, StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc />
    public SecurityRole? FindRole(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _roles.TryGetValue(key, out var role) ? role : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<SecurityRole> Roles() =>
        _roles.Values.OrderBy(role => role.Key, StringComparer.Ordinal).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<string> GrantsFor(string tenant, string subject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        using var context = _factory.CreateDbContext();
        return [.. context.Grants.AsNoTracking()
            .Where(grant => grant.Tenant == tenant && grant.Subject == subject)
            .Select(grant => grant.Permission)
            .ToList()
            .OrderBy(permission => permission, StringComparer.Ordinal)];
    }
}
