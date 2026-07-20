namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that a worker holds a certification valid until an expiry. Any module consumes it without
/// referencing the producer; the HR module records it so shift assignments can be checked against it.
/// </summary>
public sealed record WorkerCertificationRecorded : IntegrationEvent
{
    /// <summary>The tenant the worker belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The worker the certification is for.</summary>
    public required string WorkerId { get; init; }

    /// <summary>The certification code (for example <c>Forklift</c>).</summary>
    public required string Certification { get; init; }

    /// <summary>When the certification expires.</summary>
    public DateTimeOffset ExpiresAt { get; init; }
}
