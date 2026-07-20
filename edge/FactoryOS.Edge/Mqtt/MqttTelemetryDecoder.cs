using System.Globalization;
using FactoryOS.Contracts.Iot;
using FactoryOS.Domain.Results;

namespace FactoryOS.Edge.Mqtt;

/// <summary>
/// Decodes an <see cref="MqttMessage"/> into a raw <see cref="TelemetrySample"/> using a topic template
/// to extract the device and tag and parsing the payload as an invariant decimal. The edge never
/// calibrates or normalizes — that is the IoT hub's job.
/// </summary>
public sealed class MqttTelemetryDecoder
{
    private readonly MqttTopicTemplate _template;

    /// <summary>Initializes a new instance of the <see cref="MqttTelemetryDecoder"/> class.</summary>
    /// <param name="template">The topic template used to extract the device and tag.</param>
    public MqttTelemetryDecoder(MqttTopicTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        _template = template;
    }

    /// <summary>Decodes a message captured at <paramref name="receivedAt"/> into a telemetry sample.</summary>
    /// <param name="message">The MQTT message.</param>
    /// <param name="receivedAt">The instant the message was received (its sample timestamp).</param>
    /// <returns>A successful result with the sample, or a failure when the topic or payload is invalid.</returns>
    public Result<TelemetrySample> Decode(MqttMessage message, DateTimeOffset receivedAt)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!_template.TryMatch(message.Topic, out var deviceId, out var tag))
        {
            return Result.Failure<TelemetrySample>(Error.Validation(
                "Edge.Mqtt.TopicMismatch",
                $"Topic '{message.Topic}' does not match the configured template."));
        }

        if (!decimal.TryParse(message.Payload, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return Result.Failure<TelemetrySample>(Error.Validation(
                "Edge.Mqtt.InvalidPayload",
                $"Payload '{message.Payload}' on topic '{message.Topic}' is not a numeric value."));
        }

        return Result.Success(new TelemetrySample(deviceId, tag, value, receivedAt));
    }
}
