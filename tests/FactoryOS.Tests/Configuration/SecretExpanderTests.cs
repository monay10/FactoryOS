using FactoryOS.Configuration.Secrets;

namespace FactoryOS.Tests.Configuration;

public sealed class SecretExpanderTests
{
    private static SecretExpander Expander(params (string Name, string Value)[] secrets)
    {
        var map = secrets.ToDictionary(secret => secret.Name, secret => secret.Value, StringComparer.Ordinal);
        return new SecretExpander(new InMemorySecretProvider(map));
    }

    [Fact]
    public void Expands_a_single_placeholder()
    {
        var result = Expander(("DB_PASSWORD", "s3cr3t")).Expand("pwd=${secret:DB_PASSWORD};");

        Assert.True(result.IsSuccess);
        Assert.Equal("pwd=s3cr3t;", result.Value);
    }

    [Fact]
    public void Expands_multiple_placeholders()
    {
        var result = Expander(("A", "1"), ("B", "2")).Expand("${secret:A}-${secret:B}");

        Assert.True(result.IsSuccess);
        Assert.Equal("1-2", result.Value);
    }

    [Fact]
    public void Leaves_plain_values_unchanged()
    {
        var result = Expander().Expand("no placeholders here");

        Assert.True(result.IsSuccess);
        Assert.Equal("no placeholders here", result.Value);
    }

    [Fact]
    public void Reports_missing_secret()
    {
        var result = Expander().Expand("${secret:ABSENT}");

        Assert.True(result.IsFailure);
        Assert.Equal("Configuration.Secret.Missing", result.Error.Code);
        Assert.Contains("ABSENT", result.Error.Description, StringComparison.Ordinal);
    }
}
