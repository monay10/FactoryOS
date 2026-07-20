using Microsoft.Extensions.Logging;

namespace FactoryOS.Plugin.Hosting;

/// <summary>High-performance, source-generated log messages for the plugin host.</summary>
internal static partial class PluginLog
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Plugin '{PluginKey}' configured (order {Order}).")]
    public static partial void Configured(ILogger logger, string pluginKey, int order);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Plugin '{PluginKey}' started.")]
    public static partial void Started(ILogger logger, string pluginKey);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Plugin '{PluginKey}' stopped.")]
    public static partial void Stopped(ILogger logger, string pluginKey);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "Plugin '{PluginKey}' skipped: {Reason}")]
    public static partial void Skipped(ILogger logger, string pluginKey, string reason);
}
