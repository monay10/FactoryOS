namespace FactoryOS.Plugins.Workflow.Security.Domain;

/// <summary>
/// The breadth a decision belongs to. Every decision is scoped to a tenant; the optional organization and
/// module narrow it further, so a plant manager can be given authority over their own site without being given
/// it over the whole tenant.
/// </summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="Organization">The organization or site, when the decision belongs to one.</param>
/// <param name="Module">The module or plugin key the decision came from.</param>
public sealed record SecurityScope(string Tenant, string? Organization = null, string? Module = null)
{
    /// <summary>Creates a scope covering a whole tenant.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The scope.</returns>
    public static SecurityScope ForTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return new SecurityScope(tenant);
    }

    /// <summary>
    /// Gets a value indicating whether this scope contains another — a tenant-wide scope contains every scope
    /// inside it, while a scope narrowed to one site does not contain its siblings.
    /// </summary>
    /// <param name="other">The scope being tested.</param>
    /// <returns><see langword="true"/> when this scope covers <paramref name="other"/>.</returns>
    public bool Contains(SecurityScope other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (!string.Equals(Tenant, other.Tenant, StringComparison.Ordinal))
        {
            return false;
        }

        if (Organization is not null
            && !string.Equals(Organization, other.Organization, StringComparison.Ordinal))
        {
            return false;
        }

        return Module is null || string.Equals(Module, other.Module, StringComparison.Ordinal);
    }
}

/// <summary>
/// What is being done. Actions are the second half of a permission, named once here so a rule and a permission
/// can never drift into spelling the same verb two ways.
/// </summary>
/// <param name="Name">The verb.</param>
public sealed record SecurityAction(string Name)
{
    /// <summary>Look at something.</summary>
    public static SecurityAction Read { get; } = new("read");

    /// <summary>Bring something into existence.</summary>
    public static SecurityAction Create { get; } = new("create");

    /// <summary>Change something.</summary>
    public static SecurityAction Update { get; } = new("update");

    /// <summary>Remove something.</summary>
    public static SecurityAction Delete { get; } = new("delete");

    /// <summary>Run something.</summary>
    public static SecurityAction Execute { get; } = new("execute");

    /// <summary>Decide on something.</summary>
    public static SecurityAction Approve { get; } = new("approve");

    /// <summary>Take a copy of something out of the platform.</summary>
    public static SecurityAction Export { get; } = new("export");

    /// <summary>Configure something.</summary>
    public static SecurityAction Manage { get; } = new("manage");

    /// <summary>Creates an action, normalising the verb.</summary>
    /// <param name="name">The verb.</param>
    /// <returns>The action.</returns>
    public static SecurityAction Of(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new SecurityAction(name.Trim().ToLowerInvariant());
    }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>
/// What is being acted on. A resource is more than a type name: it carries the specific instance and the
/// attributes a rule may reason about — its owner, its state, the site it belongs to.
/// <para>
/// Those attributes are what make <b>resource-based</b> and <b>attribute-based</b> authorization possible at
/// all. "An operator may cancel a workflow they started" is not expressible against a type; it needs the
/// instance and its owner, and that is exactly what this carries.
/// </para>
/// </summary>
public sealed class SecurityResource
{
    private readonly Dictionary<string, string> _attributes;

    /// <summary>Initializes a new instance of the <see cref="SecurityResource"/> class.</summary>
    /// <param name="type">The resource segment of a permission (<c>workflow</c>, <c>audit</c>, …).</param>
    /// <param name="key">The definition key or name, when there is one.</param>
    /// <param name="id">The instance identifier, when the request concerns a specific instance.</param>
    /// <param name="attributes">The attributes rules may reason about.</param>
    public SecurityResource(
        string type,
        string? key = null,
        string? id = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        Type = type.Trim().ToLowerInvariant();
        Key = key;
        Id = id;
        _attributes = attributes is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The attribute naming who owns a resource instance.</summary>
    public const string OwnerAttribute = "owner";

    /// <summary>Gets the resource segment of a permission.</summary>
    public string Type { get; }

    /// <summary>Gets the definition key or name.</summary>
    public string? Key { get; }

    /// <summary>Gets the instance identifier.</summary>
    public string? Id { get; }

    /// <summary>Gets the attributes rules may reason about.</summary>
    public IReadOnlyDictionary<string, string> Attributes => _attributes;

    /// <summary>Gets who owns the instance, when it says.</summary>
    public string? Owner => this[OwnerAttribute];

    /// <summary>Gets an attribute value, or <see langword="null"/> when it is not present.</summary>
    /// <param name="name">The attribute name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    public string? this[string name] => _attributes.TryGetValue(name, out var value) ? value : null;

    /// <summary>Creates a resource describing a whole type, with no particular instance.</summary>
    /// <param name="type">The resource type.</param>
    /// <returns>The resource.</returns>
    public static SecurityResource OfType(string type) => new(type);

    /// <summary>Creates a resource describing one instance owned by somebody.</summary>
    /// <param name="type">The resource type.</param>
    /// <param name="key">The definition key.</param>
    /// <param name="id">The instance identifier.</param>
    /// <param name="owner">Who owns it.</param>
    /// <returns>The resource.</returns>
    public static SecurityResource Instance(string type, string key, string id, string? owner = null)
    {
        var resource = new SecurityResource(type, key, id);
        if (owner is not null)
        {
            resource._attributes[OwnerAttribute] = owner;
        }

        return resource;
    }

    /// <summary>Returns a copy carrying an additional attribute.</summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="value">The attribute value.</param>
    /// <returns>A new instance; this one is unchanged.</returns>
    public SecurityResource With(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);

        var copy = new Dictionary<string, string>(_attributes, StringComparer.OrdinalIgnoreCase)
        {
            [name] = value,
        };
        return new SecurityResource(Type, Key, Id, copy);
    }

    /// <inheritdoc />
    public override string ToString() => Id is null ? Type : $"{Type}:{Key ?? Type}:{Id}";
}
