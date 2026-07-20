using System.Collections.Concurrent;
using FactoryOS.Identity.Authorization.Configuration;
using Microsoft.Extensions.Options;

namespace FactoryOS.Identity.Authorization.Services;

/// <summary>Supplies named authorization policies, seeded from configuration and extendable programmatically.</summary>
public interface IPolicyProvider
{
    /// <summary>Adds or replaces a named policy.</summary>
    /// <param name="policy">The policy to register.</param>
    void AddPolicy(AuthorizationPolicy policy);

    /// <summary>Gets a policy by name.</summary>
    /// <param name="name">The policy name.</param>
    /// <returns>The policy, or <see langword="null"/> when no policy is registered under that name.</returns>
    AuthorizationPolicy? GetPolicy(string name);

    /// <summary>Gets every registered policy.</summary>
    /// <returns>The policies.</returns>
    IReadOnlyCollection<AuthorizationPolicy> GetPolicies();
}

/// <summary>
/// Default <see cref="IPolicyProvider"/>. Policies declared under <see cref="AuthorizationConstants.PoliciesSection"/>
/// are compiled into <see cref="AuthorizationPolicy"/> instances on first use; further policies can be added at
/// runtime. It reuses the existing <see cref="AuthorizationPolicy"/> model rather than introducing a new one.
/// </summary>
public sealed class PolicyProvider : IPolicyProvider
{
    private readonly ConcurrentDictionary<string, AuthorizationPolicy> _policies = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="PolicyProvider"/> class.</summary>
    /// <param name="options">The authorization options carrying the configured policies.</param>
    public PolicyProvider(IOptions<AuthorizationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        foreach (var (name, settings) in options.Value.Policies)
        {
            _policies[name] = new AuthorizationPolicy(name, [.. settings.Permissions], settings.RequireAll);
        }
    }

    /// <inheritdoc />
    public void AddPolicy(AuthorizationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policies[policy.Name] = policy;
    }

    /// <inheritdoc />
    public AuthorizationPolicy? GetPolicy(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _policies.TryGetValue(name, out var policy) ? policy : null;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<AuthorizationPolicy> GetPolicies() => _policies.Values.ToArray();
}
