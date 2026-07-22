using System.Globalization;

namespace FactoryOS.Connectors.Runtime.Domain;

/// <summary>
/// How many times a retryable failure is attempted and how long the runtime waits between attempts.
/// The delay grows geometrically and is capped, so a struggling external system is given progressively
/// more room instead of being hammered at a fixed rate.
/// </summary>
public sealed record ConnectorRetryPolicy
{
    /// <summary>A policy that never retries — the correct default for anything not known to be idempotent.</summary>
    public static readonly ConnectorRetryPolicy None = new() { MaxAttempts = 1 };

    private readonly int _maxAttempts = 1;
    private readonly double _backoffMultiplier = 2.0;

    /// <summary>Gets the number of attempts allowed, including the first. One means "never retry".</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set below one.</exception>
    public int MaxAttempts
    {
        get => _maxAttempts;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maxAttempts = value;
        }
    }

    /// <summary>Gets the delay before the second attempt.</summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Gets the factor each successive delay is multiplied by.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set below one.</exception>
    public double BackoffMultiplier
    {
        get => _backoffMultiplier;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1.0);
            _backoffMultiplier = value;
        }
    }

    /// <summary>Gets the ceiling no delay grows past.</summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets a value indicating whether this policy permits more than one attempt.</summary>
    public bool Retries => MaxAttempts > 1;

    /// <summary>Computes the delay before a given attempt.</summary>
    /// <param name="attempt">The attempt about to be made, counting the first as one.</param>
    /// <returns>The delay to wait; <see cref="TimeSpan.Zero"/> before the first attempt.</returns>
    public TimeSpan DelayBefore(int attempt)
    {
        if (attempt <= 1)
        {
            return TimeSpan.Zero;
        }

        var ticks = InitialDelay.Ticks * Math.Pow(BackoffMultiplier, attempt - 2);
        return ticks >= MaxDelay.Ticks ? MaxDelay : TimeSpan.FromTicks((long)ticks);
    }
}

/// <summary>
/// When to stop calling an external system that is failing. Consecutive failures open the circuit; while it
/// is open calls are refused without being attempted; after the break a single trial call decides whether the
/// system has recovered.
/// <para>
/// The point is not to protect the caller — it is to stop a struggling remote system being kept down by the
/// very traffic that is failing against it.
/// </para>
/// </summary>
public sealed record ConnectorCircuitBreaker
{
    /// <summary>A breaker that never opens.</summary>
    public static readonly ConnectorCircuitBreaker Disabled = new() { Enabled = false };

    private readonly int _failureThreshold = 5;

    /// <summary>Gets a value indicating whether the breaker is in effect.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Gets how many consecutive failures open the circuit.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set below one.</exception>
    public int FailureThreshold
    {
        get => _failureThreshold;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _failureThreshold = value;
        }
    }

    /// <summary>Gets how long the circuit stays open before a trial call is permitted.</summary>
    public TimeSpan BreakDuration { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// How many invocations an instance may make in a window. Implemented as a fixed window rather than a
/// leaky bucket because external systems publish their quotas that way ("1000 calls per minute"), and a
/// limit that does not match the quota it is protecting is a limit nobody can reason about.
/// </summary>
public sealed record ConnectorRateLimit
{
    /// <summary>A limit that permits everything.</summary>
    public static readonly ConnectorRateLimit Unlimited = new() { Enabled = false };

    private readonly int _permits = 100;

    /// <summary>Gets a value indicating whether the limit is in effect.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Gets how many invocations are permitted per window.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set below one.</exception>
    public int Permits
    {
        get => _permits;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _permits = value;
        }
    }

    /// <summary>Gets the window the permits are counted over.</summary>
    public TimeSpan Window { get; init; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Whether an operation's responses may be reused, and for how long. Only operations a definition marks as
/// cacheable are ever cached, and only successful responses — caching a failure would turn one bad moment
/// into a minute of them.
/// </summary>
public sealed record ConnectorCachePolicy
{
    /// <summary>A policy that caches nothing.</summary>
    public static readonly ConnectorCachePolicy None = new() { Enabled = false };

    private readonly int _capacity = 1000;

    /// <summary>Gets a value indicating whether responses are cached.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Gets how long a cached response stays fresh.</summary>
    public TimeSpan TimeToLive { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Gets how many responses are held before the oldest are evicted.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set below one.</exception>
    public int Capacity
    {
        get => _capacity;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _capacity = value;
        }
    }
}

/// <summary>
/// The resilience an instance invokes under: its retry policy, circuit breaker, rate limit and cache policy.
/// Carried as one value so a definition, an instance and an operation can each narrow the whole set coherently
/// instead of four settings drifting apart.
/// </summary>
/// <param name="Retry">The retry policy.</param>
/// <param name="Circuit">The circuit breaker.</param>
/// <param name="RateLimit">The rate limit.</param>
/// <param name="Cache">The cache policy.</param>
public sealed record ConnectorResiliencePolicy(
    ConnectorRetryPolicy Retry,
    ConnectorCircuitBreaker Circuit,
    ConnectorRateLimit RateLimit,
    ConnectorCachePolicy Cache)
{
    /// <summary>The conservative default: no retries, a breaker, a rate limit and no caching.</summary>
    public static readonly ConnectorResiliencePolicy Default = new(
        ConnectorRetryPolicy.None,
        new ConnectorCircuitBreaker(),
        new ConnectorRateLimit(),
        ConnectorCachePolicy.None);

    /// <summary>Describes the policy for a diagnostic line.</summary>
    /// <returns>A short human-readable description.</returns>
    public string Describe() => string.Create(
        CultureInfo.InvariantCulture,
        $"attempts={Retry.MaxAttempts}, breaker={(Circuit.Enabled ? Circuit.FailureThreshold.ToString(CultureInfo.InvariantCulture) : "off")}, "
        + $"rate={(RateLimit.Enabled ? RateLimit.Permits.ToString(CultureInfo.InvariantCulture) : "off")}/{RateLimit.Window}, "
        + $"cache={(Cache.Enabled ? Cache.TimeToLive.ToString() : "off")}");
}
