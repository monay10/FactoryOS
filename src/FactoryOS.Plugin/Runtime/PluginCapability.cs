using FactoryOS.Contracts.Plugins;
using FactoryOS.Domain.Results;

namespace FactoryOS.Plugin.Runtime;

/// <summary>
/// A named capability a plugin provides (its manifest <c>provides</c> entries) or that another plugin
/// requires. Capabilities let plugins depend on a feature rather than on a specific plugin key.
/// </summary>
/// <param name="Key">The capability key.</param>
public sealed record PluginCapability(string Key)
{
    /// <summary>Parses a capability key, trimming and lower-casing it for stable comparison.</summary>
    /// <param name="value">The capability key.</param>
    /// <returns>The parsed capability.</returns>
    public static PluginCapability Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new PluginCapability(value.Trim().ToLowerInvariant());
    }

    /// <inheritdoc />
    public override string ToString() => Key;
}

/// <summary>Validates capability requirements against the capabilities a set of plugins provides.</summary>
public static class PluginCapabilityValidator
{
    /// <summary>Collects the distinct capabilities provided across the supplied manifests.</summary>
    /// <param name="manifests">The manifests to inspect.</param>
    /// <returns>The provided capability keys.</returns>
    public static IReadOnlySet<string> ProvidedBy(IEnumerable<PluginManifest> manifests)
    {
        ArgumentNullException.ThrowIfNull(manifests);

        var provided = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var manifest in manifests)
        {
            foreach (var capability in manifest.Provides)
            {
                provided.Add(PluginCapability.Parse(capability).Key);
            }
        }

        return provided;
    }

    /// <summary>Validates that every required capability is provided by some plugin in the set.</summary>
    /// <param name="manifests">The manifests providing capabilities.</param>
    /// <param name="requiredCapabilities">The capabilities that must be satisfied.</param>
    /// <returns>A successful result, or a validation failure naming the first unmet capability.</returns>
    public static Result ValidateRequired(
        IEnumerable<PluginManifest> manifests, IEnumerable<string> requiredCapabilities)
    {
        ArgumentNullException.ThrowIfNull(requiredCapabilities);

        var provided = ProvidedBy(manifests);
        foreach (var required in requiredCapabilities)
        {
            if (!provided.Contains(PluginCapability.Parse(required).Key))
            {
                return Result.Failure(Error.Validation(
                    "Plugin.Capability.Missing",
                    $"No installed plugin provides the required capability '{required}'."));
            }
        }

        return Result.Success();
    }
}
