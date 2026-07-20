using System.Text.Json;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Domain.Results;

namespace FactoryOS.Connectors.Manifest;

/// <summary>
/// Reads a <c>mapping.json</c> into a strongly-typed <see cref="MappingManifest"/>. JSON scalar values
/// for constants and defaults are converted to plain CLR primitives so the normalizer never sees a raw
/// <see cref="JsonElement"/>.
/// </summary>
public static class MappingManifestReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Reads a mapping manifest from its JSON text.</summary>
    /// <param name="json">The mapping JSON.</param>
    /// <returns>A successful result with the manifest, or a failure describing the problem.</returns>
    public static Result<MappingManifest> Read(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result.Failure<MappingManifest>(
                Error.Validation("Connector.Mapping.Empty", "The mapping manifest is empty."));
        }

        ManifestDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ManifestDto>(json, SerializerOptions);
        }
        catch (JsonException exception)
        {
            return Result.Failure<MappingManifest>(Error.Validation(
                "Connector.Mapping.Malformed", $"The mapping manifest is not valid JSON: {exception.Message}"));
        }

        if (dto is null || string.IsNullOrWhiteSpace(dto.SourceSystem))
        {
            return Result.Failure<MappingManifest>(
                Error.Validation("Connector.Mapping.MissingSourceSystem", "The mapping manifest is missing 'sourceSystem'."));
        }

        var entities = new List<EntityMapping>();
        foreach (var entity in dto.Entities ?? [])
        {
            if (string.IsNullOrWhiteSpace(entity.SourceEntity) || string.IsNullOrWhiteSpace(entity.TargetEntity))
            {
                return Result.Failure<MappingManifest>(Error.Validation(
                    "Connector.Mapping.InvalidEntity", "Every entity mapping must declare 'sourceEntity' and 'targetEntity'."));
            }

            if (entity.NaturalKey is not { Count: > 0 })
            {
                return Result.Failure<MappingManifest>(Error.Validation(
                    "Connector.Mapping.MissingNaturalKey",
                    $"Entity mapping '{entity.SourceEntity}' must declare a non-empty 'naturalKey'."));
            }

            var fields = new List<FieldMapping>();
            foreach (var field in entity.Fields ?? [])
            {
                if (string.IsNullOrWhiteSpace(field.Target))
                {
                    return Result.Failure<MappingManifest>(Error.Validation(
                        "Connector.Mapping.InvalidField", $"A field mapping in '{entity.SourceEntity}' is missing 'target'."));
                }

                fields.Add(new FieldMapping
                {
                    Target = field.Target,
                    Source = field.Source,
                    Transform = field.Transform,
                    Constant = ConvertScalar(field.Constant),
                    Default = ConvertScalar(field.Default),
                    Required = field.Required,
                });
            }

            entities.Add(new EntityMapping
            {
                SourceEntity = entity.SourceEntity,
                TargetEntity = entity.TargetEntity,
                NaturalKey = entity.NaturalKey,
                Fields = fields,
            });
        }

        return Result.Success(new MappingManifest
        {
            SourceSystem = dto.SourceSystem,
            Entities = entities,
        });
    }

    /// <summary>Reads a mapping manifest from a file on disk.</summary>
    /// <param name="path">The path to the mapping file.</param>
    /// <returns>A successful result with the manifest, or a failure describing the problem.</returns>
    public static Result<MappingManifest> ReadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return Result.Failure<MappingManifest>(
                Error.NotFound("Connector.Mapping.NotFound", $"No mapping manifest was found at '{path}'."));
        }

        return Read(File.ReadAllText(path));
    }

    private static object? ConvertScalar(JsonElement? element)
    {
        if (element is not { } value)
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => value.TryGetInt64(out var integer) ? integer : (object)value.GetDecimal(),
            _ => null,
        };
    }

    private sealed class ManifestDto
    {
        public string? SourceSystem { get; set; }

        public List<EntityDto>? Entities { get; set; }
    }

    private sealed class EntityDto
    {
        public string? SourceEntity { get; set; }

        public string? TargetEntity { get; set; }

        public List<string>? NaturalKey { get; set; }

        public List<FieldDto>? Fields { get; set; }
    }

    private sealed class FieldDto
    {
        public string? Target { get; set; }

        public string? Source { get; set; }

        public string? Transform { get; set; }

        public JsonElement? Constant { get; set; }

        public JsonElement? Default { get; set; }

        public bool Required { get; set; }
    }
}
