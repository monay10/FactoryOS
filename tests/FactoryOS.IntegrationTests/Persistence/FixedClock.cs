using FactoryOS.Domain.Abstractions;

namespace FactoryOS.IntegrationTests.Persistence;

/// <summary>A fixed clock for deterministic audit assertions.</summary>
internal sealed class FixedClock : IDateTimeProvider
{
    public FixedClock(DateTimeOffset now) => UtcNow = now;

    public DateTimeOffset UtcNow { get; set; }
}
