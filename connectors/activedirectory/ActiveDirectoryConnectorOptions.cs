namespace FactoryOS.Connectors.ActiveDirectory;

/// <summary>
/// Strongly-typed configuration for the Active Directory connector. A new domain is configuration only: the
/// search bases and filters that select users and groups. The connection is supplied by the injected
/// <see cref="IActiveDirectory"/>.
/// </summary>
public sealed record ActiveDirectoryConnectorOptions
{
    /// <summary>Gets the base DN under which users are searched.</summary>
    public required string UserSearchBase { get; init; }

    /// <summary>Gets the search filter for user entries.</summary>
    public string UserFilter { get; init; } = "(&(objectCategory=person)(objectClass=user))";

    /// <summary>Gets the base DN under which groups are searched.</summary>
    public required string GroupSearchBase { get; init; }

    /// <summary>Gets the search filter for group entries.</summary>
    public string GroupFilter { get; init; } = "(objectClass=group)";
}
