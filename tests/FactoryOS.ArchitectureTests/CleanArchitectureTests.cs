using System.Reflection;
using NetArchTest.Rules;

namespace FactoryOS.ArchitectureTests;

/// <summary>
/// Enforces the Clean Architecture dependency rules of the FactoryOS Constitution. These tests
/// fail the build the moment a layer takes a forbidden dependency.
/// </summary>
public sealed class CleanArchitectureTests
{
    private static readonly Assembly Domain = FactoryOS.Domain.AssemblyReference.Assembly;
    private static readonly Assembly Contracts = FactoryOS.Contracts.AssemblyReference.Assembly;
    private static readonly Assembly Application = FactoryOS.Application.AssemblyReference.Assembly;
    private static readonly Assembly Infrastructure = FactoryOS.Infrastructure.AssemblyReference.Assembly;
    private static readonly Assembly Persistence = FactoryOS.Persistence.AssemblyReference.Assembly;
    private static readonly Assembly Identity = FactoryOS.Identity.AssemblyReference.Assembly;
    private static readonly Assembly EventBus = FactoryOS.EventBus.AssemblyReference.Assembly;
    private static readonly Assembly Plugin = FactoryOS.Plugin.AssemblyReference.Assembly;
    private static readonly Assembly Core = FactoryOS.Core.AssemblyReference.Assembly;
    private static readonly Assembly Configuration = FactoryOS.Configuration.AssemblyReference.Assembly;

    [Fact]
    public void Domain_should_not_depend_on_any_other_factory_layer()
    {
        var result = Types.InAssembly(Domain)
            .That().ResideInNamespaceStartingWith("FactoryOS.Domain")
            .ShouldNot().HaveDependencyOnAny(
                "FactoryOS.Application", "FactoryOS.Contracts", "FactoryOS.Infrastructure",
                "FactoryOS.Persistence", "FactoryOS.Identity", "FactoryOS.EventBus",
                "FactoryOS.Plugin", "FactoryOS.Core", "FactoryOS.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Domain", result));
    }

    [Fact]
    public void Application_should_not_depend_on_infrastructure_side_layers()
    {
        var result = Types.InAssembly(Application)
            .That().ResideInNamespaceStartingWith("FactoryOS.Application")
            .ShouldNot().HaveDependencyOnAny(
                "FactoryOS.Infrastructure", "FactoryOS.Persistence", "FactoryOS.Identity",
                "FactoryOS.EventBus", "FactoryOS.Plugin", "FactoryOS.Core", "FactoryOS.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Application", result));
    }

    [Fact]
    public void Contracts_should_not_depend_on_domain_or_higher_layers()
    {
        var result = Types.InAssembly(Contracts)
            .That().ResideInNamespaceStartingWith("FactoryOS.Contracts")
            .ShouldNot().HaveDependencyOnAny(
                "FactoryOS.Domain", "FactoryOS.Application", "FactoryOS.Infrastructure",
                "FactoryOS.Persistence", "FactoryOS.Identity", "FactoryOS.EventBus",
                "FactoryOS.Plugin", "FactoryOS.Core", "FactoryOS.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Contracts", result));
    }

    [Fact]
    public void No_layer_should_depend_on_the_api_host()
    {
        var layers = new[]
        {
            Domain, Contracts, Application, Infrastructure, Persistence,
            Identity, EventBus, Plugin, Core, Configuration,
        };

        var result = Types.InAssemblies(layers)
            .That().ResideInNamespaceStartingWith("FactoryOS")
            .ShouldNot().HaveDependencyOn("FactoryOS.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Solution", result));
    }

    private static string Describe(string scope, TestResult result)
    {
        if (result.IsSuccessful)
        {
            return string.Empty;
        }

        var offenders = result.FailingTypeNames is null
            ? "unknown"
            : string.Join(", ", result.FailingTypeNames);
        return $"{scope} violates its Clean Architecture dependency rule. Offending types: {offenders}.";
    }
}
