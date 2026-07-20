namespace FactoryOS.Plugins.DeliveryHealth.Domain;

/// <summary>
/// One retained failed-delivery detail, kept for troubleshooting — the transport, what was being delivered and the
/// connector's failure detail. Distinct from the aggregate <see cref="TransportHealth"/> tallies.
/// </summary>
/// <param name="Transport">The transport the delivery was attempted on.</param>
/// <param name="Channel">The logical channel the notification targeted.</param>
/// <param name="Subject">A human-readable description of the notification.</param>
/// <param name="Detail">The connector's failure detail, if any.</param>
/// <param name="At">When the failed delivery was attempted.</param>
public readonly record struct DeliveryFailure(
    string Transport,
    string Channel,
    string Subject,
    string? Detail,
    DateTimeOffset At);
