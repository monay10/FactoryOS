using FactoryOS.Ai.Gateway;
using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;

namespace FactoryOS.Tests.Insight;

/// <summary>A test double for <see cref="ILlmGateway"/> that returns a canned completion or a canned failure.</summary>
public sealed class FakeLlmGateway : ILlmGateway
{
    private readonly Result<ChatCompletionResponse> _result;

    public FakeLlmGateway(string content, string model = "upstream-model")
        => _result = Result.Success(new ChatCompletionResponse { Model = model, Content = content, FinishReason = "stop" });

    private FakeLlmGateway(Error error) => _result = Result.Failure<ChatCompletionResponse>(error);

    public static FakeLlmGateway Failing() => new(Error.Failure("llm.down", "backend unavailable"));

    public List<ChatCompletionRequest> Requests { get; } = [];

    public Task<Result<ChatCompletionResponse>> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_result);
    }
}
