using FactoryOS.Domain.Results;
using FactoryOS.Plugins.Runtime.Domain;

namespace FactoryOS.Plugins.Runtime.Security;

/// <summary>
/// The one place a lifecycle request is checked before it is allowed to happen.
/// <para>
/// The tenant gate is applied <b>here, before <see cref="IPluginAuthorizer"/> is consulted</b>, and that
/// placement is load-bearing. The authorizer is a port a host replaces with its own decision layer, and an
/// adapter forwarding to one that only ever sees the <i>caller</i> cannot know which tenant the
/// <i>instance</i> belongs to. A port may decide permissions; it may not decide tenancy.
/// </para>
/// <para>
/// Everything that drives a plugin — the lifecycle manager, the update manager, the configuration manager —
/// goes through this gate, so an authorizer that allows everything still cannot get a request across a
/// tenant boundary.
/// </para>
/// </summary>
public sealed class PluginAuthorizationGate
{
    private readonly IPluginAuthorizer _authorizer;

    /// <summary>Initializes a new instance of the <see cref="PluginAuthorizationGate"/> class.</summary>
    /// <param name="authorizer">The authorization port.</param>
    public PluginAuthorizationGate(IPluginAuthorizer authorizer)
    {
        ArgumentNullException.ThrowIfNull(authorizer);
        _authorizer = authorizer;
    }

    /// <summary>Checks whether a caller may drive an installation through a lifecycle phase.</summary>
    /// <param name="caller">Who is asking.</param>
    /// <param name="instance">The installation they addressed.</param>
    /// <param name="phase">What they want to do.</param>
    /// <returns>A successful result, or the refusal as an error.</returns>
    public Result Check(PluginCaller caller, PluginInstance instance, PluginLifecyclePhase phase) =>
        Check(caller, instance, PluginPermissions.For(phase));

    /// <summary>Checks whether a caller holds a permission over an installation.</summary>
    /// <param name="caller">Who is asking.</param>
    /// <param name="instance">The installation they addressed.</param>
    /// <param name="required">The permission the request needs.</param>
    /// <returns>A successful result, or the refusal as an error.</returns>
    public Result Check(PluginCaller caller, PluginInstance instance, PluginPermission required)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(instance);

        if (!string.Equals(caller.Tenant, instance.Tenant, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(PluginAuthorization.Deny(
                PluginAuthorizationReason.TenantMismatch,
                $"Caller '{caller.Subject}' is acting in tenant '{caller.Tenant}' but plugin "
                + $"'{instance.PluginKey}' belongs to '{instance.Tenant}'.").ToError());
        }

        var decision = _authorizer.Authorize(caller, instance, required);
        return decision.Allowed ? Result.Success() : Result.Failure(decision.ToError());
    }
}
