using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Identifiers;
using FactoryOS.Domain.Time;

namespace FactoryOS.Tests.Providers;

public sealed class ProviderTests
{
    [Fact]
    public void Id_generator_produces_non_empty_identifiers()
    {
        IIdGenerator generator = new SequentialGuidIdGenerator();

        Assert.NotEqual(Guid.Empty, generator.NewId());
    }

    [Fact]
    public void Id_generator_produces_unique_identifiers()
    {
        IIdGenerator generator = new SequentialGuidIdGenerator();

        Assert.NotEqual(generator.NewId(), generator.NewId());
    }

    [Fact]
    public void Date_time_provider_returns_a_current_utc_instant()
    {
        IDateTimeProvider provider = new SystemDateTimeProvider();

        var difference = DateTimeOffset.UtcNow - provider.UtcNow;

        Assert.True(difference.Duration() < TimeSpan.FromSeconds(5));
    }
}
