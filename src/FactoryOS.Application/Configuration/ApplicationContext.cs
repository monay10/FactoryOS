using FactoryOS.Application.Messaging;
using FactoryOS.Shared.Identifiers;

namespace FactoryOS.Application.Configuration;

/// <summary>Application-wide constants.</summary>
public static class ApplicationConstants
{
    /// <summary>The configuration section the application options bind from.</summary>
    public const string ConfigurationSection = "Application";

    /// <summary>The default per-request timeout, in seconds, when none is configured.</summary>
    public const int DefaultRequestTimeoutSeconds = 30;

    /// <summary>The default threshold, in milliseconds, above which a request is considered slow.</summary>
    public const int DefaultSlowRequestThresholdMs = 500;
}

/// <summary>Bindable options that tune application-layer behavior.</summary>
public sealed class ApplicationOptions
{
    /// <summary>Gets or sets the per-request timeout.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(ApplicationConstants.DefaultRequestTimeoutSeconds);

    /// <summary>Gets or sets the threshold above which the performance behavior logs a slow request.</summary>
    public TimeSpan SlowRequestThreshold { get; set; } =
        TimeSpan.FromMilliseconds(ApplicationConstants.DefaultSlowRequestThresholdMs);

    /// <summary>Gets or sets a value indicating whether the validation behavior is enabled.</summary>
    public bool EnableValidation { get; set; } = true;
}

/// <summary>
/// The default, scoped <see cref="IRequestContext"/>. The composition edge (for example an API filter) populates it
/// once per request; application code reads it. Mutable only through <see cref="Initialize"/> so it is set exactly once.
/// </summary>
public sealed class ApplicationContext : IRequestContext
{
    /// <summary>Initializes a new instance of the <see cref="ApplicationContext"/> class with default values.</summary>
    public ApplicationContext()
    {
        CorrelationId = CorrelationId.New();
        ReceivedAt = default;
    }

    /// <inheritdoc />
    public CorrelationId CorrelationId { get; private set; }

    /// <inheritdoc />
    public string? Tenant { get; private set; }

    /// <inheritdoc />
    public string? UserName { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset ReceivedAt { get; private set; }

    /// <summary>Populates the context for the current request.</summary>
    /// <param name="correlationId">The correlation identifier.</param>
    /// <param name="receivedAt">When the request entered the application.</param>
    /// <param name="tenant">The resolved tenant, if any.</param>
    /// <param name="userName">The authenticated user name, if any.</param>
    public void Initialize(CorrelationId correlationId, DateTimeOffset receivedAt, string? tenant, string? userName)
    {
        CorrelationId = correlationId;
        ReceivedAt = receivedAt;
        Tenant = tenant;
        UserName = userName;
    }
}
