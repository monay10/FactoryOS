namespace FactoryOS.Gateway.Ui;

/// <summary>
/// The aggregated, serializable UI registry the shell fetches once at start-up to build its
/// navigation and lazy-load module screens on demand.
/// </summary>
/// <param name="Modules">The UI contributions of every active module, ordered by module key.</param>
public sealed record ModuleUiCatalog(IReadOnlyList<ModuleUiModule> Modules);
