namespace FactoryOS.Connectors.Ldap;

/// <summary>
/// Strongly-typed configuration for the LDAP connector. A new directory is configuration only: the search
/// bases and filters that select users and groups. The connection itself is supplied by the injected
/// <see cref="ILdapClient"/>.
/// </summary>
public sealed record LdapConnectorOptions
{
    /// <summary>Gets the base DN under which users are searched.</summary>
    public required string UserBaseDn { get; init; }

    /// <summary>Gets the search filter for user entries.</summary>
    public string UserFilter { get; init; } = "(objectClass=inetOrgPerson)";

    /// <summary>Gets the base DN under which groups are searched.</summary>
    public required string GroupBaseDn { get; init; }

    /// <summary>Gets the search filter for group entries.</summary>
    public string GroupFilter { get; init; } = "(objectClass=groupOfNames)";
}
