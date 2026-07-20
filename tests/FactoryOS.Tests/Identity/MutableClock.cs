using FactoryOS.Domain.Abstractions;

namespace FactoryOS.Tests.Identity;

/// <summary>A controllable clock for deterministic token-lifetime tests.</summary>
internal sealed class MutableClock : IDateTimeProvider
{
    public MutableClock(DateTimeOffset now) => UtcNow = now;

    public DateTimeOffset UtcNow { get; set; }

    public void Advance(TimeSpan by) => UtcNow += by;
}
