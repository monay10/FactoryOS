using System.Collections.Concurrent;
using FactoryOS.Plugins.Runtime.Configuration;
using FactoryOS.Plugins.Runtime.Domain;

namespace FactoryOS.Plugins.Runtime.Isolation;

/// <summary>Why the sandbox let a plugin act, or would not.</summary>
public enum PluginSandboxRefusal
{
    /// <summary>The plugin may act.</summary>
    Granted = 0,

    /// <summary>The plugin is not running, so it has nothing to act with.</summary>
    NotRunning = 1,

    /// <summary>The tenant has not granted the plugin the permission the action needs.</summary>
    MissingPermission = 2,

    /// <summary>The plugin already has as many operations in flight as its quota allows.</summary>
    ConcurrencyExceeded = 3,

    /// <summary>The plugin is holding more memory than its quota allows.</summary>
    MemoryExceeded = 4,

    /// <summary>The plugin is occupying more storage than its quota allows.</summary>
    StorageExceeded = 5,
}

/// <summary>
/// A granted admission to the sandbox. Disposing it returns the concurrency slot, so a plugin that leaks
/// leases throttles itself rather than the host.
/// </summary>
public sealed class PluginSandboxLease : IDisposable
{
    private readonly PluginSandbox? _sandbox;
    private readonly string? _identity;
    private bool _released;

    internal PluginSandboxLease(PluginSandbox sandbox, string identity)
    {
        _sandbox = sandbox;
        _identity = identity;
        Granted = true;
        Refusal = PluginSandboxRefusal.Granted;
        Detail = "Admitted.";
    }

    private PluginSandboxLease(PluginSandboxRefusal refusal, string detail)
    {
        Granted = false;
        Refusal = refusal;
        Detail = detail;
    }

    /// <summary>Gets a value indicating whether the plugin was admitted.</summary>
    public bool Granted { get; }

    /// <summary>Gets why it was refused, when it was.</summary>
    public PluginSandboxRefusal Refusal { get; }

    /// <summary>Gets the decision in a form an operator can act on.</summary>
    public string Detail { get; }

    /// <summary>Builds a refusal.</summary>
    /// <param name="refusal">Why.</param>
    /// <param name="detail">The reason in full.</param>
    /// <returns>The refused lease.</returns>
    public static PluginSandboxLease Refuse(PluginSandboxRefusal refusal, string detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        return new PluginSandboxLease(refusal, detail);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_released || !Granted || _sandbox is null || _identity is null)
        {
            return;
        }

        _released = true;
        _sandbox.Leave(_identity);
    }
}

/// <summary>
/// The gate a sandboxed plugin passes through every time it acts: is it running, is it permitted, and is it
/// within its quota.
/// <para>
/// All three are asked per <b>instance</b>, so exhausting a quota degrades one factory rather than every
/// factory that happens to run the same plugin. The permission asked about is the instance's
/// <i>effective</i> set — what the manifest requests intersected with what the tenant granted — so a plugin
/// cannot widen its own reach by asking more often.
/// </para>
/// </summary>
public sealed class PluginSandbox
{
    private sealed record Usage(int Concurrent, long Memory, long Storage);

    private readonly ConcurrentDictionary<string, Usage> _usage = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Asks to act inside a plugin's sandbox.</summary>
    /// <param name="context">The instance's runtime context.</param>
    /// <param name="permission">The permission the action needs.</param>
    /// <returns>A granted lease, or a refusal explaining what stopped it.</returns>
    public PluginSandboxLease Enter(PluginRuntimeContext context, PluginPermission permission)
    {
        ArgumentNullException.ThrowIfNull(context);

        var instance = context.Instance;

        if (!instance.CanServe)
        {
            return PluginSandboxLease.Refuse(
                PluginSandboxRefusal.NotRunning,
                $"Plugin '{instance.PluginKey}' is {instance.Status} for tenant '{instance.Tenant}'.");
        }

        if (!context.Holds(permission))
        {
            return PluginSandboxLease.Refuse(
                PluginSandboxRefusal.MissingPermission,
                $"Plugin '{instance.PluginKey}' does not hold '{permission}' in tenant '{instance.Tenant}'.");
        }

        var quota = instance.Quota;
        var refusal = Reserve(instance.Identity, quota);
        return refusal is null
            ? new PluginSandboxLease(this, instance.Identity)
            : PluginSandboxLease.Refuse(refusal.Value, Describe(refusal.Value, instance, quota));
    }

    /// <summary>Records how much memory an instance is holding.</summary>
    /// <param name="instance">The tenant's installation.</param>
    /// <param name="bytes">The bytes held.</param>
    public void RecordMemory(PluginInstance instance, long bytes)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);

        _usage.AddOrUpdate(
            instance.Identity,
            _ => new Usage(0, bytes, 0),
            (_, current) => current with { Memory = bytes });
    }

    /// <summary>Records how much storage an instance is occupying.</summary>
    /// <param name="instance">The tenant's installation.</param>
    /// <param name="bytes">The bytes occupied.</param>
    public void RecordStorage(PluginInstance instance, long bytes)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);

        _usage.AddOrUpdate(
            instance.Identity,
            _ => new Usage(0, 0, bytes),
            (_, current) => current with { Storage = bytes });
    }

    /// <summary>Reports what an instance is consuming against what it is allowed.</summary>
    /// <param name="instance">The tenant's installation.</param>
    /// <returns>One reading per resource.</returns>
    public IReadOnlyList<PluginResourceUsage> Usages(PluginInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var usage = _usage.TryGetValue(instance.Identity, out var current) ? current : new Usage(0, 0, 0);
        var quota = instance.Quota;

        return
        [
            new PluginResourceUsage(PluginResourceKind.Concurrency, usage.Concurrent, quota.MaxConcurrentOperations),
            new PluginResourceUsage(PluginResourceKind.Memory, usage.Memory, quota.MaxMemoryBytes),
            new PluginResourceUsage(PluginResourceKind.Storage, usage.Storage, quota.MaxStorageBytes),
        ];
    }

    /// <summary>Forgets everything recorded for an instance, as a remove or a reinstall does.</summary>
    /// <param name="instance">The tenant's installation.</param>
    public void Forget(PluginInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _usage.TryRemove(instance.Identity, out _);
    }

    internal void Leave(string identity)
    {
        _usage.AddOrUpdate(
            identity,
            _ => new Usage(0, 0, 0),
            (_, current) => current with { Concurrent = Math.Max(0, current.Concurrent - 1) });
    }

    private PluginSandboxRefusal? Reserve(string identity, PluginResourceQuota quota)
    {
        PluginSandboxRefusal? refusal = null;

        _usage.AddOrUpdate(
            identity,
            _ =>
            {
                refusal = null;
                return new Usage(1, 0, 0);
            },
            (_, current) =>
            {
                if (PluginResourceQuota.Enforces(quota.MaxMemoryBytes) && current.Memory > quota.MaxMemoryBytes)
                {
                    refusal = PluginSandboxRefusal.MemoryExceeded;
                    return current;
                }

                if (PluginResourceQuota.Enforces(quota.MaxStorageBytes) && current.Storage > quota.MaxStorageBytes)
                {
                    refusal = PluginSandboxRefusal.StorageExceeded;
                    return current;
                }

                if (quota.MaxConcurrentOperations > 0 && current.Concurrent >= quota.MaxConcurrentOperations)
                {
                    refusal = PluginSandboxRefusal.ConcurrencyExceeded;
                    return current;
                }

                refusal = null;
                return current with { Concurrent = current.Concurrent + 1 };
            });

        return refusal;
    }

    private static string Describe(
        PluginSandboxRefusal refusal, PluginInstance instance, PluginResourceQuota quota) => refusal switch
        {
            PluginSandboxRefusal.ConcurrencyExceeded =>
                $"Plugin '{instance.PluginKey}' in tenant '{instance.Tenant}' already has "
                + $"{quota.MaxConcurrentOperations} operations in flight.",
            PluginSandboxRefusal.MemoryExceeded =>
                $"Plugin '{instance.PluginKey}' in tenant '{instance.Tenant}' is over its "
                + $"{quota.MaxMemoryBytes}-byte memory quota.",
            _ =>
                $"Plugin '{instance.PluginKey}' in tenant '{instance.Tenant}' is over its "
                + $"{quota.MaxStorageBytes}-byte storage quota.",
        };
}
