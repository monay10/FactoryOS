using System.Globalization;
using FactoryOS.Connectors.Framework.Health;
using FactoryOS.Connectors.Runtime.Configuration;
using FactoryOS.Connectors.Runtime.Domain;
using FactoryOS.Connectors.Runtime.Execution;
using FactoryOS.Connectors.Runtime.Integration;
using FactoryOS.Connectors.Runtime.Security;
using FactoryOS.Domain.Abstractions;
using Microsoft.Extensions.Options;

namespace FactoryOS.Connectors.Runtime.Pipeline;

/// <summary>
/// Where each stage of the connector pipeline sits. The order is load-bearing and every number here was
/// chosen for a reason; the reasons are recorded on the constants so a later change has to argue with them.
/// </summary>
public static class ConnectorPipelineOrder
{
    /// <summary>Outermost, so every later stage and every log line shares one set of identifiers.</summary>
    public const int Tracing = 100;

    /// <summary>Measures the whole call, including the waits between retries — which is what a caller waited.</summary>
    public const int Metrics = 200;

    /// <summary>Turns outcomes into health signals, above the resilience stages that hide individual attempts.</summary>
    public const int Monitoring = 300;

    /// <summary>Records the attempt and its outcome, including the ones later stages refuse.</summary>
    public const int Audit = 400;

    /// <summary>
    /// Decides before anything else is revealed. Validating first would tell an unauthorized caller which
    /// parameters an operation takes, and resolving a credential first would let one probe a secret store.
    /// </summary>
    public const int Authorization = 500;

    /// <summary>Rejects a request no attempt could satisfy, before any external system is touched.</summary>
    public const int Validation = 600;

    /// <summary>Resolves the credential the connector presents outward, once the caller has been allowed in.</summary>
    public const int Authentication = 700;

    /// <summary>Answers from the cache before a permit is taken or a circuit consulted — a hit costs nothing.</summary>
    public const int Caching = 800;

    /// <summary>Wraps the resilience stages, so every attempt takes its own permit and consults the circuit.</summary>
    public const int Retry = 900;

    /// <summary>
    /// Inside retry so a retried attempt is limited too, outside the circuit so our own throttling never
    /// counts as the remote system failing.
    /// </summary>
    public const int RateLimit = 1000;

    /// <summary>Innermost guard: the last thing consulted before the external system is actually called.</summary>
    public const int CircuitBreaker = 1100;

    /// <summary>Shapes what comes back, closest to the connector that produced it.</summary>
    public const int Transformation = 1200;
}

/// <summary>
/// Completes the identifiers an invocation travels under, and stamps them onto everything it produces.
/// A caller that supplies a correlation keeps it; a caller that supplies none gets one, because an
/// invocation nobody can join back to the work that caused it is one nobody can act on.
/// </summary>
public sealed class TracingMiddleware : IConnectorMiddleware
{
    /// <inheritdoc />
    public string Name => "tracing";

    /// <inheritdoc />
    public int Order => ConnectorPipelineOrder.Tracing;

    /// <inheritdoc />
    public async Task<ConnectorResponse> InvokeAsync(
        ConnectorInvocation invocation, ConnectorInvocationDelegate next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(next);

        invocation.Correlation = invocation.Correlation.Complete(Guid.NewGuid().ToString("N"));

        var response = await next(invocation, cancellationToken).ConfigureAwait(false);
        return response with { Correlation = invocation.Correlation };
    }
}

/// <summary>
/// Measures what the invocation cost and reports it to every metric sink.
/// <para>
/// It also stamps the duration and attempt count onto the response, so a caller is told what its call
/// actually cost rather than having to ask a metrics system afterwards.
/// </para>
/// </summary>
public sealed class MetricsMiddleware : IConnectorMiddleware
{
    private readonly ConnectorMetricPublisher _metrics;
    private readonly ConnectorMetrics _counters;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="MetricsMiddleware"/> class.</summary>
    /// <param name="metrics">The metric sinks.</param>
    /// <param name="counters">The runtime's own tally.</param>
    /// <param name="clock">The clock the duration is measured with.</param>
    public MetricsMiddleware(
        ConnectorMetricPublisher metrics, ConnectorMetrics counters, IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(counters);
        ArgumentNullException.ThrowIfNull(clock);
        _metrics = metrics;
        _counters = counters;
        _clock = clock;
    }

    /// <inheritdoc />
    public string Name => "metrics";

    /// <inheritdoc />
    public int Order => ConnectorPipelineOrder.Metrics;

    /// <inheritdoc />
    public async Task<ConnectorResponse> InvokeAsync(
        ConnectorInvocation invocation, ConnectorInvocationDelegate next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(next);

        var started = _clock.UtcNow;
        var response = await next(invocation, cancellationToken).ConfigureAwait(false);

        var measured = response with
        {
            Duration = _clock.UtcNow - started,
            Attempts = Math.Max(1, invocation.Attempts),
        };

        var telemetry = invocation.Telemetry(measured);
        _counters.Observe(telemetry);
        Report(telemetry);

        return measured;
    }

    private void Report(ConnectorTelemetry telemetry)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ConnectorRuntimeConstants.ConnectorLabel] = telemetry.Definition,
            [ConnectorRuntimeConstants.OperationLabel] = telemetry.Operation,
            [ConnectorRuntimeConstants.OutcomeLabel] = telemetry.Outcome,
        };

        var observedUtc = telemetry.StartedUtc + telemetry.Duration;

        void Observe(string name, double value) => _metrics.Observe(
            new ConnectorMeasurement(telemetry.Tenant, name, value, labels, observedUtc)
            {
                Correlation = telemetry.Correlation,
            });

        Observe(ConnectorMetricNames.Invocations, 1);
        Observe(ConnectorMetricNames.Duration, telemetry.Duration.TotalMilliseconds);

        if (telemetry.Attempts > 1)
        {
            Observe(ConnectorMetricNames.Retries, telemetry.Attempts - 1);
        }

        if (telemetry.FromCache)
        {
            Observe(ConnectorMetricNames.CacheHits, 1);
        }

        switch (telemetry.Error?.Kind)
        {
            case ConnectorErrorKind.Throttled:
                Observe(ConnectorMetricNames.Throttled, 1);
                break;
            case ConnectorErrorKind.CircuitOpen:
                Observe(ConnectorMetricNames.CircuitRefusals, 1);
                break;
            case ConnectorErrorKind.Forbidden or ConnectorErrorKind.Unauthorized:
                Observe(ConnectorMetricNames.Refusals, 1);
                break;
            case not null:
                Observe(ConnectorMetricNames.Failures, 1);
                break;
            default:
                break;
        }
    }
}

/// <summary>
/// Turns invocation outcomes into the health signals the connector framework already tracks, so the
/// platform's existing health view stays true without the framework being changed.
/// <para>
/// Only failures that say something about the external system are reported. A refused permission or a
/// malformed request is the caller's problem; letting those mark a connector unhealthy would have one
/// badly-written client take a factory's ERP off the board.
/// </para>
/// </summary>
public sealed class MonitoringMiddleware : IConnectorMiddleware
{
    private readonly IConnectorHealthService _health;

    /// <summary>Initializes a new instance of the <see cref="MonitoringMiddleware"/> class.</summary>
    /// <param name="health">The connector framework's health service.</param>
    public MonitoringMiddleware(IConnectorHealthService health)
    {
        ArgumentNullException.ThrowIfNull(health);
        _health = health;
    }

    /// <inheritdoc />
    public string Name => "monitoring";

    /// <inheritdoc />
    public int Order => ConnectorPipelineOrder.Monitoring;

    /// <inheritdoc />
    public async Task<ConnectorResponse> InvokeAsync(
        ConnectorInvocation invocation, ConnectorInvocationDelegate next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(next);

        var response = await next(invocation, cancellationToken).ConfigureAwait(false);
        var key = invocation.Definition.Key;

        if (response.Succeeded)
        {
            // A cached answer says nothing about the external system: it was never asked.
            if (!response.FromCache)
            {
                _health.Heartbeat(key);
            }

            return response;
        }

        if (response.Error is { } error && error.CountsAgainstHealth)
        {
            _health.ReportFailure(key, error.Message);
        }

        return response;
    }
}

/// <summary>
/// Records every invocation that reached the runtime — including the ones later stages refuse.
/// <para>
/// A refusal is the line most worth keeping. A trail that only records what succeeded cannot answer the
/// question anybody actually asks after an incident, which is who tried.
/// </para>
/// </summary>
public sealed class AuditMiddleware : IConnectorMiddleware
{
    private readonly ConnectorAuditPublisher _audit;
    private readonly ConnectorRuntimeOptions _options;

    /// <summary>Initializes a new instance of the <see cref="AuditMiddleware"/> class.</summary>
    /// <param name="audit">The audit sinks.</param>
    /// <param name="options">The runtime options.</param>
    public AuditMiddleware(ConnectorAuditPublisher audit, IOptions<ConnectorRuntimeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(options);
        _audit = audit;
        _options = options.Value;
    }

    /// <inheritdoc />
    public string Name => "audit";

    /// <inheritdoc />
    public int Order => ConnectorPipelineOrder.Audit;

    /// <inheritdoc />
    public async Task<ConnectorResponse> InvokeAsync(
        ConnectorInvocation invocation, ConnectorInvocationDelegate next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(next);

        var response = await next(invocation, cancellationToken).ConfigureAwait(false);

        if (response.Succeeded && !_options.AuditSuccessfulInvocations)
        {
            return response;
        }

        var subject = invocation.Request.Caller?.Subject ?? "anonymous";
        _audit.Record(ConnectorAuditEntry.From(invocation.Telemetry(response), subject));
        return response;
    }
}

/// <summary>
/// Refuses an invocation the caller may not make, before anything else is revealed to it.
/// <para>
/// The tenant gate is applied <b>here</b>, before the authorizer is consulted, and is deliberately not
/// delegated to it. <see cref="IConnectorAuthorizer"/> is a port a host replaces with its own decision layer,
/// and an adapter that forgot to compare the caller's tenant with the instance's — an easy omission, because
/// the decision layer it forwards to only ever sees the caller — would silently open a door the Constitution
/// says cannot exist. A port may decide permissions; it may not decide tenancy.
/// </para>
/// </summary>
public sealed class AuthorizationMiddleware : IConnectorMiddleware
{
    private readonly IConnectorAuthorizer _authorizer;

    /// <summary>Initializes a new instance of the <see cref="AuthorizationMiddleware"/> class.</summary>
    /// <param name="authorizer">The authorizer.</param>
    public AuthorizationMiddleware(IConnectorAuthorizer authorizer)
    {
        ArgumentNullException.ThrowIfNull(authorizer);
        _authorizer = authorizer;
    }

    /// <inheritdoc />
    public string Name => "authorization";

    /// <inheritdoc />
    public int Order => ConnectorPipelineOrder.Authorization;

    /// <inheritdoc />
    public Task<ConnectorResponse> InvokeAsync(
        ConnectorInvocation invocation, ConnectorInvocationDelegate next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(next);

        var caller = invocation.Request.Caller;
        var decision = caller is not null
                       && !string.Equals(caller.Tenant, invocation.Tenant, StringComparison.OrdinalIgnoreCase)
            ? ConnectorAuthorization.Deny(
                ConnectorAuthorizationReason.TenantMismatch,
                $"Caller '{caller.Subject}' is acting in tenant '{caller.Tenant}' but connector instance "
                + $"'{invocation.Instance.Key}' belongs to '{invocation.Tenant}'.")
            : _authorizer.Authorize(caller, invocation.Instance, invocation.Operation);

        invocation.Set("authorization", decision);

        return decision.Allowed
            ? next(invocation, cancellationToken)
            : Task.FromResult(ConnectorResponse.Failed(decision.ToError()));
    }
}

/// <summary>
/// Rejects a request no attempt could satisfy: an instance that is not running, an operation the connector
/// does not declare the capability for, or a missing required parameter.
/// </summary>
public sealed class ValidationMiddleware : IConnectorMiddleware
{
    /// <inheritdoc />
    public string Name => "validation";

    /// <inheritdoc />
    public int Order => ConnectorPipelineOrder.Validation;

    /// <inheritdoc />
    public Task<ConnectorResponse> InvokeAsync(
        ConnectorInvocation invocation, ConnectorInvocationDelegate next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(next);

        if (!invocation.Instance.CanInvoke)
        {
            return Task.FromResult(ConnectorResponse.Failed(ConnectorError.Permanent(
                "Connector.Validate.NotRunning",
                $"Connector instance '{invocation.Instance.Key}' is {invocation.Instance.Status} and cannot be "
                + $"invoked. {invocation.Instance.FailureReason ?? string.Empty}".TrimEnd())));
        }

        if (!invocation.Definition.Supports(invocation.Operation.Capability))
        {
            return Task.FromResult(ConnectorResponse.Failed(ConnectorError.Validation(
                "Connector.Validate.UndeclaredCapability",
                $"Operation '{invocation.Operation.Name}' exercises '{invocation.Operation.Capability}', which "
                + $"connector '{invocation.Definition.Key}' does not declare.")));
        }

        foreach (var required in invocation.Operation.RequiredParameters)
        {
            if (string.IsNullOrWhiteSpace(invocation.Request.Parameter(required)))
            {
                return Task.FromResult(ConnectorResponse.Failed(ConnectorError.Validation(
                    "Connector.Validate.MissingParameter",
                    $"Operation '{invocation.Operation.Name}' requires parameter '{required}'.")));
            }
        }

        return next(invocation, cancellationToken);
    }
}

/// <summary>
/// Resolves the credential the connector presents <b>outward</b>, and holds the instance's session open
/// across invocations.
/// <para>
/// An unresolvable credential fails here rather than at the external system. "Authentication failed" from an
/// ERP after a thirty-second timeout and "the secret this instance names does not exist" are the same fault
/// with wildly different repair times.
/// </para>
/// </summary>
public sealed class AuthenticationMiddleware : IConnectorMiddleware
{
    private readonly ConnectorSecretResolver _secrets;
    private readonly ConnectorSessionManager _sessions;

    /// <summary>Initializes a new instance of the <see cref="AuthenticationMiddleware"/> class.</summary>
    /// <param name="secrets">The secret resolver.</param>
    /// <param name="sessions">The session manager.</param>
    public AuthenticationMiddleware(ConnectorSecretResolver secrets, ConnectorSessionManager sessions)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentNullException.ThrowIfNull(sessions);
        _secrets = secrets;
        _sessions = sessions;
    }

    /// <inheritdoc />
    public string Name => "authentication";

    /// <inheritdoc />
    public int Order => ConnectorPipelineOrder.Authentication;

    /// <inheritdoc />
    public Task<ConnectorResponse> InvokeAsync(
        ConnectorInvocation invocation, ConnectorInvocationDelegate next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(next);

        var credential = _secrets.ResolveFor(invocation.Instance);
        if (!credential.IsComplete)
        {
            return Task.FromResult(ConnectorResponse.Failed(ConnectorError.Unauthorized(
                "Connector.Authenticate.UnresolvedSecret",
                $"Connector instance '{invocation.Instance.Key}' needs a {credential.Kind} credential, but the "
                + $"secret its credential '{credential.Key}' refers to could not be resolved.")));
        }

        invocation.Credential = credential;
        invocation.Session = _sessions.Acquire(invocation.Instance);

        return next(invocation, cancellationToken);
    }
}

/// <summary>Serves a fresh answer to a repeated read without touching the external system.</summary>
public sealed class CachingMiddleware : IConnectorMiddleware
{
    private readonly ConnectorResponseCache _cache;

    /// <summary>Initializes a new instance of the <see cref="CachingMiddleware"/> class.</summary>
    /// <param name="cache">The response cache.</param>
    public CachingMiddleware(ConnectorResponseCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
    }

    /// <inheritdoc />
    public string Name => "caching";

    /// <inheritdoc />
    public int Order => ConnectorPipelineOrder.Caching;

    /// <inheritdoc />
    public async Task<ConnectorResponse> InvokeAsync(
        ConnectorInvocation invocation, ConnectorInvocationDelegate next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(next);

        var policy = invocation.Resilience.Cache;
        if (_cache.Find(invocation.Request, policy, invocation.Operation) is { } cached)
        {
            return cached;
        }

        var response = await next(invocation, cancellationToken).ConfigureAwait(false);
        _cache.Store(invocation.Request, response, policy, invocation.Operation);
        return response;
    }
}

/// <summary>Tries a retryable failure again, waiting longer before each attempt.</summary>
public sealed class RetryMiddleware : IConnectorMiddleware
{
    private readonly RetryEngine _retry;

    /// <summary>Initializes a new instance of the <see cref="RetryMiddleware"/> class.</summary>
    /// <param name="retry">The retry engine.</param>
    public RetryMiddleware(RetryEngine retry)
    {
        ArgumentNullException.ThrowIfNull(retry);
        _retry = retry;
    }

    /// <inheritdoc />
    public string Name => "retry";

    /// <inheritdoc />
    public int Order => ConnectorPipelineOrder.Retry;

    /// <inheritdoc />
    public async Task<ConnectorResponse> InvokeAsync(
        ConnectorInvocation invocation, ConnectorInvocationDelegate next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(next);

        var policy = invocation.Resilience.Retry;
        var attempts = 0;

        while (true)
        {
            attempts++;
            var response = await next(invocation, cancellationToken).ConfigureAwait(false);

            if (response.Succeeded
                || response.Error is not { } error
                || !_retry.ShouldRetry(policy, invocation.Operation, error, attempts))
            {
                return response;
            }

            await _retry.WaitBeforeAsync(policy, attempts + 1, cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>Keeps an instance inside the call quota its external system publishes.</summary>
public sealed class RateLimitMiddleware : IConnectorMiddleware
{
    private readonly RateLimiter _limiter;

    /// <summary>Initializes a new instance of the <see cref="RateLimitMiddleware"/> class.</summary>
    /// <param name="limiter">The rate limiter.</param>
    public RateLimitMiddleware(RateLimiter limiter)
    {
        ArgumentNullException.ThrowIfNull(limiter);
        _limiter = limiter;
    }

    /// <inheritdoc />
    public string Name => "ratelimit";

    /// <inheritdoc />
    public int Order => ConnectorPipelineOrder.RateLimit;

    /// <inheritdoc />
    public Task<ConnectorResponse> InvokeAsync(
        ConnectorInvocation invocation, ConnectorInvocationDelegate next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(next);

        var limit = invocation.Resilience.RateLimit;
        var key = RateLimiter.KeyFor(invocation.Tenant, invocation.Instance.Key);

        if (_limiter.TryAcquire(key, limit))
        {
            return next(invocation, cancellationToken);
        }

        return Task.FromResult(ConnectorResponse.Failed(ConnectorError.Throttled(
            "Connector.RateLimit.Exceeded",
            string.Create(
                CultureInfo.InvariantCulture,
                $"Connector instance '{invocation.Instance.Key}' has used its {limit.Permits} permits for the "
                + $"current {limit.Window} window."))));
    }
}

/// <summary>Stops calling an external system that is failing, and lets one trial call decide when it is back.</summary>
public sealed class CircuitBreakerMiddleware : IConnectorMiddleware
{
    private readonly CircuitBreakerEngine _breaker;

    /// <summary>Initializes a new instance of the <see cref="CircuitBreakerMiddleware"/> class.</summary>
    /// <param name="breaker">The circuit breaker engine.</param>
    public CircuitBreakerMiddleware(CircuitBreakerEngine breaker)
    {
        ArgumentNullException.ThrowIfNull(breaker);
        _breaker = breaker;
    }

    /// <inheritdoc />
    public string Name => "circuitbreaker";

    /// <inheritdoc />
    public int Order => ConnectorPipelineOrder.CircuitBreaker;

    /// <inheritdoc />
    public async Task<ConnectorResponse> InvokeAsync(
        ConnectorInvocation invocation, ConnectorInvocationDelegate next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(next);

        var policy = invocation.Resilience.Circuit;
        var key = CircuitBreakerEngine.KeyFor(
            invocation.Tenant, invocation.Instance.Key, invocation.Operation.Name);

        if (!_breaker.TryEnter(key, policy))
        {
            return ConnectorResponse.Failed(ConnectorError.CircuitOpen(
                "Connector.Circuit.Open",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The circuit for '{invocation.Instance.Key}.{invocation.Operation.Name}' is open after "
                    + $"{policy.FailureThreshold} consecutive failures; it will be tried again after "
                    + $"{policy.BreakDuration}.")));
        }

        var response = await next(invocation, cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            _breaker.RecordSuccess(key);
        }
        else if (response.Error is { } error)
        {
            _breaker.RecordFailure(key, policy, error);
        }

        return response;
    }
}

/// <summary>Shapes what a connector produced into what the caller asked for.</summary>
public interface IConnectorTransform
{
    /// <summary>Gets the transform's name, as it appears in diagnostics.</summary>
    string Name { get; }

    /// <summary>Determines whether the transform applies to an invocation.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <returns><see langword="true"/> when it applies.</returns>
    bool AppliesTo(ConnectorInvocation invocation);

    /// <summary>Shapes a successful response.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <param name="response">The response to shape.</param>
    /// <returns>The shaped response.</returns>
    ConnectorResponse Apply(ConnectorInvocation invocation, ConnectorResponse response);
}

/// <summary>
/// Applies the registered transforms to what a connector produced, closest to the connector that produced it.
/// <para>
/// Nothing is registered by default. Normalizing a source record into the Standard Model is the canonical
/// transform, and the framework's existing ingestion pipeline already performs it from a mapping manifest —
/// so a deployment plugs that in here rather than the runtime growing a second copy of it.
/// </para>
/// <para>
/// Only successful responses are shaped. Rewriting a failure would let a transform change what an operator
/// is told went wrong.
/// </para>
/// </summary>
public sealed class TransformationMiddleware : IConnectorMiddleware
{
    private readonly IReadOnlyList<IConnectorTransform> _transforms;

    /// <summary>Initializes a new instance of the <see cref="TransformationMiddleware"/> class.</summary>
    /// <param name="transforms">The registered transforms.</param>
    public TransformationMiddleware(IEnumerable<IConnectorTransform> transforms)
    {
        ArgumentNullException.ThrowIfNull(transforms);
        _transforms = [.. transforms];
    }

    /// <inheritdoc />
    public string Name => "transformation";

    /// <inheritdoc />
    public int Order => ConnectorPipelineOrder.Transformation;

    /// <inheritdoc />
    public async Task<ConnectorResponse> InvokeAsync(
        ConnectorInvocation invocation, ConnectorInvocationDelegate next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(next);

        var response = await next(invocation, cancellationToken).ConfigureAwait(false);
        if (!response.Succeeded)
        {
            return response;
        }

        foreach (var transform in _transforms)
        {
            if (transform.AppliesTo(invocation))
            {
                response = transform.Apply(invocation, response);
            }
        }

        return response;
    }
}
