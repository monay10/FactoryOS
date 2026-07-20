using System.Reflection;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Domain.Results;
using FactoryOS.Plugin.Isolation;
using FactoryOS.Plugin.Runtime;

namespace FactoryOS.Plugin.Loading;

/// <summary>
/// Default <see cref="IModuleLoader"/>. Loads a plugin's entry assembly through a collectible
/// <see cref="PluginLoadContext"/> — so the plugin's private dependencies stay isolated while shared
/// contracts resolve against the host — then activates the entry type and verifies its key matches
/// the manifest.
/// </summary>
public sealed class ModuleLoader : IModuleLoader
{
    /// <inheritdoc />
    public Result<IPlugin> Load(PluginDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var manifest = descriptor.Manifest;

        if (string.IsNullOrWhiteSpace(descriptor.Location))
        {
            return Fail(manifest.Key, "the plugin has no on-disk location to load from.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Assembly))
        {
            return Fail(manifest.Key, "the manifest does not declare an 'assembly' to load.");
        }

        var assemblyPath = Path.Combine(descriptor.Location, manifest.Assembly);
        if (!File.Exists(assemblyPath))
        {
            return Fail(manifest.Key, $"the entry assembly '{assemblyPath}' does not exist.");
        }

        Assembly assembly;
        try
        {
            var loadContext = new PluginLoadContext(assemblyPath);
            assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
        }
        catch (Exception exception) when (exception is BadImageFormatException or FileLoadException)
        {
            return Fail(manifest.Key, $"the entry assembly could not be loaded: {exception.Message}");
        }

        var entryTypeResult = ResolveEntryType(manifest, assembly);
        if (entryTypeResult.IsFailure)
        {
            return Result.Failure<IPlugin>(entryTypeResult.Error);
        }

        return Activate(manifest.Key, entryTypeResult.Value);
    }

    private static Result<Type> ResolveEntryType(PluginManifest manifest, Assembly assembly)
    {
        if (!string.IsNullOrWhiteSpace(manifest.EntryType))
        {
            var declared = assembly.GetType(manifest.EntryType, throwOnError: false, ignoreCase: false);
            if (declared is null)
            {
                return Result.Failure<Type>(Error.NotFound(
                    "Plugin.Load.EntryTypeNotFound",
                    $"Plugin '{manifest.Key}' declares entry type '{manifest.EntryType}', which was not found in its assembly."));
            }

            if (!typeof(IPlugin).IsAssignableFrom(declared))
            {
                return Result.Failure<Type>(Error.Validation(
                    "Plugin.Load.EntryTypeNotAPlugin",
                    $"Entry type '{manifest.EntryType}' of plugin '{manifest.Key}' does not implement IPlugin."));
            }

            return declared;
        }

        var candidates = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IPlugin).IsAssignableFrom(type))
            .ToArray();

        return candidates.Length switch
        {
            1 => candidates[0],
            0 => Result.Failure<Type>(Error.NotFound(
                "Plugin.Load.NoEntryType",
                $"Plugin '{manifest.Key}' assembly contains no concrete IPlugin implementation.")),
            _ => Result.Failure<Type>(Error.Validation(
                "Plugin.Load.AmbiguousEntryType",
                $"Plugin '{manifest.Key}' assembly contains several IPlugin implementations; declare 'entryType' in the manifest.")),
        };
    }

    private static Result<IPlugin> Activate(string key, Type entryType)
    {
        IPlugin? instance;
        try
        {
            instance = Activator.CreateInstance(entryType) as IPlugin;
        }
        catch (Exception exception) when (exception is MissingMethodException or TargetInvocationException)
        {
            return Fail(key, $"the entry type could not be activated: {exception.Message}");
        }

        if (instance is null)
        {
            return Fail(key, "the entry type did not activate to an IPlugin instance.");
        }

        if (!string.Equals(instance.Key, key, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<IPlugin>(Error.Validation(
                "Plugin.Load.KeyMismatch",
                $"The loaded plugin reports key '{instance.Key}' but its manifest declares '{key}'."));
        }

        return Result.Success(instance);
    }

    private static Result<IPlugin> Fail(string key, string reason) =>
        Result.Failure<IPlugin>(Error.Failure("Plugin.Load.Failed", $"Plugin '{key}' could not be loaded: {reason}"));
}
