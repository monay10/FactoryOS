namespace FactoryOS.Plugins.Safety.Domain;

/// <summary>
/// Decides whether an incident warrants a stand-down. Pure and deterministic — no state, no I/O — so it is fully
/// offline-testable. A single sufficiently severe incident triggers on its own (<c>HighSeverity</c>); otherwise
/// enough incidents accumulating in the window trigger on <c>Frequency</c>. Severity takes precedence.
/// </summary>
public static class SafetyEvaluator
{
    /// <summary>Evaluates an incident against the site's window under the given options.</summary>
    /// <param name="severity">The incident severity (1–5).</param>
    /// <param name="windowIncidentCount">The incident count in the site's window, including this incident.</param>
    /// <param name="options">The severity and frequency thresholds.</param>
    /// <returns>The stand-down decision.</returns>
    public static SafetyDecision Evaluate(int severity, int windowIncidentCount, SafetyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (severity >= options.StandDownSeverity)
        {
            return new SafetyDecision(true, "HighSeverity");
        }

        if (windowIncidentCount >= options.FrequencyThreshold)
        {
            return new SafetyDecision(true, "Frequency");
        }

        return SafetyDecision.None;
    }
}
