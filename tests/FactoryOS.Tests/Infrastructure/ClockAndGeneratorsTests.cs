using FactoryOS.Application.Messaging;
using FactoryOS.Infrastructure.Identifiers;
using FactoryOS.Infrastructure.Time;
using FactoryOS.Shared.Identifiers;

namespace FactoryOS.Tests.Infrastructure;

public sealed class SystemClockTests
{
    [Fact]
    public void UtcNow_is_close_to_the_system_clock()
    {
        var clock = new SystemClock();

        var delta = (clock.UtcNow - DateTimeOffset.UtcNow).Duration();

        Assert.True(delta < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Today_matches_the_utc_date()
    {
        var clock = new SystemClock();

        Assert.Equal(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime), clock.Today);
    }
}

public sealed class GuidGeneratorTests
{
    [Fact]
    public void NewGuid_returns_distinct_non_empty_identifiers()
    {
        var generator = new GuidGenerator();

        var first = generator.NewGuid();
        var second = generator.NewGuid();

        Assert.NotEqual(Guid.Empty, first);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void NewSequentialGuid_is_a_version_7_identifier()
    {
        var generator = new GuidGenerator();

        var id = generator.NewSequentialGuid();

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(7, id.Version);
    }
}

public sealed class CorrelationIdAccessorTests
{
    private sealed class FakeRequestContext(CorrelationId correlationId) : IRequestContext
    {
        public CorrelationId CorrelationId => correlationId;

        public string? Tenant => null;

        public string? UserName => null;

        public DateTimeOffset ReceivedAt => default;
    }

    [Fact]
    public void Current_reads_the_correlation_id_from_the_request_context()
    {
        var correlation = CorrelationId.New();
        var accessor = new CorrelationIdAccessor(new FakeRequestContext(correlation));

        Assert.Equal(correlation, accessor.Current);
    }
}
