using FactoryOS.Plugins.Workflow.Security.Events;

namespace FactoryOS.Plugins.Workflow.Security.Execution;

/// <summary>
/// Publishes security events to every registered sink. The fan-out is deliberate: the audit bridge, the
/// monitoring bridge and anything a later commit adds all legitimately watch the same stream, and none of them
/// should be able to displace another by being registered second.
/// </summary>
public sealed class SecurityDispatcher
{
    private readonly IEnumerable<ISecurityEventSink> _sinks;

    /// <summary>Initializes a new instance of the <see cref="SecurityDispatcher"/> class.</summary>
    /// <param name="sinks">The sinks to fan out to.</param>
    public SecurityDispatcher(IEnumerable<ISecurityEventSink> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        _sinks = sinks;
    }

    /// <summary>Publishes an event to every sink.</summary>
    /// <param name="securityEvent">The event.</param>
    public void Publish(SecurityEvent securityEvent)
    {
        ArgumentNullException.ThrowIfNull(securityEvent);
        foreach (var sink in _sinks)
        {
            sink.Publish(securityEvent);
        }
    }
}
