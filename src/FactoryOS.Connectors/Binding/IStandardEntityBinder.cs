using FactoryOS.Contracts.Connectors;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Domain.Results;

namespace FactoryOS.Connectors.Binding;

/// <summary>
/// Materializes a strongly-typed <see cref="IStandardEntity"/> from the canonical wire form
/// (<see cref="NormalizedRecord"/>). This closes the loop of the shared language: a Logo stock row and a
/// SAP material row both bind to the same <c>InventoryItem</c> type.
/// </summary>
public interface IStandardEntityBinder
{
    /// <summary>Binds a normalized record to its typed Standard Model entity.</summary>
    /// <param name="record">The normalized record to bind.</param>
    /// <returns>A successful result with the typed entity, or a failure when the type is unknown or a field is invalid.</returns>
    Result<IStandardEntity> Bind(NormalizedRecord record);
}
