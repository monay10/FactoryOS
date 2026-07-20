namespace FactoryOS.Contracts.Events;

/// <summary>Strongly-typed configuration for the event bus, bound from the application configuration.</summary>
public sealed class EventBusOptions
{
    /// <summary>The configuration section name that binds to these options.</summary>
    public const string SectionName = "EventBus";

    /// <summary>Gets or sets the maximum number of delivery attempts per handler. Defaults to 3.</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Gets or sets the base back-off delay, in milliseconds, between attempts. Defaults to 200.</summary>
    public int RetryBaseDelayMilliseconds { get; set; } = 200;

    /// <summary>Builds a <see cref="RetryPolicy"/> from these options.</summary>
    /// <returns>The configured retry policy.</returns>
    public RetryPolicy ToRetryPolicy()
    {
        return new RetryPolicy(MaxRetryAttempts, TimeSpan.FromMilliseconds(RetryBaseDelayMilliseconds));
    }
}
