using FactoryOS.Application.Services;

namespace FactoryOS.Infrastructure.Time;

/// <summary>
/// The default <see cref="IApplicationClock"/>, backed by the system clock via <see cref="DateTimeOffset.UtcNow"/>.
/// It supplies the calendar concepts application code needs on top of the domain <c>IDateTimeProvider</c> contract it
/// inherits, keeping a single UTC source of truth for both <see cref="UtcNow"/> and <see cref="Today"/>.
/// </summary>
public sealed class SystemClock : IApplicationClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}
