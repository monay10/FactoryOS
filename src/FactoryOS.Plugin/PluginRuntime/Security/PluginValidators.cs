using FactoryOS.Contracts.Plugins;
using FactoryOS.Domain.Results;
using FactoryOS.Plugin.Runtime;
using FactoryOS.Plugins.Runtime.Domain;

namespace FactoryOS.Plugins.Runtime.Security;

/// <summary>
/// Checks that a definition is internally coherent before anything acts on it. Everything here is a problem
/// with the package itself, independent of the platform it lands on or the tenant that installs it.
/// </summary>
public sealed class PluginManifestValidator
{
    /// <summary>Validates a definition.</summary>
    /// <param name="definition">The definition.</param>
    /// <returns>A successful result, or a failure naming the first problem found.</returns>
    public Result Validate(PluginDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (string.IsNullOrWhiteSpace(definition.Key))
        {
            return Result.Failure(Error.Validation(
                "Plugin.Runtime.Manifest.NoKey", "A plugin definition must declare a key."));
        }

        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            return Result.Failure(Error.Validation(
                "Plugin.Runtime.Manifest.NoName", $"Plugin '{definition.Key}' declares no name."));
        }

        if (definition.Dependencies.Any(dependency =>
            string.Equals(dependency.PluginKey, definition.Key, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure(Error.Validation(
                "Plugin.Runtime.Manifest.SelfDependency",
                $"Plugin '{definition.Key}' declares a dependency on itself."));
        }

        var capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var capability in definition.Capabilities)
        {
            if (string.IsNullOrWhiteSpace(capability))
            {
                return Result.Failure(Error.Validation(
                    "Plugin.Runtime.Manifest.EmptyCapability",
                    $"Plugin '{definition.Key}' declares an empty capability."));
            }

            if (!capabilities.Add(capability.Trim()))
            {
                return Result.Failure(Error.Conflict(
                    "Plugin.Runtime.Manifest.DuplicateCapability",
                    $"Plugin '{definition.Key}' declares capability '{capability}' twice."));
            }
        }

        if (definition.Location is not null
            && !string.IsNullOrWhiteSpace(definition.EntryType)
            && string.IsNullOrWhiteSpace(definition.Assembly))
        {
            return Result.Failure(Error.Validation(
                "Plugin.Runtime.Manifest.NoAssembly",
                $"Plugin '{definition.Key}' names an entry type but no assembly to find it in."));
        }

        return Result.Success();
    }
}

/// <summary>
/// Checks that everything a plugin asks for has been granted by the tenant installing it.
/// <para>
/// The check happens at <b>install</b> and again at <b>start</b>, not only once. A grant can be revoked
/// after installation, and a plugin whose permissions were withdrawn while it was stopped must not quietly
/// come back with the reach it used to have.
/// </para>
/// </summary>
public sealed class PluginPermissionValidator
{
    /// <summary>Validates what a tenant has granted against what a definition asks for.</summary>
    /// <param name="instance">The tenant's installation.</param>
    /// <param name="definition">The definition it installs.</param>
    /// <returns>A successful result, or a failure naming what is ungranted.</returns>
    public Result Validate(PluginInstance instance, PluginDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(definition);

        var ungranted = instance.UngrantedRequests(definition);
        if (ungranted.Count == 0)
        {
            return Result.Success();
        }

        var names = string.Join(", ", ungranted.Select(permission => permission.ToString()));
        return Result.Failure(Error.Validation(
            "Plugin.Runtime.Permission.Ungranted",
            $"Tenant '{instance.Tenant}' has not granted plugin '{definition.Key}' the permissions it "
            + $"requires: {names}."));
    }
}

/// <summary>
/// Checks that every capability a definition requires is provided by something the tenant actually runs.
/// <para>
/// The set-membership logic itself is <b>delegated</b> to the framework's
/// <see cref="PluginCapabilityValidator"/>; this type exists to decide <i>which</i> plugins count as
/// providers — only the ones that tenant has installed, never the whole catalogue.
/// </para>
/// </summary>
public sealed class PluginCapabilityRequirementValidator
{
    /// <summary>Validates a definition's required capabilities against a set of providers.</summary>
    /// <param name="definition">The definition whose requirements are checked.</param>
    /// <param name="providers">The definitions available to satisfy them.</param>
    /// <returns>A successful result, or a failure naming the first unmet capability.</returns>
    public Result Validate(PluginDefinition definition, IEnumerable<PluginDefinition> providers)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(providers);

        if (definition.RequiredCapabilities.Count == 0)
        {
            return Result.Success();
        }

        var manifests = providers
            .Where(provider => !string.Equals(provider.Key, definition.Key, StringComparison.OrdinalIgnoreCase))
            .Select(provider => new PluginManifest
            {
                Key = provider.Key,
                Name = provider.Name,
                Version = provider.Version,
                Provides = provider.Capabilities,
            })
            .ToArray();

        var validated = PluginCapabilityValidator.ValidateRequired(manifests, definition.RequiredCapabilities);
        return validated.IsSuccess
            ? Result.Success()
            : Result.Failure(Error.Validation(
                "Plugin.Runtime.Capability.Missing",
                $"Plugin '{definition.Key}' requires a capability nothing installed provides: "
                + validated.Error.Description));
    }
}
