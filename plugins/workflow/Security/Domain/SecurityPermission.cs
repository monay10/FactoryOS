namespace FactoryOS.Plugins.Workflow.Security.Domain;

/// <summary>
/// A permission expressed as <c>resource.action</c> — <c>workflow.start</c>, <c>audit.export</c>. Wildcards
/// widen a grant: <c>workflow.*</c> covers every action on workflows and <c>*</c> covers everything.
/// <para>
/// The grammar is deliberately identical to the platform's existing <c>FactoryOS.Identity.Authorization
/// .Permission</c>, down to the wildcard rules and the lower-casing. A permission string written for one is
/// read the same way by the other, which is the whole point: two authorization vocabularies that disagreed
/// about what <c>energy.*</c> covers would be worse than either alone.
/// </para>
/// </summary>
public sealed record SecurityPermission
{
    /// <summary>The token that matches any resource or action.</summary>
    public const string Wildcard = "*";

    private SecurityPermission(string value, string resource, string action)
    {
        Value = value;
        Resource = resource;
        Action = action;
    }

    /// <summary>Gets the canonical <c>resource.action</c> string.</summary>
    public string Value { get; }

    /// <summary>Gets the resource segment.</summary>
    public string Resource { get; }

    /// <summary>Gets the action segment.</summary>
    public string Action { get; }

    /// <summary>Gets a value indicating whether the permission widens over more than one concrete permission.</summary>
    public bool IsWildcard => Resource == Wildcard || Action == Wildcard;

    /// <summary>Parses a permission string.</summary>
    /// <param name="value">The permission (<c>*</c>, <c>resource.*</c> or <c>resource.action</c>).</param>
    /// <returns>The parsed permission.</returns>
    /// <exception cref="FormatException">The string is not a valid permission.</exception>
    public static SecurityPermission Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized == Wildcard)
        {
            return new SecurityPermission(Wildcard, Wildcard, Wildcard);
        }

        var parts = normalized.Split('.');
        if (parts.Length != 2 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
        {
            throw new FormatException($"'{value}' is not a valid permission (expected 'resource.action').");
        }

        return new SecurityPermission(normalized, parts[0], parts[1]);
    }

    /// <summary>Parses a permission string, reporting failure rather than throwing.</summary>
    /// <param name="value">The permission string.</param>
    /// <param name="permission">The parsed permission, when parsing succeeded.</param>
    /// <returns><see langword="true"/> when the string was a valid permission.</returns>
    public static bool TryParse(string? value, out SecurityPermission? permission)
    {
        try
        {
            permission = Parse(value!);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            permission = null;
            return false;
        }
    }

    /// <summary>Builds a permission from its two segments.</summary>
    /// <param name="resource">The resource segment.</param>
    /// <param name="action">The action segment.</param>
    /// <returns>The permission.</returns>
    public static SecurityPermission Of(string resource, string action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        return Parse($"{resource}.{action}");
    }

    /// <summary>Gets a value indicating whether this permission covers a concrete request.</summary>
    /// <param name="requested">The concrete permission being asked for.</param>
    /// <returns><see langword="true"/> when this permission grants it.</returns>
    public bool Grants(SecurityPermission requested)
    {
        ArgumentNullException.ThrowIfNull(requested);

        if (Resource == Wildcard)
        {
            return true;
        }

        if (!string.Equals(Resource, requested.Resource, StringComparison.Ordinal))
        {
            return false;
        }

        return Action == Wildcard || string.Equals(Action, requested.Action, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// The permission surface of the platform, by area. These are the strings an administrator assigns from, and
/// they are the same for every tenant — a permission means what it means, whoever holds it.
/// <para>
/// The catalogue names <b>areas</b>, not engines. That is what lets the engines stay unmodified: nothing here
/// is imported from a workflow, forms or approval type, so no engine has to know these strings exist for the
/// platform to be able to guard them.
/// </para>
/// </summary>
public static class SecurityPermissions
{
    /// <summary>Everything — the super-administrator grant.</summary>
    public const string All = "*";

    /// <summary>The workflow runtime.</summary>
    public static class Workflow
    {
        /// <summary>The resource segment.</summary>
        public const string Resource = "workflow";

        /// <summary>Read workflow definitions and instances.</summary>
        public const string Read = "workflow.read";

        /// <summary>Start a workflow instance.</summary>
        public const string Start = "workflow.start";

        /// <summary>Cancel a running instance.</summary>
        public const string Cancel = "workflow.cancel";

        /// <summary>Register or change definitions.</summary>
        public const string Manage = "workflow.manage";
    }

    /// <summary>The forms engine.</summary>
    public static class Forms
    {
        /// <summary>The resource segment.</summary>
        public const string Resource = "forms";

        /// <summary>Read form definitions and submissions.</summary>
        public const string Read = "forms.read";

        /// <summary>Open a form instance.</summary>
        public const string Open = "forms.open";

        /// <summary>Submit a filled form.</summary>
        public const string Submit = "forms.submit";

        /// <summary>Publish or change form definitions.</summary>
        public const string Manage = "forms.manage";
    }

    /// <summary>The human task engine.</summary>
    public static class HumanTask
    {
        /// <summary>The resource segment.</summary>
        public const string Resource = "task";

        /// <summary>Read tasks.</summary>
        public const string Read = "task.read";

        /// <summary>Claim or reassign a task.</summary>
        public const string Assign = "task.assign";

        /// <summary>Decide a task.</summary>
        public const string Complete = "task.complete";

        /// <summary>Change task configuration.</summary>
        public const string Manage = "task.manage";
    }

    /// <summary>The approval engine.</summary>
    public static class Approval
    {
        /// <summary>The resource segment.</summary>
        public const string Resource = "approval";

        /// <summary>Read approvals.</summary>
        public const string Read = "approval.read";

        /// <summary>Record an approve or reject decision.</summary>
        public const string Decide = "approval.decide";

        /// <summary>Delegate a decision to somebody else.</summary>
        public const string Delegate = "approval.delegate";

        /// <summary>Change approval configuration.</summary>
        public const string Manage = "approval.manage";
    }

    /// <summary>The notification engine.</summary>
    public static class Notification
    {
        /// <summary>The resource segment.</summary>
        public const string Resource = "notification";

        /// <summary>Read notifications and their history.</summary>
        public const string Read = "notification.read";

        /// <summary>Send a notification.</summary>
        public const string Send = "notification.send";

        /// <summary>Change templates, rules and preferences.</summary>
        public const string Manage = "notification.manage";
    }

    /// <summary>The SLA engine.</summary>
    public static class Sla
    {
        /// <summary>The resource segment.</summary>
        public const string Resource = "sla";

        /// <summary>Read SLA definitions and instances.</summary>
        public const string Read = "sla.read";

        /// <summary>Pause or resume an SLA clock.</summary>
        public const string Control = "sla.control";

        /// <summary>Change SLA definitions and calendars.</summary>
        public const string Manage = "sla.manage";
    }

    /// <summary>The audit trail.</summary>
    public static class Audit
    {
        /// <summary>The resource segment.</summary>
        public const string Resource = "audit";

        /// <summary>Read the trail.</summary>
        public const string Read = "audit.read";

        /// <summary>Export the trail.</summary>
        public const string Export = "audit.export";

        /// <summary>Archive and restore records.</summary>
        public const string Manage = "audit.manage";
    }

    /// <summary>The monitoring engine.</summary>
    public static class Monitoring
    {
        /// <summary>The resource segment.</summary>
        public const string Resource = "monitoring";

        /// <summary>Read metrics.</summary>
        public const string Read = "monitoring.read";

        /// <summary>Read health.</summary>
        public const string Health = "monitoring.health";

        /// <summary>Change thresholds and alert rules.</summary>
        public const string Manage = "monitoring.manage";
    }

    /// <summary>Connectors to systems outside the platform.</summary>
    public static class Connector
    {
        /// <summary>The resource segment.</summary>
        public const string Resource = "connector";

        /// <summary>Read connector configuration and state.</summary>
        public const string Read = "connector.read";

        /// <summary>Invoke a connector.</summary>
        public const string Execute = "connector.execute";

        /// <summary>Configure connectors and their mappings.</summary>
        public const string Manage = "connector.manage";
    }

    /// <summary>Plugin lifecycle.</summary>
    public static class Plugin
    {
        /// <summary>The resource segment.</summary>
        public const string Resource = "plugin";

        /// <summary>Read the installed plugin set.</summary>
        public const string Read = "plugin.read";

        /// <summary>Install, enable, disable or remove a plugin.</summary>
        public const string Manage = "plugin.manage";
    }

    /// <summary>Platform administration.</summary>
    public static class Administration
    {
        /// <summary>The resource segment.</summary>
        public const string Resource = "admin";

        /// <summary>Read tenant and platform configuration.</summary>
        public const string Read = "admin.read";

        /// <summary>Change tenant and platform configuration.</summary>
        public const string Manage = "admin.manage";

        /// <summary>Grant and revoke permissions, roles and policies.</summary>
        public const string Security = "admin.security";
    }

    /// <summary>Gets every permission the platform ships, in area order.</summary>
    public static IReadOnlyList<string> Catalogue { get; } =
    [
        Workflow.Read, Workflow.Start, Workflow.Cancel, Workflow.Manage,
        Forms.Read, Forms.Open, Forms.Submit, Forms.Manage,
        HumanTask.Read, HumanTask.Assign, HumanTask.Complete, HumanTask.Manage,
        Approval.Read, Approval.Decide, Approval.Delegate, Approval.Manage,
        Notification.Read, Notification.Send, Notification.Manage,
        Sla.Read, Sla.Control, Sla.Manage,
        Audit.Read, Audit.Export, Audit.Manage,
        Monitoring.Read, Monitoring.Health, Monitoring.Manage,
        Connector.Read, Connector.Execute, Connector.Manage,
        Plugin.Read, Plugin.Manage,
        Administration.Read, Administration.Manage, Administration.Security,
    ];
}
