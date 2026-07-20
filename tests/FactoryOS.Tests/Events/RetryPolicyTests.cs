using FactoryOS.Contracts.Events;

namespace FactoryOS.Tests.Events;

public sealed class RetryPolicyTests
{
    [Fact]
    public void Get_delay_grows_exponentially()
    {
        var policy = new RetryPolicy(5, TimeSpan.FromMilliseconds(100));

        Assert.Equal(TimeSpan.FromMilliseconds(100), policy.GetDelay(1));
        Assert.Equal(TimeSpan.FromMilliseconds(200), policy.GetDelay(2));
        Assert.Equal(TimeSpan.FromMilliseconds(400), policy.GetDelay(3));
    }

    [Fact]
    public void Max_attempts_below_one_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicy(0, TimeSpan.Zero));
    }

    [Fact]
    public void Negative_base_delay_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicy(3, TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public void Options_produce_a_matching_policy()
    {
        var options = new EventBusOptions { MaxRetryAttempts = 4, RetryBaseDelayMilliseconds = 250 };

        var policy = options.ToRetryPolicy();

        Assert.Equal(4, policy.MaxAttempts);
        Assert.Equal(TimeSpan.FromMilliseconds(250), policy.BaseDelay);
    }
}
