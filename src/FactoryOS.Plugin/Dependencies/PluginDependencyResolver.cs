using FactoryOS.Contracts.Plugins;
using FactoryOS.Domain.Results;

namespace FactoryOS.Plugin.Dependencies;

/// <summary>
/// Orders plugins so that every plugin is loaded after the plugins it depends on. Detects duplicate
/// keys, missing dependencies, unsatisfied version constraints and dependency cycles.
/// </summary>
public static class PluginDependencyResolver
{
    /// <summary>Resolves a load order for the supplied manifests.</summary>
    /// <param name="manifests">The manifests to order.</param>
    /// <returns>
    /// A successful result whose value lists the manifests in dependency-first order, or a failure
    /// describing the first structural problem found.
    /// </returns>
    public static Result<IReadOnlyList<PluginManifest>> Resolve(IReadOnlyCollection<PluginManifest> manifests)
    {
        ArgumentNullException.ThrowIfNull(manifests);

        var byKey = new Dictionary<string, PluginManifest>(StringComparer.OrdinalIgnoreCase);
        foreach (var manifest in manifests)
        {
            if (!byKey.TryAdd(manifest.Key, manifest))
            {
                return Result.Failure<IReadOnlyList<PluginManifest>>(
                    Error.Conflict("Plugin.Dependency.Duplicate", $"Duplicate plugin key '{manifest.Key}'."));
            }
        }

        foreach (var manifest in byKey.Values)
        {
            foreach (var dependency in manifest.Dependencies)
            {
                if (!byKey.TryGetValue(dependency.PluginKey, out var target))
                {
                    return Result.Failure<IReadOnlyList<PluginManifest>>(
                        Error.NotFound(
                            "Plugin.Dependency.Missing",
                            $"Plugin '{manifest.Key}' depends on missing plugin '{dependency.PluginKey}'."));
                }

                if (!dependency.IsSatisfiedBy(target.Version))
                {
                    return Result.Failure<IReadOnlyList<PluginManifest>>(
                        Error.Validation(
                            "Plugin.Dependency.VersionMismatch",
                            $"Plugin '{manifest.Key}' requires '{dependency.PluginKey}' >= {dependency.MinimumVersion}, "
                            + $"but {target.Version} is present."));
                }
            }
        }

        var ordered = new List<PluginManifest>(byKey.Count);
        var marks = new Dictionary<string, Mark>(StringComparer.OrdinalIgnoreCase);

        foreach (var manifest in byKey.Values)
        {
            var cycle = Visit(manifest, byKey, marks, ordered);
            if (cycle is not null)
            {
                return Result.Failure<IReadOnlyList<PluginManifest>>(cycle);
            }
        }

        return Result.Success<IReadOnlyList<PluginManifest>>(ordered);
    }

    private static Error? Visit(
        PluginManifest manifest,
        Dictionary<string, PluginManifest> byKey,
        Dictionary<string, Mark> marks,
        List<PluginManifest> ordered)
    {
        if (marks.TryGetValue(manifest.Key, out var mark))
        {
            if (mark == Mark.Visiting)
            {
                return Error.Conflict(
                    "Plugin.Dependency.Cycle", $"A dependency cycle involves plugin '{manifest.Key}'.");
            }

            return null;
        }

        marks[manifest.Key] = Mark.Visiting;

        foreach (var dependency in manifest.Dependencies)
        {
            var target = byKey[dependency.PluginKey];
            var cycle = Visit(target, byKey, marks, ordered);
            if (cycle is not null)
            {
                return cycle;
            }
        }

        marks[manifest.Key] = Mark.Done;
        ordered.Add(manifest);
        return null;
    }

    private enum Mark
    {
        Visiting,
        Done,
    }
}
