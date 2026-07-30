using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugins.Runtime.Configuration;
using FactoryOS.Plugins.Runtime.Domain;
using Xunit;

namespace FactoryOS.Tests.Plugins;

/// <summary>
/// Verifies that <see cref="PluginInstance.Rehydrate"/> reconstructs an installation from stored state exactly —
/// the guarantee a persistence store relies on so a reloaded installation is indistinguishable from a live one.
/// </summary>
public sealed class PluginInstanceRehydrateTests
{
    [Fact]
    public void Rehydrate_reconstructs_every_persisted_field()
    {
        var version = PluginVersion.Parse("2.1.0");
        var previous = PluginVersion.Parse("2.0.0");
        var grants = new[]
        {
            PluginPermission.Parse("uimetadata.extend"),
            PluginPermission.Parse("connector.extend"),
        };
        var settings = new PluginSettings("acme", "demo", new Dictionary<string, string?> { ["endpoint"] = "tcp://x" });
        var quota = new PluginResourceQuota
        {
            MaxConcurrentOperations = 4,
            MaxMemoryBytes = 1_000,
            MaxStorageBytes = 2_000,
        };
        var started = new DateTimeOffset(2026, 7, 30, 8, 0, 0, TimeSpan.Zero);

        var instance = PluginInstance.Rehydrate(
            "acme", "demo", version, previous, grants, settings, quota,
            PluginRuntimeStatus.Running, enabled: false, "it stopped responding", PluginFailureKind.Signature, started);

        Assert.Equal("acme", instance.Tenant);
        Assert.Equal("demo", instance.PluginKey);
        Assert.Equal(version, instance.Version);
        Assert.Equal(previous, instance.PreviousVersion);
        Assert.True(instance.CanRollback);
        Assert.Equal(2, instance.Granted.Count);
        Assert.Equal("tcp://x", instance.Settings.Get("endpoint"));
        Assert.Equal(4, instance.Quota.MaxConcurrentOperations);
        Assert.Equal(2_000, instance.Quota.MaxStorageBytes);
        Assert.Equal(PluginRuntimeStatus.Running, instance.Status);
        Assert.False(instance.Enabled);
        Assert.Equal("it stopped responding", instance.FailureReason);
        Assert.Equal(PluginFailureKind.Signature, instance.FailureKind);
        Assert.Equal(started, instance.StartedUtc);
    }

    [Fact]
    public void Rehydrate_of_a_fresh_install_has_no_previous_version_and_no_failure()
    {
        var instance = PluginInstance.Rehydrate(
            "acme", "demo", PluginVersion.Parse("1.0.0"), previousVersion: null,
            [], new PluginSettings("acme", "demo"), PluginResourceQuota.Unlimited,
            PluginRuntimeStatus.Installed, enabled: true, failureReason: null, PluginFailureKind.Unknown, startedUtc: null);

        Assert.Null(instance.PreviousVersion);
        Assert.False(instance.CanRollback);
        Assert.Empty(instance.Granted);
        Assert.Null(instance.FailureReason);
        Assert.True(instance.Enabled);
        Assert.Null(instance.StartedUtc);
    }
}
