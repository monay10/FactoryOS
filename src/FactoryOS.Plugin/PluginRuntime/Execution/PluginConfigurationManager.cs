using FactoryOS.Domain.Results;
using FactoryOS.Plugins.Runtime.Configuration;
using FactoryOS.Plugins.Runtime.Domain;
using FactoryOS.Plugins.Runtime.Security;

namespace FactoryOS.Plugins.Runtime.Execution;

/// <summary>
/// Changes what a tenant's plugin is configured with, granted and allowed to consume.
/// <para>
/// Every change here goes through the same gate a lifecycle transition does, guarded by
/// <see cref="PluginPermissions.Configure"/>. Configuration is not a lesser operation than starting a plugin
/// — widening a grant or raising a quota changes what the plugin can do to a factory just as decisively.
/// </para>
/// </summary>
public sealed class PluginConfigurationManager
{
    private readonly PluginInstanceRegistry _registry;
    private readonly PluginAuthorizationGate _gate;
    private readonly PluginValidationSuite _validation;

    /// <summary>Initializes a new instance of the <see cref="PluginConfigurationManager"/> class.</summary>
    /// <param name="registry">The instance registry.</param>
    /// <param name="gate">The tenant-and-permission gate.</param>
    /// <param name="validation">The validators, used to re-check a narrowed grant.</param>
    public PluginConfigurationManager(
        PluginInstanceRegistry registry, PluginAuthorizationGate gate, PluginValidationSuite validation)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(validation);

        _registry = registry;
        _gate = gate;
        _validation = validation;
    }

    /// <summary>Reads a tenant's settings for a plugin.</summary>
    /// <param name="caller">Who is asking.</param>
    /// <param name="pluginKey">The plugin key.</param>
    /// <returns>The settings, or a failure when the caller may not read them.</returns>
    public Result<PluginSettings> Read(PluginCaller caller, string pluginKey)
    {
        var resolved = Resolve(caller, pluginKey);
        return resolved.IsFailure
            ? Result.Failure<PluginSettings>(resolved.Error)
            : Result.Success(resolved.Value.Settings);
    }

    /// <summary>Replaces a tenant's settings for a plugin.</summary>
    /// <param name="caller">Who is asking.</param>
    /// <param name="pluginKey">The plugin key.</param>
    /// <param name="values">The new settings.</param>
    /// <returns>A successful result, or a failure.</returns>
    public Result Configure(
        PluginCaller caller, string pluginKey, IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var resolved = Resolve(caller, pluginKey);
        if (resolved.IsFailure)
        {
            return resolved;
        }

        resolved.Value.Settings.Replace(values);
        _registry.Register(resolved.Value);
        return Result.Success();
    }

    /// <summary>
    /// Replaces what a tenant grants a plugin.
    /// <para>
    /// Narrowing a grant below what a <b>running</b> plugin's manifest requires is refused rather than
    /// applied: the alternative is a plugin that keeps running with reach the tenant has just said it should
    /// not have. Stop it first, and the narrower grant then simply prevents it from starting again.
    /// </para>
    /// </summary>
    /// <param name="caller">Who is asking.</param>
    /// <param name="pluginKey">The plugin key.</param>
    /// <param name="permissions">The permissions now granted.</param>
    /// <returns>A successful result, or a failure.</returns>
    public Result Grant(PluginCaller caller, string pluginKey, IEnumerable<PluginPermission> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        var resolved = Resolve(caller, pluginKey);
        if (resolved.IsFailure)
        {
            return resolved;
        }

        var instance = resolved.Value;
        var definition = _registry.DefinitionFor(instance);
        var previous = instance.Granted.ToArray();

        instance.Grant(permissions);

        if (definition is not null && instance.Status == PluginRuntimeStatus.Running)
        {
            var permitted = _validation.ValidatePermissions(instance, definition);
            if (permitted.IsFailure)
            {
                instance.Grant(previous);
                return Result.Failure(Error.Conflict(
                    "Plugin.Runtime.Grant.WouldStrandRunningPlugin",
                    $"Plugin '{instance.PluginKey}' is running in tenant '{instance.Tenant}' and needs the "
                    + $"permissions this grant would remove. Stop it first. ({permitted.Error.Description})"));
            }
        }

        _registry.Register(instance);
        return Result.Success();
    }

    /// <summary>Narrows the resource quota a tenant's plugin runs under.</summary>
    /// <param name="caller">Who is asking.</param>
    /// <param name="pluginKey">The plugin key.</param>
    /// <param name="quota">The quota.</param>
    /// <returns>A successful result, or a failure.</returns>
    public Result Limit(PluginCaller caller, string pluginKey, PluginResourceQuota quota)
    {
        ArgumentNullException.ThrowIfNull(quota);

        var resolved = Resolve(caller, pluginKey);
        if (resolved.IsFailure)
        {
            return resolved;
        }

        resolved.Value.UseQuota(quota);
        _registry.Register(resolved.Value);
        return Result.Success();
    }

    /// <summary>Switches a tenant's plugin on or off.</summary>
    /// <param name="caller">Who is asking.</param>
    /// <param name="pluginKey">The plugin key.</param>
    /// <param name="enabled">Whether the plugin is switched on.</param>
    /// <returns>A successful result, or a failure.</returns>
    public Result SetEnabled(PluginCaller caller, string pluginKey, bool enabled)
    {
        var resolved = Resolve(caller, pluginKey);
        if (resolved.IsFailure)
        {
            return resolved;
        }

        if (enabled)
        {
            resolved.Value.Enable();
        }
        else
        {
            resolved.Value.Disable();
        }

        _registry.Register(resolved.Value);
        return Result.Success();
    }

    private Result<PluginInstance> Resolve(PluginCaller caller, string pluginKey)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginKey);

        var instance = _registry.Find(caller.Tenant, pluginKey);
        if (instance is null)
        {
            return Result.Failure<PluginInstance>(Error.NotFound(
                "Plugin.Runtime.NotInstalled",
                $"Tenant '{caller.Tenant}' has not installed plugin '{pluginKey}'."));
        }

        var guarded = _gate.Check(caller, instance, PluginPermissions.Configure);
        return guarded.IsFailure ? Result.Failure<PluginInstance>(guarded.Error) : Result.Success(instance);
    }
}
