using System.Diagnostics;
using System.Globalization;
using FactoryOS.Shared.Constants;
using FactoryOS.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FactoryOS.Api.Middleware;

/// <summary>
/// Ensures every request carries a correlation identifier: it is read from the inbound
/// <c>X-Correlation-Id</c> header or generated, stashed on the <see cref="HttpContext"/>, echoed on the response and
/// pushed into the log scope so every log line for the request is correlated.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    /// <summary>The <see cref="HttpContext.Items"/> key under which the correlation identifier is stored.</summary>
    public const string ItemsKey = "FactoryOS.CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    /// <summary>Initializes a new instance of the <see cref="CorrelationIdMiddleware"/> class.</summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger used to open the correlation scope.</param>
    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Invokes the middleware.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var inbound = context.Request.Headers[HeaderNames.CorrelationId].ToString();
        var correlationId = string.IsNullOrWhiteSpace(inbound) ? Guid.NewGuid().ToString("N") : inbound;

        context.TraceIdentifier = correlationId;
        context.Items[ItemsKey] = correlationId;
        context.Response.OnStarting(static state =>
        {
            var ctx = (HttpContext)state;
            ctx.Response.Headers[HeaderNames.CorrelationId] = (string)ctx.Items[ItemsKey]!;
            return Task.CompletedTask;
        }, context);

        using (_logger.BeginScope(new Dictionary<string, object> { [HeaderNames.CorrelationId] = correlationId }))
        {
            await _next(context);
        }
    }
}

/// <summary>
/// Catches unhandled exceptions and translates them into RFC 7807 <c>application/problem+json</c> responses. The
/// FactoryOS domain-exception family maps to precise status codes; anything else becomes a 500 without leaking
/// internal detail. Validation failures carry their individual messages under an <c>errors</c> extension.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    /// <summary>Initializes a new instance of the <see cref="GlobalExceptionMiddleware"/> class.</summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger used to record failures.</param>
    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Invokes the middleware.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="problemDetailsService">The service that writes the problem-details response.</param>
    /// <returns>A task that completes when the pipeline has run or the error has been written.</returns>
    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetailsService)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await _next(context);
        }
        catch (DomainException exception)
        {
            _logger.LogWarning(exception, "Handled domain exception: {Code}", exception.Code);
            await WriteProblemAsync(context, problemDetailsService, exception);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception processing {Method} {Path}.",
                context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, problemDetailsService, exception);
        }
    }

    private async Task WriteProblemAsync(
        HttpContext context,
        IProblemDetailsService problemDetailsService,
        Exception exception)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogWarning("The response has already started; the error response could not be written.");
            return;
        }

        var (status, title) = Map(exception);
        context.Response.Clear();
        context.Response.StatusCode = status;

        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = exception is DomainException ? exception.Message : "An unexpected error occurred.",
            Type = $"https://httpstatuses.io/{status}",
        };

        if (context.Items.TryGetValue(CorrelationIdMiddleware.ItemsKey, out var correlationId))
        {
            problem.Extensions["correlationId"] = correlationId;
        }

        if (exception is DomainException domain)
        {
            problem.Extensions["code"] = domain.Code;
        }

        if (exception is ValidationException validation)
        {
            problem.Extensions["errors"] = validation.Errors;
        }

        var written = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
        });

        if (!written)
        {
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem);
        }
    }

    private static (int Status, string Title) Map(Exception exception) => exception switch
    {
        ValidationException => (StatusCodes.Status400BadRequest, "One or more validation errors occurred."),
        NotFoundException => (StatusCodes.Status404NotFound, "The requested resource was not found."),
        ConflictException => (StatusCodes.Status409Conflict, "The request conflicts with the current state."),
        UnauthorizedException => (StatusCodes.Status401Unauthorized, "Authentication is required."),
        ForbiddenException => (StatusCodes.Status403Forbidden, "You do not have permission to perform this action."),
        BusinessException => (StatusCodes.Status422UnprocessableEntity, "A business rule was violated."),
        DomainException => (StatusCodes.Status400BadRequest, "The request could not be processed."),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
    };
}

/// <summary>Measures how long a request takes and stamps the elapsed milliseconds onto the response.</summary>
public sealed class RequestTimingMiddleware
{
    /// <summary>The response header carrying the server-side processing time, in milliseconds.</summary>
    public const string HeaderName = "X-Response-Time-ms";

    private readonly RequestDelegate _next;

    /// <summary>Initializes a new instance of the <see cref="RequestTimingMiddleware"/> class.</summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public RequestTimingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Invokes the middleware.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var start = Stopwatch.GetTimestamp();
        context.Response.OnStarting(static state =>
        {
            var (ctx, from) = ((HttpContext, long))state;
            var elapsed = Stopwatch.GetElapsedTime(from);
            ctx.Response.Headers[HeaderName] =
                elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture);
            return Task.CompletedTask;
        }, (context, start));

        await _next(context);
    }
}

/// <summary>Logs the completion of each request with its method, path, status code and duration.</summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    /// <summary>Initializes a new instance of the <see cref="RequestLoggingMiddleware"/> class.</summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger the request summary is written to.</param>
    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Invokes the middleware.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var start = Stopwatch.GetTimestamp();
        try
        {
            await _next(context);
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(start);
            _logger.LogInformation(
                "{Method} {Path} responded {StatusCode} in {ElapsedMs:F1} ms.",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                elapsed.TotalMilliseconds);
        }
    }
}

/// <summary>Echoes the culture resolved for the request onto the response, for observability by clients and proxies.</summary>
public sealed class CultureMiddleware
{
    /// <summary>The response header carrying the culture the request was processed under.</summary>
    public const string HeaderName = "X-FactoryOS-Culture";

    private readonly RequestDelegate _next;

    /// <summary>Initializes a new instance of the <see cref="CultureMiddleware"/> class.</summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public CultureMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Invokes the middleware.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var culture = CultureInfo.CurrentUICulture.Name;
        context.Response.OnStarting(static state =>
        {
            var (ctx, name) = ((HttpContext, string))state;
            if (!string.IsNullOrEmpty(name))
            {
                ctx.Response.Headers[HeaderName] = name;
            }

            return Task.CompletedTask;
        }, (context, culture));

        return _next(context);
    }
}
