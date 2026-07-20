using System.Reflection;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Domain.Results;

namespace FactoryOS.Plugin.Activation;

/// <summary>
/// Activates an already-resolved plugin <see cref="Type"/> into an <see cref="IPlugin"/> instance and
/// verifies its key. This is the in-process activation seam of the modular monolith — distinct from the
/// assembly-loading <see cref="Loading.IModuleLoader"/>; it does not load external DLLs.
/// </summary>
public interface IPluginActivator
{
    /// <summary>Activates a plugin type and checks its reported key matches the expectation.</summary>
    /// <param name="pluginType">A concrete type implementing <see cref="IPlugin"/>.</param>
    /// <param name="expectedKey">The key the activated plugin must report.</param>
    /// <returns>A successful result with the instance, or a failure describing why activation failed.</returns>
    Result<IPlugin> Activate(Type pluginType, string expectedKey);
}

/// <summary>Default <see cref="IPluginActivator"/> using the parameterless constructor of the plugin type.</summary>
public sealed class PluginActivator : IPluginActivator
{
    /// <inheritdoc />
    public Result<IPlugin> Activate(Type pluginType, string expectedKey)
    {
        ArgumentNullException.ThrowIfNull(pluginType);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedKey);

        if (pluginType is not { IsClass: true, IsAbstract: false } || !typeof(IPlugin).IsAssignableFrom(pluginType))
        {
            return Result.Failure<IPlugin>(Error.Validation(
                "Plugin.Activate.NotAPlugin",
                $"Type '{pluginType.FullName}' is not a concrete IPlugin implementation."));
        }

        IPlugin? instance;
        try
        {
            instance = Activator.CreateInstance(pluginType) as IPlugin;
        }
        catch (Exception exception) when (exception is MissingMethodException or TargetInvocationException)
        {
            return Result.Failure<IPlugin>(Error.Failure(
                "Plugin.Activate.Failed", $"Type '{pluginType.FullName}' could not be activated: {exception.Message}"));
        }

        if (instance is null)
        {
            return Result.Failure<IPlugin>(Error.Failure(
                "Plugin.Activate.Failed", $"Type '{pluginType.FullName}' did not activate to an IPlugin instance."));
        }

        if (!string.Equals(instance.Key, expectedKey, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<IPlugin>(Error.Validation(
                "Plugin.Activate.KeyMismatch",
                $"The activated plugin reports key '{instance.Key}' but '{expectedKey}' was expected."));
        }

        return Result.Success(instance);
    }
}
