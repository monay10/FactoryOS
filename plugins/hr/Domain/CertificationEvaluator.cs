namespace FactoryOS.Plugins.Hr.Domain;

/// <summary>
/// Decides whether a worker has a valid required certification at a shift's start. Pure and deterministic — no
/// state, no I/O — so it is fully offline-testable. A never-held certification is a <c>Missing</c> gap (subject
/// to configuration); one that expired by the shift start is an <c>Expired</c> gap.
/// </summary>
public static class CertificationEvaluator
{
    /// <summary>Evaluates a certification's validity against a shift start.</summary>
    /// <param name="expiry">The certification's expiry, or <see langword="null"/> if the worker never held it.</param>
    /// <param name="shiftStart">The instant the certification must be valid at.</param>
    /// <param name="options">The gap-policy options.</param>
    /// <returns>The gap decision.</returns>
    public static CertificationGap Evaluate(DateTimeOffset? expiry, DateTimeOffset shiftStart, HrOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (expiry is null)
        {
            return options.TreatMissingAsGap ? new CertificationGap(true, "Missing") : CertificationGap.None;
        }

        return expiry.Value < shiftStart ? new CertificationGap(true, "Expired") : CertificationGap.None;
    }
}
