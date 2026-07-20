using Microsoft.Extensions.Logging;

namespace FactoryOS.EventBus.InProcess;

/// <summary>
/// High-performance, source-generated log messages for the in-process event bus. Using
/// <see cref="LoggerMessageAttribute"/> avoids boxing and needless allocation on the hot path.
/// </summary>
internal static partial class EventBusLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Debug,
        Message = "No handlers are registered for event {EventType}.")]
    public static partial void NoHandlers(ILogger logger, string eventType);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Handler {Handler} failed for {EventType} on attempt {Attempt} of {MaxAttempts}; retrying.")]
    public static partial void HandlerFailedRetrying(
        ILogger logger,
        Exception exception,
        string handler,
        string eventType,
        int attempt,
        int maxAttempts);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error,
        Message = "Handler {Handler} exhausted {MaxAttempts} attempts for {EventType}; dead-lettering.")]
    public static partial void HandlerExhausted(
        ILogger logger,
        Exception exception,
        string handler,
        int maxAttempts,
        string eventType);
}
