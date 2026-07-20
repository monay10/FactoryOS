using FactoryOS.Configuration.Secrets;

namespace FactoryOS.Tests.Configuration;

public sealed class EnvironmentVariableSecretProviderTests
{
    [Fact]
    public void Resolves_prefixed_environment_variables()
    {
        var name = "TEST_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable("FACTORYOS_SECRET_" + name, "value-42");
        try
        {
            var provider = new EnvironmentVariableSecretProvider();

            Assert.True(provider.TryGet(name, out var value));
            Assert.Equal("value-42", value);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FACTORYOS_SECRET_" + name, null);
        }
    }

    [Fact]
    public void Reports_absent_secrets()
    {
        var provider = new EnvironmentVariableSecretProvider();

        Assert.False(provider.TryGet("DEFINITELY_ABSENT_" + Guid.NewGuid().ToString("N"), out var value));
        Assert.Null(value);
    }
}
