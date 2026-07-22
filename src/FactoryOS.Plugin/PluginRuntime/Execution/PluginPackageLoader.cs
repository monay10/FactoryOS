using System.Reflection;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Domain.Results;
using FactoryOS.Plugin.Activation;
using FactoryOS.Plugin.Isolation;
using FactoryOS.Plugins.Runtime.Domain;
using FactoryOS.Plugins.Runtime.Isolation;

namespace FactoryOS.Plugins.Runtime.Execution;

/// <summary>
/// Turns an installed package into a live <see cref="IPlugin"/>, by one of two routes.
/// <para>
/// A plugin compiled into the host — the first-party modular monolith — is already an <c>IPlugin</c> in the
/// container, and is simply taken. A plugin that lives on disk has its entry assembly loaded into a
/// collectible context that the <see cref="PluginIsolationManager"/> <b>keeps</b>, which is the difference
/// between this and the framework's loader: the framework lets the context go, so a plugin it loads can
/// never afterwards be unloaded, updated or rolled back without restarting the host.
/// </para>
/// <para>
/// Activation itself is delegated to the framework's <see cref="IPluginActivator"/>, so the key check that
/// stops an assembly claiming to be a different plugin is enforced in exactly one place.
/// </para>
/// </summary>
public sealed class PluginPackageLoader
{
    private readonly IPluginActivator _activator;
    private readonly PluginIsolationManager _isolation;
    private readonly IReadOnlyDictionary<string, IPlugin> _compiled;

    /// <summary>Initializes a new instance of the <see cref="PluginPackageLoader"/> class.</summary>
    /// <param name="activator">The framework activator.</param>
    /// <param name="isolation">The isolation manager that keeps load contexts.</param>
    /// <param name="compiled">The plugins compiled into the host.</param>
    public PluginPackageLoader(
        IPluginActivator activator, PluginIsolationManager isolation, IEnumerable<IPlugin> compiled)
    {
        ArgumentNullException.ThrowIfNull(activator);
        ArgumentNullException.ThrowIfNull(isolation);
        ArgumentNullException.ThrowIfNull(compiled);

        _activator = activator;
        _isolation = isolation;
        _compiled = compiled.ToDictionary(plugin => plugin.Key, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Gets the keys of the plugins compiled into the host.</summary>
    public IReadOnlyCollection<string> CompiledIn => (IReadOnlyCollection<string>)_compiled.Keys;

    /// <summary>Loads the plugin an instance installs.</summary>
    /// <param name="instance">The tenant's installation.</param>
    /// <param name="definition">The definition it installs.</param>
    /// <returns>A successful result with the plugin, or a failure describing why it could not be loaded.</returns>
    public Result<IPlugin> Load(PluginInstance instance, PluginDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(definition);

        if (_compiled.TryGetValue(definition.Key, out var compiled))
        {
            return Result.Success(compiled);
        }

        if (string.IsNullOrWhiteSpace(definition.Location))
        {
            return Fail(definition.Key, "it is neither compiled into the host nor present on disk.");
        }

        if (string.IsNullOrWhiteSpace(definition.Assembly))
        {
            return Fail(definition.Key, "its manifest declares no 'assembly' to load.");
        }

        var assemblyPath = Path.Combine(definition.Location, definition.Assembly);
        if (!File.Exists(assemblyPath))
        {
            return Fail(definition.Key, $"its entry assembly '{assemblyPath}' does not exist.");
        }

        PluginLoadContext context;
        Assembly assembly;
        try
        {
            context = new PluginLoadContext(assemblyPath);
            assembly = context.LoadFromAssemblyPath(assemblyPath);
        }
        catch (Exception exception) when (exception is BadImageFormatException or FileLoadException)
        {
            return Fail(definition.Key, $"its entry assembly could not be loaded: {exception.Message}");
        }

        var entryType = ResolveEntryType(definition, assembly);
        if (entryType.IsFailure)
        {
            context.Unload();
            return Result.Failure<IPlugin>(entryType.Error);
        }

        var activated = _activator.Activate(entryType.Value, definition.Key);
        if (activated.IsFailure)
        {
            context.Unload();
            return activated;
        }

        _isolation.Attach(instance, context);
        return activated;
    }

    private static Result<Type> ResolveEntryType(PluginDefinition definition, Assembly assembly)
    {
        // Deliberately the same rule the framework's module loader applies: a declared entry type wins, and
        // otherwise the assembly must contain exactly one plugin.
        if (!string.IsNullOrWhiteSpace(definition.EntryType))
        {
            var declared = assembly.GetType(definition.EntryType, throwOnError: false, ignoreCase: false);
            if (declared is null)
            {
                return Result.Failure<Type>(Error.NotFound(
                    "Plugin.Runtime.Load.EntryTypeNotFound",
                    $"Plugin '{definition.Key}' declares entry type '{definition.EntryType}', which is not in its "
                    + "assembly."));
            }

            return typeof(IPlugin).IsAssignableFrom(declared)
                ? declared
                : Result.Failure<Type>(Error.Validation(
                    "Plugin.Runtime.Load.EntryTypeNotAPlugin",
                    $"Entry type '{definition.EntryType}' of plugin '{definition.Key}' does not implement IPlugin."));
        }

        var candidates = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IPlugin).IsAssignableFrom(type))
            .ToArray();

        return candidates.Length switch
        {
            1 => candidates[0],
            0 => Result.Failure<Type>(Error.NotFound(
                "Plugin.Runtime.Load.NoEntryType",
                $"The assembly of plugin '{definition.Key}' contains no concrete IPlugin implementation.")),
            _ => Result.Failure<Type>(Error.Validation(
                "Plugin.Runtime.Load.AmbiguousEntryType",
                $"The assembly of plugin '{definition.Key}' contains several IPlugin implementations; declare "
                + "'entryType' in the manifest.")),
        };
    }

    private static Result<IPlugin> Fail(string key, string reason) =>
        Result.Failure<IPlugin>(Error.Failure(
            "Plugin.Runtime.Load.Failed", $"Plugin '{key}' could not be loaded: {reason}"));
}
