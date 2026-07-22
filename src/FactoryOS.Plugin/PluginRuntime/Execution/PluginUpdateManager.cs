using System.Diagnostics;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Results;
using FactoryOS.Plugins.Runtime.Domain;
using FactoryOS.Plugins.Runtime.Events;
using FactoryOS.Plugins.Runtime.Persistence;
using FactoryOS.Plugins.Runtime.Security;

namespace FactoryOS.Plugins.Runtime.Execution;

/// <summary>
/// Moves a tenant's plugin from one version to another, and back again.
/// <para>
/// Two rules make this safe rather than merely possible.
/// </para>
/// <para>
/// <b>An update always stops first.</b> Replacing the code under a running plugin is how a half-updated
/// process is produced: the old instance keeps serving with state the new version does not understand. The
/// update stops, unloads, installs, reloads and — only if it was running before — starts again.
/// </para>
/// <para>
/// <b>A rollback is only offered when there is something to roll back to.</b> The package it would return to
/// must still be retained; if retention dropped it, the runtime says so plainly rather than reporting a
/// rollback that silently did nothing.
/// </para>
/// </summary>
public sealed class PluginUpdateManager
{
    private readonly IPluginLifecycleManager _lifecycle;
    private readonly PluginInstanceRegistry _registry;
    private readonly IPluginPackageStore _packages;
    private readonly PluginAuthorizationGate _gate;
    private readonly PluginRuntimeAnnouncer _announcer;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="PluginUpdateManager"/> class.</summary>
    /// <param name="lifecycle">The lifecycle manager the update drives.</param>
    /// <param name="registry">The instance registry.</param>
    /// <param name="packages">The package store retaining superseded versions.</param>
    /// <param name="gate">The tenant-and-permission gate.</param>
    /// <param name="announcer">The event, audit and metric announcer.</param>
    /// <param name="clock">The clock.</param>
    public PluginUpdateManager(
        IPluginLifecycleManager lifecycle,
        PluginInstanceRegistry registry,
        IPluginPackageStore packages,
        PluginAuthorizationGate gate,
        PluginRuntimeAnnouncer announcer,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(clock);

        _lifecycle = lifecycle;
        _registry = registry;
        _packages = packages;
        _gate = gate;
        _announcer = announcer;
        _clock = clock;
    }

    /// <summary>Replaces a tenant's installed version with a newer one.</summary>
    /// <param name="caller">Who is asking.</param>
    /// <param name="package">The package to move to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result, or a failure explaining why the update was refused.</returns>
    public Task<Result> UpdateAsync(
        PluginCaller caller, PluginPackage package, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(package);

        return MoveAsync(caller, package.Key, PluginLifecyclePhase.Update, instance =>
        {
            if (package.Version <= instance.Version)
            {
                return Result.Failure<PluginPackage>(Error.Validation(
                    "Plugin.Runtime.Update.NotNewer",
                    $"Plugin '{instance.PluginKey}' is at {instance.Version}; {package.Version} is not an update. "
                    + "Returning to an earlier version is a rollback, and is recorded as one."));
            }

            return Result.Success(package);
        }, cancellationToken);
    }

    /// <summary>Returns a tenant's plugin to the version an update replaced.</summary>
    /// <param name="caller">Who is asking.</param>
    /// <param name="pluginKey">The plugin key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result, or a failure explaining why the rollback was refused.</returns>
    public Task<Result> RollbackAsync(
        PluginCaller caller, string pluginKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginKey);

        return MoveAsync(caller, pluginKey, PluginLifecyclePhase.Rollback, instance =>
        {
            if (instance.PreviousVersion is not { } previous)
            {
                return Result.Failure<PluginPackage>(Error.Validation(
                    "Plugin.Runtime.Rollback.NothingToRollBackTo",
                    $"Plugin '{instance.PluginKey}' has not been updated in tenant '{instance.Tenant}', so there "
                    + "is no earlier version to return to."));
            }

            var package = _packages.Find(instance.PluginKey, previous);
            return package is null
                ? Result.Failure<PluginPackage>(Error.NotFound(
                    "Plugin.Runtime.Rollback.PackageNotRetained",
                    $"Plugin '{instance.PluginKey}' {previous} is no longer retained, so the rollback cannot be "
                    + "performed. Increase the retained-version count to keep rollbacks available."))
                : Result.Success(package);
        }, cancellationToken);
    }

    private async Task<Result> MoveAsync(
        PluginCaller caller,
        string pluginKey,
        PluginLifecyclePhase phase,
        Func<PluginInstance, Result<PluginPackage>> choose,
        CancellationToken cancellationToken)
    {
        var instance = _registry.Find(caller.Tenant, pluginKey);
        if (instance is null)
        {
            return Result.Failure(Error.NotFound(
                "Plugin.Runtime.NotInstalled",
                $"Tenant '{caller.Tenant}' has not installed plugin '{pluginKey}'."));
        }

        var guarded = _gate.Check(caller, instance, phase);
        if (guarded.IsFailure)
        {
            return Announce(caller, instance, phase, guarded, instance.Version, instance.Version, TimeSpan.Zero);
        }

        var chosen = choose(instance);
        if (chosen.IsFailure)
        {
            return Announce(
                caller,
                instance,
                phase,
                Result.Failure(chosen.Error),
                instance.Version,
                instance.Version,
                TimeSpan.Zero);
        }

        var started = Stopwatch.GetTimestamp();
        var from = instance.Version;
        var wasRunning = instance.Status == PluginRuntimeStatus.Running;
        var grants = instance.Granted.ToArray();

        var outcome = await ApplyAsync(caller, instance, chosen.Value, grants, wasRunning, cancellationToken)
            .ConfigureAwait(false);

        var elapsed = Stopwatch.GetElapsedTime(started);
        var result = Announce(caller, instance, phase, outcome, from, instance.Version, elapsed);

        if (result.IsSuccess)
        {
            _announcer.Publish(new PluginUpdated(
                instance.Tenant,
                instance.PluginKey,
                _clock.UtcNow,
                from,
                instance.Version,
                phase == PluginLifecyclePhase.Rollback));
        }

        return result;
    }

    private async Task<Result> ApplyAsync(
        PluginCaller caller,
        PluginInstance instance,
        PluginPackage package,
        IReadOnlyList<PluginPermission> grants,
        bool wasRunning,
        CancellationToken cancellationToken)
    {
        // The unload happens first, while the instance still reports what it actually is: marking it
        // 'updating' beforehand would hide a running plugin from the very step whose job is to stop it.
        var unloaded = await _lifecycle.UnloadAsync(caller, instance.PluginKey, cancellationToken)
            .ConfigureAwait(false);
        if (unloaded.IsFailure)
        {
            return unloaded;
        }

        instance.MarkUpdating();
        _registry.Register(instance);

        var installed = await _lifecycle.InstallAsync(caller, package, grants, cancellationToken)
            .ConfigureAwait(false);
        if (installed.IsFailure)
        {
            return Result.Failure(installed.Error);
        }

        var loaded = await _lifecycle.LoadAsync(caller, instance.PluginKey, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure || !wasRunning)
        {
            return loaded;
        }

        return await _lifecycle.StartAsync(caller, instance.PluginKey, cancellationToken).ConfigureAwait(false);
    }

    private Result Announce(
        PluginCaller caller,
        PluginInstance instance,
        PluginLifecyclePhase phase,
        Result outcome,
        PluginVersion from,
        PluginVersion to,
        TimeSpan duration)
    {
        // The umbrella transition is announced in its own right, alongside the stop, install, load and start
        // it is made of. An operator asking "when was this plugin updated?" should not have to infer it from
        // four lower-level lines that happen to sit next to each other.
        _announcer.Announce(new PluginTelemetry(
            instance.Tenant,
            instance.PluginKey,
            outcome.IsSuccess ? to : from,
            phase,
            outcome.IsSuccess,
            duration,
            _clock.UtcNow)
        {
            Subject = caller.Subject,
            FailureKind = outcome.IsSuccess ? PluginFailureKind.Unknown : PluginFailures.Classify(outcome.Error.Code),
            FailureReason = outcome.IsSuccess ? null : outcome.Error.Description,
        });

        if (outcome.IsFailure)
        {
            _announcer.Publish(new PluginFailed(
                instance.Tenant,
                instance.PluginKey,
                _clock.UtcNow,
                phase,
                PluginFailures.Classify(outcome.Error.Code),
                outcome.Error.Description));
        }

        return outcome;
    }
}
