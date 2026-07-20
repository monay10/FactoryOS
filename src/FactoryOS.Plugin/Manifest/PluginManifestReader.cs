using System.Text.Json;
using System.Text.Json.Serialization;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Domain.Results;

namespace FactoryOS.Plugin.Manifest;

/// <summary>
/// Reads a plugin <c>module.json</c> manifest into a strongly-typed <see cref="PluginManifest"/>,
/// validating required fields and version formats. Mapping is data, not code.
/// </summary>
public static class PluginManifestReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Reads a manifest from its JSON text.</summary>
    /// <param name="json">The manifest JSON.</param>
    /// <returns>A successful result with the manifest, or a failure describing the problem.</returns>
    public static Result<PluginManifest> Read(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result.Failure<PluginManifest>(
                Error.Validation("Plugin.Manifest.Empty", "The plugin manifest is empty."));
        }

        ManifestDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ManifestDto>(json, SerializerOptions);
        }
        catch (JsonException exception)
        {
            return Result.Failure<PluginManifest>(
                Error.Validation("Plugin.Manifest.Malformed", $"The plugin manifest is not valid JSON: {exception.Message}"));
        }

        if (dto is null)
        {
            return Result.Failure<PluginManifest>(
                Error.Validation("Plugin.Manifest.Empty", "The plugin manifest deserialized to nothing."));
        }

        if (string.IsNullOrWhiteSpace(dto.Key))
        {
            return Result.Failure<PluginManifest>(
                Error.Validation("Plugin.Manifest.MissingKey", "The plugin manifest is missing 'key'."));
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Result.Failure<PluginManifest>(
                Error.Validation("Plugin.Manifest.MissingName", "The plugin manifest is missing 'name'."));
        }

        if (!PluginVersion.TryParse(dto.Version, out var version))
        {
            return Result.Failure<PluginManifest>(
                Error.Validation("Plugin.Manifest.InvalidVersion", $"'{dto.Version}' is not a valid plugin version."));
        }

        var dependencies = new List<PluginDependency>();
        foreach (var dependency in dto.Dependencies ?? [])
        {
            if (string.IsNullOrWhiteSpace(dependency.Key))
            {
                return Result.Failure<PluginManifest>(
                    Error.Validation("Plugin.Manifest.InvalidDependency", "A dependency is missing 'key'."));
            }

            if (!PluginVersion.TryParse(dependency.MinimumVersion, out var minimumVersion))
            {
                return Result.Failure<PluginManifest>(
                    Error.Validation(
                        "Plugin.Manifest.InvalidDependency",
                        $"Dependency '{dependency.Key}' has an invalid minimum version '{dependency.MinimumVersion}'."));
            }

            dependencies.Add(new PluginDependency(dependency.Key, minimumVersion));
        }

        var screens = new List<PluginUiScreen>();
        foreach (var screen in dto.Ui ?? [])
        {
            if (string.IsNullOrWhiteSpace(screen.Id) ||
                string.IsNullOrWhiteSpace(screen.Title) ||
                string.IsNullOrWhiteSpace(screen.Route) ||
                string.IsNullOrWhiteSpace(screen.Component))
            {
                return Result.Failure<PluginManifest>(
                    Error.Validation(
                        "Plugin.Manifest.InvalidUiScreen",
                        "A UI screen must declare non-empty 'id', 'title', 'route' and 'component'."));
            }

            screens.Add(new PluginUiScreen
            {
                Id = screen.Id,
                Title = screen.Title,
                Route = screen.Route,
                Component = screen.Component,
                Icon = screen.Icon,
                RequiredPermission = screen.RequiredPermission,
                NavSection = screen.NavSection,
                Order = screen.Order,
            });
        }

        var routes = new List<PluginApiRoute>();
        foreach (var route in dto.Api ?? [])
        {
            if (string.IsNullOrWhiteSpace(route.Method) || string.IsNullOrWhiteSpace(route.Path))
            {
                return Result.Failure<PluginManifest>(
                    Error.Validation(
                        "Plugin.Manifest.InvalidApiRoute",
                        "An API route must declare non-empty 'method' and 'path'."));
            }

            routes.Add(new PluginApiRoute
            {
                Method = route.Method,
                Path = route.Path,
                Query = route.Query ?? [],
                Description = route.Description,
            });
        }

        var manifest = new PluginManifest
        {
            Key = dto.Key,
            Name = dto.Name,
            Version = version,
            Description = dto.Description,
            Author = dto.Author,
            Assembly = dto.Assembly,
            EntryType = dto.EntryType,
            Dependencies = dependencies,
            Provides = dto.Provides ?? [],
            Consumes = dto.Consumes ?? [],
            Emits = dto.Emits ?? [],
            Ui = screens,
            Api = routes,
        };

        return manifest;
    }

    /// <summary>Reads a manifest from a file on disk.</summary>
    /// <param name="path">The path to the manifest file.</param>
    /// <returns>A successful result with the manifest, or a failure describing the problem.</returns>
    public static Result<PluginManifest> ReadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return Result.Failure<PluginManifest>(
                Error.NotFound("Plugin.Manifest.NotFound", $"No manifest was found at '{path}'."));
        }

        return Read(File.ReadAllText(path));
    }

    private sealed class ManifestDto
    {
        public string? Key { get; set; }

        public string? Name { get; set; }

        public string? Version { get; set; }

        public string? Description { get; set; }

        public string? Author { get; set; }

        public string? Assembly { get; set; }

        public string? EntryType { get; set; }

        public List<DependencyDto>? Dependencies { get; set; }

        public List<string>? Provides { get; set; }

        public List<string>? Consumes { get; set; }

        public List<string>? Emits { get; set; }

        public List<UiScreenDto>? Ui { get; set; }

        public List<ApiRouteDto>? Api { get; set; }
    }

    private sealed class ApiRouteDto
    {
        public string? Method { get; set; }

        public string? Path { get; set; }

        public List<string>? Query { get; set; }

        public string? Description { get; set; }
    }

    private sealed class DependencyDto
    {
        public string? Key { get; set; }

        [JsonPropertyName("minimumVersion")]
        public string? MinimumVersion { get; set; }
    }

    private sealed class UiScreenDto
    {
        public string? Id { get; set; }

        public string? Title { get; set; }

        public string? Route { get; set; }

        public string? Component { get; set; }

        public string? Icon { get; set; }

        [JsonPropertyName("requiredPermission")]
        public string? RequiredPermission { get; set; }

        [JsonPropertyName("navSection")]
        public string? NavSection { get; set; }

        public int Order { get; set; }
    }
}
