namespace FactoryOS.Plugins.Quality.Domain;

/// <summary>The outcome of evaluating a rolling defect-rate window against the module's threshold.</summary>
/// <param name="IsBreach">Whether the window's defect rate exceeded the threshold with enough evidence.</param>
/// <param name="DefectRate">The window's defect rate, as a fraction in <c>[0, 1]</c>.</param>
/// <param name="Window">The window aggregate the decision was made from.</param>
public readonly record struct QualityEvaluation(bool IsBreach, decimal DefectRate, DefectRateSnapshot Window);
