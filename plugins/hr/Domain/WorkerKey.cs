namespace FactoryOS.Plugins.Hr.Domain;

/// <summary>
/// Identifies a worker within a tenant. Certifications are tracked per worker, never global — mirroring the
/// per-aggregate isolation guarantee.
/// </summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="WorkerId">The worker identifier.</param>
public sealed record WorkerKey(string Tenant, string WorkerId);
