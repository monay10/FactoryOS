using System.Collections.Generic;
using FactoryOS.Plugins.Carbon;
using FactoryOS.Plugins.Carbon.Domain;

namespace FactoryOS.Tests.Carbon;

public sealed class CarbonCalculationTests
{
    private static CarbonOptions Options() => new()
    {
        EmissionFactors = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["ActivePower"] = 0.4m,
            ["NaturalGas"] = 2.02m,
        },
        DefaultEmissionFactor = 0m,
    };

    [Fact]
    public void Resolves_an_explicit_factor_for_a_known_metric()
    {
        Assert.Equal(0.4m, EmissionFactorResolver.Resolve("ActivePower", Options()));
    }

    [Fact]
    public void Metric_lookup_is_case_insensitive()
    {
        Assert.Equal(0.4m, EmissionFactorResolver.Resolve("activepower", Options()));
    }

    [Fact]
    public void Falls_back_to_the_default_for_an_unknown_metric()
    {
        var options = new CarbonOptions { DefaultEmissionFactor = 0.5m };
        Assert.Equal(0.5m, EmissionFactorResolver.Resolve("Unknown", options));
    }

    [Fact]
    public void Emission_is_energy_times_factor()
    {
        Assert.Equal(50m, CarbonCalculator.Co2eKg(energyValue: 125m, emissionFactor: 0.4m));
    }
}
