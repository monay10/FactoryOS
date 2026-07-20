using FactoryOS.Domain.Abstractions;

namespace FactoryOS.Domain.Identifiers;

/// <summary>
/// Default <see cref="IIdGenerator"/> producing version-7 (time-ordered) GUIDs, which keep database
/// indexes compact by generating monotonically increasing keys.
/// </summary>
public sealed class SequentialGuidIdGenerator : IIdGenerator
{
    /// <inheritdoc />
    public Guid NewId()
    {
        return Guid.CreateVersion7();
    }
}
