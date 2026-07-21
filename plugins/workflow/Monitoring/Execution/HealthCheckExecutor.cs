using System.Diagnostics;
using FactoryOS.Plugins.Workflow.Monitoring.Configuration;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;

namespace FactoryOS.Plugins.Workflow.Monitoring.Execution;

/// <summary>
/// Runs a probe and always comes back with an answer.
/// <para>
/// A probe that throws, and a probe that hangs, are both reported as an unhealthy component rather than being
/// allowed to escape. This is the whole reason the executor exists: the layer that tells you whether the
/// platform is up must not be able to take it down, and a health endpoint that itself fails is the worst
/// possible failure mode — it turns "is anything wrong?" into a question nobody can get an answer to.
/// </para>
/// </summary>
public sealed class HealthCheckExecutor
{
    private readonly MonitoringEngineOptions _options;

    /// <summary>Initializes a new instance of the <see cref="HealthCheckExecutor"/> class.</summary>
    /// <param name="options">The engine options carrying the default probe timeout.</param>
    public HealthCheckExecutor(MonitoringEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>Runs a probe, enforcing its timeout and containing its failures.</summary>
    /// <param name="check">The check being run.</param>
    /// <param name="probe">The probe.</param>
    /// <param name="context">What the probe has to work with.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>What the probe found, or why it could not say.</returns>
    public async Task<HealthCheckResult> ExecuteAsync(
        HealthCheck check,
        HealthProbe probe,
        HealthProbeContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(context);

        var timeout = check.Timeout ?? _options.HealthCheckTimeout;
        var started = Stopwatch.GetTimestamp();

        try
        {
            using var timeoutSource = new CancellationTokenSource(timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutSource.Token);

            var result = await probe(context, linked.Token).AsTask().WaitAsync(timeout, cancellationToken)
                .ConfigureAwait(false);
            return result with { Duration = Stopwatch.GetElapsedTime(started) };
        }
        catch (TimeoutException)
        {
            return HealthCheckResult.Unhealthy(
                check.Key, context.NowUtc, $"The probe did not answer within {timeout}.") with
            {
                Duration = Stopwatch.GetElapsedTime(started),
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy(
                check.Key, context.NowUtc, $"The probe was cancelled after {timeout}.") with
            {
                Duration = Stopwatch.GetElapsedTime(started),
            };
        }
#pragma warning disable CA1031 // A failing probe must degrade one component, never the health report itself.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return HealthCheckResult.Unhealthy(
                check.Key, context.NowUtc, $"The probe failed: {exception.Message}") with
            {
                Duration = Stopwatch.GetElapsedTime(started),
            };
        }
    }
}
