using System.Text;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugin.Health;
using FactoryOS.Plugin.Hosting;
using FactoryOS.Plugin.Lifecycle;
using FactoryOS.Plugin.Runtime;
using FactoryOS.Plugins.Runtime.Configuration;
using FactoryOS.Plugins.Runtime.Discovery;
using FactoryOS.Plugins.Runtime.Domain;
using FactoryOS.Plugins.Runtime.Events;
using FactoryOS.Plugins.Runtime.Execution;
using FactoryOS.Plugins.Runtime.Integration;
using FactoryOS.Plugins.Runtime.Isolation;
using FactoryOS.Plugins.Runtime.Persistence;
using FactoryOS.Plugins.Runtime.Security;
using FactoryOS.Tests.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FactoryOS.Tests.Plugins;

public sealed class PluginRuntimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 22, 09, 00, 00, TimeSpan.Zero);

    private const string Tenant = "acme";
    private const string Other = "borusan";

    // ---------------------------------------------------------------------------------------------------
    // Permissions and extension points
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("plugin.install", "plugin.install", true)]
    [InlineData("plugin.*", "plugin.remove", true)]
    [InlineData("*.*", "workflow.extend", true)]
    [InlineData("plugin.install", "plugin.remove", false)]
    [InlineData("workflow.extend", "forms.extend", false)]
    public void A_permission_grants_exactly_what_its_segments_cover(string held, string requested, bool grants)
    {
        Assert.Equal(grants, PluginPermission.Parse(held).Grants(PluginPermission.Parse(requested)));
    }

    [Theory]
    [InlineData("plugin")]
    [InlineData("plugin.install.extra")]
    [InlineData("")]
    [InlineData(null)]
    public void Something_that_is_not_a_permission_never_parses(string? value)
    {
        Assert.False(PluginPermission.TryParse(value, out _));
    }

    [Fact]
    public void Every_lifecycle_phase_is_guarded_by_a_permission_the_catalogue_recognises()
    {
        foreach (var phase in Enum.GetValues<PluginLifecyclePhase>())
        {
            Assert.Contains(PluginPermissions.For(phase), PluginPermissions.Catalogue);
        }
    }

    [Fact]
    public void Every_published_extension_point_has_a_key_that_parses_back_to_it()
    {
        foreach (var point in PluginExtensionPoints.All())
        {
            Assert.True(PluginExtensionPoints.TryParse(point.Key, out var parsed));
            Assert.Equal(point.Kind, parsed.Kind);
        }
    }

    [Fact]
    public void An_extension_point_a_plugin_invented_is_not_a_published_one()
    {
        Assert.False(PluginExtensionPoints.TryParse("workflow-internals", out _));
        Assert.Throws<FormatException>(() => PluginExtensionPoints.Parse("whatever"));
    }

    [Fact]
    public void Contributing_to_a_point_implies_asking_for_permission_to_extend_it()
    {
        var definition = Definition("reports") with
        {
            Contributions = [PluginContribution.To(PluginExtensionPointKind.Reporting, "shift-summary")],
        };

        Assert.Contains(PluginPermission.Parse("reporting.extend"), definition.EffectiveRequests());
    }

    // ---------------------------------------------------------------------------------------------------
    // Definitions, packages and signatures
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void A_manifests_screens_and_routes_are_already_extension_contributions()
    {
        var manifest = Manifest("energy") with
        {
            Ui = [new PluginUiScreen { Id = "energy.dash", Title = "Energy", Route = "/e", Component = "e/Dash" }],
            Api = [new PluginApiRoute { Method = "GET", Path = "/m/energy/meters" }],
        };

        var definition = PluginDefinition.FromManifest(manifest);

        Assert.Single(definition.ContributionsTo(PluginExtensionPointKind.UiMetadata));
        Assert.Single(definition.ContributionsTo(PluginExtensionPointKind.Api));
    }

    [Fact]
    public void A_packages_identity_carries_its_version_so_two_versions_are_two_packages()
    {
        var first = Package("energy", "1.0.0");
        var second = Package("energy", "1.1.0");

        Assert.NotEqual(first.Identity, second.Identity);
        Assert.Equal("energy@1.0.0", first.Identity);
    }

    [Fact]
    public void A_signature_covers_the_claims_the_runtime_acts_on_not_only_the_assembly_name()
    {
        var package = Package("energy");
        var widened = package with
        {
            Definition = package.Definition with
            {
                RequestedPermissions = [PluginPermission.Parse("workflow.extend")],
            },
        };

        Assert.NotEqual(package.CanonicalContent(), widened.CanonicalContent());
    }

    [Fact]
    public void A_signed_package_verifies_and_a_tampered_one_does_not()
    {
        var harness = new Harness();
        var key = Encoding.UTF8.GetBytes("a-development-signing-key");
        harness.Keys.Add("store", key);

        var package = Package("energy");
        var signed = package with { Signature = PluginSignature.Hmac(PluginSignatureValidator.Sign(package, key), "store") };

        Assert.True(harness.Signatures.Validate(signed).IsSuccess);

        var tampered = signed with
        {
            Definition = signed.Definition with { EntryType = "Evil.Plugin" },
        };

        var refused = harness.Signatures.Validate(tampered);
        Assert.True(refused.IsFailure);
        Assert.Equal("Plugin.Runtime.Signature.Invalid", refused.Error.Code);
    }

    [Fact]
    public void A_signature_produced_with_a_key_the_host_does_not_trust_is_refused()
    {
        var harness = new Harness();
        var package = Package("energy");
        var signed = package with
        {
            Signature = PluginSignature.Hmac(
                PluginSignatureValidator.Sign(package, Encoding.UTF8.GetBytes("someone-elses-key")), "elsewhere"),
        };

        var refused = harness.Signatures.Validate(signed);
        Assert.True(refused.IsFailure);
        Assert.Equal("Plugin.Runtime.Signature.UnknownKey", refused.Error.Code);
    }

    [Fact]
    public void An_unsigned_package_installs_unless_the_host_requires_signing()
    {
        Assert.True(new Harness().Signatures.Validate(Package("energy")).IsSuccess);

        var strict = new Harness(services => services.Configure<PluginRuntimeOptions>(
            options => options.RequireSignature = true));

        var refused = strict.Signatures.Validate(Package("energy"));
        Assert.True(refused.IsFailure);
        Assert.Equal("Plugin.Runtime.Signature.Missing", refused.Error.Code);
    }

    [Fact]
    public async Task An_invalid_signature_is_fatal_even_when_the_host_does_not_require_signing()
    {
        var harness = new Harness();
        harness.Keys.Add("store", Encoding.UTF8.GetBytes("the-real-key"));

        var package = Package("energy");
        var forged = package with
        {
            Signature = PluginSignature.Hmac(
                PluginSignatureValidator.Sign(package, Encoding.UTF8.GetBytes("not-the-real-key")), "store"),
        };

        var installed = await harness.Runtime.InstallAsync(Admin(Tenant), forged, Grants());

        Assert.True(installed.IsFailure);
        Assert.Equal("Plugin.Runtime.Signature.Invalid", installed.Error.Code);
    }

    // ---------------------------------------------------------------------------------------------------
    // Manifest reading
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void A_manifest_written_before_this_runtime_existed_still_reads()
    {
        var read = PluginRuntimeManifestReader.Read(
            """{"key":"energy","name":"Energy","version":"1.0.0","provides":["energy.monitoring"]}""");

        Assert.True(read.IsSuccess);
        Assert.Equal(PluginIsolationMode.AssemblyIsolated, read.Value.Definition.Isolation);
        Assert.Equal(PluginCompatibility.Any, read.Value.Definition.Compatibility);
        Assert.False(read.Value.Signature.IsPresent);
    }

    [Fact]
    public void The_runtime_sections_of_a_manifest_are_read_alongside_the_frameworks()
    {
        var read = PluginRuntimeManifestReader.Read(
            """
            {
              "key": "shiftreport", "name": "Shift Report", "version": "2.1.0",
              "extends": [ { "point": "reporting", "name": "shift-summary", "reference": "reports/Shift" } ],
              "permissions": [ "plugin.observe" ],
              "requires": [ "energy.readmodel" ],
              "isolation": "sandboxed",
              "compatibility": { "minimumPlatform": "1.0.0", "maximumPlatform": "2.0.0" }
            }
            """);

        Assert.True(read.IsSuccess);
        var definition = read.Value.Definition;
        Assert.Equal(PluginIsolationMode.Sandboxed, definition.Isolation);
        Assert.Single(definition.ContributionsTo(PluginExtensionPointKind.Reporting));
        Assert.Contains("energy.readmodel", definition.RequiredCapabilities);
        Assert.True(definition.Compatibility.Supports(new PluginVersion(1, 5, 0)));
        Assert.False(definition.Compatibility.Supports(new PluginVersion(3, 0, 0)));
    }

    [Fact]
    public void A_manifest_extending_something_the_platform_does_not_publish_is_refused()
    {
        var read = PluginRuntimeManifestReader.Read(
            """{"key":"x","name":"X","version":"1.0.0","extends":[{"point":"workflow-internals","name":"y"}]}""");

        Assert.True(read.IsFailure);
        Assert.Equal("Plugin.Runtime.Manifest.UnknownExtensionPoint", read.Error.Code);
    }

    [Fact]
    public void A_compatibility_ceiling_below_its_floor_is_refused()
    {
        var read = PluginRuntimeManifestReader.Read(
            """{"key":"x","name":"X","version":"1.0.0","compatibility":{"minimumPlatform":"3.0.0","maximumPlatform":"1.0.0"}}""");

        Assert.True(read.IsFailure);
        Assert.Equal("Plugin.Runtime.Manifest.InvalidCompatibility", read.Error.Code);
    }

    [Fact]
    public void A_signature_that_names_no_key_is_refused_at_read_time()
    {
        var read = PluginRuntimeManifestReader.Read(
            """{"key":"x","name":"X","version":"1.0.0","signature":{"algorithm":"hmacsha256","value":"abc"}}""");

        Assert.True(read.IsFailure);
        Assert.Equal("Plugin.Runtime.Manifest.InvalidSignature", read.Error.Code);
    }

    // ---------------------------------------------------------------------------------------------------
    // Discovery
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Discovery_finds_a_package_per_folder_and_reports_the_folders_it_could_not_read()
    {
        using var root = new TempRoot();
        root.Write("energy", """{"key":"energy","name":"Energy","version":"1.0.0"}""");
        root.Write("broken", """{"name":"No key","version":"1.0.0"}""");
        root.Write("notapackage", null);

        var result = new Harness().Runtime.Discover(root.Path);

        Assert.Single(result.Packages);
        Assert.Equal("energy", result.Packages[0].Key);
        var rejection = Assert.Single(result.Rejected);
        Assert.Contains("key", rejection.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_detached_signature_beside_a_manifest_wins_over_one_written_inside_it()
    {
        using var root = new TempRoot();
        root.Write(
            "energy",
            """{"key":"energy","name":"Energy","version":"1.0.0","signature":{"algorithm":"hmacsha256","value":"inline","keyId":"inline-key"}}""");
        File.WriteAllText(Path.Combine(root.Path, "energy", "module.sig"), "hmacsha256 detached-key ZGV0YWNoZWQ=");

        var package = Assert.Single(new Harness().Runtime.Discover(root.Path).Packages);

        Assert.Equal("detached-key", package.Signature.KeyId);
    }

    [Fact]
    public void Discovering_a_root_that_does_not_exist_finds_nothing_rather_than_throwing()
    {
        var result = new Harness().Runtime.Discover(Path.Combine(Path.GetTempPath(), "factoryos-absent-root"));

        Assert.Empty(result.Packages);
        Assert.Empty(result.Rejected);
    }

    // ---------------------------------------------------------------------------------------------------
    // Version, dependency and compatibility resolution
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("2.1.0", "2.1.0", true)]
    [InlineData("2.4.0", "2.1.0", true)]
    [InlineData("2.0.9", "2.1.0", false)]
    [InlineData("3.0.0", "2.1.0", false)]
    public void A_version_satisfies_a_requirement_only_within_its_major(
        string available, string required, bool satisfies)
    {
        var resolver = new PluginVersionResolver();

        Assert.Equal(
            satisfies, resolver.Satisfies(PluginVersion.Parse(available), PluginVersion.Parse(required)));
    }

    [Fact]
    public void The_highest_satisfying_version_is_chosen_and_a_newer_major_is_not()
    {
        var resolver = new PluginVersionResolver();
        var candidates = new List<string> { "2.0.0", "2.3.0", "3.0.0" }.Select(PluginVersion.Parse);

        Assert.Equal(PluginVersion.Parse("2.3.0"), resolver.Resolve(candidates, PluginVersion.Parse("2.1.0")));
    }

    [Fact]
    public void Dependencies_decide_the_load_order()
    {
        var resolver = new PluginRuntimeDependencyResolver();
        var reports = Definition("reports") with
        {
            Dependencies = [new PluginDependency("energy", PluginVersion.Parse("1.0.0"))],
        };

        var ordered = resolver.Resolve([reports, Definition("energy")]);

        Assert.True(ordered.IsSuccess);
        Assert.Equal(["energy", "reports"], ordered.Value.Select(definition => definition.Key));
    }

    [Fact]
    public void A_dependency_cycle_is_reported_rather_than_ordered()
    {
        var resolver = new PluginRuntimeDependencyResolver();
        var first = Definition("a") with { Dependencies = [new PluginDependency("b", default)] };
        var second = Definition("b") with { Dependencies = [new PluginDependency("a", default)] };

        var ordered = resolver.Resolve([first, second]);

        Assert.True(ordered.IsFailure);
        Assert.Contains("cycle", ordered.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_package_built_for_a_later_platform_is_refused_before_anything_is_installed()
    {
        var validator = new PluginCompatibilityValidator();
        var definition = Definition("future") with
        {
            Compatibility = new PluginCompatibility(PluginVersion.Parse("9.0.0")),
        };

        var validated = validator.Validate(definition, PluginVersion.Parse("1.0.0"));

        Assert.True(validated.IsFailure);
        Assert.Equal("Plugin.Runtime.Compatibility.Unsupported", validated.Error.Code);
    }

    [Fact]
    public void A_package_with_no_version_is_refused_because_it_could_never_be_updated()
    {
        var validator = new PluginCompatibilityValidator();
        var definition = Definition("nameless") with { Version = default };

        Assert.Equal(
            "Plugin.Runtime.Compatibility.NoVersion",
            validator.Validate(definition, PluginVersion.Parse("1.0.0")).Error.Code);
    }

    [Fact]
    public void A_plugin_contributing_the_same_name_twice_is_refused()
    {
        var validator = new PluginCompatibilityValidator();
        var definition = Definition("dup") with
        {
            Contributions =
            [
                PluginContribution.To(PluginExtensionPointKind.Rules, "spike"),
                PluginContribution.To(PluginExtensionPointKind.Rules, "spike"),
            ],
        };

        Assert.Equal(
            "Plugin.Runtime.Compatibility.DuplicateContribution",
            validator.Validate(definition, PluginVersion.Parse("1.0.0")).Error.Code);
    }

    [Fact]
    public void A_plugin_that_depends_on_itself_is_refused()
    {
        var validator = new PluginManifestValidator();
        var definition = Definition("loop") with
        {
            Dependencies = [new PluginDependency("loop", default)],
        };

        Assert.Equal("Plugin.Runtime.Manifest.SelfDependency", validator.Validate(definition).Error.Code);
    }

    [Fact]
    public async Task A_required_capability_nothing_installed_provides_refuses_the_install()
    {
        var harness = new Harness();
        var definition = Definition("reports") with { RequiredCapabilities = ["energy.readmodel"] };
        var package = new PluginPackage(Manifest("reports"), definition, PluginSignature.None);

        var installed = await harness.Runtime.InstallAsync(Admin(Tenant), package, Grants());

        Assert.True(installed.IsFailure);
        Assert.Equal("Plugin.Runtime.Capability.Missing", installed.Error.Code);
    }

    [Fact]
    public async Task A_required_capability_another_installed_plugin_provides_is_satisfied()
    {
        var harness = new Harness();
        await harness.Install(Package("energy") with
        {
            Definition = Definition("energy") with { Capabilities = ["energy.readmodel"] },
        });

        var reports = new PluginPackage(
            Manifest("reports"),
            Definition("reports") with { RequiredCapabilities = ["energy.readmodel"] },
            PluginSignature.None);

        Assert.True((await harness.Runtime.InstallAsync(Admin(Tenant), reports, Grants())).IsSuccess);
    }

    // ---------------------------------------------------------------------------------------------------
    // Permissions are a ceiling intersected with a grant
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void What_a_plugin_may_do_is_what_it_asked_for_intersected_with_what_the_tenant_granted()
    {
        var definition = Definition("reports") with
        {
            RequestedPermissions = [PluginPermission.Parse("reporting.extend")],
        };

        var instance = new PluginInstance(
            Tenant,
            "reports",
            PluginVersion.Parse("1.0.0"),
            [PluginPermission.Parse("reporting.extend"), PluginPermission.Parse("security.extend")]);

        var effective = instance.EffectivePermissions(definition);

        Assert.Equal([PluginPermission.Parse("reporting.extend")], effective);
    }

    [Fact]
    public async Task A_plugin_asking_for_more_than_the_tenant_granted_does_not_install()
    {
        var harness = new Harness();
        var package = Package("reports") with
        {
            Definition = Definition("reports") with
            {
                RequestedPermissions = [PluginPermission.Parse("workflow.extend")],
            },
        };

        var installed = await harness.Runtime.InstallAsync(Admin(Tenant), package, Grants());

        Assert.True(installed.IsFailure);
        Assert.Equal("Plugin.Runtime.Permission.Ungranted", installed.Error.Code);
    }

    [Fact]
    public async Task A_grant_revoked_while_a_plugin_was_stopped_stops_it_starting_again()
    {
        var harness = new Harness(compiled: [new StubPlugin("reports")]);
        var package = Package("reports") with
        {
            Definition = Definition("reports") with
            {
                RequestedPermissions = [PluginPermission.Parse("reporting.extend")],
            },
        };

        await harness.Install(package, [PluginPermission.Parse("plugin.*"), PluginPermission.Parse("reporting.extend")]);
        await harness.Start("reports");
        await harness.Runtime.Lifecycle.StopAsync(Admin(Tenant), "reports");

        var narrowed = harness.Runtime.Configuration.Grant(
            Admin(Tenant), "reports", [PluginPermission.Parse("plugin.*")]);
        Assert.True(narrowed.IsSuccess);

        var restarted = await harness.Runtime.Lifecycle.StartAsync(Admin(Tenant), "reports");

        Assert.True(restarted.IsFailure);
        Assert.Equal("Plugin.Runtime.Permission.Ungranted", restarted.Error.Code);
    }

    [Fact]
    public async Task A_grant_cannot_be_narrowed_out_from_under_a_running_plugin()
    {
        var harness = new Harness(compiled: [new StubPlugin("reports")]);
        var package = Package("reports") with
        {
            Definition = Definition("reports") with
            {
                RequestedPermissions = [PluginPermission.Parse("reporting.extend")],
            },
        };

        await harness.Install(package, [PluginPermission.Parse("plugin.*"), PluginPermission.Parse("reporting.extend")]);
        await harness.Start("reports");

        var narrowed = harness.Runtime.Configuration.Grant(
            Admin(Tenant), "reports", [PluginPermission.Parse("plugin.*")]);

        Assert.True(narrowed.IsFailure);
        Assert.Equal("Plugin.Runtime.Grant.WouldStrandRunningPlugin", narrowed.Error.Code);
        Assert.Contains(
            PluginPermission.Parse("reporting.extend"),
            harness.Instance("reports")!.Granted);
    }

    // ---------------------------------------------------------------------------------------------------
    // Tenant isolation
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void An_instances_identity_is_the_tenant_and_the_plugin_together()
    {
        Assert.NotEqual(
            PluginInstance.Identify(Tenant, "energy"), PluginInstance.Identify(Other, "energy"));
    }

    [Fact]
    public async Task Two_factories_install_the_same_plugin_without_seeing_each_others()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);

        await harness.Install(Package("energy"));
        await harness.Install(Package("energy", "1.2.0"), tenant: Other);

        Assert.Equal(PluginVersion.Parse("1.0.0"), harness.Instance("energy")!.Version);
        Assert.Equal(PluginVersion.Parse("1.2.0"), harness.Instance("energy", Other)!.Version);
        Assert.Single(harness.Runtime.Installed(Tenant));
        Assert.Single(harness.Runtime.Installed(Other));
    }

    [Fact]
    public async Task A_caller_reaching_across_tenants_is_refused()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);
        await harness.Install(Package("energy"));

        var trespasser = PluginCaller.Holding(Other, "ops", PluginPermission.Parse("plugin.*"));
        var refused = await harness.Runtime.Lifecycle.StartAsync(trespasser, "energy");

        Assert.True(refused.IsFailure);
        Assert.Equal("Plugin.Runtime.NotInstalled", refused.Error.Code);
    }

    [Fact]
    public void The_tenant_gate_holds_even_when_the_authorizer_allows_everything()
    {
        var gate = new PluginAuthorizationGate(new PermissiveAuthorizer());
        var instance = new PluginInstance(Tenant, "energy", PluginVersion.Parse("1.0.0"));
        var trespasser = PluginCaller.Holding(Other, "ops", PluginPermission.Parse("*.*"));

        var refused = gate.Check(trespasser, instance, PluginLifecyclePhase.Start);

        Assert.True(refused.IsFailure);
        Assert.Contains("belongs to", refused.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unauthenticated_caller_is_refused_by_the_default_authorizer()
    {
        var authorizer = new PermissionPluginAuthorizer();
        var instance = new PluginInstance(Tenant, "energy", PluginVersion.Parse("1.0.0"));

        var decision = authorizer.Authorize(PluginCaller.Anonymous(Tenant), instance, PluginPermissions.Manage);

        Assert.False(decision.Allowed);
        Assert.Equal(PluginAuthorizationReason.NotAuthenticated, decision.Reason);
    }

    [Fact]
    public void A_caller_that_does_not_hold_the_permission_is_refused_and_told_which_one()
    {
        var authorizer = new PermissionPluginAuthorizer();
        var instance = new PluginInstance(Tenant, "energy", PluginVersion.Parse("1.0.0"));
        var caller = PluginCaller.Holding(Tenant, "viewer", PluginPermissions.Observe);

        var decision = authorizer.Authorize(caller, instance, PluginPermissions.Remove);

        Assert.False(decision.Allowed);
        Assert.Equal(PluginAuthorizationReason.MissingPermission, decision.Reason);
        Assert.Contains("plugin.remove", decision.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_caller_without_the_permission_a_phase_needs_cannot_drive_it()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);
        await harness.Install(Package("energy"));

        var viewer = PluginCaller.Holding(Tenant, "viewer", PluginPermissions.Observe);
        var refused = await harness.Runtime.Lifecycle.StartAsync(viewer, "energy");

        Assert.True(refused.IsFailure);
        Assert.Equal("Plugin.Runtime.Forbidden", refused.Error.Code);
    }

    // ---------------------------------------------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_plugin_walks_install_load_start_stop_unload_and_remove()
    {
        var plugin = new StubPlugin("energy");
        var harness = new Harness(compiled: [plugin]);

        await harness.Install(Package("energy"));
        Assert.Equal(PluginRuntimeStatus.Installed, harness.Instance("energy")!.Status);

        await harness.Runtime.Lifecycle.LoadAsync(Admin(Tenant), "energy");
        Assert.Equal(PluginRuntimeStatus.Loaded, harness.Instance("energy")!.Status);

        await harness.Runtime.Lifecycle.StartAsync(Admin(Tenant), "energy");
        Assert.Equal(PluginRuntimeStatus.Running, harness.Instance("energy")!.Status);
        Assert.Equal(1, plugin.Starts);

        await harness.Runtime.Lifecycle.StopAsync(Admin(Tenant), "energy");
        Assert.Equal(PluginRuntimeStatus.Stopped, harness.Instance("energy")!.Status);
        Assert.Equal(1, plugin.Stops);

        await harness.Runtime.Lifecycle.UnloadAsync(Admin(Tenant), "energy");
        Assert.Equal(PluginRuntimeStatus.Installed, harness.Instance("energy")!.Status);

        await harness.Runtime.Lifecycle.RemoveAsync(Admin(Tenant), "energy");
        Assert.Null(harness.Instance("energy"));
    }

    [Fact]
    public async Task Suspending_keeps_the_plugin_loaded_and_resuming_does_not_start_it_again()
    {
        var plugin = new StubPlugin("energy");
        var harness = new Harness(compiled: [plugin]);
        await harness.Install(Package("energy"));
        await harness.Start("energy");

        await harness.Runtime.Lifecycle.SuspendAsync(Admin(Tenant), "energy", "maintenance window");
        Assert.Equal(PluginRuntimeStatus.Suspended, harness.Instance("energy")!.Status);
        Assert.False(harness.Instance("energy")!.CanServe);
        Assert.Equal(0, plugin.Stops);

        await harness.Runtime.Lifecycle.ResumeAsync(Admin(Tenant), "energy");
        Assert.Equal(PluginRuntimeStatus.Running, harness.Instance("energy")!.Status);
        Assert.Equal(1, plugin.Starts);
    }

    [Fact]
    public async Task Only_a_running_plugin_can_be_suspended_and_only_a_suspended_one_resumed()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);
        await harness.Install(Package("energy"));

        var suspended = await harness.Runtime.Lifecycle.SuspendAsync(Admin(Tenant), "energy", "why not");
        Assert.True(suspended.IsFailure);

        await harness.Start("energy");
        var resumed = await harness.Runtime.Lifecycle.ResumeAsync(Admin(Tenant), "energy");
        Assert.True(resumed.IsFailure);
        Assert.Contains("suspended", resumed.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_plugin_switched_off_for_a_tenant_does_not_start()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);
        await harness.Install(Package("energy"));
        harness.Runtime.Configuration.SetEnabled(Admin(Tenant), "energy", false);

        await harness.Runtime.Lifecycle.LoadAsync(Admin(Tenant), "energy");
        var refused = await harness.Runtime.Lifecycle.StartAsync(Admin(Tenant), "energy");

        Assert.True(refused.IsFailure);
        Assert.Contains("switched off", refused.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unloading_a_running_plugin_stops_it_first()
    {
        var plugin = new StubPlugin("energy");
        var harness = new Harness(compiled: [plugin]);
        await harness.Install(Package("energy"));
        await harness.Start("energy");

        await harness.Runtime.Lifecycle.UnloadAsync(Admin(Tenant), "energy");

        Assert.Equal(1, plugin.Stops);
        Assert.Equal(PluginRuntimeStatus.Installed, harness.Instance("energy")!.Status);
    }

    [Fact]
    public async Task The_extended_lifecycle_hooks_are_honoured_when_a_plugin_implements_them()
    {
        var plugin = new LifecyclePlugin("energy");
        var harness = new Harness(compiled: [plugin]);

        await harness.Install(Package("energy"));
        await harness.Start("energy");
        await harness.Runtime.Lifecycle.UnloadAsync(Admin(Tenant), "energy");

        Assert.Equal(1, plugin.Initializations);
        Assert.Equal(1, plugin.Unloads);
        Assert.Equal(Tenant, plugin.SeenTenant);
    }

    [Fact]
    public async Task Starting_something_the_tenant_never_installed_says_so()
    {
        var harness = new Harness();

        var refused = await harness.Runtime.Lifecycle.StartAsync(Admin(Tenant), "nothing");

        Assert.True(refused.IsFailure);
        Assert.Equal("Plugin.Runtime.NotInstalled", refused.Error.Code);
    }

    [Fact]
    public async Task Starting_twice_is_a_no_op_rather_than_a_second_start()
    {
        var plugin = new StubPlugin("energy");
        var harness = new Harness(compiled: [plugin]);
        await harness.Install(Package("energy"));
        await harness.Start("energy");

        Assert.True((await harness.Runtime.Lifecycle.StartAsync(Admin(Tenant), "energy")).IsSuccess);
        Assert.Equal(1, plugin.Starts);
    }

    // ---------------------------------------------------------------------------------------------------
    // Loading
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_plugin_compiled_into_the_host_is_taken_rather_than_loaded_from_disk()
    {
        var plugin = new StubPlugin("energy");
        var harness = new Harness(compiled: [plugin]);
        await harness.Install(Package("energy"));

        await harness.Runtime.Lifecycle.LoadAsync(Admin(Tenant), "energy");

        Assert.Same(plugin, harness.Registry.Attached(harness.Instance("energy")!));
        Assert.False(harness.Isolation.IsIsolated(harness.Instance("energy")!));
    }

    [Fact]
    public async Task A_plugin_that_is_neither_compiled_in_nor_on_disk_cannot_be_loaded()
    {
        var harness = new Harness();
        await harness.Install(Package("energy"));

        var refused = await harness.Runtime.Lifecycle.LoadAsync(Admin(Tenant), "energy");

        Assert.True(refused.IsFailure);
        Assert.Contains("nor present on disk", refused.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_manifest_naming_an_assembly_that_is_not_there_fails_with_the_path()
    {
        using var root = new TempRoot();
        var location = root.Write("energy", """{"key":"energy","name":"Energy","version":"1.0.0"}""");

        var harness = new Harness();
        var package = Package("energy") with
        {
            Definition = Definition("energy") with { Location = location, Assembly = "Absent.dll" },
        };

        await harness.Install(package);
        var refused = await harness.Runtime.Lifecycle.LoadAsync(Admin(Tenant), "energy");

        Assert.True(refused.IsFailure);
        Assert.Contains("Absent.dll", refused.Error.Description, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------------
    // Update and rollback
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_update_stops_the_plugin_installs_the_new_version_and_starts_it_again()
    {
        var plugin = new StubPlugin("energy");
        var harness = new Harness(compiled: [plugin]);
        await harness.Install(Package("energy"));
        await harness.Start("energy");

        var updated = await harness.Runtime.Updates.UpdateAsync(Admin(Tenant), Package("energy", "1.1.0"));

        Assert.True(updated.IsSuccess);
        Assert.Equal(PluginVersion.Parse("1.1.0"), harness.Instance("energy")!.Version);
        Assert.Equal(PluginVersion.Parse("1.0.0"), harness.Instance("energy")!.PreviousVersion);
        Assert.Equal(PluginRuntimeStatus.Running, harness.Instance("energy")!.Status);
        Assert.Equal(1, plugin.Stops);
        Assert.Equal(2, plugin.Starts);
    }

    [Fact]
    public async Task An_update_leaves_a_stopped_plugin_stopped()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);
        await harness.Install(Package("energy"));

        await harness.Runtime.Updates.UpdateAsync(Admin(Tenant), Package("energy", "1.1.0"));

        Assert.Equal(PluginRuntimeStatus.Loaded, harness.Instance("energy")!.Status);
    }

    [Fact]
    public async Task Moving_to_an_earlier_version_is_not_an_update()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);
        await harness.Install(Package("energy", "2.0.0"));

        var refused = await harness.Runtime.Updates.UpdateAsync(Admin(Tenant), Package("energy", "1.0.0"));

        Assert.True(refused.IsFailure);
        Assert.Equal("Plugin.Runtime.Update.NotNewer", refused.Error.Code);
    }

    [Fact]
    public async Task A_rollback_returns_to_the_version_the_update_replaced()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);
        await harness.Install(Package("energy"));
        await harness.Start("energy");
        await harness.Runtime.Updates.UpdateAsync(Admin(Tenant), Package("energy", "1.1.0"));

        var rolled = await harness.Runtime.Updates.RollbackAsync(Admin(Tenant), "energy");

        Assert.True(rolled.IsSuccess);
        Assert.Equal(PluginVersion.Parse("1.0.0"), harness.Instance("energy")!.Version);
        Assert.Equal(PluginRuntimeStatus.Running, harness.Instance("energy")!.Status);
    }

    [Fact]
    public async Task A_plugin_that_was_never_updated_has_nothing_to_roll_back_to()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);
        await harness.Install(Package("energy"));

        var refused = await harness.Runtime.Updates.RollbackAsync(Admin(Tenant), "energy");

        Assert.True(refused.IsFailure);
        Assert.Equal("Plugin.Runtime.Rollback.NothingToRollBackTo", refused.Error.Code);
    }

    [Fact]
    public async Task A_rollback_whose_package_retention_dropped_says_so_rather_than_doing_nothing()
    {
        var harness = new Harness(services => services.Configure<PluginRuntimeOptions>(
            options => options.RetainedVersions = 0));

        await harness.Install(Package("energy"));
        await harness.Runtime.Updates.UpdateAsync(Admin(Tenant), Package("energy", "1.1.0"));

        var refused = await harness.Runtime.Updates.RollbackAsync(Admin(Tenant), "energy");

        Assert.True(refused.IsFailure);
        Assert.Equal("Plugin.Runtime.Rollback.PackageNotRetained", refused.Error.Code);
    }

    [Fact]
    public async Task An_update_and_a_rollback_are_both_announced_as_one_version_move()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);
        await harness.Install(Package("energy"));
        await harness.Runtime.Updates.UpdateAsync(Admin(Tenant), Package("energy", "1.1.0"));
        await harness.Runtime.Updates.RollbackAsync(Admin(Tenant), "energy");

        var moves = harness.Events.Of<PluginUpdated>();

        Assert.Equal(2, moves.Count);
        Assert.False(moves[0].RolledBack);
        Assert.True(moves[1].RolledBack);
        Assert.Equal(PluginVersion.Parse("1.0.0"), moves[1].ToVersion);
    }

    // ---------------------------------------------------------------------------------------------------
    // Configuration and settings isolation
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Two_factories_settings_for_the_same_plugin_are_separate()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);
        await harness.Install(Package("energy"));
        await harness.Install(Package("energy"), tenant: Other);

        harness.Runtime.Configuration.Configure(
            Admin(Tenant), "energy", new Dictionary<string, string?> { ["Region"] = "eu-west" });

        Assert.Equal("eu-west", harness.Runtime.Configuration.Read(Admin(Tenant), "energy").Value.Get("Region"));
        Assert.Null(harness.Runtime.Configuration.Read(Admin(Other), "energy").Value.Get("Region"));
    }

    [Fact]
    public void A_settings_section_names_the_tenant_so_two_can_never_share_one()
    {
        var first = new PluginSettings(Tenant, "energy");
        var second = new PluginSettings(Other, "energy");

        Assert.NotEqual(first.Section, second.Section);
        Assert.Contains(Tenant, first.Section, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Configuring_a_plugin_a_tenant_has_not_installed_says_so()
    {
        var harness = new Harness();

        var refused = harness.Runtime.Configuration.Configure(
            Admin(Tenant), "absent", new Dictionary<string, string?>());

        Assert.True(refused.IsFailure);
        Assert.Equal("Plugin.Runtime.NotInstalled", refused.Error.Code);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Configuration_needs_the_configure_permission_not_merely_a_lifecycle_one()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);
        await harness.Install(Package("energy"));

        var operatorCaller = PluginCaller.Holding(Tenant, "operator", PluginPermissions.Manage);
        var refused = harness.Runtime.Configuration.Configure(
            operatorCaller, "energy", new Dictionary<string, string?>());

        Assert.True(refused.IsFailure);
        Assert.Contains("plugin.configure", refused.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_read_typed_values_and_fall_back_when_they_are_absent_or_wrong()
    {
        var settings = new PluginSettings(Tenant, "energy");
        settings.Set("Interval", "45");
        settings.Set("Verbose", "not-a-boolean");

        Assert.Equal(45, settings.GetInt("Interval"));
        Assert.Equal(7, settings.GetInt("Missing", 7));
        Assert.True(settings.GetBool("Verbose", fallback: true));
    }

    // ---------------------------------------------------------------------------------------------------
    // Isolation and sandbox
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void An_instances_storage_path_carries_the_tenant_but_not_the_version()
    {
        var harness = new Harness();
        var instance = new PluginInstance(Tenant, "energy", PluginVersion.Parse("1.0.0"));

        var path = harness.Isolation.StoragePathFor(instance);

        Assert.Contains(Tenant, path, StringComparison.Ordinal);
        Assert.Contains("energy", path, StringComparison.Ordinal);
        Assert.DoesNotContain("1.0.0", path, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_tenants_running_one_plugin_get_two_load_contexts_and_two_storage_paths()
    {
        var harness = new Harness();
        var first = new PluginInstance(Tenant, "energy", PluginVersion.Parse("1.0.0"));
        var second = new PluginInstance(Other, "energy", PluginVersion.Parse("1.0.0"));

        Assert.NotEqual(
            PluginIsolationManager.LoadContextNameFor(first),
            PluginIsolationManager.LoadContextNameFor(second));
        Assert.NotEqual(harness.Isolation.StoragePathFor(first), harness.Isolation.StoragePathFor(second));
    }

    [Fact]
    public void An_isolation_scope_carries_the_effective_permissions_not_the_requested_ones()
    {
        var harness = new Harness();
        var definition = Definition("reports") with
        {
            RequestedPermissions = [PluginPermission.Parse("reporting.extend"), PluginPermission.Parse("ai.extend")],
        };

        var instance = new PluginInstance(
            Tenant, "reports", PluginVersion.Parse("1.0.0"), [PluginPermission.Parse("reporting.extend")]);

        Assert.Equal([PluginPermission.Parse("reporting.extend")], harness.Isolation.Scope(instance, definition).Permissions);
    }

    [Fact]
    public void The_sandbox_refuses_a_plugin_that_is_not_running()
    {
        var harness = new Harness();
        var context = harness.Context(Definition("energy"), status: PluginRuntimeStatus.Stopped);

        using var lease = harness.Sandbox.Enter(context, PluginPermission.Parse("reporting.extend"));

        Assert.False(lease.Granted);
        Assert.Equal(PluginSandboxRefusal.NotRunning, lease.Refusal);
    }

    [Fact]
    public void The_sandbox_refuses_a_permission_the_tenant_never_granted()
    {
        var harness = new Harness();
        var definition = Definition("reports") with
        {
            RequestedPermissions = [PluginPermission.Parse("reporting.extend")],
        };

        var context = harness.Context(definition, granted: []);

        using var lease = harness.Sandbox.Enter(context, PluginPermission.Parse("reporting.extend"));

        Assert.False(lease.Granted);
        Assert.Equal(PluginSandboxRefusal.MissingPermission, lease.Refusal);
    }

    [Fact]
    public void The_sandbox_admits_up_to_the_quota_and_a_released_lease_frees_its_slot()
    {
        var harness = new Harness();
        var definition = Definition("reports") with
        {
            RequestedPermissions = [PluginPermission.Parse("reporting.extend")],
        };

        var context = harness.Context(
            definition,
            granted: [PluginPermission.Parse("reporting.extend")],
            quota: new PluginResourceQuota { MaxConcurrentOperations = 1 });

        var first = harness.Sandbox.Enter(context, PluginPermission.Parse("reporting.extend"));
        Assert.True(first.Granted);

        using (var second = harness.Sandbox.Enter(context, PluginPermission.Parse("reporting.extend")))
        {
            Assert.False(second.Granted);
            Assert.Equal(PluginSandboxRefusal.ConcurrencyExceeded, second.Refusal);
        }

        first.Dispose();

        using var third = harness.Sandbox.Enter(context, PluginPermission.Parse("reporting.extend"));
        Assert.True(third.Granted);
    }

    [Fact]
    public void A_plugin_over_its_storage_quota_is_refused_before_it_takes_a_slot()
    {
        var harness = new Harness();
        var definition = Definition("reports") with
        {
            RequestedPermissions = [PluginPermission.Parse("reporting.extend")],
        };

        var context = harness.Context(
            definition,
            granted: [PluginPermission.Parse("reporting.extend")],
            quota: new PluginResourceQuota { MaxStorageBytes = 1024 });

        harness.Sandbox.RecordStorage(context.Instance, 4096);

        using var lease = harness.Sandbox.Enter(context, PluginPermission.Parse("reporting.extend"));

        Assert.False(lease.Granted);
        Assert.Equal(PluginSandboxRefusal.StorageExceeded, lease.Refusal);
    }

    [Fact]
    public void One_tenants_exhausted_quota_does_not_touch_another_running_the_same_plugin()
    {
        var harness = new Harness();
        var definition = Definition("reports") with
        {
            RequestedPermissions = [PluginPermission.Parse("reporting.extend")],
        };

        var quota = new PluginResourceQuota { MaxConcurrentOperations = 1 };
        var mine = harness.Context(definition, granted: [PluginPermission.Parse("reporting.extend")], quota: quota);
        var theirs = harness.Context(
            definition, granted: [PluginPermission.Parse("reporting.extend")], quota: quota, tenant: Other);

        using var first = harness.Sandbox.Enter(mine, PluginPermission.Parse("reporting.extend"));
        using var second = harness.Sandbox.Enter(theirs, PluginPermission.Parse("reporting.extend"));

        Assert.True(first.Granted);
        Assert.True(second.Granted);
    }

    [Fact]
    public void Usage_without_a_quota_is_reported_as_unlimited_rather_than_as_zero_headroom()
    {
        var harness = new Harness();
        var instance = new PluginInstance(Tenant, "energy", PluginVersion.Parse("1.0.0"));

        var usage = harness.Sandbox.Usages(instance);

        Assert.All(usage, reading => Assert.False(reading.IsLimited));
        Assert.All(usage, reading => Assert.Equal(long.MaxValue, reading.Remaining));
    }

    // ---------------------------------------------------------------------------------------------------
    // Extension point resolution
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_engine_asking_who_extends_it_gets_data_from_the_running_plugins()
    {
        var harness = new Harness(compiled: [new StubPlugin("reports")]);
        await harness.Install(ReportingPackage(), Grants(PluginPermission.Parse("reporting.extend")));
        await harness.Start("reports");

        var extensions = harness.Runtime.Extensions(Tenant, PluginExtensionPointKind.Reporting);

        var extension = Assert.Single(extensions);
        Assert.Equal("shift-summary", extension.Contribution.Name);
        Assert.Equal("reports", extension.PluginKey);
    }

    [Fact]
    public async Task A_stopped_plugin_is_withdrawn_from_the_extension_surface()
    {
        var harness = new Harness(compiled: [new StubPlugin("reports")]);
        await harness.Install(ReportingPackage(), Grants(PluginPermission.Parse("reporting.extend")));
        await harness.Start("reports");
        await harness.Runtime.Lifecycle.StopAsync(Admin(Tenant), "reports");

        Assert.Empty(harness.Runtime.Extensions(Tenant, PluginExtensionPointKind.Reporting));
    }

    [Fact]
    public async Task A_suspended_plugin_contributes_nothing_while_it_is_suspended()
    {
        var harness = new Harness(compiled: [new StubPlugin("reports")]);
        await harness.Install(ReportingPackage(), Grants(PluginPermission.Parse("reporting.extend")));
        await harness.Start("reports");
        await harness.Runtime.Lifecycle.SuspendAsync(Admin(Tenant), "reports", "maintenance");

        Assert.Empty(harness.Runtime.Extensions(Tenant, PluginExtensionPointKind.Reporting));

        await harness.Runtime.Lifecycle.ResumeAsync(Admin(Tenant), "reports");
        Assert.Single(harness.Runtime.Extensions(Tenant, PluginExtensionPointKind.Reporting));
    }

    [Fact]
    public async Task One_tenants_extensions_are_never_another_tenants()
    {
        var harness = new Harness(compiled: [new StubPlugin("reports")]);
        await harness.Install(ReportingPackage(), Grants(PluginPermission.Parse("reporting.extend")));
        await harness.Start("reports");

        Assert.Empty(harness.Runtime.Extensions(Other, PluginExtensionPointKind.Reporting));
    }

    [Fact]
    public async Task A_named_contribution_can_be_found_and_an_unknown_one_cannot()
    {
        var harness = new Harness(compiled: [new StubPlugin("reports")]);
        await harness.Install(ReportingPackage(), Grants(PluginPermission.Parse("reporting.extend")));
        await harness.Start("reports");

        var resolver = harness.Provider.GetRequiredService<PluginExtensionPointResolver>();

        Assert.NotNull(resolver.Find(Tenant, PluginExtensionPointKind.Reporting, "shift-summary"));
        Assert.Null(resolver.Find(Tenant, PluginExtensionPointKind.Reporting, "nothing-like-that"));
        Assert.Single(resolver.ResolveAll(Tenant));
    }

    // ---------------------------------------------------------------------------------------------------
    // Health
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void A_plugin_nobody_installed_reports_unknown_rather_than_healthy()
    {
        var report = new Harness().Runtime.Health(Tenant, "energy");

        Assert.Equal(PluginHealthStatus.Unknown, report.Status);
    }

    [Fact]
    public async Task A_plugin_that_has_never_been_started_is_unknown_not_healthy()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);
        await harness.Install(Package("energy"));

        var report = harness.Runtime.Health(Tenant, "energy");

        Assert.Equal(PluginHealthStatus.Unknown, report.Status);
        Assert.Equal(PluginHealthStatus.Unknown, report.For(PluginHealthAspect.Liveness)!.Status);
    }

    [Fact]
    public async Task A_running_plugin_with_everything_in_place_is_healthy()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);
        await harness.Install(Package("energy"));
        await harness.Start("energy");

        var report = harness.Runtime.Health(Tenant, "energy");

        Assert.Equal(PluginHealthStatus.Healthy, report.Status);
        Assert.Empty(report.Problems);
    }

    [Fact]
    public async Task A_suspended_plugin_reads_as_degraded_and_not_as_failed()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);
        await harness.Install(Package("energy"));
        await harness.Start("energy");
        await harness.Runtime.Lifecycle.SuspendAsync(Admin(Tenant), "energy", "maintenance");

        Assert.Equal(PluginHealthStatus.Degraded, harness.Runtime.Health(Tenant, "energy").Status);
    }

    [Fact]
    public async Task A_plugin_an_operator_switched_off_is_not_reported_as_a_fault()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);
        await harness.Install(Package("energy"));
        harness.Runtime.Configuration.SetEnabled(Admin(Tenant), "energy", false);

        var liveness = harness.Runtime.Health(Tenant, "energy").For(PluginHealthAspect.Liveness)!;

        Assert.Equal(PluginHealthStatus.Unknown, liveness.Status);
        Assert.Contains("switched off", liveness.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_missing_dependency_makes_a_running_plugin_unhealthy()
    {
        var harness = new Harness(compiled: [new StubPlugin("reports")]);
        var package = Package("reports") with
        {
            Definition = Definition("reports") with
            {
                Dependencies = [new PluginDependency("energy", PluginVersion.Parse("1.0.0"))],
            },
        };

        await harness.Install(package);
        await harness.Start("reports");

        var dependencies = harness.Runtime.Health(Tenant, "reports").For(PluginHealthAspect.Dependencies)!;

        Assert.Equal(PluginHealthStatus.Unhealthy, dependencies.Status);
        Assert.Contains("energy", dependencies.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_dependency_that_is_installed_but_stopped_degrades_rather_than_fails()
    {
        var harness = new Harness(compiled: [new StubPlugin("reports"), new StubPlugin("energy")]);
        await harness.Install(Package("energy"));

        var package = Package("reports") with
        {
            Definition = Definition("reports") with
            {
                Dependencies = [new PluginDependency("energy", PluginVersion.Parse("1.0.0"))],
            },
        };

        await harness.Install(package);
        await harness.Start("reports");

        var dependencies = harness.Runtime.Health(Tenant, "reports").For(PluginHealthAspect.Dependencies)!;

        Assert.Equal(PluginHealthStatus.Degraded, dependencies.Status);
    }

    [Fact]
    public async Task The_worst_aspect_decides_the_overall_verdict()
    {
        var harness = new Harness(compiled: [new StubPlugin("reports")]);
        var package = Package("reports") with
        {
            Definition = Definition("reports") with
            {
                Dependencies = [new PluginDependency("energy", PluginVersion.Parse("1.0.0"))],
            },
        };

        await harness.Install(package);
        await harness.Start("reports");

        var report = harness.Runtime.Health(Tenant, "reports");

        Assert.Equal(PluginHealthStatus.Healthy, report.For(PluginHealthAspect.Liveness)!.Status);
        Assert.Equal(PluginHealthStatus.Unhealthy, report.Status);
    }

    [Fact]
    public async Task A_health_verdict_is_announced_once_when_it_changes_and_not_again_while_it_holds()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);
        await harness.Install(Package("energy"));
        await harness.Start("energy");

        harness.Runtime.Health(Tenant, "energy");
        var afterFirst = harness.Events.Of<PluginHealthChanged>().Count;
        harness.Runtime.Health(Tenant, "energy");

        Assert.Equal(1, afterFirst);
        Assert.Equal(afterFirst, harness.Events.Of<PluginHealthChanged>().Count);
    }

    // ---------------------------------------------------------------------------------------------------
    // Scheduling, host and bootstrap
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_instance_nobody_has_probed_is_due_immediately_and_not_again_until_the_interval()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);
        await harness.Install(Package("energy"));

        Assert.Single(harness.Scheduler.Due(Now));
        harness.Scheduler.RunDue(Now);
        Assert.Empty(harness.Scheduler.Due(Now.AddSeconds(30)));
        Assert.Single(harness.Scheduler.Due(Now.AddMinutes(2)));
    }

    [Fact]
    public async Task Starting_a_tenant_brings_its_plugins_up_in_dependency_order()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy"), new StubPlugin("reports")]);
        await harness.Install(Package("energy"));
        await harness.Install(Package("reports") with
        {
            Definition = Definition("reports") with
            {
                Dependencies = [new PluginDependency("energy", PluginVersion.Parse("1.0.0"))],
            },
        });

        var summary = await harness.Host.StartTenantAsync(Admin(Tenant));

        Assert.True(summary.IsClean);
        Assert.Equal(2, summary.Succeeded);

        var started = harness.Events.Of<PluginStarted>();
        Assert.Equal(["energy", "reports"], started.Select(item => item.PluginKey));
    }

    [Fact]
    public async Task One_broken_plugin_does_not_stop_a_factory_coming_up()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);
        await harness.Install(Package("energy"));
        await harness.Install(Package("ghost"));

        var summary = await harness.Host.StartTenantAsync(Admin(Tenant));

        Assert.Equal(1, summary.Succeeded);
        Assert.Equal(1, summary.Failed);
        Assert.Contains("ghost", summary.Problems[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stopping_a_tenant_takes_its_plugins_down_in_reverse_order()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy"), new StubPlugin("reports")]);
        await harness.Install(Package("energy"));
        await harness.Install(Package("reports") with
        {
            Definition = Definition("reports") with
            {
                Dependencies = [new PluginDependency("energy", PluginVersion.Parse("1.0.0"))],
            },
        });
        await harness.Host.StartTenantAsync(Admin(Tenant));

        var summary = await harness.Host.StopTenantAsync(Admin(Tenant));

        Assert.Equal(2, summary.Succeeded);
        Assert.Equal(["reports", "energy"], harness.Events.Of<PluginStopped>().Select(item => item.PluginKey));
    }

    [Fact]
    public async Task A_cold_start_discovers_installs_and_starts_in_one_call()
    {
        using var root = new TempRoot();
        root.Write("energy", """{"key":"energy","name":"Energy","version":"1.0.0"}""");
        root.Write("broken", """{"name":"missing key","version":"1.0.0"}""");

        var harness = new Harness(compiled: [new StubPlugin("energy")]);
        var result = await harness.Engine.BootstrapAsync(Admin(Tenant), Grants(), root.Path);

        Assert.Equal(1, result.Discovered);
        Assert.Equal(1, result.Installed);
        Assert.Equal(1, result.Started);
        Assert.Single(result.Problems);
    }

    // ---------------------------------------------------------------------------------------------------
    // Events, audit and measurements
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Every_lifecycle_step_is_announced_once_with_the_tenant_on_it()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);
        await harness.Install(Package("energy"));
        await harness.Start("energy");

        Assert.Single(harness.Events.Of<PluginInstalled>());
        Assert.Single(harness.Events.Of<PluginLoaded>());
        Assert.Single(harness.Events.Of<PluginStarted>());
        Assert.All(harness.Events.All(), announced => Assert.Equal(Tenant, announced.Tenant));
        Assert.Single(harness.Events.ForTenant(Tenant).OfType<PluginStarted>());
    }

    [Fact]
    public async Task A_lifecycle_step_produces_an_audit_line_naming_who_asked()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);
        await harness.Install(Package("energy"));

        var entry = Assert.Single(harness.Audit.ForTenant(Tenant));

        Assert.Equal(PluginLifecyclePhase.Install, entry.Phase);
        Assert.Equal("ops", entry.Subject);
        Assert.True(entry.Succeeded);
    }

    [Fact]
    public async Task A_failure_is_always_audited_even_when_successes_are_not()
    {
        var harness = new Harness(services => services.Configure<PluginRuntimeOptions>(
            options => options.AuditLifecycle = false));

        await harness.Install(Package("energy"));
        await harness.Runtime.Lifecycle.StartAsync(Admin(Tenant), "energy");

        var entry = Assert.Single(harness.Audit.All());

        Assert.False(entry.Succeeded);
        Assert.Equal(PluginLifecyclePhase.Start, entry.Phase);
    }

    [Fact]
    public async Task A_failed_transition_is_announced_with_a_classified_reason()
    {
        var harness = new Harness();
        var package = Package("reports") with
        {
            Definition = Definition("reports") with
            {
                RequestedPermissions = [PluginPermission.Parse("workflow.extend")],
            },
        };

        await harness.Runtime.InstallAsync(Admin(Tenant), package, Grants());

        var failure = Assert.Single(harness.Events.Of<PluginFailed>());

        Assert.Equal(PluginFailureKind.Permission, failure.Kind);
        Assert.Equal(PluginLifecyclePhase.Install, failure.Phase);
    }

    [Theory]
    [InlineData("Plugin.Runtime.Signature.Invalid", PluginFailureKind.Signature)]
    [InlineData("Plugin.Runtime.Manifest.NoKey", PluginFailureKind.Manifest)]
    [InlineData("Plugin.Runtime.Compatibility.Unsupported", PluginFailureKind.Compatibility)]
    [InlineData("Plugin.Runtime.Load.Failed", PluginFailureKind.Activation)]
    [InlineData("Plugin.Runtime.Lifecycle.Refused", PluginFailureKind.Lifecycle)]
    public void A_failure_is_classified_from_the_code_that_reported_it(string code, PluginFailureKind kind)
    {
        Assert.Equal(kind, PluginFailures.Classify(code));
    }

    [Fact]
    public async Task A_lifecycle_step_produces_measurements_labelled_by_tenant_and_phase()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);
        await harness.Install(Package("energy"));
        await harness.Start("energy");

        var transitions = harness.Metrics.Named(PluginMetricNames.Transitions);

        Assert.NotEmpty(transitions);
        Assert.All(transitions, measurement =>
            Assert.Equal(Tenant, measurement.Labels[PluginRuntimeConstants.TenantLabel]));
        Assert.Single(harness.Metrics.Named(PluginMetricNames.Starts));
        Assert.NotEmpty(harness.Metrics.Named(PluginMetricNames.TransitionDuration));
    }

    [Fact]
    public async Task A_tally_distinguishes_nothing_having_happened_from_everything_having_worked()
    {
        var harness = new Harness(compiled: [new StubPlugin("energy")]);

        Assert.Null(harness.Announcer.Metrics(Tenant, "energy").Snapshot().FailureRate);

        await harness.Install(Package("energy"));

        Assert.Equal(0d, harness.Announcer.Metrics(Tenant, "energy").Snapshot().FailureRate);
    }

    [Fact]
    public void Every_port_fans_out_to_every_registered_sink()
    {
        var extra = new CountingEventSink();
        var harness = new Harness(services => services.AddSingleton<IPluginRuntimeEventSink>(extra));

        harness.Provider.GetRequiredService<PluginRuntimePublisher>()
            .Publish(new PluginResumed(Tenant, "energy", Now));

        Assert.Equal(1, extra.Count);
    }

    [Fact]
    public void The_runtime_registration_pulls_in_no_workflow_engine()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddPluginRuntime();

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType.FullName?.Contains("Workflow", StringComparison.Ordinal) == true);
    }

    // ---------------------------------------------------------------------------------------------------
    // Persistence
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void A_package_store_keeps_versions_side_by_side_and_prunes_the_oldest()
    {
        var store = new InMemoryPluginPackageStore();
        store.Save(Package("energy", "1.0.0"));
        store.Save(Package("energy", "1.1.0"));
        store.Save(Package("energy", "1.2.0"));

        var dropped = store.Prune("energy", 2);

        Assert.Equal([PluginVersion.Parse("1.0.0")], dropped);
        Assert.Equal(2, store.Versions("energy").Count);
    }

    [Fact]
    public void Pruning_always_keeps_at_least_the_newest_version()
    {
        var store = new InMemoryPluginPackageStore();
        store.Save(Package("energy", "1.0.0"));

        store.Prune("energy", 0);

        Assert.Single(store.Versions("energy"));
    }

    [Fact]
    public void The_repository_answers_with_the_highest_known_version()
    {
        var repository = new InMemoryPluginRepository();
        repository.Save(Definition("energy"));
        repository.Save(Definition("energy") with { Version = PluginVersion.Parse("2.0.0") });

        Assert.Equal(PluginVersion.Parse("2.0.0"), repository.Latest("energy")!.Version);
        Assert.Null(repository.Latest("absent"));
    }

    [Fact]
    public void The_store_never_returns_one_tenants_installation_to_another()
    {
        var store = new InMemoryPluginStore();
        store.Save(new PluginInstance(Tenant, "energy", PluginVersion.Parse("1.0.0")));

        Assert.NotNull(store.Find(Tenant, "energy"));
        Assert.Null(store.Find(Other, "energy"));
        Assert.Empty(store.ForTenant(Other));
    }

    // ---------------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------------

    private static PluginCaller Admin(string tenant) =>
        PluginCaller.Holding(tenant, "ops", PluginPermission.Parse("plugin.*"));

    private static IReadOnlyList<PluginPermission> Grants(params PluginPermission[] extra) =>
        [PluginPermission.Parse("plugin.*"), .. extra];

    private static PluginManifest Manifest(string key, string version = "1.0.0") => new()
    {
        Key = key,
        Name = key,
        Version = PluginVersion.Parse(version),
    };

    private static PluginDefinition Definition(string key, string version = "1.0.0") =>
        PluginDefinition.FromManifest(Manifest(key, version));

    private static PluginPackage Package(string key, string version = "1.0.0") =>
        PluginPackage.WithoutSignature(Manifest(key, version), Definition(key, version));

    private static PluginPackage ReportingPackage() => Package("reports") with
    {
        Definition = Definition("reports") with
        {
            Contributions =
            [
                PluginContribution.To(PluginExtensionPointKind.Reporting, "shift-summary") with
                {
                    Reference = "reports/Shift",
                },
            ],
        },
    };

    private sealed class Harness
    {
        public Harness(Action<IServiceCollection>? configure = null, IEnumerable<IPlugin>? compiled = null)
        {
            Clock = new MutableClock(Now);
            Keys = new InMemoryPluginSigningKeySource();

            var services = new ServiceCollection();

            // The framework's configuration provider and host read the host's configuration and logger; a
            // container without them cannot construct them. Every real host has both.
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddSingleton<ILogger<PluginHost>>(NullLogger<PluginHost>.Instance);
            services.AddSingleton<IDateTimeProvider>(Clock);
            services.AddSingleton<IPluginSigningKeySource>(Keys);

            foreach (var plugin in compiled ?? [])
            {
                services.AddSingleton(plugin);
            }

            configure?.Invoke(services);
            services.AddPluginRuntime();

            Provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
        }

        public MutableClock Clock { get; }

        public InMemoryPluginSigningKeySource Keys { get; }

        public ServiceProvider Provider { get; }

        public IPluginRuntime Runtime => Provider.GetRequiredService<IPluginRuntime>();

        public PluginEngine Engine => Provider.GetRequiredService<PluginEngine>();

        public PluginRuntimeHost Host => Provider.GetRequiredService<PluginRuntimeHost>();

        public PluginRuntimeScheduler Scheduler => Provider.GetRequiredService<PluginRuntimeScheduler>();

        public PluginInstanceRegistry Registry => Provider.GetRequiredService<PluginInstanceRegistry>();

        public PluginIsolationManager Isolation => Provider.GetRequiredService<PluginIsolationManager>();

        public PluginSandbox Sandbox => Provider.GetRequiredService<PluginSandbox>();

        public PluginSignatureValidator Signatures => Provider.GetRequiredService<PluginSignatureValidator>();

        public PluginRuntimeAnnouncer Announcer => Provider.GetRequiredService<PluginRuntimeAnnouncer>();

        public InMemoryPluginRuntimeEventSink Events =>
            Provider.GetRequiredService<InMemoryPluginRuntimeEventSink>();

        public InMemoryPluginAuditSink Audit => Provider.GetRequiredService<InMemoryPluginAuditSink>();

        public InMemoryPluginMetricSink Metrics => Provider.GetRequiredService<InMemoryPluginMetricSink>();

        public PluginInstance? Instance(string key, string tenant = Tenant) => Registry.Find(tenant, key);

        public async Task Install(
            PluginPackage package, IReadOnlyList<PluginPermission>? granted = null, string tenant = Tenant)
        {
            var installed = await Runtime.InstallAsync(
                Admin(tenant), package, granted ?? Grants());
            Assert.True(installed.IsSuccess, installed.IsFailure ? installed.Error.Description : string.Empty);
        }

        public async Task Start(string key, string tenant = Tenant)
        {
            var loaded = await Runtime.Lifecycle.LoadAsync(Admin(tenant), key);
            Assert.True(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.Description : string.Empty);

            var started = await Runtime.Lifecycle.StartAsync(Admin(tenant), key);
            Assert.True(started.IsSuccess, started.IsFailure ? started.Error.Description : string.Empty);
        }

        public PluginRuntimeContext Context(
            PluginDefinition definition,
            IReadOnlyList<PluginPermission>? granted = null,
            PluginResourceQuota? quota = null,
            PluginRuntimeStatus status = PluginRuntimeStatus.Running,
            string tenant = Tenant)
        {
            var instance = new PluginInstance(
                tenant, definition.Key, definition.Version, granted ?? definition.EffectiveRequests());

            if (quota is not null)
            {
                instance.UseQuota(quota);
            }

            switch (status)
            {
                case PluginRuntimeStatus.Running:
                    instance.MarkRunning(Now);
                    break;
                case PluginRuntimeStatus.Stopped:
                    instance.MarkStopped();
                    break;
                default:
                    instance.MarkInstalled();
                    break;
            }

            var manifest = Manifest(definition.Key, definition.Version.ToString());
            return new PluginRuntimeContext(
                instance, definition, manifest, Isolation.StoragePathFor(instance));
        }
    }

    private sealed class StubPlugin : IPlugin
    {
        public StubPlugin(string key) => Key = key;

        public string Key { get; }

        public int Starts { get; private set; }

        public int Stops { get; private set; }

        public void ConfigureServices(IServiceCollection services)
        {
            // The runtime never calls this; the host's composition root does.
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Starts++;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stops++;
            return Task.CompletedTask;
        }
    }

    private sealed class LifecyclePlugin : IPlugin, IPluginLifecycle
    {
        public LifecyclePlugin(string key) => Key = key;

        public string Key { get; }

        public int Initializations { get; private set; }

        public int Unloads { get; private set; }

        public string? SeenTenant { get; private set; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Nothing to contribute.
        }

        public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken)
        {
            Initializations++;
            SeenTenant = (context as PluginRuntimeContext)?.Tenant;
            return Task.CompletedTask;
        }

        public Task UnloadAsync(CancellationToken cancellationToken)
        {
            Unloads++;
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class PermissiveAuthorizer : IPluginAuthorizer
    {
        public PluginAuthorization Authorize(
            PluginCaller? caller, PluginInstance instance, PluginPermission required) =>
            PluginAuthorization.Allow();
    }

    private sealed class CountingEventSink : IPluginRuntimeEventSink
    {
        public int Count { get; private set; }

        public void Publish(PluginRuntimeEvent runtimeEvent) => Count++;
    }

    private sealed class TempRoot : IDisposable
    {
        public TempRoot()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "factoryos-plugins-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string Write(string folder, string? manifest)
        {
            var directory = System.IO.Path.Combine(Path, folder);
            Directory.CreateDirectory(directory);

            if (manifest is not null)
            {
                File.WriteAllText(System.IO.Path.Combine(directory, "module.json"), manifest);
            }

            return directory;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
