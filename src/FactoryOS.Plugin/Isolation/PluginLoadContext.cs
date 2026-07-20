using System.Reflection;
using System.Runtime.Loader;

namespace FactoryOS.Plugin.Isolation;

/// <summary>
/// A collectible <see cref="AssemblyLoadContext"/> that loads a plugin's assemblies in isolation from
/// the host, while deferring shared contract assemblies to the default context so a plugin and the
/// core exchange the same contract types. This is the seam that lets first-party modular-monolith
/// plugins move to out-of-process, sandboxed loading later without changing the contract surface.
/// </summary>
public sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    /// <summary>Initializes a new instance of the <see cref="PluginLoadContext"/> class.</summary>
    /// <param name="pluginMainAssemblyPath">The full path to the plugin's entry assembly.</param>
    public PluginLoadContext(string pluginMainAssemblyPath)
        : base(name: BuildName(pluginMainAssemblyPath), isCollectible: true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginMainAssemblyPath);
        _resolver = new AssemblyDependencyResolver(pluginMainAssemblyPath);
    }

    /// <inheritdoc />
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Contract and framework assemblies the host has already loaded must be unified with it: a
        // plugin and the core have to exchange the *same* IPlugin, event and domain types. Returning
        // null defers such assemblies to the default context even when the plugin ships a private copy.
        if (IsSharedWithHost(assemblyName))
        {
            return null;
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);

        // A null path means the assembly is not private to the plugin (e.g. a framework assembly);
        // returning null defers resolution to the default context.
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    /// <summary>
    /// Determines whether an assembly is already loaded in the default context and must therefore be
    /// shared with the host rather than loaded privately, preserving a single type identity.
    /// </summary>
    /// <param name="assemblyName">The assembly the plugin is requesting.</param>
    /// <returns><see langword="true"/> when the host already provides the assembly; otherwise <see langword="false"/>.</returns>
    private static bool IsSharedWithHost(AssemblyName assemblyName)
    {
        foreach (var loaded in Default.Assemblies)
        {
            if (string.Equals(loaded.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }

    private static string BuildName(string pluginMainAssemblyPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(pluginMainAssemblyPath);
        return $"PluginLoadContext:{fileName}";
    }
}
