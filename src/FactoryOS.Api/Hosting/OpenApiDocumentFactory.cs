using System.Collections.Generic;
using FactoryOS.Shared.Guards;

namespace FactoryOS.Api.Hosting;

/// <summary>
/// Builds the host's OpenAPI 3.0 document from real host metadata and the endpoints the foundation actually serves.
/// Business endpoints are contributed by plugin modules and are out of scope here, so the document describes the
/// host surface only (health and the API-documentation endpoints).
/// </summary>
public static class OpenApiDocumentFactory
{
    /// <summary>Builds the OpenAPI document as a serializable object graph.</summary>
    /// <param name="settings">The OpenAPI settings carrying the title and description.</param>
    /// <param name="version">The document version (the host's default API version).</param>
    /// <returns>A nested dictionary representing the OpenAPI 3.0.1 document.</returns>
    public static IReadOnlyDictionary<string, object?> Build(OpenApiSettings settings, ApiVersion version)
    {
        Guard.AgainstNull(settings);

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["openapi"] = "3.0.1",
            ["info"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = settings.Title,
                ["description"] = settings.Description,
                ["version"] = version.ToString(),
            },
            ["paths"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["/health"] = HealthPath("Aggregate health", "Reports the aggregate health of the host and its checks."),
                ["/health/live"] = HealthPath("Liveness probe", "Reports whether the host process is alive."),
                ["/health/ready"] = HealthPath("Readiness probe", "Reports whether the host is ready to serve traffic."),
            },
        };
    }

    private static Dictionary<string, object?> HealthPath(string summary, string description) =>
        new(StringComparer.Ordinal)
        {
            ["get"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["tags"] = new[] { "Health" },
                ["summary"] = summary,
                ["description"] = description,
                ["responses"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["200"] = ResponseWithStatus("The host is healthy."),
                    ["503"] = ResponseWithStatus("The host is unhealthy."),
                },
            },
        };

    private static Dictionary<string, object?> ResponseWithStatus(string description) =>
        new(StringComparer.Ordinal)
        {
            ["description"] = description,
            ["content"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["application/json"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["schema"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["status"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["type"] = "string" },
                        },
                    },
                },
            },
        };
}
