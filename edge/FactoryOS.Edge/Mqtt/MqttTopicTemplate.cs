namespace FactoryOS.Edge.Mqtt;

/// <summary>
/// A slash-segmented MQTT topic template that extracts the device and tag from a concrete topic. Segments
/// are literals or the placeholders <c>{device}</c> and <c>{tag}</c> — for example
/// <c>factory/+/{device}/{tag}</c>, where <c>+</c> matches any single segment. Mapping is data, not code.
/// </summary>
public sealed class MqttTopicTemplate
{
    private const string DevicePlaceholder = "{device}";
    private const string TagPlaceholder = "{tag}";

    private readonly string[] _segments;

    /// <summary>Initializes a new instance of the <see cref="MqttTopicTemplate"/> class.</summary>
    /// <param name="template">The topic template (for example <c>factory/{device}/{tag}</c>).</param>
    public MqttTopicTemplate(string template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        _segments = template.Split('/');
    }

    /// <summary>Attempts to match a concrete topic and extract its device and tag.</summary>
    /// <param name="topic">The concrete MQTT topic.</param>
    /// <param name="deviceId">The extracted device identifier when matched.</param>
    /// <param name="tag">The extracted tag when matched.</param>
    /// <returns><see langword="true"/> when the topic matches the template; otherwise <see langword="false"/>.</returns>
    public bool TryMatch(string topic, out string deviceId, out string tag)
    {
        deviceId = string.Empty;
        tag = string.Empty;

        if (string.IsNullOrWhiteSpace(topic))
        {
            return false;
        }

        var parts = topic.Split('/');
        if (parts.Length != _segments.Length)
        {
            return false;
        }

        for (var index = 0; index < _segments.Length; index++)
        {
            var segment = _segments[index];
            var part = parts[index];

            switch (segment)
            {
                case DevicePlaceholder:
                    deviceId = part;
                    break;
                case TagPlaceholder:
                    tag = part;
                    break;
                case "+":
                    break;
                default:
                    if (!string.Equals(segment, part, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    break;
            }
        }

        return deviceId.Length > 0 && tag.Length > 0;
    }
}
