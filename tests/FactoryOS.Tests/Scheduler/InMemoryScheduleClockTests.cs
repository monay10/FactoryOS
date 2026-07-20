using FactoryOS.Plugins.Scheduler.Domain;

namespace FactoryOS.Tests.Scheduler;

public sealed class InMemoryScheduleClockTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    [Fact]
    public void The_first_claim_succeeds_and_an_immediate_reclaim_fails()
    {
        var clock = new InMemoryScheduleClock();

        Assert.True(clock.TryClaim("acme", "s-1", T0, Hour));
        Assert.False(clock.TryClaim("acme", "s-1", T0, Hour)); // same instant, within interval
    }

    [Fact]
    public void A_claim_succeeds_again_once_the_interval_has_passed()
    {
        var clock = new InMemoryScheduleClock();

        Assert.True(clock.TryClaim("acme", "s-1", T0, Hour));
        Assert.False(clock.TryClaim("acme", "s-1", T0.AddMinutes(30), Hour));
        Assert.True(clock.TryClaim("acme", "s-1", T0.AddHours(1), Hour));
    }

    [Fact]
    public void Schedules_and_tenants_are_independent()
    {
        var clock = new InMemoryScheduleClock();

        Assert.True(clock.TryClaim("acme", "s-1", T0, Hour));
        Assert.True(clock.TryClaim("acme", "s-2", T0, Hour));   // different schedule
        Assert.True(clock.TryClaim("globex", "s-1", T0, Hour)); // different tenant
    }
}
