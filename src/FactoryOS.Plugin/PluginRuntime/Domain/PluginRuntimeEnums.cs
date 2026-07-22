namespace FactoryOS.Plugins.Runtime.Domain;

/// <summary>
/// The lifecycle status of a plugin <b>instance</b> — one tenant's activation of one plugin.
/// <para>
/// This is deliberately richer than the framework's <see cref="FactoryOS.Contracts.Plugins.PluginState"/>,
/// which tracks a plugin process-wide. A runtime instance can additionally be installed but not loaded,
/// suspended rather than stopped, mid-update, or removed — states that only exist once a plugin can be
/// managed per tenant at run time.
/// </para>
/// </summary>
public enum PluginRuntimeStatus
{
    /// <summary>The package is known but nothing has been installed for this tenant.</summary>
    Discovered = 0,

    /// <summary>The package is installed for the tenant; its assembly has not been loaded.</summary>
    Installed = 1,

    /// <summary>The plugin's assembly is loaded and its instance activated, but it has not started.</summary>
    Loaded = 2,

    /// <summary>The plugin is starting.</summary>
    Starting = 3,

    /// <summary>The plugin is running and accepting work.</summary>
    Running = 4,

    /// <summary>The plugin is loaded and holds its state, but refuses new work until it is resumed.</summary>
    Suspended = 5,

    /// <summary>The plugin is stopping.</summary>
    Stopping = 6,

    /// <summary>The plugin has stopped; its instance is still loaded.</summary>
    Stopped = 7,

    /// <summary>The plugin is being replaced by another version.</summary>
    Updating = 8,

    /// <summary>The plugin failed a transition; see the recorded reason.</summary>
    Failed = 9,

    /// <summary>The plugin has been removed from the tenant.</summary>
    Removed = 10,
}

/// <summary>
/// One step in the plugin lifecycle. Every transition the runtime performs names its phase, so an audit
/// line, a measurement and an event all describe the same thing with the same word.
/// </summary>
public enum PluginLifecyclePhase
{
    /// <summary>Placing a validated package into a tenant's set of installed plugins.</summary>
    Install = 0,

    /// <summary>Loading the package's assembly and activating its entry type.</summary>
    Load = 1,

    /// <summary>Starting a loaded plugin.</summary>
    Start = 2,

    /// <summary>Stopping a running plugin.</summary>
    Stop = 3,

    /// <summary>Pausing a running plugin without unloading it.</summary>
    Suspend = 4,

    /// <summary>Returning a suspended plugin to service.</summary>
    Resume = 5,

    /// <summary>Releasing the plugin's assembly load context.</summary>
    Unload = 6,

    /// <summary>Replacing the installed version with a newer one.</summary>
    Update = 7,

    /// <summary>Returning to the version an update replaced.</summary>
    Rollback = 8,

    /// <summary>Removing the plugin from the tenant entirely.</summary>
    Remove = 9,
}

/// <summary>
/// A published extension point — the only surface through which a plugin joins the platform.
/// <para>
/// The list is closed on purpose. A plugin cannot invent an extension point, and the platform never
/// discovers one from a plugin's assembly: contributing to an unknown point is a validation failure, which
/// is what keeps a plugin from reaching into an engine's internals and calling it an extension.
/// </para>
/// </summary>
public enum PluginExtensionPointKind
{
    /// <summary>Workflow activities, triggers and definitions.</summary>
    Workflow = 0,

    /// <summary>Form field types, layouts and validators.</summary>
    Forms = 1,

    /// <summary>Human task types and assignment strategies.</summary>
    HumanTask = 2,

    /// <summary>Approval policies and resolution strategies.</summary>
    Approval = 3,

    /// <summary>Notification templates and channel bindings.</summary>
    Notification = 4,

    /// <summary>Service-level definitions and escalation policies.</summary>
    Sla = 5,

    /// <summary>Audit entry kinds and enrichers.</summary>
    Audit = 6,

    /// <summary>Metric definitions and health probes.</summary>
    Monitoring = 7,

    /// <summary>Permissions, roles and policy contributions.</summary>
    Security = 8,

    /// <summary>Connector definitions and operations.</summary>
    Connector = 9,

    /// <summary>HTTP endpoints mounted through the gateway.</summary>
    Api = 10,

    /// <summary>Screens, widgets and navigation entries described as data.</summary>
    UiMetadata = 11,

    /// <summary>Rule-engine predicates and actions.</summary>
    Rules = 12,

    /// <summary>Agents, prompts and tools.</summary>
    Ai = 13,

    /// <summary>Report definitions and data sets.</summary>
    Reporting = 14,
}

/// <summary>How much of the host a plugin is allowed to share.</summary>
public enum PluginIsolationMode
{
    /// <summary>The plugin is compiled into the host and shares its load context — first-party only.</summary>
    Shared = 0,

    /// <summary>The plugin's private dependencies load into their own collectible context.</summary>
    AssemblyIsolated = 1,

    /// <summary>The plugin is additionally gated by permissions and a resource quota on every call.</summary>
    Sandboxed = 2,
}

/// <summary>A resource a sandboxed plugin consumes and can therefore exhaust.</summary>
public enum PluginResourceKind
{
    /// <summary>Operations running at once.</summary>
    Concurrency = 0,

    /// <summary>Memory held.</summary>
    Memory = 1,

    /// <summary>Storage occupied under the instance's isolated root.</summary>
    Storage = 2,
}

/// <summary>
/// One question the health engine asks about a plugin instance. They are separate because they fail
/// separately and are repaired differently.
/// </summary>
public enum PluginHealthAspect
{
    /// <summary>Is the instance in a state that can serve at all?</summary>
    Liveness = 0,

    /// <summary>Is it beating within its heartbeat window?</summary>
    Readiness = 1,

    /// <summary>Is every plugin it depends on installed, compatible and running?</summary>
    Dependencies = 2,

    /// <summary>Is the installed version still the one the tenant pinned?</summary>
    Version = 3,

    /// <summary>Has the tenant granted everything the plugin's manifest asks for?</summary>
    Permissions = 4,

    /// <summary>Is it within its resource quota?</summary>
    Resources = 5,
}

/// <summary>The algorithm a package signature was produced with.</summary>
public enum PluginSignatureAlgorithm
{
    /// <summary>The package carries no signature.</summary>
    None = 0,

    /// <summary>A keyed HMAC over the package's canonical content, using SHA-256.</summary>
    HmacSha256 = 1,
}

/// <summary>Why a plugin failed, in a form an operator can act on without reading a stack trace.</summary>
public enum PluginFailureKind
{
    /// <summary>The cause was not classified.</summary>
    Unknown = 0,

    /// <summary>The manifest was missing, malformed or incomplete.</summary>
    Manifest = 1,

    /// <summary>The package signature was absent when required, or did not verify.</summary>
    Signature = 2,

    /// <summary>A declared dependency was missing, or the dependency graph has a cycle.</summary>
    Dependency = 3,

    /// <summary>A dependency was present at a version that does not satisfy the requirement.</summary>
    Version = 4,

    /// <summary>The package does not support this platform version.</summary>
    Compatibility = 5,

    /// <summary>The plugin asked for a permission the tenant has not granted.</summary>
    Permission = 6,

    /// <summary>The entry assembly or entry type could not be loaded or activated.</summary>
    Activation = 7,

    /// <summary>A lifecycle transition was refused or threw.</summary>
    Lifecycle = 8,

    /// <summary>The instance exceeded its resource quota.</summary>
    Resource = 9,
}

/// <summary>
/// Classifies a failure from the error code that reported it.
/// <para>
/// The code is already the classification — <c>Plugin.Runtime.Signature.Invalid</c> says what went wrong far
/// more precisely than a second enum assigned by hand at each call site would. Deriving the kind here keeps
/// the two from drifting apart.
/// </para>
/// </summary>
public static class PluginFailures
{
    /// <summary>Classifies a failure from its error code.</summary>
    /// <param name="errorCode">The code of the error that reported the failure.</param>
    /// <returns>The failure kind.</returns>
    public static PluginFailureKind Classify(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            return PluginFailureKind.Unknown;
        }

        return errorCode switch
        {
            var code when Mentions(code, "Signature") => PluginFailureKind.Signature,
            var code when Mentions(code, "Manifest") => PluginFailureKind.Manifest,
            var code when Mentions(code, "Permission") || Mentions(code, "Forbidden")
                || Mentions(code, "Unauthenticated") => PluginFailureKind.Permission,
            var code when Mentions(code, "Compatibility") => PluginFailureKind.Compatibility,
            var code when Mentions(code, "Dependency") || Mentions(code, "Capability") =>
                PluginFailureKind.Dependency,
            var code when Mentions(code, "Version") => PluginFailureKind.Version,
            var code when Mentions(code, "Load") || Mentions(code, "Activate") => PluginFailureKind.Activation,
            var code when Mentions(code, "Resource") || Mentions(code, "Quota") => PluginFailureKind.Resource,
            _ => PluginFailureKind.Lifecycle,
        };
    }

    private static bool Mentions(string code, string segment) =>
        code.Contains(segment, StringComparison.OrdinalIgnoreCase);
}
