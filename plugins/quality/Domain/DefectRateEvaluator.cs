namespace FactoryOS.Plugins.Quality.Domain;

/// <summary>
/// Decides whether a rolling defect-rate window breaches the configured threshold. Pure and deterministic — no
/// state, no I/O — so it is fully offline-testable. Detection stays inert until enough units have been inspected
/// within the window, so a cold start never raises a false alert on a single early defect.
/// </summary>
public static class DefectRateEvaluator
{
    /// <summary>Evaluates a defect-rate window under the given options.</summary>
    /// <param name="window">The rolling window aggregate, including the latest inspection.</param>
    /// <param name="options">The threshold and minimum-evidence settings.</param>
    /// <returns>The evaluation, carrying the window's defect rate.</returns>
    public static QualityEvaluation Evaluate(DefectRateSnapshot window, QualityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var rate = window.DefectRate;
        if (window.InspectedUnits < options.MinimumInspectedUnits)
        {
            return new QualityEvaluation(false, rate, window);
        }

        return new QualityEvaluation(rate > options.DefectRateThreshold, rate, window);
    }
}
