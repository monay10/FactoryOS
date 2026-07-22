using FactoryOS.Connectors.Framework.Runtime;
using FactoryOS.Connectors.Runtime.Domain;
using FactoryOS.Domain.Results;

namespace FactoryOS.Connectors.Runtime.Discovery;

/// <summary>The outcome of inspecting one candidate connector folder.</summary>
/// <param name="Location">The folder that was inspected.</param>
/// <param name="Definition">The definition read from it, when the manifest was valid.</param>
/// <param name="Error">Why the folder yielded no definition, when it did not.</param>
public sealed record ConnectorDiscoveryResult(string Location, ConnectorDefinition? Definition, Error? Error)
{
    /// <summary>Gets a value indicating whether the folder yielded a definition.</summary>
    public bool Succeeded => Definition is not null;
}

/// <summary>Finds connectors on disk by scanning for manifests.</summary>
public interface IConnectorDiscovery
{
    /// <summary>Scans the immediate subfolders of a root for connector manifests.</summary>
    /// <param name="rootDirectory">The directory whose child folders each hold one connector.</param>
    /// <returns>
    /// One result per candidate folder, in a stable order. A folder whose manifest is invalid is reported
    /// with its error rather than skipped: a connector that silently fails to appear is a support call, and
    /// a connector that appears with its problem stated is a fix.
    /// </returns>
    IReadOnlyList<ConnectorDiscoveryResult> Discover(string rootDirectory);
}

/// <summary>
/// The default <see cref="IConnectorDiscovery"/>: each immediate subfolder of the root that contains a
/// <c>connector.json</c> is one connector. This is the same convention the plugin framework uses for
/// <c>module.json</c>, deliberately — a factory engineer should learn the layout once.
/// </summary>
public sealed class ConnectorDiscovery : IConnectorDiscovery
{
    /// <inheritdoc />
    public IReadOnlyList<ConnectorDiscoveryResult> Discover(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        if (!Directory.Exists(rootDirectory))
        {
            return [];
        }

        var results = new List<ConnectorDiscoveryResult>();

        foreach (var directory in Directory
                     .EnumerateDirectories(rootDirectory)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            var manifestPath = Path.Combine(directory, ConnectorRuntimeManifestReader.ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            var read = ConnectorRuntimeManifestReader.ReadFile(manifestPath);
            results.Add(read.IsSuccess
                ? new ConnectorDiscoveryResult(directory, read.Value, null)
                : new ConnectorDiscoveryResult(directory, null, read.Error));
        }

        return results;
    }
}

/// <summary>
/// Answers "which connector can do this?" without the caller naming one. A business module asking for a
/// connector by key would be naming a vendor; asking for a capability is asking the core to look one up.
/// </summary>
public sealed class CapabilityResolver
{
    /// <summary>Selects the definitions that declare every requested capability.</summary>
    /// <param name="definitions">The definitions to search.</param>
    /// <param name="required">The capabilities required.</param>
    /// <returns>The matching definitions, ordered by key.</returns>
    public IReadOnlyList<ConnectorDefinition> Resolve(
        IEnumerable<ConnectorDefinition> definitions, ConnectorCapability required)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        return [.. definitions.Where(definition => definition.Supports(required))
            .OrderBy(definition => definition.Key, StringComparer.Ordinal)];
    }

    /// <summary>Determines whether a definition can serve an operation.</summary>
    /// <param name="definition">The definition.</param>
    /// <param name="operation">The operation.</param>
    /// <returns><see langword="true"/> when the definition declares the capability the operation exercises.</returns>
    public bool CanServe(ConnectorDefinition definition, ConnectorOperation operation)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(operation);
        return definition.Supports(operation.Capability);
    }
}

/// <summary>
/// Chooses which version of a connector to use when more than one is present, and answers whether a version
/// satisfies a requirement.
/// </summary>
public sealed class VersionResolver
{
    /// <summary>Selects the highest version among definitions sharing a key.</summary>
    /// <param name="definitions">The candidates.</param>
    /// <param name="key">The definition key.</param>
    /// <returns>The highest-versioned definition, or <see langword="null"/> when none carries that key.</returns>
    public ConnectorDefinition? Highest(IEnumerable<ConnectorDefinition> definitions, string key)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        ConnectorDefinition? best = null;
        foreach (var definition in definitions)
        {
            if (!string.Equals(definition.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (best is null || definition.Version > best.Version)
            {
                best = definition;
            }
        }

        return best;
    }

    /// <summary>
    /// Determines whether an available version satisfies a required minimum. Compatibility is
    /// same-major-and-at-least: a connector may add operations within a major version, but a new major
    /// version is a new contract and must be adopted deliberately.
    /// </summary>
    /// <param name="available">The version present.</param>
    /// <param name="required">The minimum version required.</param>
    /// <returns><see langword="true"/> when the available version satisfies the requirement.</returns>
    public bool Satisfies(ConnectorVersion available, ConnectorVersion required) =>
        available.Major == required.Major && available >= required;
}

/// <summary>
/// Checks that a definition is internally coherent before the runtime loads it. Every problem it reports is
/// one that would otherwise surface as a confusing failure at invocation time, on a shift, in a factory.
/// </summary>
public sealed class CompatibilityValidator
{
    /// <summary>Validates a definition.</summary>
    /// <param name="definition">The definition to validate.</param>
    /// <returns>A successful result, or a failure naming the first incoherence found.</returns>
    public Result Validate(ConnectorDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Capabilities == ConnectorCapability.None)
        {
            return Result.Failure(Error.Validation(
                "Connector.Compatibility.NoCapabilities",
                $"Connector '{definition.Key}' declares no capabilities, so nothing could ever be asked of it."));
        }

        if (definition.Operations.Count == 0)
        {
            return Result.Failure(Error.Validation(
                "Connector.Compatibility.NoOperations",
                $"Connector '{definition.Key}' declares no operations."));
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var operation in definition.Operations)
        {
            if (!seen.Add(operation.Name))
            {
                return Result.Failure(Error.Conflict(
                    "Connector.Compatibility.DuplicateOperation",
                    $"Connector '{definition.Key}' declares operation '{operation.Name}' more than once."));
            }

            if (!definition.Supports(operation.Capability))
            {
                return Result.Failure(Error.Validation(
                    "Connector.Compatibility.UndeclaredCapability",
                    $"Operation '{operation.Name}' exercises '{operation.Capability}', which connector "
                    + $"'{definition.Key}' does not declare."));
            }

            if (operation is { Idempotent: false, Cacheable: true })
            {
                return Result.Failure(Error.Validation(
                    "Connector.Compatibility.CacheableSideEffect",
                    $"Operation '{operation.Name}' is cacheable but not idempotent; reusing the response of an "
                    + "operation with side effects would hide the side effect from its caller."));
            }
        }

        return Result.Success();
    }

    /// <summary>Determines whether a loaded definition satisfies what an instance requires of it.</summary>
    /// <param name="definition">The loaded definition.</param>
    /// <param name="instance">The instance.</param>
    /// <returns>A successful result, or a failure explaining the mismatch.</returns>
    public Result ValidateInstance(ConnectorDefinition definition, ConnectorInstance instance)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(instance);

        if (!string.Equals(definition.Key, instance.DefinitionKey, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(Error.Validation(
                "Connector.Compatibility.WrongDefinition",
                $"Instance '{instance.Key}' activates '{instance.DefinitionKey}', not '{definition.Key}'."));
        }

        if (instance.MinimumVersion is { } required && !new VersionResolver().Satisfies(definition.Version, required))
        {
            return Result.Failure(Error.Validation(
                "Connector.Compatibility.VersionMismatch",
                $"Instance '{instance.Key}' requires connector '{definition.Key}' {required} or a later version "
                + $"of the same major, but {definition.Version} is loaded."));
        }

        return Result.Success();
    }
}
