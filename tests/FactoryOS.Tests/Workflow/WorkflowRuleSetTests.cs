using FactoryOS.Plugins.Workflow;
using FactoryOS.Plugins.Workflow.Domain;

namespace FactoryOS.Tests.Workflow;

public sealed class WorkflowRuleSetTests
{
    [Fact]
    public void Resolves_a_configured_rule_by_trigger()
    {
        var ruleSet = new WorkflowRuleSet(new WorkflowOptions
        {
            Rules = [new WorkflowRule { Trigger = "QualityAlertRaised", Action = "Notify", Priority = "High", Channel = "quality" }],
        });

        var rule = ruleSet.Resolve("QualityAlertRaised");

        Assert.NotNull(rule);
        Assert.Equal("Notify", rule.Action);
        Assert.Equal("High", rule.Priority);
        Assert.Equal("quality", rule.Channel);
    }

    [Fact]
    public void Returns_null_for_an_unconfigured_trigger()
    {
        var ruleSet = new WorkflowRuleSet(new WorkflowOptions());

        Assert.Null(ruleSet.Resolve("LowStockDetected"));
    }

    [Fact]
    public void The_first_rule_for_a_trigger_wins()
    {
        var ruleSet = new WorkflowRuleSet(new WorkflowOptions
        {
            Rules =
            [
                new WorkflowRule { Trigger = "LowStockDetected", Action = "Escalate" },
                new WorkflowRule { Trigger = "LowStockDetected", Action = "Ignore" },
            ],
        });

        Assert.Equal("Escalate", ruleSet.Resolve("LowStockDetected")!.Action);
    }
}
