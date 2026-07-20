using FactoryOS.Configuration.Model;
using FactoryOS.Configuration.Validation;

namespace FactoryOS.Tests.Configuration;

public sealed class TenantConfigurationValidatorTests
{
    private static readonly TenantConfigurationValidator Validator = new();

    private static TenantConfiguration Valid() => new()
    {
        TenantId = "tenant_001",
        Name = "Demo",
    };

    [Fact]
    public void Valid_configuration_passes()
    {
        Assert.True(Validator.Validate(Valid()).IsSuccess);
    }

    [Fact]
    public void Missing_tenant_id_fails()
    {
        var result = Validator.Validate(Valid() with { TenantId = "" });

        Assert.Equal("Configuration.Tenant.MissingId", result.Error.Code);
    }

    [Fact]
    public void Missing_name_fails()
    {
        var result = Validator.Validate(Valid() with { Name = "  " });

        Assert.Equal("Configuration.Tenant.MissingName", result.Error.Code);
    }

    [Fact]
    public void Duplicate_module_key_fails()
    {
        var result = Validator.Validate(Valid() with
        {
            Modules =
            [
                new ModuleConfiguration { Key = "energy" },
                new ModuleConfiguration { Key = "Energy" },
            ],
        });

        Assert.Equal("Configuration.Tenant.DuplicateModule", result.Error.Code);
    }

    [Fact]
    public void Duplicate_plugin_key_fails()
    {
        var result = Validator.Validate(Valid() with
        {
            Plugins =
            [
                new PluginConfiguration { Key = "logo" },
                new PluginConfiguration { Key = "logo" },
            ],
        });

        Assert.Equal("Configuration.Tenant.DuplicatePlugin", result.Error.Code);
    }

    [Fact]
    public void Incomplete_localization_fails()
    {
        var result = Validator.Validate(Valid() with
        {
            Localization = new TenantLocalization("tr", "", UnitSystem.Metric),
        });

        Assert.Equal("Configuration.Tenant.InvalidLocalization", result.Error.Code);
    }
}
