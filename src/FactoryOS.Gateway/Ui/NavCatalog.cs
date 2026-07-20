namespace FactoryOS.Gateway.Ui;

/// <summary>
/// The shell's navigation model: every active module's UI screens flattened and regrouped by navigation
/// <b>section</b> rather than by module, so a sidebar can render its section headings directly. It is the
/// cross-module counterpart to <see cref="ModuleUiCatalog"/> (which stays module-centric for lazy-loading).
/// Built purely from manifests, so the nav varies only by which plugins are active — never by core code.
/// </summary>
/// <param name="Sections">The navigation sections, ordered by section name (unsectioned screens sort first under an empty name).</param>
public sealed record NavCatalog(IReadOnlyList<NavSection> Sections);

/// <summary>One navigation section — a heading in the shell sidebar and the screens grouped under it.</summary>
/// <param name="Section">The section name (an empty string groups screens that declare no section).</param>
/// <param name="Items">The section's screens, ordered by declared order, then title, then owning module.</param>
public sealed record NavSection(string Section, IReadOnlyList<NavItem> Items);

/// <summary>One screen in the navigation, flattened with the key of the module that owns it.</summary>
/// <param name="Module">The manifest key of the module that contributes the screen.</param>
/// <param name="Id">The screen's plugin-unique identifier.</param>
/// <param name="Title">The screen title shown in navigation.</param>
/// <param name="Route">The client-side route the screen is mounted at.</param>
/// <param name="Component">The lazily loaded component the shell resolves against the module's bundle.</param>
/// <param name="Icon">The optional icon key for the navigation entry.</param>
/// <param name="RequiredPermission">The permission a user must hold to see the entry, if any.</param>
/// <param name="Order">The screen's sort order within its section; lower sorts first.</param>
public sealed record NavItem(
    string Module,
    string Id,
    string Title,
    string Route,
    string Component,
    string? Icon,
    string? RequiredPermission,
    int Order);
