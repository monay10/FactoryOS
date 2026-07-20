namespace FactoryOS.Plugins.DigitalTwin.Domain;

/// <summary>The latest observed value of one metric on an asset — a single live gauge on the twin.</summary>
/// <param name="Metric">The metric name (for example <c>Temperature</c> or <c>ActivePower</c>).</param>
/// <param name="Value">The latest measured value.</param>
/// <param name="Unit">The unit of measure.</param>
/// <param name="At">When the value was observed.</param>
public readonly record struct MetricReading(string Metric, decimal Value, string Unit, DateTimeOffset At);
