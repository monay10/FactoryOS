namespace FactoryOS.Connectors.ActiveDirectory;

/// <summary>
/// The transport the Active Directory connector reads through. Abstracting the directory search keeps the
/// connector free of any specific AD/LDAP library and fully offline-testable: production wires a real
/// LDAP/Global Catalog client, tests feed canned entries.
/// </summary>
public interface IActiveDirectory
{
    /// <summary>Searches a subtree and streams matching entries.</summary>
    /// <param name="searchBase">The search base distinguished name.</param>
    /// <param name="filter">The LDAP search filter (for example <c>(objectClass=user)</c>).</param>
    /// <param name="cancellationToken">A token to cancel the search.</param>
    /// <returns>An asynchronous stream of directory entries.</returns>
    IAsyncEnumerable<AdEntry> SearchAsync(string searchBase, string filter, CancellationToken cancellationToken);
}

/// <summary>A single Active Directory entry, its attributes in the AD dialect.</summary>
/// <param name="Attributes">The entry's attributes, keyed by AD attribute name (for example <c>sAMAccountName</c>, <c>userAccountControl</c>).</param>
public sealed record AdEntry(IReadOnlyDictionary<string, object?> Attributes);
