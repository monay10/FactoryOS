using FactoryOS.Contracts.Plugins;
using FactoryOS.Domain.Results;
using FactoryOS.Plugin.Dependencies;
using FactoryOS.Plugins.Runtime.Domain;
using FactoryOS.Plugins.Runtime.Persistence;

namespace FactoryOS.Plugins.Runtime.Discovery;

/// <summary>
/// Decides whether an available version satisfies a required one.
/// <para>
/// The rule is <b>same major, at least the required minor and patch</b>. A dependency written against 2.1 is
/// satisfied by 2.4 and not by 3.0, because a major bump is the only signal a plugin author has to say "this
/// breaks you" — and honouring it is what lets two plugins be updated independently.
/// </para>
/// </summary>
public sealed class PluginVersionResolver
{
    /// <summary>Determines whether an available version satisfies a requirement.</summary>
    /// <param name="available">The version that is present.</param>
    /// <param name="required">The version that is required.</param>
    /// <returns><see langword="true"/> when the requirement is met.</returns>
    public bool Satisfies(PluginVersion available, PluginVersion required) =>
        available.Major == required.Major && available >= required;

    /// <summary>Picks the highest satisfying version from a set of candidates.</summary>
    /// <param name="candidates">The versions available.</param>
    /// <param name="required">The version required.</param>
    /// <returns>The chosen version, or <see langword="null"/> when none satisfies the requirement.</returns>
    public PluginVersion? Resolve(IEnumerable<PluginVersion> candidates, PluginVersion required)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        PluginVersion? best = null;
        foreach (var candidate in candidates)
        {
            if (Satisfies(candidate, required) && (best is not { } current || candidate > current))
            {
                best = candidate;
            }
        }

        return best;
    }
}

/// <summary>
/// Orders plugin definitions so each loads after everything it depends on.
/// <para>
/// The graph algorithm itself — duplicate keys, missing dependencies, unsatisfied minimums and cycles — is
/// <b>delegated</b> to the framework's <see cref="PluginDependencyResolver"/>, which already implements it.
/// This type exists to speak in definitions rather than manifests, not to own a second implementation that
/// would one day disagree with the first.
/// </para>
/// </summary>
public sealed class PluginRuntimeDependencyResolver
{
    /// <summary>Resolves a load order for a set of definitions.</summary>
    /// <param name="definitions">The definitions to order.</param>
    /// <returns>The definitions in dependency-first order, or a failure describing the structural problem.</returns>
    public Result<IReadOnlyList<PluginDefinition>> Resolve(IReadOnlyCollection<PluginDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var byKey = new Dictionary<string, PluginDefinition>(StringComparer.OrdinalIgnoreCase);
        var manifests = new List<PluginManifest>(definitions.Count);

        foreach (var definition in definitions)
        {
            if (!byKey.TryAdd(definition.Key, definition))
            {
                return Result.Failure<IReadOnlyList<PluginDefinition>>(Error.Conflict(
                    "Plugin.Runtime.Dependency.Duplicate",
                    $"Two definitions share the plugin key '{definition.Key}'."));
            }

            manifests.Add(new PluginManifest
            {
                Key = definition.Key,
                Name = definition.Name,
                Version = definition.Version,
                Dependencies = definition.Dependencies,
            });
        }

        var ordered = PluginDependencyResolver.Resolve(manifests);
        return ordered.IsFailure
            ? Result.Failure<IReadOnlyList<PluginDefinition>>(ordered.Error)
            : Result.Success<IReadOnlyList<PluginDefinition>>(
                [.. ordered.Value.Select(manifest => byKey[manifest.Key])]);
    }
}

/// <summary>
/// Checks that a definition can run on this platform at all — before anything is installed, loaded or
/// started. Everything it rejects is something no amount of retrying would fix.
/// </summary>
public sealed class PluginCompatibilityValidator
{
    /// <summary>Validates a definition against the running platform.</summary>
    /// <param name="definition">The definition.</param>
    /// <param name="platform">The running platform version.</param>
    /// <returns>A successful result, or a failure naming the first problem found.</returns>
    public Result Validate(PluginDefinition definition, PluginVersion platform)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Version == default)
        {
            return Result.Failure(Error.Validation(
                "Plugin.Runtime.Compatibility.NoVersion",
                $"Plugin '{definition.Key}' declares no version; a package that cannot be identified cannot be "
                + "updated or rolled back."));
        }

        if (!definition.Compatibility.Supports(platform))
        {
            return Result.Failure(Error.Validation(
                "Plugin.Runtime.Compatibility.Unsupported",
                $"Plugin '{definition.Key}' {definition.Version} supports platform {definition.Compatibility}, "
                + $"but this platform is {platform}."));
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var contribution in definition.Contributions)
        {
            if (!seen.Add(contribution.ToString()))
            {
                return Result.Failure(Error.Conflict(
                    "Plugin.Runtime.Compatibility.DuplicateContribution",
                    $"Plugin '{definition.Key}' contributes '{contribution}' twice; a contribution has to be "
                    + "resolvable by name."));
            }
        }

        return Result.Success();
    }
}

/// <summary>
/// Answers "who extends me?" for one tenant and one published extension point.
/// <para>
/// Only a <b>running</b> instance contributes. A stopped, suspended, failed or disabled plugin is withdrawn
/// from the extension surface rather than left listed, because an engine that resolves a contribution
/// expects to be able to use it — a list that includes plugins which are not there turns every consumer into
/// a place where a missing plugin is discovered.
/// </para>
/// </summary>
public sealed class PluginExtensionPointResolver
{
    private readonly IPluginStore _store;
    private readonly IPluginRepository _repository;

    /// <summary>Initializes a new instance of the <see cref="PluginExtensionPointResolver"/> class.</summary>
    /// <param name="store">The tenant installations.</param>
    /// <param name="repository">The definition catalogue.</param>
    public PluginExtensionPointResolver(IPluginStore store, IPluginRepository repository)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(repository);

        _store = store;
        _repository = repository;
    }

    /// <summary>Lists what extends one point, for one tenant.</summary>
    /// <param name="tenant">The tenant asking.</param>
    /// <param name="kind">The extension point.</param>
    /// <returns>The contributions currently in service.</returns>
    public IReadOnlyList<PluginExtension> Resolve(string tenant, PluginExtensionPointKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var extensions = new List<PluginExtension>();

        foreach (var instance in _store.ForTenant(tenant))
        {
            if (!instance.CanServe)
            {
                continue;
            }

            var definition = _repository.Find(instance.PluginKey, instance.Version);
            if (definition is null)
            {
                continue;
            }

            var point = new PluginExtensionPoint(kind);
            if (!instance.EffectivePermissions(definition).Any(held => held.Grants(point.Permission)))
            {
                // The tenant has not granted this plugin the right to extend this point, so whatever its
                // manifest claims, it contributes nothing here.
                continue;
            }

            foreach (var contribution in definition.ContributionsTo(kind))
            {
                extensions.Add(new PluginExtension(tenant, instance.PluginKey, contribution));
            }
        }

        return extensions;
    }

    /// <summary>Lists everything one tenant contributes, across every published point.</summary>
    /// <param name="tenant">The tenant asking.</param>
    /// <returns>The contributions currently in service.</returns>
    public IReadOnlyList<PluginExtension> ResolveAll(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return [.. PluginExtensionPoints.All().SelectMany(point => Resolve(tenant, point.Kind))];
    }

    /// <summary>Finds one named contribution to a point.</summary>
    /// <param name="tenant">The tenant asking.</param>
    /// <param name="kind">The extension point.</param>
    /// <param name="name">The contribution name.</param>
    /// <returns>The contribution, or <see langword="null"/> when nothing in service offers it.</returns>
    public PluginExtension? Find(string tenant, PluginExtensionPointKind kind, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Resolve(tenant, kind)
            .FirstOrDefault(extension =>
                string.Equals(extension.Contribution.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
