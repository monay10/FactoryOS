using FactoryOS.Contracts.Events;
using FactoryOS.Gateway.Endpoints;
using FactoryOS.Gateway.Tenancy;
using FactoryOS.Plugins.Brain.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FactoryOS.Plugins.Brain.Api;

/// <summary>
/// The Brain HTTP API — the ask/read face of the Company Brain, mounted over the gateway at <c>/m/brain/*</c> purely
/// from the manifest key. <c>POST /ask</c> publishes a <see cref="BrainQuestionAsked"/> on the bus and returns the
/// question id, so asking is decoupled from answering — the API never touches the RAG/LLM stack, it only speaks the
/// shared event vocabulary. <c>GET /answers</c> reads the tenant's newest-first grounded answer log, which is fed
/// exclusively by <see cref="BrainAnswered"/> events. The tenant is taken from the ambient
/// <see cref="ITenantContext"/> the gateway resolves at the edge, never re-parsed per route.
/// </summary>
internal sealed class BrainApi : IModuleApi
{
    private readonly IBrainAnswerLog _log;
    private readonly BrainReadModelOptions _options;

    public BrainApi(IBrainAnswerLog log, BrainReadModelOptions options)
    {
        _log = log;
        _options = options;
    }

    public string ModuleKey => BrainPlugin.PluginKey;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/answers", ([FromServices] ITenantContext context, int? max) =>
            {
                var limit = Math.Clamp(max ?? _options.LogCapacity, 1, _options.LogCapacity);
                return Results.Ok(new BrainAnswersResponse(context.Tenant, _log.Recent(context.Tenant, limit)));
            })
            .RequireTenant()
            .WithName("GetBrainAnswers");

        endpoints.MapGet("/summary", ([FromServices] ITenantContext context) =>
                Results.Ok(_log.Summarize(context.Tenant)))
            .RequireTenant()
            .WithName("GetBrainSummary");

        endpoints.MapPost("/ask", async (
            [FromServices] ITenantContext context,
            [FromServices] IEventBus bus,
            BrainAskRequest request,
            CancellationToken cancellationToken) =>
            {
                if (request is null || string.IsNullOrWhiteSpace(request.Question))
                {
                    return Results.BadRequest(new { error = "A non-empty 'question' is required." });
                }

                var asked = new BrainQuestionAsked
                {
                    Tenant = context.Tenant,
                    Question = request.Question.Trim(),
                    AskedBy = request.AskedBy?.Trim() ?? string.Empty,
                    AskedAt = DateTimeOffset.UtcNow,
                };

                await bus.PublishAsync(asked, cancellationToken: cancellationToken).ConfigureAwait(false);

                // 202: the question is accepted onto the bus; the grounded answer arrives asynchronously as a
                // BrainAnswered event and surfaces at GET /answers. The question id correlates the two.
                return Results.Accepted(
                    $"/m/brain/answers",
                    new BrainAskResponse(context.Tenant, asked.EventId, asked.Question));
            })
            .RequireTenant()
            .WithName("AskBrain");
    }
}

/// <summary>A tenant's most recent grounded Brain answers.</summary>
/// <param name="Tenant">The tenant the answers belong to.</param>
/// <param name="Answers">The recent grounded answers, newest first.</param>
internal sealed record BrainAnswersResponse(string Tenant, IReadOnlyList<BrainAnswerEntry> Answers);

/// <summary>The body of a <c>POST /m/brain/ask</c> request.</summary>
/// <param name="Question">The natural-language question to pose to the Company Brain.</param>
/// <param name="AskedBy">Optional: who or what asked (for traceability); may be null.</param>
internal sealed record BrainAskRequest(string? Question, string? AskedBy);

/// <summary>The acknowledgement that a question was accepted onto the bus.</summary>
/// <param name="Tenant">The tenant the question was asked for.</param>
/// <param name="QuestionId">The id of the published question event; it correlates to the answer's source id.</param>
/// <param name="Question">The normalized question that was published.</param>
internal sealed record BrainAskResponse(string Tenant, Guid QuestionId, string Question);
