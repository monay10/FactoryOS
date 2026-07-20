using FactoryOS.Ai.Prompts;
using FactoryOS.Contracts.Ai;

namespace FactoryOS.Tests.Ai;

public sealed class PromptEngineTests
{
    private static readonly Dictionary<string, string> NoVars = [];

    [Fact]
    public void Renderer_substitutes_placeholders()
    {
        var renderer = new PromptRenderer();

        var result = renderer.Render(
            "Hello {{ name }}, you have {{count}} alerts.",
            new Dictionary<string, string> { ["name"] = "Ada", ["count"] = "3" });

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal("Hello Ada, you have 3 alerts.", result.Value);
    }

    [Fact]
    public void Renderer_is_strict_about_missing_variables()
    {
        var renderer = new PromptRenderer();

        var result = renderer.Render("{{greeting}} {{name}}", new Dictionary<string, string> { ["name"] = "Ada" });

        Assert.True(result.IsFailure);
        Assert.Equal("Ai.Prompt.MissingVariable", result.Error.Code);
        Assert.Contains("greeting", result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Renderer_leaves_text_without_placeholders_untouched()
    {
        var renderer = new PromptRenderer();

        var result = renderer.Render("plain text, no vars", NoVars);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal("plain text, no vars", result.Value);
    }

    [Fact]
    public void Catalog_keeps_the_highest_version_for_a_key()
    {
        var catalog = new InMemoryPromptCatalog();
        catalog.Register(new PromptTemplate { Key = "greet", Version = 1, User = "v1" });
        catalog.Register(new PromptTemplate { Key = "greet", Version = 2, User = "v2" });
        catalog.Register(new PromptTemplate { Key = "greet", Version = 1, User = "stale" }); // lower — ignored

        Assert.True(catalog.TryGet("greet", out var template));
        Assert.Equal(2, template.Version);
        Assert.Equal("v2", template.User);
    }

    [Fact]
    public void Composer_builds_system_and_user_messages()
    {
        var catalog = new InMemoryPromptCatalog();
        catalog.Register(new PromptTemplate
        {
            Key = "maintenance.summarize",
            System = "You are a {{role}} assistant.",
            User = "Summarize work order {{number}}.",
        });
        var composer = new PromptComposer(catalog, new PromptRenderer());

        var result = composer.Compose(
            "maintenance.summarize",
            new Dictionary<string, string> { ["role"] = "maintenance", ["number"] = "WO-42" });

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal(ChatRole.System, result.Value[0].Role);
        Assert.Equal("You are a maintenance assistant.", result.Value[0].Content);
        Assert.Equal(ChatRole.User, result.Value[1].Role);
        Assert.Equal("Summarize work order WO-42.", result.Value[1].Content);
    }

    [Fact]
    public void Composer_omits_the_system_message_when_the_template_has_none()
    {
        var catalog = new InMemoryPromptCatalog();
        catalog.Register(new PromptTemplate { Key = "ask", User = "{{question}}" });
        var composer = new PromptComposer(catalog, new PromptRenderer());

        var result = composer.Compose("ask", new Dictionary<string, string> { ["question"] = "why?" });

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Single(result.Value);
        Assert.Equal(ChatRole.User, result.Value[0].Role);
    }

    [Fact]
    public void Composer_fails_for_an_unknown_template()
    {
        var composer = new PromptComposer(new InMemoryPromptCatalog(), new PromptRenderer());

        var result = composer.Compose("missing", NoVars);

        Assert.True(result.IsFailure);
        Assert.Equal("Ai.Prompt.UnknownTemplate", result.Error.Code);
    }

    [Fact]
    public void Composer_propagates_a_missing_variable_failure()
    {
        var catalog = new InMemoryPromptCatalog();
        catalog.Register(new PromptTemplate { Key = "ask", User = "{{question}}" });
        var composer = new PromptComposer(catalog, new PromptRenderer());

        var result = composer.Compose("ask", NoVars);

        Assert.True(result.IsFailure);
        Assert.Equal("Ai.Prompt.MissingVariable", result.Error.Code);
    }
}
