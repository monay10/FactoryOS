namespace FactoryOS.Plugins.Forms.Engine.Domain;

/// <summary>
/// The immutable design-time description of a form: its identity and version, the layout and the section /
/// group / field tree, its permissions and assignment, and the optional workflow activity key it satisfies.
/// A form key plus its version identifies one specific layout; open instances keep the version they started on.
/// </summary>
public sealed class FormDefinition
{
    private readonly Dictionary<string, FieldDefinition> _fields;

    internal FormDefinition(
        string key,
        string name,
        FormVersion version,
        string title,
        FormLayout layout,
        IReadOnlyList<FormSection> sections,
        IReadOnlyList<FormPermission> permissions,
        FormAssignment? assignment,
        string? activityKey,
        Dictionary<string, FieldDefinition> fields)
    {
        Key = key;
        Name = name;
        Version = version;
        Title = title;
        Layout = layout;
        Sections = sections;
        Permissions = permissions;
        Assignment = assignment;
        ActivityKey = activityKey;
        _fields = fields;
    }

    /// <summary>Gets the form key.</summary>
    public string Key { get; }

    /// <summary>Gets the display name.</summary>
    public string Name { get; }

    /// <summary>Gets the form version.</summary>
    public FormVersion Version { get; }

    /// <summary>Gets the form title shown to the user.</summary>
    public string Title { get; }

    /// <summary>Gets the layout strategy.</summary>
    public FormLayout Layout { get; }

    /// <summary>Gets the sections that make up the form.</summary>
    public IReadOnlyList<FormSection> Sections { get; }

    /// <summary>Gets the access grants on the form.</summary>
    public IReadOnlyList<FormPermission> Permissions { get; }

    /// <summary>Gets the assignment that names who fills the form, if any.</summary>
    public FormAssignment? Assignment { get; }

    /// <summary>
    /// Gets the workflow activity key this form satisfies. When an instance bound to a workflow activity is
    /// submitted, that activity completes and the workflow advances.
    /// </summary>
    public string? ActivityKey { get; }

    /// <summary>Gets every field in the form, keyed by field key.</summary>
    public IReadOnlyDictionary<string, FieldDefinition> Fields => _fields;

    /// <summary>Gets a field by key.</summary>
    /// <param name="key">The field key.</param>
    /// <returns>The field.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no field has the key.</exception>
    public FieldDefinition Field(string key) => _fields.TryGetValue(key, out var field)
        ? field
        : throw new KeyNotFoundException($"Form '{Key}' has no field '{key}'.");

    /// <summary>Begins building a form definition.</summary>
    /// <param name="key">The form key.</param>
    /// <param name="name">The display name.</param>
    /// <param name="version">The version; defaults to the initial version.</param>
    /// <returns>A builder.</returns>
    public static FormDefinitionBuilder Create(string key, string name, FormVersion version = default) =>
        new(key, name, version.Value < 1 ? FormVersion.Initial : version);
}

/// <summary>A fluent builder that assembles and validates a <see cref="FormDefinition"/>.</summary>
public sealed class FormDefinitionBuilder
{
    private readonly string _key;
    private readonly string _name;
    private readonly FormVersion _version;
    private readonly List<FormSection> _sections = [];
    private readonly List<FormPermission> _permissions = [];
    private string? _title;
    private FormLayout _layout = FormLayout.Stack;
    private FormAssignment? _assignment;
    private string? _activityKey;

    /// <summary>Initializes a new instance of the <see cref="FormDefinitionBuilder"/> class.</summary>
    /// <param name="key">The form key.</param>
    /// <param name="name">The display name.</param>
    /// <param name="version">The version.</param>
    public FormDefinitionBuilder(string key, string name, FormVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _key = key;
        _name = name;
        _version = version;
    }

    /// <summary>Sets the form title.</summary>
    /// <param name="title">The title.</param>
    /// <returns>The same builder.</returns>
    public FormDefinitionBuilder WithTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        _title = title;
        return this;
    }

    /// <summary>Sets the layout.</summary>
    /// <param name="layout">The layout.</param>
    /// <returns>The same builder.</returns>
    public FormDefinitionBuilder WithLayout(FormLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        _layout = layout;
        return this;
    }

    /// <summary>Binds the form to a workflow activity key it satisfies on submission.</summary>
    /// <param name="activityKey">The workflow activity key.</param>
    /// <returns>The same builder.</returns>
    public FormDefinitionBuilder ForActivity(string activityKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activityKey);
        _activityKey = activityKey;
        return this;
    }

    /// <summary>Sets the assignment.</summary>
    /// <param name="assignment">The assignment.</param>
    /// <returns>The same builder.</returns>
    public FormDefinitionBuilder AssignedTo(FormAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        _assignment = assignment;
        return this;
    }

    /// <summary>Adds an access grant.</summary>
    /// <param name="permission">The permission.</param>
    /// <returns>The same builder.</returns>
    public FormDefinitionBuilder AddPermission(FormPermission permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        _permissions.Add(permission);
        return this;
    }

    /// <summary>Adds a section.</summary>
    /// <param name="section">The section.</param>
    /// <returns>The same builder.</returns>
    public FormDefinitionBuilder AddSection(FormSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        _sections.Add(section);
        return this;
    }

    /// <summary>Validates and builds the definition.</summary>
    /// <returns>The built definition.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the structure is invalid.</exception>
    public FormDefinition Build()
    {
        if (_sections.Count == 0)
        {
            throw new InvalidOperationException($"Form '{_key}' must have at least one section.");
        }

        var sectionKeys = new HashSet<string>(StringComparer.Ordinal);
        var groupKeys = new HashSet<string>(StringComparer.Ordinal);
        var fields = new Dictionary<string, FieldDefinition>(StringComparer.Ordinal);

        foreach (var section in _sections)
        {
            if (!sectionKeys.Add(section.Key))
            {
                throw new InvalidOperationException($"Duplicate section key '{section.Key}' in form '{_key}'.");
            }

            foreach (var group in section.Groups)
            {
                if (!groupKeys.Add(group.Key))
                {
                    throw new InvalidOperationException($"Duplicate group key '{group.Key}' in form '{_key}'.");
                }

                foreach (var placement in group.Fields)
                {
                    if (!fields.TryAdd(placement.Field.Key, placement.Field))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate field key '{placement.Field.Key}' in form '{_key}'.");
                    }
                }
            }
        }

        if (fields.Count == 0)
        {
            throw new InvalidOperationException($"Form '{_key}' must have at least one field.");
        }

        return new FormDefinition(
            _key, _name, _version, _title ?? _name, _layout,
            _sections, _permissions, _assignment, _activityKey, fields);
    }
}
