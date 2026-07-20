namespace FactoryOS.Edge.Mqtt;

/// <summary>A single MQTT message as received by the edge gateway, before decoding into telemetry.</summary>
/// <param name="Topic">The MQTT topic the message was published to.</param>
/// <param name="Payload">The message payload as text (a numeric value).</param>
public sealed record MqttMessage(string Topic, string Payload);
