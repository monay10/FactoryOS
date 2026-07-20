using FactoryOS.Agents.Insight;
using FactoryOS.Contracts.Ai;

namespace FactoryOS.Tests.Insight;

public sealed class InsightPromptBuilderTests
{
    [Fact]
    public void Builds_a_tenant_scoped_request_with_system_and_user_messages()
    {
        var options = new InsightAgentOptions { Model = "reasoning", SystemPrompt = "advise", Temperature = 0.2m, MaxTokens = 128 };
        var signal = new InsightSignal("acme", "QualityAlertRaised", "Defect rate high on line-1", DateTimeOffset.UnixEpoch, Guid.NewGuid());

        var request = InsightPromptBuilder.Build(signal, options);

        Assert.Equal("acme", request.Tenant);
        Assert.Equal("reasoning", request.Model);
        Assert.Equal(0.2m, request.Temperature);
        Assert.Equal(128, request.MaxTokens);
        Assert.Equal(2, request.Messages.Count);
        Assert.Equal(ChatRole.System, request.Messages[0].Role);
        Assert.Equal("advise", request.Messages[0].Content);
        Assert.Equal(ChatRole.User, request.Messages[1].Role);
        Assert.Contains("QualityAlertRaised", request.Messages[1].Content, StringComparison.Ordinal);
        Assert.Contains("Defect rate high on line-1", request.Messages[1].Content, StringComparison.Ordinal);
    }
}
