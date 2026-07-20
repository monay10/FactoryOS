using System.Text.Json;
using FactoryOS.Configuration.Model;
using FactoryOS.Configuration.Secrets;
using FactoryOS.Configuration.Validation;
using FactoryOS.Domain.Results;

namespace FactoryOS.Configuration.Reading;

/// <summary>
/// Parses a tenant's <c>tenant.json</c> into a strongly-typed, secret-expanded and validated
/// <see cref="TenantConfiguration"/>. Mapping is data, not code.
/// </summary>
public sealed class TenantConfigurationReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly SecretExpander _secretExpander;
    private readonly ITenantConfigurationValidator _validator;

    /// <summary>Initializes a new instance of the <see cref="TenantConfigurationReader"/> class.</summary>
    /// <param name="secretExpander">Expands secret placeholders in setting values.</param>
    /// <param name="validator">Validates the parsed configuration.</param>
    public TenantConfigurationReader(SecretExpander secretExpander, ITenantConfigurationValidator validator)
    {
        ArgumentNullException.ThrowIfNull(secretExpander);
        ArgumentNullException.ThrowIfNull(validator);

        _secretExpander = secretExpander;
        _validator = validator;
    }

    /// <summary>Reads a tenant configuration from JSON text.</summary>
    /// <param name="json">The <c>tenant.json</c> content.</param>
    /// <returns>A validated configuration, or a failure describing the problem.</returns>
    public Result<TenantConfiguration> Read(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result.Failure<TenantConfiguration>(
                Error.Validation("Configuration.Tenant.Empty", "The tenant configuration is empty."));
        }

        TenantDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<TenantDto>(json, SerializerOptions);
        }
        catch (JsonException exception)
        {
            return Result.Failure<TenantConfiguration>(
                Error.Validation("Configuration.Tenant.Malformed", $"The tenant configuration is not valid JSON: {exception.Message}"));
        }

        if (dto is null)
        {
            return Result.Failure<TenantConfiguration>(
                Error.Validation("Configuration.Tenant.Empty", "The tenant configuration deserialized to nothing."));
        }

        if (!TryParseEnum<DeploymentEnvironment>(dto.Environment, DeploymentEnvironment.Production, out var environment))
        {
            return Result.Failure<TenantConfiguration>(
                Error.Validation("Configuration.Tenant.InvalidEnvironment", $"'{dto.Environment}' is not a valid environment."));
        }

        var modules = new List<ModuleConfiguration>();
        foreach (var component in dto.Modules ?? [])
        {
            var settings = ExpandSettings(component.Settings);
            if (settings.IsFailure)
            {
                return Result.Failure<TenantConfiguration>(settings.Error);
            }

            modules.Add(new ModuleConfiguration
            {
                Key = component.Key ?? string.Empty,
                Enabled = component.Enabled ?? true,
                Settings = settings.Value,
            });
        }

        var plugins = new List<PluginConfiguration>();
        foreach (var component in dto.Plugins ?? [])
        {
            var settings = ExpandSettings(component.Settings);
            if (settings.IsFailure)
            {
                return Result.Failure<TenantConfiguration>(settings.Error);
            }

            plugins.Add(new PluginConfiguration
            {
                Key = component.Key ?? string.Empty,
                Enabled = component.Enabled ?? true,
                Settings = settings.Value,
            });
        }

        var localizationResult = MapLocalization(dto.Localization);
        if (localizationResult.IsFailure)
        {
            return Result.Failure<TenantConfiguration>(localizationResult.Error);
        }

        var configuration = new TenantConfiguration
        {
            TenantId = dto.TenantId ?? string.Empty,
            Name = dto.Name ?? string.Empty,
            Environment = environment,
            Branding = dto.Branding is null
                ? null
                : new TenantBranding(dto.Branding.DisplayName ?? string.Empty, dto.Branding.PrimaryColor, dto.Branding.LogoUrl),
            Localization = localizationResult.Value,
            Modules = modules,
            Plugins = plugins,
        };

        var validation = _validator.Validate(configuration);
        return validation.IsFailure
            ? Result.Failure<TenantConfiguration>(validation.Error)
            : configuration;
    }

    private static Result<TenantLocalization?> MapLocalization(LocalizationDto? dto)
    {
        if (dto is null)
        {
            return Result.Success<TenantLocalization?>(null);
        }

        if (!TryParseEnum<UnitSystem>(dto.Units, UnitSystem.Metric, out var units))
        {
            return Result.Failure<TenantLocalization?>(
                Error.Validation("Configuration.Tenant.InvalidUnits", $"'{dto.Units}' is not a valid unit system."));
        }

        return Result.Success<TenantLocalization?>(
            new TenantLocalization(dto.Language ?? string.Empty, dto.TimeZone ?? string.Empty, units));
    }

    private Result<IReadOnlyDictionary<string, string>> ExpandSettings(Dictionary<string, string>? settings)
    {
        var expanded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in settings ?? [])
        {
            var value = _secretExpander.Expand(pair.Value);
            if (value.IsFailure)
            {
                return Result.Failure<IReadOnlyDictionary<string, string>>(value.Error);
            }

            expanded[pair.Key] = value.Value;
        }

        return expanded;
    }

    private static bool TryParseEnum<TEnum>(string? text, TEnum fallback, out TEnum value)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = fallback;
            return true;
        }

        return Enum.TryParse(text, ignoreCase: true, out value);
    }

    private sealed class TenantDto
    {
        public string? TenantId { get; set; }

        public string? Name { get; set; }

        public string? Environment { get; set; }

        public BrandingDto? Branding { get; set; }

        public LocalizationDto? Localization { get; set; }

        public List<ComponentDto>? Modules { get; set; }

        public List<ComponentDto>? Plugins { get; set; }
    }

    private sealed class BrandingDto
    {
        public string? DisplayName { get; set; }

        public string? PrimaryColor { get; set; }

        public string? LogoUrl { get; set; }
    }

    private sealed class LocalizationDto
    {
        public string? Language { get; set; }

        public string? TimeZone { get; set; }

        public string? Units { get; set; }
    }

    private sealed class ComponentDto
    {
        public string? Key { get; set; }

        public bool? Enabled { get; set; }

        public Dictionary<string, string>? Settings { get; set; }
    }
}
