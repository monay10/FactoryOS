using FactoryOS.Contracts.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.Plugins.Sample;

/// <summary>
/// The reference plugin. It demonstrates the full plugin contract: a stable key that matches the
/// manifest, service registration through <see cref="ConfigureServices"/>, and lifecycle hooks.
/// </summary>
public sealed class SamplePlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>module.json</c>.</summary>
    public const string PluginKey = "sample";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ISampleGreeter, SampleGreeter>();
    }
}
