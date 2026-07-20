using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.Contracts.Plugins;

/// <summary>
/// Convenience base class for plugins, providing no-op implementations of the optional lifecycle
/// members so a plugin only overrides what it needs.
/// </summary>
public abstract class PluginBase : IPlugin
{
    /// <inheritdoc />
    public abstract string Key { get; }

    /// <inheritdoc />
    public virtual void ConfigureServices(IServiceCollection services)
    {
        // No services by default; plugins override to contribute registrations.
    }

    /// <inheritdoc />
    public virtual Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
