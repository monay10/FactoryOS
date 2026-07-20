namespace FactoryOS.Contracts.StandardModel;

/// <summary>
/// Canonical representation of a directory group. LDAP <c>groupOfNames</c>, Active Directory <c>group</c> and
/// Microsoft Entra ID <c>group</c> all normalize into this single entity.
/// </summary>
public sealed record DirectoryGroup : IStandardEntity
{
    /// <summary>The canonical entity type name.</summary>
    public const string Type = "DirectoryGroup";

    /// <inheritdoc />
    public required string Tenant { get; init; }

    /// <summary>Gets the group name; the natural key of the group within a tenant.</summary>
    public required string GroupName { get; init; }

    /// <summary>Gets the human-readable display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Gets an optional description of the group.</summary>
    public string? Description { get; init; }

    /// <inheritdoc />
    public string EntityType => Type;

    /// <inheritdoc />
    public string NaturalKey => GroupName;
}
