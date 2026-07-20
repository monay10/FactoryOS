using FactoryOS.Shared.Identifiers;

namespace FactoryOS.Application.Messaging;

/// <summary>Marks a command — a request that changes state and returns no value.</summary>
public interface ICommand
{
}

/// <summary>Marks a command that changes state and returns a result.</summary>
/// <typeparam name="TResult">The result type.</typeparam>
public interface ICommand<out TResult>
{
}

/// <summary>Marks a query — a read request that returns a single result and changes no state.</summary>
/// <typeparam name="TResult">The result type.</typeparam>
public interface IQuery<out TResult>
{
}

/// <summary>Marks a streaming query — a read request that returns an asynchronous sequence of results.</summary>
/// <typeparam name="TResult">The element type of the stream.</typeparam>
public interface IStreamQuery<out TResult>
{
}

/// <summary>Handles a <see cref="ICommand"/> that returns no value.</summary>
/// <typeparam name="TCommand">The command type.</typeparam>
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the command has been handled.</returns>
    Task HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>Handles a <see cref="ICommand{TResult}"/> that returns a result.</summary>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    /// <summary>Handles the command and returns its result.</summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The command result.</returns>
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>Handles an <see cref="IQuery{TResult}"/>.</summary>
/// <typeparam name="TQuery">The query type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    /// <summary>Handles the query and returns its result.</summary>
    /// <param name="query">The query to handle.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The query result.</returns>
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}

/// <summary>Handles an <see cref="IStreamQuery{TResult}"/>, producing an asynchronous sequence.</summary>
/// <typeparam name="TQuery">The stream query type.</typeparam>
/// <typeparam name="TResult">The element type of the stream.</typeparam>
public interface IStreamQueryHandler<in TQuery, out TResult>
    where TQuery : IStreamQuery<TResult>
{
    /// <summary>Handles the stream query.</summary>
    /// <param name="query">The stream query to handle.</param>
    /// <param name="cancellationToken">A token to cancel the enumeration.</param>
    /// <returns>An asynchronous sequence of results.</returns>
    IAsyncEnumerable<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}

/// <summary>Represents the continuation of a request pipeline — invoking it runs the next behavior or the handler.</summary>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <returns>The response produced by the rest of the pipeline.</returns>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>
/// A behavior in the request pipeline, wrapping the handler to add cross-cutting concerns (logging, validation,
/// performance, transactions, authorization). Behaviors compose around the handler in registration order.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>Runs this behavior, invoking <paramref name="next"/> to continue the pipeline.</summary>
    /// <param name="request">The request flowing through the pipeline.</param>
    /// <param name="next">The continuation that runs the next behavior or the handler.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The response.</returns>
    Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default);
}

/// <summary>Ambient metadata for the request currently being handled.</summary>
public interface IRequestContext
{
    /// <summary>Gets the correlation identifier threading this request across boundaries.</summary>
    CorrelationId CorrelationId { get; }

    /// <summary>Gets the tenant the request runs within, if resolved.</summary>
    string? Tenant { get; }

    /// <summary>Gets the name of the user the request runs as, if authenticated.</summary>
    string? UserName { get; }

    /// <summary>Gets the instant the request entered the application.</summary>
    DateTimeOffset ReceivedAt { get; }
}
