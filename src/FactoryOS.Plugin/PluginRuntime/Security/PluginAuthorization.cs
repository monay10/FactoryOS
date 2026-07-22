using FactoryOS.Domain.Results;
using FactoryOS.Plugins.Runtime.Domain;

namespace FactoryOS.Plugins.Runtime.Security;

/// <summary>Why a lifecycle transition was allowed or refused.</summary>
public enum PluginAuthorizationReason
{
    /// <summary>The caller may proceed.</summary>
    Granted = 0,

    /// <summary>No caller was supplied, and a guarded transition always has one.</summary>
    NoCaller = 1,

    /// <summary>The caller was not authenticated.</summary>
    NotAuthenticated = 2,

    /// <summary>The caller was acting in a different tenant from the plugin they addressed.</summary>
    TenantMismatch = 3,

    /// <summary>The caller does not hold the permission the transition requires.</summary>
    MissingPermission = 4,

    /// <summary>The plugin is switched off for this tenant.</summary>
    PluginDisabled = 5,
}

/// <summary>The decision made about one lifecycle transition.</summary>
/// <param name="Allowed">Whether the caller may proceed.</param>
/// <param name="Reason">Why.</param>
/// <param name="Detail">The reason in a form an operator can act on.</param>
public sealed record PluginAuthorization(bool Allowed, PluginAuthorizationReason Reason, string Detail)
{
    /// <summary>Allows a transition.</summary>
    /// <returns>The decision.</returns>
    public static PluginAuthorization Allow() =>
        new(true, PluginAuthorizationReason.Granted, "The caller holds the required permission.");

    /// <summary>Refuses a transition.</summary>
    /// <param name="reason">Why.</param>
    /// <param name="detail">The reason in full.</param>
    /// <returns>The decision.</returns>
    public static PluginAuthorization Deny(PluginAuthorizationReason reason, string detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        return new PluginAuthorization(false, reason, detail);
    }

    /// <summary>Turns a refusal into the error the caller receives.</summary>
    /// <returns>The error.</returns>
    public Error ToError() => Reason switch
    {
        PluginAuthorizationReason.NoCaller or PluginAuthorizationReason.NotAuthenticated =>
            Error.Validation("Plugin.Runtime.Unauthenticated", Detail),
        PluginAuthorizationReason.PluginDisabled =>
            Error.Conflict("Plugin.Runtime.Disabled", Detail),
        _ => Error.Validation("Plugin.Runtime.Forbidden", Detail),
    };
}

/// <summary>
/// Decides whether a caller may drive a plugin through a lifecycle phase.
/// <para>
/// This is a <b>port</b>: the default answers from the permissions the caller arrived holding, and a host
/// substitutes the platform's security engine. What the port is never asked is <i>which tenant</i> a plugin
/// belongs to — that gate is applied before it is consulted, because an adapter forwarding to a decision
/// layer that only sees the caller cannot know.
/// </para>
/// </summary>
public interface IPluginAuthorizer
{
    /// <summary>Decides one request.</summary>
    /// <param name="caller">Who is asking.</param>
    /// <param name="instance">The installation they addressed.</param>
    /// <param name="required">The permission the request needs.</param>
    /// <returns>The decision.</returns>
    PluginAuthorization Authorize(PluginCaller? caller, PluginInstance instance, PluginPermission required);
}

/// <summary>
/// Default <see cref="IPluginAuthorizer"/>: the caller must be authenticated and hold the permission the
/// request requires.
/// </summary>
public sealed class PermissionPluginAuthorizer : IPluginAuthorizer
{
    /// <inheritdoc />
    public PluginAuthorization Authorize(
        PluginCaller? caller, PluginInstance instance, PluginPermission required)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (caller is null)
        {
            return PluginAuthorization.Deny(
                PluginAuthorizationReason.NoCaller,
                $"No caller was supplied for '{required}' on plugin '{instance.PluginKey}'.");
        }

        if (!caller.IsAuthenticated)
        {
            return PluginAuthorization.Deny(
                PluginAuthorizationReason.NotAuthenticated,
                $"Caller '{caller.Subject}' is not authenticated.");
        }

        return caller.Holds(required)
            ? PluginAuthorization.Allow()
            : PluginAuthorization.Deny(
                PluginAuthorizationReason.MissingPermission,
                $"Caller '{caller.Subject}' does not hold '{required}', which this request on plugin "
                + $"'{instance.PluginKey}' requires.");
    }
}
