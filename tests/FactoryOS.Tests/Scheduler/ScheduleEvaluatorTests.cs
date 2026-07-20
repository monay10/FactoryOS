using FactoryOS.Plugins.Scheduler.Domain;

namespace FactoryOS.Tests.Scheduler;

public sealed class ScheduleEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_schedule_that_never_ran_is_due()
    {
        Assert.True(ScheduleEvaluator.IsDue(null, Now, TimeSpan.FromHours(1)));
    }

    [Fact]
    public void A_schedule_is_due_once_its_interval_has_elapsed()
    {
        Assert.True(ScheduleEvaluator.IsDue(Now.AddHours(-1), Now, TimeSpan.FromHours(1)));
    }

    [Fact]
    public void A_schedule_is_not_due_before_its_interval_elapses()
    {
        Assert.False(ScheduleEvaluator.IsDue(Now.AddMinutes(-30), Now, TimeSpan.FromHours(1)));
    }

    [Fact]
    public void A_non_positive_interval_fires_on_every_pulse()
    {
        Assert.True(ScheduleEvaluator.IsDue(Now, Now, TimeSpan.Zero));
    }
}
