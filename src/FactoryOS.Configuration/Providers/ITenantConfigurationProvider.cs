using FactoryOS.Configuration.Model;
using FactoryOS.Domain.Results;

namespace FactoryOS.Configuration.Providers;

/// <summary>
/// Holds the current strongly-typed tenant configuration snapshot and supports hot reload: callers
/// read <see cref="Current"/> and subscribe to <see cref="Changed"/> to react to updates.
/// </summary>
public interface ITenantConfigurationProvider
{
    /// <summary>Gets the current, validated tenant configuration.</summary>
    TenantConfiguration Current { get; }

    /// <summary>Raised after a successful reload swaps in a new configuration.</summary>
    event EventHandler<TenantConfiguration>? Changed;

    /// <summary>Reloads the configuration from its source.</summary>
    /// <returns>
    /// A successful result with the new configuration, or a failure (in which case
    /// <see cref="Current"/> is left unchanged).
    /// </returns>
    Result<TenantConfiguration> Reload();
}
