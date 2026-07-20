namespace FactoryOS.Connectors.Ldap;

/// <summary>
/// The transport the LDAP connector reads through. Abstracting the directory search keeps the connector free
/// of any specific LDAP library and fully offline-testable: production wires a real LDAP/LDAPS client, tests
/// feed canned entries.
/// </summary>
public interface ILdapClient
{
    /// <summary>Searches a subtree and streams matching entries.</summary>
    /// <param name="baseDn">The search base distinguished name.</param>
    /// <param name="filter">The LDAP search filter (for example <c>(objectClass=inetOrgPerson)</c>).</param>
    /// <param name="cancellationToken">A token to cancel the search.</param>
    /// <returns>An asynchronous stream of directory entries.</returns>
    IAsyncEnumerable<LdapEntry> SearchAsync(string baseDn, string filter, CancellationToken cancellationToken);
}

/// <summary>A single directory entry: its distinguished name and its attributes in the source dialect.</summary>
/// <param name="Dn">The entry's distinguished name.</param>
/// <param name="Attributes">The entry's attributes, keyed by LDAP attribute name (for example <c>uid</c>, <c>cn</c>, <c>mail</c>).</param>
public sealed record LdapEntry(string Dn, IReadOnlyDictionary<string, object?> Attributes);
