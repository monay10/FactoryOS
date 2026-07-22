using System.Collections.Concurrent;
using FactoryOS.Plugins.Runtime.Configuration;
using FactoryOS.Plugins.Runtime.Domain;
using FactoryOS.Plugins.Runtime.Health;
using Microsoft.Extensions.Options;

namespace FactoryOS.Plugins.Runtime.Execution;

/// <summary>
/// Decides which instances are due for a health probe, and probes them.
/// <para>
/// It owns <b>no timer</b>. The host already has one scheduler, and a runtime that starts threads of its own
/// is a runtime that cannot be tested deterministically or driven from a single tick. This type answers
/// "what is due at this instant?" and runs it; when that instant arrives is the host's business.
/// </para>
/// </summary>
public sealed class PluginRuntimeScheduler
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastProbe =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly PluginInstanceRegistry _registry;
    private readonly PluginHealthEngine _health;
    private readonly PluginRuntimeOptions _options;

    /// <summary>Initializes a new instance of the <see cref="PluginRuntimeScheduler"/> class.</summary>
    /// <param name="registry">The instance registry.</param>
    /// <param name="health">The health engine.</param>
    /// <param name="options">The runtime options.</param>
    public PluginRuntimeScheduler(
        PluginInstanceRegistry registry, PluginHealthEngine health, IOptions<PluginRuntimeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(options);

        _registry = registry;
        _health = health;
        _options = options.Value;
    }

    /// <summary>Lists the instances whose health probe is due at an instant.</summary>
    /// <param name="now">The instant.</param>
    /// <returns>The instances due, in a stable order.</returns>
    public IReadOnlyList<PluginInstance> Due(DateTimeOffset now) =>
        [.. _registry.All().Where(instance => IsDue(instance, now))
            .OrderBy(instance => instance.Identity, StringComparer.Ordinal)];

    /// <summary>Probes every instance that is due, and records that it was probed.</summary>
    /// <param name="now">The instant.</param>
    /// <returns>The reports taken.</returns>
    public IReadOnlyList<PluginHealthReport> RunDue(DateTimeOffset now)
    {
        var reports = new List<PluginHealthReport>();

        foreach (var instance in Due(now))
        {
            _lastProbe[instance.Identity] = now;
            reports.Add(_health.Check(instance.Tenant, instance.PluginKey));
        }

        return reports;
    }

    /// <summary>Gets when an instance was last probed.</summary>
    /// <param name="instance">The installation.</param>
    /// <returns>The instant, or <see langword="null"/> when it has never been probed.</returns>
    public DateTimeOffset? LastProbe(PluginInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return _lastProbe.TryGetValue(instance.Identity, out var last) ? last : null;
    }

    private bool IsDue(PluginInstance instance, DateTimeOffset now)
    {
        // A plugin that has never been probed is due immediately: the first report is the one that says
        // whether the thing an operator just installed actually works.
        if (!_lastProbe.TryGetValue(instance.Identity, out var last))
        {
            return true;
        }

        return now - last >= _options.HealthProbeInterval;
    }
}
