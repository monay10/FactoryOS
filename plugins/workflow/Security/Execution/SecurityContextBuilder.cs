using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Security.Domain;

namespace FactoryOS.Plugins.Workflow.Security.Execution;

/// <summary>
/// Assembles the facts a decision is made from.
/// <para>
/// It exists so that "now" is stamped once, from the platform clock, rather than read again inside each rule.
/// A policy whose time window read a different instant than the rule beside it would produce an outcome that
/// depended on how long evaluation took — which is exactly the kind of bug that only appears under load.
/// </para>
/// </summary>
public sealed class SecurityContextBuilder
{
    private readonly IDateTimeProvider _clock;
    private readonly Dictionary<string, string> _environment = new(StringComparer.OrdinalIgnoreCase);
    private SecurityPrincipal? _principal;
    private SecurityResource? _resource;
    private SecurityAction? _action;
    private SecurityScope? _scope;
    private SecurityCorrelation _correlation = SecurityCorrelation.None;
    private string? _networkAddress;
    private DateTimeOffset? _requestedOnUtc;

    /// <summary>Initializes a new instance of the <see cref="SecurityContextBuilder"/> class.</summary>
    /// <param name="clock">The clock the request is stamped from.</param>
    public SecurityContextBuilder(IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    /// <summary>Sets who is asking. The scope defaults to the principal's own tenant.</summary>
    /// <param name="principal">The principal.</param>
    /// <returns>This builder.</returns>
    public SecurityContextBuilder For(SecurityPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        _principal = principal;
        _scope ??= SecurityScope.ForTenant(principal.Tenant);
        return this;
    }

    /// <summary>Sets what is being acted on.</summary>
    /// <param name="resource">The resource.</param>
    /// <returns>This builder.</returns>
    public SecurityContextBuilder On(SecurityResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        _resource = resource;
        return this;
    }

    /// <summary>Sets what is being done.</summary>
    /// <param name="action">The action.</param>
    /// <returns>This builder.</returns>
    public SecurityContextBuilder To(SecurityAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _action = action;
        return this;
    }

    /// <summary>Sets the resource and action from a <c>resource.action</c> permission string.</summary>
    /// <param name="permission">The permission being asked for.</param>
    /// <returns>This builder.</returns>
    public SecurityContextBuilder Requesting(string permission)
    {
        var parsed = SecurityPermission.Parse(permission);
        _resource ??= SecurityResource.OfType(parsed.Resource);
        _action = SecurityAction.Of(parsed.Action);
        return this;
    }

    /// <summary>Sets the breadth the request belongs to.</summary>
    /// <param name="scope">The scope.</param>
    /// <returns>This builder.</returns>
    public SecurityContextBuilder In(SecurityScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        _scope = scope;
        return this;
    }

    /// <summary>Sets the identifiers tying the request to its operation.</summary>
    /// <param name="correlation">The correlation.</param>
    /// <returns>This builder.</returns>
    public SecurityContextBuilder CorrelatedBy(SecurityCorrelation correlation)
    {
        ArgumentNullException.ThrowIfNull(correlation);
        _correlation = correlation;
        return this;
    }

    /// <summary>Sets where the request came from.</summary>
    /// <param name="networkAddress">The address.</param>
    /// <returns>This builder.</returns>
    public SecurityContextBuilder From(string? networkAddress)
    {
        _networkAddress = networkAddress;
        return this;
    }

    /// <summary>Sets the instant the request was made, overriding the clock.</summary>
    /// <param name="requestedOnUtc">The instant.</param>
    /// <returns>This builder.</returns>
    public SecurityContextBuilder At(DateTimeOffset requestedOnUtc)
    {
        _requestedOnUtc = requestedOnUtc;
        return this;
    }

    /// <summary>Adds a fact rules may reason about.</summary>
    /// <param name="name">The name.</param>
    /// <param name="value">The value.</param>
    /// <returns>This builder.</returns>
    public SecurityContextBuilder With(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        _environment[name] = value;
        return this;
    }

    /// <summary>Builds the context.</summary>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException">The principal, resource or action was never set.</exception>
    public SecurityContext Build()
    {
        var principal = _principal
            ?? throw new InvalidOperationException("A security context needs a principal.");
        var resource = _resource
            ?? throw new InvalidOperationException("A security context needs a resource.");
        var action = _action
            ?? throw new InvalidOperationException("A security context needs an action.");

        return new SecurityContext(
            principal,
            resource,
            action,
            _scope ?? SecurityScope.ForTenant(principal.Tenant),
            _requestedOnUtc ?? _clock.UtcNow,
            _correlation,
            _networkAddress,
            _environment);
    }
}
