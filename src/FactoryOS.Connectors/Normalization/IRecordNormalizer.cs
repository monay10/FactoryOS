using FactoryOS.Contracts.Connectors;
using FactoryOS.Domain.Results;

namespace FactoryOS.Connectors.Normalization;

/// <summary>
/// Normalizes a raw <see cref="SourceRecord"/> into a canonical <see cref="NormalizedRecord"/> by
/// applying a data-driven <see cref="MappingManifest"/>. This is where vendor dialects become the
/// Standard Model.
/// </summary>
public interface IRecordNormalizer
{
    /// <summary>Normalizes a single source record for a tenant using the supplied mapping.</summary>
    /// <param name="record">The raw source record.</param>
    /// <param name="mapping">The mapping manifest that drives normalization.</param>
    /// <param name="tenant">The tenant the record belongs to.</param>
    /// <returns>A successful result with the normalized record, or a failure describing the problem.</returns>
    Result<NormalizedRecord> Normalize(SourceRecord record, MappingManifest mapping, string tenant);
}
