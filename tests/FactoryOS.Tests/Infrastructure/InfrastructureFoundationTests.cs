using FactoryOS.Application.Caching;
using FactoryOS.Application.Files;
using FactoryOS.Application.Localization;
using FactoryOS.Application.Services;
using FactoryOS.Application.Transactions;
using FactoryOS.Infrastructure.Configuration;
using FactoryOS.Infrastructure.Identifiers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.Tests.Infrastructure;

public sealed class InfrastructureFoundationRegistrationTests
{
    private static ServiceCollection BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructureFoundation(new ConfigurationBuilder().Build());
        return services;
    }

    [Theory]
    [InlineData(typeof(IApplicationClock))]
    [InlineData(typeof(IGuidGenerator))]
    [InlineData(typeof(ICorrelationIdAccessor))]
    [InlineData(typeof(ICurrentUser))]
    [InlineData(typeof(ICurrentTenant))]
    [InlineData(typeof(ICurrentFactory))]
    [InlineData(typeof(ICurrentPlant))]
    [InlineData(typeof(ICurrentWorkCenter))]
    [InlineData(typeof(ICacheKeyGenerator))]
    [InlineData(typeof(ICacheProvider))]
    [InlineData(typeof(ICacheService))]
    [InlineData(typeof(IFileStorage))]
    [InlineData(typeof(IFileProvider))]
    [InlineData(typeof(ILocalizationService))]
    [InlineData(typeof(ITransactionManager))]
    public void AddInfrastructureFoundation_registers(Type serviceType)
    {
        var services = BuildServices();

        Assert.Contains(services, descriptor => descriptor.ServiceType == serviceType);
    }

    [Fact]
    public void The_self_contained_services_resolve()
    {
        using var provider = BuildServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.NotNull(sp.GetRequiredService<IApplicationClock>());
        Assert.NotNull(sp.GetRequiredService<IGuidGenerator>());
        Assert.NotNull(sp.GetRequiredService<ICacheService>());
        Assert.NotNull(sp.GetRequiredService<IFileStorage>());
        Assert.NotNull(sp.GetRequiredService<ILocalizationService>());
        Assert.NotNull(sp.GetRequiredService<ICurrentUser>());
        Assert.NotNull(sp.GetRequiredService<ICurrentTenant>());
    }

    [Fact]
    public void The_file_provider_resolves_to_the_same_type_as_the_file_storage()
    {
        using var provider = BuildServices().BuildServiceProvider();

        var storage = provider.GetRequiredService<IFileStorage>();
        var fileProvider = provider.GetRequiredService<IFileProvider>();

        Assert.Same(storage, fileProvider);
    }

    [Fact]
    public void Options_bind_from_the_infrastructure_section()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{InfrastructureConstants.ConfigurationSection}:DefaultCulture"] = "tr",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructureFoundation(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<InfrastructureOptions>>();

        Assert.Equal("tr", options.Value.DefaultCulture);
    }
}
