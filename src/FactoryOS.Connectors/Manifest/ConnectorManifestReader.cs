using System.Text.Json;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Domain.Results;

namespace FactoryOS.Connectors.Manifest;

/// <summary>
/// Reads a connector <c>connector.json</c> manifest into a strongly-typed <see cref="ConnectorManifest"/>,
/// validating required fields. Mapping is data, not code.
/// </summary>
public static class ConnectorManifestReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Reads a connector manifest from its JSON text.</summary>
    /// <param name="json">The manifest JSON.</param>
    /// <returns>A successful result with the manifest, or a failure describing the problem.</returns>
    public static Result<ConnectorManifest> Read(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result.Failure<ConnectorManifest>(
                Error.Validation("Connector.Manifest.Empty", "The connector manifest is empty."));
        }

        ManifestDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ManifestDto>(json, SerializerOptions);
        }
        catch (JsonException exception)
        {
            return Result.Failure<ConnectorManifest>(Error.Validation(
                "Connector.Manifest.Malformed", $"The connector manifest is not valid JSON: {exception.Message}"));
        }

        if (dto is null)
        {
            return Result.Failure<ConnectorManifest>(
                Error.Validation("Connector.Manifest.Empty", "The connector manifest deserialized to nothing."));
        }

        if (string.IsNullOrWhiteSpace(dto.Key))
        {
            return Result.Failure<ConnectorManifest>(
                Error.Validation("Connector.Manifest.MissingKey", "The connector manifest is missing 'key'."));
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Result.Failure<ConnectorManifest>(
                Error.Validation("Connector.Manifest.MissingName", "The connector manifest is missing 'name'."));
        }

        if (string.IsNullOrWhiteSpace(dto.SourceSystem))
        {
            return Result.Failure<ConnectorManifest>(Error.Validation(
                "Connector.Manifest.MissingSourceSystem", "The connector manifest is missing 'sourceSystem'."));
        }

        return Result.Success(new ConnectorManifest
        {
            Key = dto.Key,
            Name = dto.Name,
            SourceSystem = dto.SourceSystem,
            Description = dto.Description,
            Provides = dto.Provides ?? [],
            Mapping = dto.Mapping,
        });
    }

    /// <summary>Reads a connector manifest from a file on disk.</summary>
    /// <param name="path">The path to the manifest file.</param>
    /// <returns>A successful result with the manifest, or a failure describing the problem.</returns>
    public static Result<ConnectorManifest> ReadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return Result.Failure<ConnectorManifest>(
                Error.NotFound("Connector.Manifest.NotFound", $"No connector manifest was found at '{path}'."));
        }

        return Read(File.ReadAllText(path));
    }

    private sealed class ManifestDto
    {
        public string? Key { get; set; }

        public string? Name { get; set; }

        public string? SourceSystem { get; set; }

        public string? Description { get; set; }

        public List<string>? Provides { get; set; }

        public string? Mapping { get; set; }
    }
}
