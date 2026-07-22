using FactoryOS.Connectors.Runtime.Domain;

namespace FactoryOS.Connectors.Runtime.Security;

/// <summary>Why an invocation was allowed or refused.</summary>
public enum ConnectorAuthorizationReason
{
    /// <summary>The caller holds a permission covering the operation.</summary>
    Granted = 0,

    /// <summary>The request carried no caller at all.</summary>
    NoCaller = 1,

    /// <summary>The caller was not authenticated, so it holds nothing whatever it claims.</summary>
    NotAuthenticated = 2,

    /// <summary>The caller is acting in one tenant and the instance belongs to another.</summary>
    TenantMismatch = 3,

    /// <summary>The caller is authenticated but holds no permission covering the operation.</summary>
    MissingPermission = 4,

    /// <summary>An operator has switched the instance off.</summary>
    InstanceDisabled = 5,
}

/// <summary>The outcome of deciding whether an invocation may proceed.</summary>
/// <param name="Allowed">Whether it may proceed.</param>
/// <param name="Reason">Why it went the way it did.</param>
/// <param name="Description">A description an operator can act on.</param>
public sealed record ConnectorAuthorization(
    bool Allowed, ConnectorAuthorizationReason Reason, string Description)
{
    /// <summary>Allows an invocation.</summary>
    /// <param name="description">Why.</param>
    /// <returns>The decision.</returns>
    public static ConnectorAuthorization Allow(string description) =>
        new(true, ConnectorAuthorizationReason.Granted, description);

    /// <summary>Refuses an invocation.</summary>
    /// <param name="reason">Why.</param>
    /// <param name="description">A description an operator can act on.</param>
    /// <returns>The decision.</returns>
    public static ConnectorAuthorization Deny(ConnectorAuthorizationReason reason, string description) =>
        new(false, reason, description);

    /// <summary>Turns a refusal into the error the invocation fails with.</summary>
    /// <returns>The error.</returns>
    public ConnectorError ToError() => Reason switch
    {
        ConnectorAuthorizationReason.NoCaller or ConnectorAuthorizationReason.NotAuthenticated =>
            ConnectorError.Unauthorized("Connector.Authorize.NotAuthenticated", Description),
        ConnectorAuthorizationReason.InstanceDisabled =>
            ConnectorError.Permanent("Connector.Authorize.Disabled", Description),
        _ => ConnectorError.Forbidden("Connector.Authorize." + Reason, Description),
    };
}

/// <summary>
/// Decides whether a caller may invoke an operation on an instance.
/// <para>
/// This is a <b>port, not a second authorization system</b>. The runtime names what is being asked for — a
/// permission, a tenant, an instance — and something above it answers. The default implementation evaluates
/// the permissions the caller arrived with; a host that has the platform's security engine wired in replaces
/// it with an adapter, and no connector, no engine and no business module changes.
/// </para>
/// </summary>
public interface IConnectorAuthorizer
{
    /// <summary>Decides whether an invocation may proceed.</summary>
    /// <param name="caller">Who is asking; <see langword="null"/> when the request carried nobody.</param>
    /// <param name="instance">The instance being invoked.</param>
    /// <param name="operation">The operation being invoked.</param>
    /// <returns>The decision, carrying why it went the way it did.</returns>
    ConnectorAuthorization Authorize(
        ConnectorCaller? caller, ConnectorInstance instance, ConnectorOperation operation);
}

/// <summary>
/// The default <see cref="IConnectorAuthorizer"/>: it evaluates the permissions the caller arrived holding,
/// in the platform's <c>resource.action</c> grammar.
/// <para>
/// The decision order is fixed and the first two steps are <b>invariants rather than policy</b>. An
/// unauthenticated caller holds nothing, whatever permissions it names — otherwise "who are you?" would be
/// answered by the answer itself. And a caller acting in one tenant may never reach an instance belonging to
/// another: that is checked structurally, before any permission is consulted, and there is no permission and
/// no configuration that grants around it.
/// </para>
/// </summary>
public sealed class PermissionConnectorAuthorizer : IConnectorAuthorizer
{
    /// <inheritdoc />
    public ConnectorAuthorization Authorize(
        ConnectorCaller? caller, ConnectorInstance instance, ConnectorOperation operation)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(operation);

        if (caller is null)
        {
            return ConnectorAuthorization.Deny(
                ConnectorAuthorizationReason.NoCaller,
                "The request named nobody, so nothing could be granted to it.");
        }

        if (!caller.IsAuthenticated)
        {
            return ConnectorAuthorization.Deny(
                ConnectorAuthorizationReason.NotAuthenticated,
                $"Caller '{caller.Subject}' is not authenticated and holds nothing.");
        }

        if (!string.Equals(caller.Tenant, instance.Tenant, StringComparison.OrdinalIgnoreCase))
        {
            return ConnectorAuthorization.Deny(
                ConnectorAuthorizationReason.TenantMismatch,
                $"Caller '{caller.Subject}' is acting in tenant '{caller.Tenant}' but connector instance "
                + $"'{instance.Key}' belongs to '{instance.Tenant}'.");
        }

        if (!instance.Enabled)
        {
            return ConnectorAuthorization.Deny(
                ConnectorAuthorizationReason.InstanceDisabled,
                $"Connector instance '{instance.Key}' has been switched off.");
        }

        foreach (var held in caller.Permissions)
        {
            if (ConnectorPermission.TryParse(held, out var permission) && permission.Grants(operation.Permission))
            {
                return ConnectorAuthorization.Allow(
                    $"Caller '{caller.Subject}' holds '{permission}', which covers '{operation.Permission}'.");
            }
        }

        return ConnectorAuthorization.Deny(
            ConnectorAuthorizationReason.MissingPermission,
            $"Caller '{caller.Subject}' holds nothing covering '{operation.Permission}', which operation "
            + $"'{operation.Name}' requires.");
    }
}
