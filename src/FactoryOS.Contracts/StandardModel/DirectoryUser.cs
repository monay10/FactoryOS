namespace FactoryOS.Contracts.StandardModel;

/// <summary>
/// Canonical representation of a directory user. Vendor dialects such as LDAP <c>inetOrgPerson</c>, Active
/// Directory <c>user</c> and Microsoft Entra ID <c>user</c> all normalize into this single entity, so no
/// module or agent ever speaks a directory dialect.
/// </summary>
public sealed record DirectoryUser : IStandardEntity
{
    /// <summary>The canonical entity type name.</summary>
    public const string Type = "DirectoryUser";

    /// <inheritdoc />
    public required string Tenant { get; init; }

    /// <summary>Gets the login name; the natural key of the user within a tenant (for example a UPN or sAMAccountName).</summary>
    public required string Username { get; init; }

    /// <summary>Gets the human-readable display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Gets the primary email address, if known.</summary>
    public string? Email { get; init; }

    /// <summary>Gets a value indicating whether the account is enabled.</summary>
    public bool Enabled { get; init; } = true;

    /// <inheritdoc />
    public string EntityType => Type;

    /// <inheritdoc />
    public string NaturalKey => Username;
}
