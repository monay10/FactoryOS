using System.Globalization;
using FactoryOS.Connectors.Transforms;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Domain.Results;

namespace FactoryOS.Connectors.Normalization;

/// <summary>
/// Default <see cref="IRecordNormalizer"/>. For each field mapping it resolves a raw value (constant,
/// source field, or default), applies the named transform, enforces required fields, and finally builds
/// the natural key from the mapped key fields.
/// </summary>
public sealed class RecordNormalizer : IRecordNormalizer
{
    private readonly IValueTransformer _transformer;

    /// <summary>Initializes a new instance of the <see cref="RecordNormalizer"/> class.</summary>
    /// <param name="transformer">The value transformer used to apply field transforms.</param>
    public RecordNormalizer(IValueTransformer transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);
        _transformer = transformer;
    }

    /// <inheritdoc />
    public Result<NormalizedRecord> Normalize(SourceRecord record, MappingManifest mapping, string tenant)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var entityMapping = mapping.FindEntity(record.SourceEntity);
        if (entityMapping is null)
        {
            return Result.Failure<NormalizedRecord>(Error.NotFound(
                "Connector.Normalize.NoMapping",
                $"No mapping is declared for source entity '{record.SourceEntity}'."));
        }

        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in entityMapping.Fields)
        {
            var valueResult = ResolveField(record, field);
            if (valueResult.IsFailure)
            {
                return Result.Failure<NormalizedRecord>(valueResult.Error);
            }

            values[field.Target] = valueResult.Value;
        }

        var naturalKeyResult = BuildNaturalKey(entityMapping, values);
        if (naturalKeyResult.IsFailure)
        {
            return Result.Failure<NormalizedRecord>(naturalKeyResult.Error);
        }

        return Result.Success(new NormalizedRecord(
            tenant,
            mapping.SourceSystem,
            entityMapping.TargetEntity,
            naturalKeyResult.Value,
            values));
    }

    private Result<object?> ResolveField(SourceRecord record, FieldMapping field)
    {
        object? raw = field.Constant;

        if (raw is null && field.Source is not null && record.Fields.TryGetValue(field.Source, out var sourceValue))
        {
            raw = sourceValue;
        }

        raw ??= field.Default;

        var transformed = _transformer.Apply(field.Transform, raw);
        if (transformed.IsFailure)
        {
            return transformed;
        }

        var value = transformed.Value ?? field.Default;

        if (field.Required && value is null)
        {
            return Result.Failure<object?>(Error.Validation(
                "Connector.Normalize.RequiredFieldMissing",
                $"Required field '{field.Target}' produced no value."));
        }

        return Result.Success(value);
    }

    private static Result<string> BuildNaturalKey(EntityMapping mapping, Dictionary<string, object?> values)
    {
        var parts = new List<string>(mapping.NaturalKey.Count);
        foreach (var keyField in mapping.NaturalKey)
        {
            if (!values.TryGetValue(keyField, out var value) || value is null)
            {
                return Result.Failure<string>(Error.Validation(
                    "Connector.Normalize.MissingNaturalKey",
                    $"Natural-key field '{keyField}' of entity '{mapping.TargetEntity}' has no value."));
            }

            parts.Add(System.Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
        }

        return Result.Success(string.Join(':', parts));
    }
}
