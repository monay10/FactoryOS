using FactoryOS.Edge.Mqtt;

namespace FactoryOS.Tests.Edge;

public sealed class MqttTelemetryDecoderTests
{
    private static readonly DateTimeOffset At = new(2026, 07, 20, 10, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Extracts_device_and_tag_from_the_topic_and_parses_the_payload()
    {
        var decoder = new MqttTelemetryDecoder(new MqttTopicTemplate("factory/+/{device}/{tag}"));

        var result = decoder.Decode(new MqttMessage("factory/lineA/pm-1/power", "123.45"), At);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal("pm-1", result.Value.DeviceId);
        Assert.Equal("power", result.Value.Tag);
        Assert.Equal(123.45m, result.Value.Value);
        Assert.Equal(At, result.Value.Timestamp);
    }

    [Fact]
    public void Rejects_a_topic_that_does_not_match_the_template()
    {
        var decoder = new MqttTelemetryDecoder(new MqttTopicTemplate("factory/{device}/{tag}"));

        var result = decoder.Decode(new MqttMessage("other/pm-1", "1"), At);

        Assert.True(result.IsFailure);
        Assert.Equal("Edge.Mqtt.TopicMismatch", result.Error.Code);
    }

    [Fact]
    public void Rejects_a_non_numeric_payload()
    {
        var decoder = new MqttTelemetryDecoder(new MqttTopicTemplate("factory/{device}/{tag}"));

        var result = decoder.Decode(new MqttMessage("factory/pm-1/power", "N/A"), At);

        Assert.True(result.IsFailure);
        Assert.Equal("Edge.Mqtt.InvalidPayload", result.Error.Code);
    }

    [Fact]
    public void Matches_literal_and_wildcard_segments()
    {
        var template = new MqttTopicTemplate("f/+/{device}/{tag}");

        Assert.True(template.TryMatch("f/anything/d1/t1", out var device, out var tag));
        Assert.Equal("d1", device);
        Assert.Equal("t1", tag);
        Assert.False(template.TryMatch("f/d1/t1", out _, out _));
    }
}
