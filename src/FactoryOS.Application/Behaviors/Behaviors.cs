using System.Diagnostics;
using FactoryOS.Application.Configuration;
using FactoryOS.Application.Messaging;
using FactoryOS.Application.Services;
using FactoryOS.Application.Validation;
using FactoryOS.Shared.Exceptions;
using Microsoft.Extensions.Logging;

namespace FactoryOS.Application.Behaviors;

/// <summary>Logs the start, completion and failure of each request flowing through the pipeline.</summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    /// <summary>Initializes a new instance of the <see cref="LoggingBehavior{TRequest, TResponse}"/> class.</summary>
    /// <param name="logger">The logger.</param>
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(next);
        var name = typeof(TRequest).Name;
        _logger.LogInformation("Handling {RequestName}", name);
        try
        {
            var response = await next().ConfigureAwait(false);
            _logger.LogInformation("Handled {RequestName}", name);
            return response;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Request {RequestName} failed", name);
            throw;
        }
    }
}

/// <summary>Runs all registered validators for a request and throws when any fail.</summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ApplicationOptions _options;

    /// <summary>Initializes a new instance of the <see cref="ValidationBehavior{TRequest, TResponse}"/> class.</summary>
    /// <param name="validators">The validators registered for the request.</param>
    /// <param name="options">The application options.</param>
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators, ApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(validators);
        ArgumentNullException.ThrowIfNull(options);
        _validators = validators;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (!_options.EnableValidation)
        {
            return await next().ConfigureAwait(false);
        }

        var failures = new List<ValidationFailure>();
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!result.IsValid)
            {
                failures.AddRange(result.Failures);
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures.Select(failure => $"{failure.PropertyName}: {failure.Message}"));
        }

        return await next().ConfigureAwait(false);
    }
}

/// <summary>Measures request duration and logs a warning when it exceeds the configured slow threshold.</summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private readonly ApplicationOptions _options;

    /// <summary>Initializes a new instance of the <see cref="PerformanceBehavior{TRequest, TResponse}"/> class.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The application options.</param>
    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger, ApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(next);
        var start = Stopwatch.GetTimestamp();
        var response = await next().ConfigureAwait(false);
        var elapsed = Stopwatch.GetElapsedTime(start);
        if (elapsed > _options.SlowRequestThreshold)
        {
            _logger.LogWarning(
                "Slow request {RequestName} took {ElapsedMs}ms",
                typeof(TRequest).Name,
                (long)elapsed.TotalMilliseconds);
        }

        return response;
    }
}

/// <summary>Wraps commands in a transaction, committing on success and rolling back on failure.</summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly Transactions.ITransactionManager _transactions;

    /// <summary>Initializes a new instance of the <see cref="TransactionBehavior{TRequest, TResponse}"/> class.</summary>
    /// <param name="transactions">The transaction manager.</param>
    public TransactionBehavior(Transactions.ITransactionManager transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        _transactions = transactions;
    }

    /// <inheritdoc />
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(next);

        // Queries do not mutate state, so they run outside a transaction.
        if (request is not (ICommand or ICommand<TResponse>))
        {
            return await next().ConfigureAwait(false);
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var response = await next().ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return response;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}

/// <summary>Enforces a request's declared permission against the current user before the handler runs.</summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUser _currentUser;

    /// <summary>Initializes a new instance of the <see cref="AuthorizationBehavior{TRequest, TResponse}"/> class.</summary>
    /// <param name="currentUser">The current user.</param>
    public AuthorizationBehavior(ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (request is IAuthorizedRequest authorized && !_currentUser.HasPermission(authorized.RequiredPermission))
        {
            throw new ForbiddenException($"The '{authorized.RequiredPermission}' permission is required.");
        }

        return await next().ConfigureAwait(false);
    }
}
