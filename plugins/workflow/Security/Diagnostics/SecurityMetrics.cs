using System.Threading;

namespace FactoryOS.Plugins.Workflow.Security.Diagnostics;

/// <summary>An immutable snapshot of the security engine's counters.</summary>
/// <param name="AuthorizationsGranted">How many requests were permitted.</param>
/// <param name="AuthorizationsDenied">How many requests were refused.</param>
/// <param name="AuthenticationsSucceeded">How many principals were established.</param>
/// <param name="AuthenticationsFailed">How many could not be.</param>
/// <param name="SessionsCreated">How many sessions were opened.</param>
/// <param name="SessionsEnded">How many sessions stopped being usable.</param>
/// <param name="TokensIssued">How many tokens were recorded as issued.</param>
/// <param name="TokensRejected">How many presented tokens were refused.</param>
/// <param name="Violations">How many violations were recorded.</param>
/// <param name="Incidents">How many incidents were raised.</param>
public sealed record SecurityMetricsSnapshot(
    long AuthorizationsGranted,
    long AuthorizationsDenied,
    long AuthenticationsSucceeded,
    long AuthenticationsFailed,
    long SessionsCreated,
    long SessionsEnded,
    long TokensIssued,
    long TokensRejected,
    long Violations,
    long Incidents);

/// <summary>
/// Thread-safe counters for the security engine. They are plain counters rather than metrics in the monitoring
/// store, so the engine can always report on itself — including when the thing that is wrong is downstream.
/// </summary>
public sealed class SecurityMetrics
{
    private long _granted;
    private long _denied;
    private long _authenticated;
    private long _authenticationFailures;
    private long _sessionsCreated;
    private long _sessionsEnded;
    private long _tokensIssued;
    private long _tokensRejected;
    private long _violations;
    private long _incidents;

    /// <summary>Records that a request was permitted.</summary>
    public void RecordGranted() => Interlocked.Increment(ref _granted);

    /// <summary>Records that a request was refused.</summary>
    public void RecordDenied() => Interlocked.Increment(ref _denied);

    /// <summary>Records that a principal was established.</summary>
    public void RecordAuthenticated() => Interlocked.Increment(ref _authenticated);

    /// <summary>Records that a principal could not be established.</summary>
    public void RecordAuthenticationFailure() => Interlocked.Increment(ref _authenticationFailures);

    /// <summary>Records that a session was opened.</summary>
    public void RecordSessionCreated() => Interlocked.Increment(ref _sessionsCreated);

    /// <summary>Records that sessions stopped being usable.</summary>
    /// <param name="count">How many.</param>
    public void RecordSessionsEnded(int count) => Interlocked.Add(ref _sessionsEnded, count);

    /// <summary>Records that a token was issued.</summary>
    public void RecordTokenIssued() => Interlocked.Increment(ref _tokensIssued);

    /// <summary>Records that a presented token was refused.</summary>
    public void RecordTokenRejected() => Interlocked.Increment(ref _tokensRejected);

    /// <summary>Records that a violation was recorded.</summary>
    public void RecordViolation() => Interlocked.Increment(ref _violations);

    /// <summary>Records that an incident was raised.</summary>
    public void RecordIncident() => Interlocked.Increment(ref _incidents);

    /// <summary>Reads the current counters as a snapshot.</summary>
    /// <returns>The snapshot.</returns>
    public SecurityMetricsSnapshot Snapshot() => new(
        Interlocked.Read(ref _granted),
        Interlocked.Read(ref _denied),
        Interlocked.Read(ref _authenticated),
        Interlocked.Read(ref _authenticationFailures),
        Interlocked.Read(ref _sessionsCreated),
        Interlocked.Read(ref _sessionsEnded),
        Interlocked.Read(ref _tokensIssued),
        Interlocked.Read(ref _tokensRejected),
        Interlocked.Read(ref _violations),
        Interlocked.Read(ref _incidents));
}
