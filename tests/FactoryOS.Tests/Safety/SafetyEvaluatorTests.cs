using FactoryOS.Plugins.Safety;
using FactoryOS.Plugins.Safety.Domain;

namespace FactoryOS.Tests.Safety;

public sealed class SafetyEvaluatorTests
{
    private static readonly SafetyOptions Options = new()
    {
        StandDownSeverity = 4,
        FrequencyThreshold = 3,
        WindowSize = 10,
    };

    [Fact]
    public void A_severe_incident_triggers_on_high_severity()
    {
        var decision = SafetyEvaluator.Evaluate(severity: 5, windowIncidentCount: 1, Options);

        Assert.True(decision.StandDown);
        Assert.Equal("HighSeverity", decision.Reason);
    }

    [Fact]
    public void Frequency_triggers_when_the_count_reaches_the_threshold()
    {
        var decision = SafetyEvaluator.Evaluate(severity: 2, windowIncidentCount: 3, Options);

        Assert.True(decision.StandDown);
        Assert.Equal("Frequency", decision.Reason);
    }

    [Fact]
    public void Severity_takes_precedence_over_frequency()
    {
        var decision = SafetyEvaluator.Evaluate(severity: 4, windowIncidentCount: 5, Options);

        Assert.Equal("HighSeverity", decision.Reason);
    }

    [Fact]
    public void A_minor_isolated_incident_does_not_trigger()
    {
        var decision = SafetyEvaluator.Evaluate(severity: 1, windowIncidentCount: 1, Options);

        Assert.False(decision.StandDown);
        Assert.Equal(SafetyDecision.None, decision);
    }
}
