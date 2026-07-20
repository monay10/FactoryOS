using System.Globalization;
using FactoryOS.Api.Hosting;
using FactoryOS.Api.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration and pipeline wiring for the FactoryOS <b>API host foundation</b>: the cross-cutting HTTP concerns
/// (problem details, health, versioning, OpenAPI, localization, CORS, compression, HTTP logging and the request
/// middleware) that every deployment needs, independent of any business module.
/// </summary>
public static class ApiHostFoundationExtensions
{
    private const string LiveTag = "live";
    private const string ReadyTag = "ready";

    /// <summary>Registers the API host foundation services and binds their configuration sections.</summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The same <see cref="WebApplicationBuilder"/> instance, to allow chaining.</returns>
    public static WebApplicationBuilder AddApiHostFoundation(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;
        var configuration = builder.Configuration;

        services.AddOptions<FactoryOS.Api.Hosting.CorsSettings>().Bind(configuration.GetSection(ApiHostSections.Cors));
        services.AddOptions<LocalizationSettings>().Bind(configuration.GetSection(ApiHostSections.Localization));
        services.AddOptions<ApiVersioningSettings>().Bind(configuration.GetSection(ApiHostSections.ApiVersioning));
        services.AddOptions<OpenApiSettings>().Bind(configuration.GetSection(ApiHostSections.OpenApi));

        services.AddSingleton<IApiVersionReader, ApiVersionReader>();

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            if (context.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemsKey, out var correlationId))
            {
                context.ProblemDetails.Extensions["correlationId"] = correlationId;
            }
        });

        services.AddHealthChecks()
            .AddCheck<HostSelfHealthCheck>("self", tags: [LiveTag, ReadyTag]);

        var cors = configuration.GetSection(ApiHostSections.Cors).Get<FactoryOS.Api.Hosting.CorsSettings>()
            ?? new FactoryOS.Api.Hosting.CorsSettings();
        services.AddCors(options => options.AddPolicy(cors.PolicyName, policy =>
        {
            policy.AllowAnyHeader().AllowAnyMethod();
            if (cors.AllowAnyOrigin)
            {
                policy.AllowAnyOrigin();
            }
            else if (cors.AllowedOrigins.Count > 0)
            {
                policy.WithOrigins([.. cors.AllowedOrigins]);
            }
        }));

        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes =
                [.. ResponseCompressionDefaults.MimeTypes, "application/json", "application/problem+json"];
        });

        services.AddHttpLogging(options => options.LoggingFields =
            Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestMethod
            | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPath
            | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponseStatusCode
            | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.Duration);

        return builder;
    }

    /// <summary>Wires the API host foundation request pipeline. Call before mapping any endpoints.</summary>
    /// <param name="app">The web application.</param>
    /// <returns>The same <see cref="WebApplication"/> instance, to allow chaining.</returns>
    public static WebApplication UseApiHostFoundation(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Outermost first: the exception boundary must wrap everything below it.
        app.UseMiddleware<GlobalExceptionMiddleware>();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseResponseCompression();
        app.UseMiddleware<RequestTimingMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseHttpLogging();

        var localization = app.Services.GetRequiredService<IOptions<LocalizationSettings>>().Value;
        app.UseRequestLocalization(BuildLocalizationOptions(localization));
        app.UseMiddleware<CultureMiddleware>();

        var cors = app.Services.GetRequiredService<IOptions<FactoryOS.Api.Hosting.CorsSettings>>().Value;
        app.UseCors(cors.PolicyName);

        return app;
    }

    /// <summary>Maps the foundation endpoints: the health probes, the OpenAPI document and the Swagger UI.</summary>
    /// <param name="app">The web application.</param>
    /// <returns>The same <see cref="WebApplication"/> instance, to allow chaining.</returns>
    public static WebApplication MapApiHostFoundation(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteHealthAsync });
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(LiveTag),
            ResponseWriter = WriteHealthAsync,
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
            ResponseWriter = WriteHealthAsync,
        });

        var openApi = app.Services.GetRequiredService<IOptions<OpenApiSettings>>().Value;
        if (openApi.Enabled)
        {
            app.MapGet("/openapi/v1.json", (
                IOptions<OpenApiSettings> settings,
                IApiVersionReader versionReader) =>
                Results.Json(
                    OpenApiDocumentFactory.Build(settings.Value, versionReader.DefaultVersion),
                    contentType: "application/json"));

            app.MapGet("/swagger", () => Results.Content(SwaggerUiHtml, "text/html; charset=utf-8"));
        }

        return app;
    }

    private static RequestLocalizationOptions BuildLocalizationOptions(LocalizationSettings settings)
    {
        var cultures = new List<CultureInfo>();
        foreach (var name in settings.ResolveSupportedCultures())
        {
            var culture = SafeCulture(name);
            if (!cultures.Exists(existing => existing.Name == culture.Name))
            {
                cultures.Add(culture);
            }
        }

        if (cultures.Count == 0)
        {
            cultures.Add(CultureInfo.InvariantCulture);
        }

        var options = new RequestLocalizationOptions
        {
            ApplyCurrentCultureToResponseHeaders = true,
            SupportedCultures = cultures,
            SupportedUICultures = cultures,
        };
        options.SetDefaultCulture(cultures[0].Name);
        return options;
    }

    private static CultureInfo SafeCulture(string name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            // Under InvariantGlobalization a specific culture is unavailable; the invariant culture is the fallback.
            return CultureInfo.InvariantCulture;
        }
    }

    private static Task WriteHealthAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = report.Status.ToString(),
            ["totalDurationMs"] = report.TotalDuration.TotalMilliseconds,
            ["checks"] = report.Entries.Select(entry => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = entry.Key,
                ["status"] = entry.Value.Status.ToString(),
                ["description"] = entry.Value.Description,
            }).ToArray(),
        };

        return context.Response.WriteAsJsonAsync(payload);
    }

    private const string SwaggerUiHtml = """
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>FactoryOS API — Swagger UI</title>
          <link rel="stylesheet" href="https://unpkg.com/swagger-ui-dist/swagger-ui.css" />
        </head>
        <body>
          <div id="swagger-ui"></div>
          <script src="https://unpkg.com/swagger-ui-dist/swagger-ui-bundle.js"></script>
          <script>
            window.onload = () => {
              window.ui = SwaggerUIBundle({ url: '/openapi/v1.json', dom_id: '#swagger-ui' });
            };
          </script>
        </body>
        </html>
        """;
}

/// <summary>
/// The host's own health check: it reports that the process is up and its composition root resolved. It carries no
/// external dependency (there is no database in the host foundation), so it is a genuine self/liveness signal.
/// </summary>
public sealed class HostSelfHealthCheck : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(HealthCheckResult.Healthy("The FactoryOS host is running."));
}
