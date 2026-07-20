using FactoryOS.Application.Configuration;
using FactoryOS.Application.Messaging;
using FactoryOS.Application.Validation;
using FactoryOS.Shared.Identifiers;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.Tests.Application;

public sealed class ApplicationRegistrationTests
{
    [Fact]
    public void AddApplication_registers_the_five_pipeline_behaviors()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        var behaviors = services.Count(descriptor => descriptor.ServiceType == typeof(IPipelineBehavior<,>));
        Assert.Equal(5, behaviors);
    }

    [Fact]
    public void AddApplication_registers_options_and_the_request_context()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ApplicationOptions));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRequestContext));
    }

    [Fact]
    public void The_request_context_resolves_to_the_scoped_application_context()
    {
        using var provider = new ServiceCollection().AddApplication().BuildServiceProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<IRequestContext>();

        Assert.IsType<ApplicationContext>(context);
    }
}

public sealed class ValidationResultTests
{
    [Fact]
    public void A_success_result_is_valid_with_no_failures()
    {
        var result = ValidationResult.Success();

        Assert.True(result.IsValid);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void A_failure_result_carries_the_failures()
    {
        var failures = new[] { new ValidationFailure("Amount", "must be positive") };

        var result = ValidationResult.Failure(failures);

        Assert.False(result.IsValid);
        Assert.Single(result.Failures);
    }
}

public sealed class ApplicationContextTests
{
    [Fact]
    public void Initialize_populates_the_request_context()
    {
        var context = new ApplicationContext();
        var correlation = CorrelationId.New();
        var at = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

        context.Initialize(correlation, at, tenant: "acme", userName: "tester");

        Assert.Equal(correlation, context.CorrelationId);
        Assert.Equal("acme", context.Tenant);
        Assert.Equal("tester", context.UserName);
        Assert.Equal(at, context.ReceivedAt);
    }
}
