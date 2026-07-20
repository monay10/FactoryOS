namespace FactoryOS.Identity.Seeding;

/// <summary>
/// Configures the default identity seed — the demo tenant's roles and one user per role — so a fresh
/// deployment has something to log in with. Disabled by default; the password is supplied by configuration
/// (a secret placeholder), never hard-coded, so no real credential ships in the repository.
/// </summary>
public sealed class IdentitySeedOptions
{
    /// <summary>The configuration section these options bind from (<c>Identity:Seed</c>).</summary>
    public const string SectionName = "Identity:Seed";

    /// <summary>The fixed tenant the default seed is created under (so a client knows which tenant to log in to).</summary>
    public static readonly Guid DefaultTenantId = new("11111111-1111-1111-1111-111111111111");

    /// <summary>Gets or sets a value indicating whether the seed runs at start-up. Defaults to <see langword="false"/>.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the tenant the seed is created under. Defaults to <see cref="DefaultTenantId"/>.</summary>
    public Guid TenantId { get; set; } = DefaultTenantId;

    /// <summary>
    /// Gets or sets the password assigned to every seeded demo user. Supplied by configuration (a secret
    /// placeholder). When empty, roles are still seeded but no users are created — no credential is invented.
    /// </summary>
    public string? Password { get; set; }
}
