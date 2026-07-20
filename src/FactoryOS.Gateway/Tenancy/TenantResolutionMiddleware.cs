using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FactoryOS.Gateway.Tenancy;

/// <summary>
/// Resolves the tenant of each request once, at the edge, and publishes it into the request-scoped
/// <see cref="ITenantContext"/>. It reads the configured header first and falls back to the configured
/// query key, so every downstream module endpoint reads a single, already-validated tenant instead of
/// re-parsing it. When a tenant is resolved the rest of the pipeline runs inside a logging scope carrying
/// it, so every log line for the request is stamped with its tenant — "tenant is always in scope", literally.
/// The middleware never rejects a request: whether a tenant is required is the endpoint's decision, keeping
/// tenant-agnostic routes (such as the module inventory) unaffected.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    /// <summary>The logging-scope key under which the resolved tenant is published.</summary>
    public const string TenantScopeKey = "Tenant";

    private readonly RequestDelegate _next;
    private readonly TenantResolutionOptions _options;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    /// <summary>Initializes the middleware.</summary>
    /// <param name="next">The next delegate in the request pipeline.</param>
    /// <param name="options">The tenant-resolution configuration.</param>
    /// <param name="logger">The logger used to open the per-request tenant scope.</param>
    public TenantResolutionMiddleware(
        RequestDelegate next,
        TenantResolutionOptions options,
        ILogger<TenantResolutionMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _next = next;
        _options = options;
        _logger = logger;
    }

    /// <summary>Resolves the tenant into the scoped context, then invokes the rest of the pipeline.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="tenant">The request-scoped tenant context to populate.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    public async Task InvokeAsync(HttpContext context, ITenantContext tenant)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenant);

        var resolved = Resolve(context.Request);
        if (resolved is null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (tenant is TenantContext writable)
        {
            writable.Set(resolved);
        }

        using (_logger.BeginScope(new Dictionary<string, object> { [TenantScopeKey] = resolved }))
        {
            await _next(context).ConfigureAwait(false);
        }
    }

    private string? Resolve(HttpRequest request)
    {
        if (request.Headers.TryGetValue(_options.HeaderName, out var header))
        {
            var value = header.ToString().Trim();
            if (value.Length > 0)
            {
                return value;
            }
        }

        if (request.Query.TryGetValue(_options.QueryFallbackKey, out var query))
        {
            var value = query.ToString().Trim();
            if (value.Length > 0)
            {
                return value;
            }
        }

        return null;
    }
}
