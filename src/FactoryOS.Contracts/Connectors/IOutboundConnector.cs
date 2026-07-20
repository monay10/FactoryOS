namespace FactoryOS.Contracts.Connectors;

/// <summary>
/// The contract every <b>outbound</b> connector implements — the door <b>out</b>. Where <see cref="IConnector"/>
/// reads the outside world in, this delivers the Standard Model's notifications out to a transport (webhook,
/// email, SMS, chat, a log). Business modules never deliver directly; they emit facts and a connector carries
/// them across the boundary, keeping "connectors are the only door to the outside" true in both directions.
/// </summary>
public interface IOutboundConnector
{
    /// <summary>Gets the stable key identifying this connector; it must match the connector manifest.</summary>
    string Key { get; }

    /// <summary>Gets the transport this connector delivers on (for example <c>webhook</c>, <c>email</c>, <c>log</c>).</summary>
    string Transport { get; }

    /// <summary>Delivers a normalized message to the outside world.</summary>
    /// <param name="message">The message to deliver.</param>
    /// <param name="cancellationToken">A token to cancel the delivery.</param>
    /// <returns>The delivery outcome.</returns>
    Task<OutboundResult> DeliverAsync(OutboundMessage message, CancellationToken cancellationToken);
}
