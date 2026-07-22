using System.Diagnostics;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Results;
using FactoryOS.Plugin.Health;
using FactoryOS.Plugin.Lifecycle;
using FactoryOS.Plugins.Runtime.Configuration;
using FactoryOS.Plugins.Runtime.Discovery;
using FactoryOS.Plugins.Runtime.Domain;
using FactoryOS.Plugins.Runtime.Events;
using FactoryOS.Plugins.Runtime.Isolation;
using FactoryOS.Plugins.Runtime.Persistence;
using FactoryOS.Plugins.Runtime.Security;
using Microsoft.Extensions.Options;

namespace FactoryOS.Plugins.Runtime.Execution;

/// <summary>
/// Drives one tenant's plugin through the lifecycle: install, load, start, suspend, resume, stop, unload and
/// remove.
/// <para>
/// Every transition follows the same shape — the tenant gate, then the authorizer, then the phase's own
/// preconditions, then the work, then one announcement. Keeping the shape identical is what makes the guards
/// impossible to forget: there is one place they are applied, not ten.
/// </para>
/// </summary>
public interface IPluginLifecycleManager
{
    /// <summary>Installs a validated package for a tenant.</summary>
    /// <param name="caller">Who is asking.</param>
    /// <param name="package">The package to install.</param>
    /// <param name="granted">The permissions the tenant grants the plugin.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The installation, or a failure explaining why it was refused.</returns>
    Task<Result<PluginInstance>> InstallAsync(
        PluginCaller caller,
        PluginPackage package,
        IEnumerable<PluginPermission> granted,
        CancellationToken cancellationToken = default);

    /// <summary>Loads an installed plugin's assembly and activates it.</summary>
    /// <param name="caller">Who is asking.</param>
    /// <param name="pluginKey">The plugin key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result, or a failure.</returns>
    Task<Result> LoadAsync(PluginCaller caller, string pluginKey, CancellationToken cancellationToken = default);

    /// <summary>Starts a loaded plugin.</summary>
    /// <param name="caller">Who is asking.</param>
    /// <param name="pluginKey">The plugin key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result, or a failure.</returns>
    Task<Result> StartAsync(PluginCaller caller, string pluginKey, CancellationToken cancellationToken = default);

    /// <summary>Stops a running or suspended plugin, leaving it loaded.</summary>
    /// <param name="caller">Who is asking.</param>
    /// <param name="pluginKey">The plugin key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result, or a failure.</returns>
    Task<Result> StopAsync(PluginCaller caller, string pluginKey, CancellationToken cancellationToken = default);

    /// <summary>Suspends a running plugin without unloading it.</summary>
    /// <param name="caller">Who is asking.</param>
    /// <param name="pluginKey">The plugin key.</param>
    /// <param name="reason">Why it is being suspended.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result, or a failure.</returns>
    Task<Result> SuspendAsync(
        PluginCaller caller, string pluginKey, string reason, CancellationToken cancellationToken = default);

    /// <summary>Returns a suspended plugin to service.</summary>
    /// <param name="caller">Who is asking.</param>
    /// <param name="pluginKey">The plugin key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result, or a failure.</returns>
    Task<Result> ResumeAsync(PluginCaller caller, string pluginKey, CancellationToken cancellationToken = default);

    /// <summary>Stops the plugin if it is running and releases its assembly load context.</summary>
    /// <param name="caller">Who is asking.</param>
    /// <param name="pluginKey">The plugin key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result, or a failure.</returns>
    Task<Result> UnloadAsync(PluginCaller caller, string pluginKey, CancellationToken cancellationToken = default);

    /// <summary>Removes the plugin from the tenant entirely.</summary>
    /// <param name="caller">Who is asking.</param>
    /// <param name="pluginKey">The plugin key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result, or a failure.</returns>
    Task<Result> RemoveAsync(PluginCaller caller, string pluginKey, CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IPluginLifecycleManager"/>.</summary>
public sealed class PluginLifecycleManager : IPluginLifecycleManager
{
    private readonly PluginInstanceRegistry _registry;
    private readonly IPluginRepository _repository;
    private readonly IPluginManifestRepository _manifests;
    private readonly IPluginPackageStore _packages;
    private readonly PluginAuthorizationGate _gate;
    private readonly PluginValidationSuite _validation;
    private readonly PluginPackageLoader _loader;
    private readonly PluginIsolationManager _isolation;
    private readonly PluginSandbox _sandbox;
    private readonly IPluginHealthService _health;
    private readonly PluginRuntimeAnnouncer _announcer;
    private readonly IDateTimeProvider _clock;
    private readonly PluginRuntimeOptions _options;

    /// <summary>Initializes a new instance of the <see cref="PluginLifecycleManager"/> class.</summary>
    /// <param name="registry">The instance registry.</param>
    /// <param name="repository">The definition catalogue.</param>
    /// <param name="manifests">The manifest repository.</param>
    /// <param name="packages">The package store.</param>
    /// <param name="gate">The tenant-and-permission gate every transition passes through.</param>
    /// <param name="validation">The validators a package must pass.</param>
    /// <param name="loader">The package loader.</param>
    /// <param name="isolation">The isolation manager.</param>
    /// <param name="sandbox">The sandbox.</param>
    /// <param name="health">The framework health service.</param>
    /// <param name="announcer">The event, audit and metric announcer.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="options">The runtime options.</param>
    public PluginLifecycleManager(
        PluginInstanceRegistry registry,
        IPluginRepository repository,
        IPluginManifestRepository manifests,
        IPluginPackageStore packages,
        PluginAuthorizationGate gate,
        PluginValidationSuite validation,
        PluginPackageLoader loader,
        PluginIsolationManager isolation,
        PluginSandbox sandbox,
        IPluginHealthService health,
        PluginRuntimeAnnouncer announcer,
        IDateTimeProvider clock,
        IOptions<PluginRuntimeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(manifests);
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(validation);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(isolation);
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);

        _registry = registry;
        _repository = repository;
        _manifests = manifests;
        _packages = packages;
        _gate = gate;
        _validation = validation;
        _loader = loader;
        _isolation = isolation;
        _sandbox = sandbox;
        _health = health;
        _announcer = announcer;
        _clock = clock;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<Result<PluginInstance>> InstallAsync(
        PluginCaller caller,
        PluginPackage package,
        IEnumerable<PluginPermission> granted,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(granted);

        var existing = _registry.Find(caller.Tenant, package.Key);

        // Validation runs against a candidate, never against the installation the tenant is already running.
        // A reinstall that fails its signature or permission check must leave what is running exactly as it
        // was — mutating first and rolling back afterwards is how a rejected package half-lands.
        var grants = granted as IReadOnlyList<PluginPermission> ?? [.. granted];
        var candidate = new PluginInstance(
            caller.Tenant,
            package.Key,
            package.Version,
            grants,
            existing?.Settings ?? new PluginSettings(caller.Tenant, package.Key));

        var instance = existing ?? candidate;

        var outcome = await RunAsync(
            caller,
            instance,
            PluginLifecyclePhase.Install,
            () =>
            {
                var validated = _validation.ValidateForInstall(
                    package, candidate, TenantProviders(caller.Tenant, package.Key));
                if (validated.IsFailure)
                {
                    return Task.FromResult(validated);
                }

                _repository.Save(package.Definition);
                _manifests.Save(package.Manifest);
                _packages.Save(package);
                _packages.Prune(package.Key, _options.RetainedVersions + 1);

                if (existing is not null)
                {
                    existing.Grant(grants);
                    existing.MoveTo(package.Version);
                }

                instance.UseQuota(_options.DefaultQuota);
                instance.MarkInstalled();
                _registry.Register(instance);

                _announcer.Publish(new PluginInstalled(
                    instance.Tenant, instance.PluginKey, _clock.UtcNow, package.Version, package.IsSigned));

                return Task.FromResult(Result.Success());
            },
            cancellationToken).ConfigureAwait(false);

        return outcome.IsFailure ? Result.Failure<PluginInstance>(outcome.Error) : Result.Success(instance);
    }

    /// <inheritdoc />
    public Task<Result> LoadAsync(
        PluginCaller caller, string pluginKey, CancellationToken cancellationToken = default) =>
        WithInstanceAsync(caller, pluginKey, PluginLifecyclePhase.Load, (instance, definition) =>
        {
            if (instance.Status is PluginRuntimeStatus.Discovered or PluginRuntimeStatus.Removed)
            {
                return Task.FromResult(Refuse(instance, "it is not installed for this tenant."));
            }

            if (_registry.Attached(instance) is not null)
            {
                return Task.FromResult(Result.Success());
            }

            var loaded = _loader.Load(instance, definition);
            if (loaded.IsFailure)
            {
                return Task.FromResult(Result.Failure(loaded.Error));
            }

            _registry.Attach(instance, loaded.Value);
            instance.MarkLoaded();
            _registry.Register(instance);

            _announcer.Publish(new PluginLoaded(
                instance.Tenant, instance.PluginKey, _clock.UtcNow, instance.Version, definition.Isolation));

            return Task.FromResult(Result.Success());
        });

    /// <inheritdoc />
    public Task<Result> StartAsync(
        PluginCaller caller, string pluginKey, CancellationToken cancellationToken = default) =>
        WithInstanceAsync(caller, pluginKey, PluginLifecyclePhase.Start, async (instance, definition) =>
        {
            if (!instance.Enabled)
            {
                return Refuse(instance, "it is switched off for this tenant.");
            }

            if (instance.Status == PluginRuntimeStatus.Running)
            {
                return Result.Success();
            }

            if (instance.Status is not (PluginRuntimeStatus.Loaded or PluginRuntimeStatus.Stopped))
            {
                return Refuse(instance, $"a plugin cannot be started from {instance.Status}.");
            }

            // The grant is checked again here, not only at install. A permission revoked while the plugin was
            // stopped must not come back with it.
            var permitted = _validation.ValidatePermissions(instance, definition);
            if (permitted.IsFailure)
            {
                return permitted;
            }

            var plugin = _registry.Attached(instance);
            if (plugin is null)
            {
                return Refuse(instance, "nothing is loaded to start.");
            }

            instance.MarkStarting();

            var context = ContextFor(instance, definition);
            if (context is not null && plugin is IPluginLifecycle lifecycle)
            {
                await lifecycle.InitializeAsync(context, cancellationToken).ConfigureAwait(false);
            }

            await plugin.StartAsync(cancellationToken).ConfigureAwait(false);

            instance.MarkRunning(_clock.UtcNow);
            _registry.Register(instance);
            _health.Heartbeat(instance.PluginKey);

            _announcer.Publish(new PluginStarted(
                instance.Tenant, instance.PluginKey, _clock.UtcNow, instance.Version));

            return Result.Success();
        });

    /// <inheritdoc />
    public Task<Result> StopAsync(
        PluginCaller caller, string pluginKey, CancellationToken cancellationToken = default) =>
        WithInstanceAsync(caller, pluginKey, PluginLifecyclePhase.Stop, async (instance, _) =>
        {
            if (instance.Status is PluginRuntimeStatus.Stopped or PluginRuntimeStatus.Installed)
            {
                return Result.Success();
            }

            if (instance.Status is not (PluginRuntimeStatus.Running or PluginRuntimeStatus.Suspended))
            {
                return Refuse(instance, $"a plugin cannot be stopped from {instance.Status}.");
            }

            var plugin = _registry.Attached(instance);
            if (plugin is null)
            {
                return Refuse(instance, "nothing is loaded to stop.");
            }

            instance.MarkStopping();
            await plugin.StopAsync(cancellationToken).ConfigureAwait(false);
            instance.MarkStopped();
            _registry.Register(instance);

            _announcer.Publish(new PluginStopped(
                instance.Tenant, instance.PluginKey, _clock.UtcNow, instance.Version));

            return Result.Success();
        });

    /// <inheritdoc />
    public Task<Result> SuspendAsync(
        PluginCaller caller, string pluginKey, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return WithInstanceAsync(caller, pluginKey, PluginLifecyclePhase.Suspend, (instance, _) =>
        {
            if (instance.Status != PluginRuntimeStatus.Running)
            {
                return Task.FromResult(Refuse(instance, $"only a running plugin can be suspended, not one that is "
                    + $"{instance.Status}."));
            }

            // Suspending deliberately does not stop the plugin object: it keeps its state and its loaded
            // assemblies, which is the whole difference from a stop, and the reason a resume is instant.
            instance.MarkSuspended();
            _registry.Register(instance);

            _announcer.Publish(new PluginSuspended(instance.Tenant, instance.PluginKey, _clock.UtcNow, reason));

            return Task.FromResult(Result.Success());
        });
    }

    /// <inheritdoc />
    public Task<Result> ResumeAsync(
        PluginCaller caller, string pluginKey, CancellationToken cancellationToken = default) =>
        WithInstanceAsync(caller, pluginKey, PluginLifecyclePhase.Resume, (instance, definition) =>
        {
            if (instance.Status != PluginRuntimeStatus.Suspended)
            {
                return Task.FromResult(Refuse(
                    instance, $"only a suspended plugin can be resumed, not one that is {instance.Status}."));
            }

            var permitted = _validation.ValidatePermissions(instance, definition);
            if (permitted.IsFailure)
            {
                return Task.FromResult(permitted);
            }

            instance.MarkRunning(_clock.UtcNow);
            _registry.Register(instance);
            _health.Heartbeat(instance.PluginKey);

            _announcer.Publish(new PluginResumed(instance.Tenant, instance.PluginKey, _clock.UtcNow));

            return Task.FromResult(Result.Success());
        });

    /// <inheritdoc />
    public Task<Result> UnloadAsync(
        PluginCaller caller, string pluginKey, CancellationToken cancellationToken = default) =>
        WithInstanceAsync(caller, pluginKey, PluginLifecyclePhase.Unload, async (instance, _) =>
        {
            var plugin = _registry.Attached(instance);

            if (plugin is not null && instance.Status is PluginRuntimeStatus.Running or PluginRuntimeStatus.Suspended)
            {
                instance.MarkStopping();
                await plugin.StopAsync(cancellationToken).ConfigureAwait(false);
                instance.MarkStopped();

                _announcer.Publish(new PluginStopped(
                    instance.Tenant, instance.PluginKey, _clock.UtcNow, instance.Version));
            }

            if (plugin is IPluginLifecycle lifecycle)
            {
                await lifecycle.UnloadAsync(cancellationToken).ConfigureAwait(false);
            }

            _registry.Detach(instance);
            _isolation.Release(instance);
            instance.MarkUnloaded();
            _registry.Register(instance);

            return Result.Success();
        });

    /// <inheritdoc />
    public Task<Result> RemoveAsync(
        PluginCaller caller, string pluginKey, CancellationToken cancellationToken = default) =>
        WithInstanceAsync(caller, pluginKey, PluginLifecyclePhase.Remove, async (instance, _) =>
        {
            var plugin = _registry.Attached(instance);

            if (plugin is not null && instance.Status is PluginRuntimeStatus.Running or PluginRuntimeStatus.Suspended)
            {
                await plugin.StopAsync(cancellationToken).ConfigureAwait(false);
            }

            if (plugin is IPluginLifecycle lifecycle)
            {
                await lifecycle.UnloadAsync(cancellationToken).ConfigureAwait(false);
            }

            _isolation.Release(instance);
            _sandbox.Forget(instance);

            var version = instance.Version;
            instance.MarkRemoved();
            _registry.Remove(instance);

            // The package itself is deliberately kept. It belongs to the platform, not to the tenant that
            // happened to remove it, and another factory may still be running it.
            _announcer.Publish(new PluginRemoved(instance.Tenant, instance.PluginKey, _clock.UtcNow, version));

            return Result.Success();
        });

    /// <summary>Builds the runtime context one tenant's plugin runs in.</summary>
    /// <param name="instance">The installation.</param>
    /// <param name="definition">The definition it installs.</param>
    /// <returns>The context, or <see langword="null"/> when the manifest is no longer available.</returns>
    public PluginRuntimeContext? ContextFor(PluginInstance instance, PluginDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(definition);

        var manifest = _manifests.Find(instance.PluginKey, instance.Version);
        if (manifest is null)
        {
            return null;
        }

        var scope = _isolation.Scope(instance, definition);
        return new PluginRuntimeContext(instance, definition, manifest, scope.StoragePath);
    }

    private IReadOnlyList<PluginDefinition> TenantProviders(string tenant, string excludedKey) =>
        [.. _registry.ForTenant(tenant)
            .Where(other => !string.Equals(other.PluginKey, excludedKey, StringComparison.OrdinalIgnoreCase))
            .Select(_registry.DefinitionFor)
            .OfType<PluginDefinition>()];

    private Task<Result> WithInstanceAsync(
        PluginCaller caller,
        string pluginKey,
        PluginLifecyclePhase phase,
        Func<PluginInstance, PluginDefinition, Task<Result>> action)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginKey);

        var instance = _registry.Find(caller.Tenant, pluginKey);
        if (instance is null)
        {
            return Task.FromResult(Result.Failure(Error.NotFound(
                "Plugin.Runtime.NotInstalled",
                $"Tenant '{caller.Tenant}' has not installed plugin '{pluginKey}'.")));
        }

        var definition = _registry.DefinitionFor(instance);
        if (definition is null)
        {
            return Task.FromResult(Result.Failure(Error.NotFound(
                "Plugin.Runtime.Definition.Missing",
                $"The catalogue holds no definition for plugin '{pluginKey}' at version {instance.Version}.")));
        }

        return RunAsync(caller, instance, phase, () => action(instance, definition), CancellationToken.None);
    }

    private async Task<Result> RunAsync(
        PluginCaller caller,
        PluginInstance instance,
        PluginLifecyclePhase phase,
        Func<Task<Result>> action,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var guarded = _gate.Check(caller, instance, phase);
        if (guarded.IsFailure)
        {
            return Complete(caller, instance, phase, guarded, TimeSpan.Zero);
        }

        var started = Stopwatch.GetTimestamp();
        var outcome = await action().ConfigureAwait(false);
        return Complete(caller, instance, phase, outcome, Stopwatch.GetElapsedTime(started));
    }

    private Result Complete(
        PluginCaller caller,
        PluginInstance instance,
        PluginLifecyclePhase phase,
        Result outcome,
        TimeSpan duration)
    {
        var kind = outcome.IsSuccess ? PluginFailureKind.Unknown : PluginFailures.Classify(outcome.Error.Code);

        _announcer.Announce(new PluginTelemetry(
            instance.Tenant,
            instance.PluginKey,
            instance.Version,
            phase,
            outcome.IsSuccess,
            duration,
            _clock.UtcNow)
        {
            Subject = caller.Subject,
            FailureKind = kind,
            FailureReason = outcome.IsSuccess ? null : outcome.Error.Description,
        });

        if (outcome.IsSuccess)
        {
            return outcome;
        }

        _announcer.Publish(new PluginFailed(
            instance.Tenant, instance.PluginKey, _clock.UtcNow, phase, kind, outcome.Error.Description));

        return outcome;
    }

    private static Result Refuse(PluginInstance instance, string reason) =>
        Result.Failure(Error.Conflict(
            "Plugin.Runtime.Lifecycle.Refused",
            $"Plugin '{instance.PluginKey}' in tenant '{instance.Tenant}' cannot do this: {reason}"));
}
