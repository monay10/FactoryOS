using System.Text.Json;
using System.Text.Json.Serialization;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Domain.Results;
using FactoryOS.Plugin.Manifest;
using FactoryOS.Plugins.Runtime.Domain;

namespace FactoryOS.Plugins.Runtime.Discovery;

/// <summary>
/// What a package manifest yields once the runtime has read it: the framework manifest exactly as before,
/// the runtime's projection of it, and the signature it declares.
/// </summary>
/// <param name="Manifest">The framework manifest.</param>
/// <param name="Definition">The runtime definition projected from it.</param>
/// <param name="Signature">The signature the manifest declares, or <see cref="PluginSignature.None"/>.</param>
public sealed record PluginManifestReadResult(
    PluginManifest Manifest, PluginDefinition Definition, PluginSignature Signature);

/// <summary>
/// Reads a <c>module.json</c> into a runtime definition.
/// <para>
/// Base validation is <b>delegated</b> to the framework's <see cref="PluginManifestReader"/> — key, name,
/// version, dependencies, UI screens and API routes are parsed and validated exactly once, by the code that
/// already did it. This reader adds only the four fields the runtime introduces, all of them optional, so a
/// manifest written before this runtime existed still reads cleanly and gets conservative defaults.
/// </para>
/// </summary>
public static class PluginRuntimeManifestReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Reads a manifest from its JSON text.</summary>
    /// <param name="json">The manifest JSON.</param>
    /// <param name="location">The folder it was read from, if any.</param>
    /// <returns>A successful result, or a failure describing the problem.</returns>
    public static Result<PluginManifestReadResult> Read(string json, string? location = null)
    {
        var manifest = PluginManifestReader.Read(json);
        if (manifest.IsFailure)
        {
            return Result.Failure<PluginManifestReadResult>(manifest.Error);
        }

        RuntimeSectionDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<RuntimeSectionDto>(json, SerializerOptions);
        }
        catch (JsonException exception)
        {
            return Result.Failure<PluginManifestReadResult>(Error.Validation(
                "Plugin.Runtime.Manifest.Malformed",
                $"The plugin manifest's runtime section is not valid JSON: {exception.Message}"));
        }

        var definition = PluginDefinition.FromManifest(manifest.Value, location);

        var contributions = ReadContributions(dto, definition);
        if (contributions.IsFailure)
        {
            return Result.Failure<PluginManifestReadResult>(contributions.Error);
        }

        var permissions = ReadPermissions(dto);
        if (permissions.IsFailure)
        {
            return Result.Failure<PluginManifestReadResult>(permissions.Error);
        }

        var isolation = ReadIsolation(dto);
        if (isolation.IsFailure)
        {
            return Result.Failure<PluginManifestReadResult>(isolation.Error);
        }

        var compatibility = ReadCompatibility(dto);
        if (compatibility.IsFailure)
        {
            return Result.Failure<PluginManifestReadResult>(compatibility.Error);
        }

        var signature = ReadSignature(dto);
        if (signature.IsFailure)
        {
            return Result.Failure<PluginManifestReadResult>(signature.Error);
        }

        definition = definition with
        {
            Contributions = contributions.Value,
            RequestedPermissions = permissions.Value,
            RequiredCapabilities = dto?.Requires ?? [],
            Isolation = isolation.Value,
            Compatibility = compatibility.Value,
        };

        return new PluginManifestReadResult(manifest.Value, definition, signature.Value);
    }

    /// <summary>Reads a manifest from a file on disk.</summary>
    /// <param name="path">The path to the manifest file.</param>
    /// <returns>A successful result, or a failure describing the problem.</returns>
    public static Result<PluginManifestReadResult> ReadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return Result.Failure<PluginManifestReadResult>(Error.NotFound(
                "Plugin.Runtime.Manifest.NotFound", $"No manifest was found at '{path}'."));
        }

        return Read(File.ReadAllText(path), Path.GetDirectoryName(path));
    }

    private static Result<IReadOnlyList<PluginContribution>> ReadContributions(
        RuntimeSectionDto? dto, PluginDefinition definition)
    {
        // The UI screens and API routes the framework manifest already carries are contributions in their
        // own right; the definition names them. An 'extends' block adds the rest.
        var contributions = new List<PluginContribution>(definition.Contributions);

        foreach (var entry in dto?.Extends ?? [])
        {
            if (!PluginExtensionPoints.TryParse(entry.Point, out var point))
            {
                return Result.Failure<IReadOnlyList<PluginContribution>>(Error.Validation(
                    "Plugin.Runtime.Manifest.UnknownExtensionPoint",
                    $"'{entry.Point}' is not a published extension point. A plugin may only extend what the "
                    + "platform publishes."));
            }

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                return Result.Failure<IReadOnlyList<PluginContribution>>(Error.Validation(
                    "Plugin.Runtime.Manifest.InvalidContribution",
                    $"A contribution to '{point.Key}' is missing 'name'."));
            }

            contributions.Add(new PluginContribution(point, entry.Name.Trim())
            {
                Description = entry.Description,
                Reference = entry.Reference,
            });
        }

        return contributions;
    }

    private static Result<IReadOnlyList<PluginPermission>> ReadPermissions(RuntimeSectionDto? dto)
    {
        var permissions = new List<PluginPermission>();

        foreach (var value in dto?.Permissions ?? [])
        {
            if (!PluginPermission.TryParse(value, out var permission))
            {
                return Result.Failure<IReadOnlyList<PluginPermission>>(Error.Validation(
                    "Plugin.Runtime.Manifest.InvalidPermission",
                    $"'{value}' is not a valid permission (expected 'resource.action')."));
            }

            permissions.Add(permission);
        }

        return permissions;
    }

    private static Result<PluginIsolationMode> ReadIsolation(RuntimeSectionDto? dto)
    {
        if (string.IsNullOrWhiteSpace(dto?.Isolation))
        {
            return PluginIsolationMode.AssemblyIsolated;
        }

        return Enum.TryParse<PluginIsolationMode>(dto.Isolation, ignoreCase: true, out var mode)
            ? mode
            : Result.Failure<PluginIsolationMode>(Error.Validation(
                "Plugin.Runtime.Manifest.InvalidIsolation",
                $"'{dto.Isolation}' is not a known isolation mode."));
    }

    private static Result<PluginCompatibility> ReadCompatibility(RuntimeSectionDto? dto)
    {
        if (dto?.Compatibility is not { } compatibility)
        {
            return PluginCompatibility.Any;
        }

        var minimum = new PluginVersion(0, 0, 0);
        if (!string.IsNullOrWhiteSpace(compatibility.MinimumPlatform)
            && !PluginVersion.TryParse(compatibility.MinimumPlatform, out minimum))
        {
            return Result.Failure<PluginCompatibility>(Error.Validation(
                "Plugin.Runtime.Manifest.InvalidCompatibility",
                $"'{compatibility.MinimumPlatform}' is not a valid platform version."));
        }

        PluginVersion? maximum = null;
        if (!string.IsNullOrWhiteSpace(compatibility.MaximumPlatform))
        {
            if (!PluginVersion.TryParse(compatibility.MaximumPlatform, out var parsed))
            {
                return Result.Failure<PluginCompatibility>(Error.Validation(
                    "Plugin.Runtime.Manifest.InvalidCompatibility",
                    $"'{compatibility.MaximumPlatform}' is not a valid platform version."));
            }

            maximum = parsed;
        }

        if (maximum is { } ceiling && ceiling < minimum)
        {
            return Result.Failure<PluginCompatibility>(Error.Validation(
                "Plugin.Runtime.Manifest.InvalidCompatibility",
                $"The compatibility ceiling {ceiling} is below its floor {minimum}."));
        }

        return new PluginCompatibility(minimum) { MaximumPlatform = maximum };
    }

    private static Result<PluginSignature> ReadSignature(RuntimeSectionDto? dto)
    {
        if (dto?.Signature is not { } signature)
        {
            return PluginSignature.None;
        }

        if (string.IsNullOrWhiteSpace(signature.Value))
        {
            return PluginSignature.None;
        }

        if (!Enum.TryParse<PluginSignatureAlgorithm>(signature.Algorithm, ignoreCase: true, out var algorithm)
            || algorithm == PluginSignatureAlgorithm.None)
        {
            return Result.Failure<PluginSignature>(Error.Validation(
                "Plugin.Runtime.Manifest.InvalidSignature",
                $"'{signature.Algorithm}' is not a supported signature algorithm."));
        }

        if (string.IsNullOrWhiteSpace(signature.KeyId))
        {
            return Result.Failure<PluginSignature>(Error.Validation(
                "Plugin.Runtime.Manifest.InvalidSignature",
                "A signature must name the key that produced it."));
        }

        return new PluginSignature(algorithm, signature.Value.Trim()) { KeyId = signature.KeyId.Trim() };
    }

    private sealed class RuntimeSectionDto
    {
        public List<ExtendsDto>? Extends { get; set; }

        public List<string>? Permissions { get; set; }

        public List<string>? Requires { get; set; }

        public string? Isolation { get; set; }

        public CompatibilityDto? Compatibility { get; set; }

        public SignatureDto? Signature { get; set; }
    }

    private sealed class ExtendsDto
    {
        public string? Point { get; set; }

        public string? Name { get; set; }

        public string? Reference { get; set; }

        public string? Description { get; set; }
    }

    private sealed class CompatibilityDto
    {
        [JsonPropertyName("minimumPlatform")]
        public string? MinimumPlatform { get; set; }

        [JsonPropertyName("maximumPlatform")]
        public string? MaximumPlatform { get; set; }
    }

    private sealed class SignatureDto
    {
        public string? Algorithm { get; set; }

        public string? Value { get; set; }

        [JsonPropertyName("keyId")]
        public string? KeyId { get; set; }
    }
}
