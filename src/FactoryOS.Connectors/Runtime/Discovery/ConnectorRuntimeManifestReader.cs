using System.Text.Json;
using FactoryOS.Connectors.Framework.Runtime;
using FactoryOS.Connectors.Manifest;
using FactoryOS.Connectors.Runtime.Domain;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Domain.Results;

namespace FactoryOS.Connectors.Runtime.Discovery;

/// <summary>
/// Reads a <c>connector.json</c> into a <see cref="ConnectorDefinition"/>.
/// <para>
/// The base fields are read by the existing <see cref="ConnectorManifestReader"/> — this reader does not
/// re-validate them, so there is exactly one place that decides whether a manifest is well-formed. What it
/// adds are the runtime fields the contract manifest deliberately never carried: the version, the capability
/// surface, the category and the operations. All four are <b>optional</b>: a manifest written before this
/// runtime existed still reads, and gets sensible defaults.
/// </para>
/// </summary>
public static class ConnectorRuntimeManifestReader
{
    /// <summary>The conventional manifest file name every connector folder contains.</summary>
    public const string ManifestFileName = "connector.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Reads a definition from manifest JSON.</summary>
    /// <param name="json">The manifest JSON.</param>
    /// <param name="location">The directory the manifest was read from, if any.</param>
    /// <returns>A successful result with the definition, or a failure describing the problem.</returns>
    public static Result<ConnectorDefinition> Read(string json, string? location = null)
    {
        var manifest = ConnectorManifestReader.Read(json);
        if (manifest.IsFailure)
        {
            return Result.Failure<ConnectorDefinition>(manifest.Error);
        }

        RuntimeSection? runtime;
        try
        {
            runtime = JsonSerializer.Deserialize<RuntimeSection>(json, SerializerOptions);
        }
        catch (JsonException exception)
        {
            return Result.Failure<ConnectorDefinition>(Error.Validation(
                "Connector.Runtime.Manifest.Malformed",
                $"The connector manifest's runtime fields are not valid JSON: {exception.Message}"));
        }

        return Build(manifest.Value, runtime, location);
    }

    /// <summary>Reads a definition from a manifest file on disk.</summary>
    /// <param name="path">The path to the manifest.</param>
    /// <returns>A successful result with the definition, or a failure describing the problem.</returns>
    public static Result<ConnectorDefinition> ReadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return Result.Failure<ConnectorDefinition>(Error.NotFound(
                "Connector.Runtime.Manifest.NotFound", $"No connector manifest was found at '{path}'."));
        }

        return Read(File.ReadAllText(path), Path.GetDirectoryName(path));
    }

    private static Result<ConnectorDefinition> Build(
        ConnectorManifest manifest, RuntimeSection? runtime, string? location)
    {
        var version = new ConnectorVersion(1, 0, 0);
        if (!string.IsNullOrWhiteSpace(runtime?.Version) && !ConnectorVersion.TryParse(runtime.Version, out version))
        {
            return Result.Failure<ConnectorDefinition>(Error.Validation(
                "Connector.Runtime.Manifest.Version",
                $"'{runtime.Version}' is not a valid connector version (expected 'Major.Minor.Patch')."));
        }

        ConnectorCapability capabilities;
        try
        {
            capabilities = string.IsNullOrWhiteSpace(runtime?.Capabilities)
                ? ConnectorCapability.Read
                : ConnectorCapabilities.Parse(runtime.Capabilities);
        }
        catch (FormatException exception)
        {
            return Result.Failure<ConnectorDefinition>(Error.Validation(
                "Connector.Runtime.Manifest.Capabilities", exception.Message));
        }

        var category = ConnectorCategory.Unknown;
        if (!string.IsNullOrWhiteSpace(runtime?.Category)
            && !Enum.TryParse(runtime.Category, ignoreCase: true, out category))
        {
            return Result.Failure<ConnectorDefinition>(Error.Validation(
                "Connector.Runtime.Manifest.Category",
                $"'{runtime.Category}' is not a connector category the runtime recognises."));
        }

        var definition = ConnectorDefinition.FromManifest(manifest, version, capabilities, category, location);
        if (runtime?.Operations is not { Count: > 0 })
        {
            return Result.Success(definition);
        }

        var operations = new List<ConnectorOperation>();
        foreach (var declared in runtime.Operations)
        {
            var operation = ReadOperation(declared, capabilities);
            if (operation.IsFailure)
            {
                return Result.Failure<ConnectorDefinition>(operation.Error);
            }

            operations.Add(operation.Value);
        }

        return Result.Success(definition with { Operations = operations });
    }

    private static Result<ConnectorOperation> ReadOperation(OperationSection declared, ConnectorCapability declaredSet)
    {
        if (string.IsNullOrWhiteSpace(declared.Name))
        {
            return Result.Failure<ConnectorOperation>(Error.Validation(
                "Connector.Runtime.Manifest.Operation", "A declared operation is missing 'name'."));
        }

        ConnectorCapability capability;
        try
        {
            capability = string.IsNullOrWhiteSpace(declared.Capability)
                ? ConnectorCapability.Read
                : ConnectorCapabilities.Parse(declared.Capability);
        }
        catch (FormatException exception)
        {
            return Result.Failure<ConnectorOperation>(Error.Validation(
                "Connector.Runtime.Manifest.Operation", exception.Message));
        }

        if (!declaredSet.Supports(capability))
        {
            return Result.Failure<ConnectorOperation>(Error.Validation(
                "Connector.Runtime.Manifest.Operation",
                $"Operation '{declared.Name}' exercises '{capability}', which the connector does not declare."));
        }

        var permission = ConnectorPermissions.For(capability);
        if (!string.IsNullOrWhiteSpace(declared.Permission)
            && !ConnectorPermission.TryParse(declared.Permission, out permission))
        {
            return Result.Failure<ConnectorOperation>(Error.Validation(
                "Connector.Runtime.Manifest.Operation",
                $"'{declared.Permission}' is not a valid permission (expected 'resource.action')."));
        }

        return Result.Success(new ConnectorOperation(declared.Name, capability, permission)
        {
            Description = declared.Description,
            Idempotent = declared.Idempotent,
            Cacheable = declared.Cacheable,
            RequiredParameters = declared.RequiredParameters ?? [],
            Timeout = declared.TimeoutSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null,
        });
    }

    private sealed class RuntimeSection
    {
        public string? Version { get; set; }

        public string? Capabilities { get; set; }

        public string? Category { get; set; }

        public List<OperationSection>? Operations { get; set; }
    }

    private sealed class OperationSection
    {
        public string? Name { get; set; }

        public string? Capability { get; set; }

        public string? Permission { get; set; }

        public string? Description { get; set; }

        public bool Idempotent { get; set; }

        public bool Cacheable { get; set; }

        public List<string>? RequiredParameters { get; set; }

        public double? TimeoutSeconds { get; set; }
    }
}
