using Microsoft.Extensions.Logging;

namespace FactoryOS.Gateway.Routing;

/// <summary>High-performance, source-generated log messages for the API gateway's module mounting.</summary>
internal static partial class GatewayLog
{
    [LoggerMessage(
        EventId = 6000,
        Level = LogLevel.Information,
        Message = "Mounted module '{ModuleKey}' endpoints under '{RoutePrefix}'.")]
    public static partial void ModuleMounted(ILogger logger, string moduleKey, string routePrefix);

    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Warning,
        Message = "Skipped mounting endpoints for module '{ModuleKey}': {Reason}.")]
    public static partial void ModuleApiSkipped(ILogger logger, string moduleKey, string reason);
}
