namespace FactoryOS.Connectors.Runtime.Domain;

/// <summary>
/// A connection held open across several invocations of one instance. Opening a session to an ERP or a PLC
/// is expensive, so the runtime keeps one and lets it lapse when it stops being used, rather than paying the
/// cost per call or holding it forever.
/// <para>
/// The session belongs to a tenant's instance, never to a caller: two callers in the same factory share the
/// factory's connection, and no caller can reach another factory's.
/// </para>
/// </summary>
public sealed class ConnectorSession
{
    /// <summary>Initializes a new instance of the <see cref="ConnectorSession"/> class.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="tenant">The tenant that owns it.</param>
    /// <param name="instance">The instance key it belongs to.</param>
    /// <param name="openedUtc">When it was opened.</param>
    /// <param name="idleTimeout">How long it may sit unused before it lapses.</param>
    public ConnectorSession(
        Guid id, string tenant, string instance, DateTimeOffset openedUtc, TimeSpan idleTimeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(instance);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idleTimeout, TimeSpan.Zero);

        Id = id;
        Tenant = tenant;
        Instance = instance;
        OpenedUtc = openedUtc;
        LastUsedUtc = openedUtc;
        IdleTimeout = idleTimeout;
    }

    /// <summary>Gets the session identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the tenant that owns the session.</summary>
    public string Tenant { get; }

    /// <summary>Gets the instance key the session belongs to.</summary>
    public string Instance { get; }

    /// <summary>Gets the identity the session is filed under.</summary>
    public string Identity => ConnectorInstance.Identify(Tenant, Instance);

    /// <summary>Gets when the session was opened.</summary>
    public DateTimeOffset OpenedUtc { get; }

    /// <summary>Gets when the session was last used.</summary>
    public DateTimeOffset LastUsedUtc { get; private set; }

    /// <summary>Gets how long the session may sit unused before it lapses.</summary>
    public TimeSpan IdleTimeout { get; }

    /// <summary>Gets when the session was closed, if it has been.</summary>
    public DateTimeOffset? ClosedUtc { get; private set; }

    /// <summary>Gets how many invocations have used the session.</summary>
    public int Uses { get; private set; }

    /// <summary>Determines whether the session may still be used.</summary>
    /// <param name="nowUtc">The current instant.</param>
    /// <returns><see langword="true"/> when it is open and has not lapsed.</returns>
    public bool IsActive(DateTimeOffset nowUtc) => ClosedUtc is null && nowUtc <= LastUsedUtc + IdleTimeout;

    /// <summary>Records a use, sliding the idle window forward.</summary>
    /// <param name="nowUtc">The current instant.</param>
    /// <returns><see langword="true"/> when the session was active and the use was recorded.</returns>
    public bool Touch(DateTimeOffset nowUtc)
    {
        if (!IsActive(nowUtc))
        {
            return false;
        }

        LastUsedUtc = nowUtc;
        Uses++;
        return true;
    }

    /// <summary>Closes the session. Closing an already-closed session changes nothing.</summary>
    /// <param name="nowUtc">The current instant.</param>
    public void Close(DateTimeOffset nowUtc) => ClosedUtc ??= nowUtc;
}
