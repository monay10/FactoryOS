namespace FactoryOS.Plugins.Forms.Engine.Configuration;

/// <summary>Stable constants for the forms engine runtime.</summary>
public static class FormConstants
{
    /// <summary>The configuration section the engine options bind from.</summary>
    public const string ConfigurationSection = "Forms:Engine";

    /// <summary>The tenant used when a caller supplies none.</summary>
    public const string DefaultTenant = "default";

    /// <summary>The culture used when a form declares no localization.</summary>
    public const string DefaultCulture = "en";
}

/// <summary>
/// Runtime options for the forms engine (namespace <c>Engine.Configuration</c>). These govern the form
/// runtime; they are independent of the workflow engine's own options.
/// </summary>
public sealed record FormEngineOptions
{
    /// <summary>
    /// Gets a value indicating whether opening an instance from an in-memory definition object also registers
    /// it in the repository (so the instance can be reloaded and resumed later).
    /// </summary>
    public bool AutoRegisterDefinitions { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether saving a draft on a freshly opened instance moves it to the draft
    /// state. When <see langword="false"/> the instance stays open until submitted.
    /// </summary>
    public bool TrackDraftState { get; init; } = true;

    /// <summary>Gets the default culture used when a form declares no localization.</summary>
    public string DefaultCulture { get; init; } = FormConstants.DefaultCulture;
}

/// <summary>
/// The caller-supplied context a form is opened within: the owning tenant and, optionally, the acting user.
/// The tenant is stamped onto the instance so no filling can cross tenants.
/// </summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="User">The acting user, if known.</param>
/// <param name="Culture">The requested culture, or <see langword="null"/> for the engine default.</param>
public sealed record FormContext(string Tenant, string? User = null, string? Culture = null)
{
    /// <summary>A context for the default tenant.</summary>
    public static FormContext Default { get; } = new(FormConstants.DefaultTenant);
}
