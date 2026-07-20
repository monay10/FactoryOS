using FactoryOS.Configuration.Model;
using FactoryOS.Configuration.Reading;
using FactoryOS.Configuration.Secrets;
using FactoryOS.Configuration.Validation;

namespace FactoryOS.Tests.Configuration;

public sealed class TenantConfigurationReaderTests
{
    private static TenantConfigurationReader Reader(params (string Name, string Value)[] secrets)
    {
        var map = secrets.ToDictionary(secret => secret.Name, secret => secret.Value, StringComparer.Ordinal);
        return new TenantConfigurationReader(
            new SecretExpander(new InMemorySecretProvider(map)),
            new TenantConfigurationValidator());
    }

    [Fact]
    public void Reads_a_complete_tenant()
    {
        const string json = """
        {
          "tenantId": "tenant_001",
          "name": "Demo Factory",
          "environment": "Staging",
          "branding": { "displayName": "Demo" },
          "localization": { "language": "tr", "timeZone": "Europe/Istanbul", "units": "Metric" },
          "modules": [ { "key": "energy", "enabled": true, "settings": { "interval": "60" } } ],
          "plugins": [ { "key": "logo", "settings": { "cs": "pwd=${secret:LOGO_PW};" } } ]
        }
        """;

        var result = Reader(("LOGO_PW", "hunter2")).Read(json);

        Assert.True(result.IsSuccess);
        var config = result.Value;
        Assert.Equal("tenant_001", config.TenantId);
        Assert.Equal(DeploymentEnvironment.Staging, config.Environment);
        Assert.Equal(UnitSystem.Metric, config.Localization!.Units);
        Assert.True(config.IsModuleEnabled("energy"));
        Assert.Equal("pwd=hunter2;", config.GetPlugin("logo")!.Settings["cs"]);
    }

    [Fact]
    public void Environment_defaults_to_production_when_absent()
    {
        var result = Reader().Read("""{ "tenantId": "t", "name": "n" }""");

        Assert.True(result.IsSuccess);
        Assert.Equal(DeploymentEnvironment.Production, result.Value.Environment);
    }

    [Fact]
    public void Missing_secret_fails_the_read()
    {
        const string json = """
        { "tenantId": "t", "name": "n",
          "plugins": [ { "key": "logo", "settings": { "cs": "${secret:ABSENT}" } } ] }
        """;

        var result = Reader().Read(json);

        Assert.True(result.IsFailure);
        Assert.Equal("Configuration.Secret.Missing", result.Error.Code);
    }

    [Fact]
    public void Malformed_json_fails()
    {
        var result = Reader().Read("{ not json");

        Assert.True(result.IsFailure);
        Assert.Equal("Configuration.Tenant.Malformed", result.Error.Code);
    }

    [Fact]
    public void Invalid_environment_fails()
    {
        var result = Reader().Read("""{ "tenantId": "t", "name": "n", "environment": "Mars" }""");

        Assert.True(result.IsFailure);
        Assert.Equal("Configuration.Tenant.InvalidEnvironment", result.Error.Code);
    }

    [Fact]
    public void Validation_failure_is_surfaced()
    {
        var result = Reader().Read("""{ "name": "n" }""");

        Assert.True(result.IsFailure);
        Assert.Equal("Configuration.Tenant.MissingId", result.Error.Code);
    }
}
