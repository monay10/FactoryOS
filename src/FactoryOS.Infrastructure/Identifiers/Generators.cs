using FactoryOS.Application.Messaging;
using FactoryOS.Shared.Guards;
using FactoryOS.Shared.Identifiers;

namespace FactoryOS.Infrastructure.Identifiers;

/// <summary>Generates <see cref="Guid"/> values, abstracted so identifier creation stays testable.</summary>
public interface IGuidGenerator
{
    /// <summary>Creates a new random (version 4) identifier.</summary>
    /// <returns>A new <see cref="Guid"/>.</returns>
    Guid NewGuid();

    /// <summary>Creates a new time-ordered (version 7) identifier, well-suited to database keys.</summary>
    /// <returns>A new, monotonically increasing <see cref="Guid"/>.</returns>
    Guid NewSequentialGuid();
}

/// <summary>The default <see cref="IGuidGenerator"/>, backed by the runtime's cryptographic GUID generation.</summary>
public sealed class GuidGenerator : IGuidGenerator
{
    /// <inheritdoc />
    public Guid NewGuid() => Guid.NewGuid();

    /// <inheritdoc />
    public Guid NewSequentialGuid() => Guid.CreateVersion7();
}

/// <summary>Exposes the correlation identifier threading the current request across boundaries.</summary>
public interface ICorrelationIdAccessor
{
    /// <summary>Gets the correlation identifier for the current request.</summary>
    CorrelationId Current { get; }
}

/// <summary>
/// The default <see cref="ICorrelationIdAccessor"/>, reading the correlation identifier from the ambient
/// <see cref="IRequestContext"/> so infrastructure code shares the single correlation the pipeline established.
/// </summary>
public sealed class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private readonly IRequestContext _requestContext;

    /// <summary>Initializes a new instance of the <see cref="CorrelationIdAccessor"/> class.</summary>
    /// <param name="requestContext">The ambient request context.</param>
    public CorrelationIdAccessor(IRequestContext requestContext)
    {
        _requestContext = Guard.AgainstNull(requestContext);
    }

    /// <inheritdoc />
    public CorrelationId Current => _requestContext.CorrelationId;
}
