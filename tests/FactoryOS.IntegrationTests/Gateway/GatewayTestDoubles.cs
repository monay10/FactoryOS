using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Endpoints;
using FactoryOS.Plugin.Hosting;
using FactoryOS.Plugin.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Gateway;

/// <summary>An in-process plugin host backed by a fixed set of descriptors, for gateway tests.</summary>
internal sealed class FakePluginHost : IPluginHost
{
    private readonly List<PluginDescriptor> _descriptors;

    public FakePluginHost(params PluginDescriptor[] descriptors) => _descriptors = [.. descriptors];

    public IReadOnlyCollection<PluginDescriptor> Plugins => _descriptors;

    public IReadOnlyList<PluginDescriptor> LoadOrder =>
        _descriptors.Where(descriptor => descriptor.State is not (PluginState.Disabled or PluginState.Failed)).ToArray();

    public void ConfigureServices(IServiceCollection services)
    {
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>A module API that maps a single <c>/ping</c> endpoint returning its module key.</summary>
internal sealed class FakeModuleApi : IModuleApi
{
    public FakeModuleApi(string moduleKey) => ModuleKey = moduleKey;

    public string ModuleKey { get; }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/ping", () => Results.Ok(new PingResponse(ModuleKey)));
}

internal sealed record PingResponse(string Module);

/// <summary>Factory helpers for building descriptors and screens in a readable way.</summary>
internal static class GatewayFixtures
{
    public static PluginDescriptor Module(string key, PluginState state, params PluginUiScreen[] ui) =>
        Describe(new PluginManifest
        {
            Key = key,
            Name = $"{key} Module",
            Version = new PluginVersion(1, 0, 0),
            Ui = ui,
        }, state);

    public static PluginDescriptor ApiModule(string key, PluginState state, params PluginApiRoute[] api) =>
        Describe(new PluginManifest
        {
            Key = key,
            Name = $"{key} Module",
            Version = new PluginVersion(1, 0, 0),
            Api = api,
        }, state);

    public static PluginApiRoute Route(string method, string path, params string[] query) =>
        new() { Method = method, Path = path, Query = query };

    public static PluginDescriptor DependentModule(string key, PluginState state, params PluginDependency[] dependencies) =>
        Describe(new PluginManifest
        {
            Key = key,
            Name = $"{key} Module",
            Version = new PluginVersion(1, 0, 0),
            Description = $"{key} description",
            Author = "FactoryOS",
            Provides = [$"{key}.capability"],
            Dependencies = dependencies,
        }, state);

    public static PluginDependency Requires(string pluginKey, int major = 1, int minor = 0, int patch = 0) =>
        new(pluginKey, new PluginVersion(major, minor, patch));

    public static PluginDescriptor CapabilityModule(
        string key,
        PluginState state,
        string[] provides,
        string[] consumes,
        string[] emits) =>
        Describe(new PluginManifest
        {
            Key = key,
            Name = $"{key} Module",
            Version = new PluginVersion(1, 0, 0),
            Provides = provides,
            Consumes = consumes,
            Emits = emits,
        }, state);

    private static PluginDescriptor Describe(PluginManifest manifest, PluginState state)
    {
        var descriptor = new PluginDescriptor(manifest, $"/plugins/{manifest.Key}");

        switch (state)
        {
            case PluginState.Disabled:
                descriptor.MarkDisabled();
                break;
            case PluginState.Failed:
                descriptor.MarkFailed("test failure");
                break;
            case PluginState.Started:
                descriptor.MarkStarted();
                break;
            default:
                break;
        }

        return descriptor;
    }

    public static PluginUiScreen Screen(string id, int order, string section = "Main", string? permission = null) => new()
    {
        Id = id,
        Title = id,
        Route = $"/{id}",
        Component = id,
        NavSection = section,
        Order = order,
        RequiredPermission = permission,
    };
}
