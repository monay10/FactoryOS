using System.Collections.Concurrent;
using FactoryOS.Connectors.Runtime.Domain;
using FactoryOS.Domain.Abstractions;

namespace FactoryOS.Connectors.Runtime.Execution;

/// <summary>
/// A recurring connector invocation: read the ERP's stock every ten minutes, poll the scale every second.
/// </summary>
public sealed class ConnectorSchedule
{
    /// <summary>Initializes a new instance of the <see cref="ConnectorSchedule"/> class.</summary>
    /// <param name="key">The schedule key, unique within its tenant.</param>
    /// <param name="request">The request to make each time it comes due.</param>
    /// <param name="interval">How often it comes due.</param>
    public ConnectorSchedule(string key, ConnectorRequest request, TimeSpan interval)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);

        Key = key;
        Request = request;
        Interval = interval;
    }

    /// <summary>Gets the schedule key.</summary>
    public string Key { get; }

    /// <summary>Gets the request made each time the schedule comes due.</summary>
    public ConnectorRequest Request { get; }

    /// <summary>Gets the tenant the schedule belongs to.</summary>
    public string Tenant => Request.Tenant;

    /// <summary>Gets how often the schedule comes due.</summary>
    public TimeSpan Interval { get; }

    /// <summary>Gets a value indicating whether the schedule is running.</summary>
    public bool Enabled { get; private set; } = true;

    /// <summary>Gets when the schedule last ran.</summary>
    public DateTimeOffset? LastRunUtc { get; private set; }

    /// <summary>Gets how many times it has run.</summary>
    public int Runs { get; private set; }

    /// <summary>Gets the identity the scheduler files it under.</summary>
    public string Identity => ConnectorInstance.Identify(Tenant, Key);

    /// <summary>Determines whether the schedule is due.</summary>
    /// <param name="nowUtc">The current instant.</param>
    /// <returns><see langword="true"/> when it is enabled and its interval has elapsed.</returns>
    public bool IsDue(DateTimeOffset nowUtc) =>
        Enabled && (LastRunUtc is not { } last || nowUtc >= last + Interval);

    /// <summary>Records that the schedule ran.</summary>
    /// <param name="nowUtc">The current instant.</param>
    public void MarkRun(DateTimeOffset nowUtc)
    {
        LastRunUtc = nowUtc;
        Runs++;
    }

    /// <summary>Starts the schedule.</summary>
    public void Enable() => Enabled = true;

    /// <summary>Stops the schedule without removing it.</summary>
    public void Disable() => Enabled = false;
}

/// <summary>
/// Runs connector invocations on a recurrence.
/// <para>
/// The scheduler holds no timer and starts no thread. It answers "what is due at this instant?" and runs
/// what is; the host decides how often to ask. That keeps polling policy where deployment concerns belong,
/// and it means the recurrence logic can be proven at a hundred simulated instants in a millisecond instead
/// of being sampled by a test that sleeps.
/// </para>
/// </summary>
public sealed class ConnectorScheduler
{
    private readonly ConcurrentDictionary<string, ConnectorSchedule> _schedules =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConnectorRuntime _runtime;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="ConnectorScheduler"/> class.</summary>
    /// <param name="runtime">The connector runtime the schedules invoke through.</param>
    /// <param name="clock">The clock that decides what is due.</param>
    public ConnectorScheduler(ConnectorRuntime runtime, IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(clock);
        _runtime = runtime;
        _clock = clock;
    }

    /// <summary>Adds or replaces a schedule.</summary>
    /// <param name="schedule">The schedule.</param>
    public void Schedule(ConnectorSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        _schedules[schedule.Identity] = schedule;
    }

    /// <summary>Removes a schedule.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="key">The schedule key.</param>
    /// <returns><see langword="true"/> when a schedule was removed.</returns>
    public bool Unschedule(string tenant, string key) =>
        _schedules.TryRemove(ConnectorInstance.Identify(tenant, key), out _);

    /// <summary>Lists one tenant's schedules.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The schedules, ordered by key.</returns>
    public IReadOnlyList<ConnectorSchedule> ListByTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return
        [
            .. _schedules.Values
                .Where(schedule => string.Equals(schedule.Tenant, tenant, StringComparison.OrdinalIgnoreCase))
                .OrderBy(schedule => schedule.Key, StringComparer.Ordinal),
        ];
    }

    /// <summary>Lists the schedules due at an instant.</summary>
    /// <param name="nowUtc">The instant, or <see langword="null"/> for now.</param>
    /// <returns>The due schedules, ordered by key.</returns>
    public IReadOnlyList<ConnectorSchedule> Due(DateTimeOffset? nowUtc = null)
    {
        var instant = nowUtc ?? _clock.UtcNow;
        return
        [
            .. _schedules.Values
                .Where(schedule => schedule.IsDue(instant))
                .OrderBy(schedule => schedule.Identity, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// Runs every due schedule once, in order. A schedule that fails is still marked as run: a connector
    /// that is down must not accumulate a backlog of missed polls that all fire the moment it returns.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the pass.</param>
    /// <returns>Each schedule that ran, with the response it produced.</returns>
    public async Task<IReadOnlyList<(ConnectorSchedule Schedule, ConnectorResponse Response)>> RunDueAsync(
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var ran = new List<(ConnectorSchedule, ConnectorResponse)>();

        foreach (var schedule in Due(now))
        {
            var response = await _runtime.InvokeAsync(schedule.Request, cancellationToken).ConfigureAwait(false);
            schedule.MarkRun(now);
            ran.Add((schedule, response));
        }

        return ran;
    }
}
