namespace FactoryOS.Plugins.Hr.Domain;

/// <summary>The outcome of checking a worker's certification against a shift requirement.</summary>
/// <param name="IsGap">Whether the worker lacks a valid required certification.</param>
/// <param name="Reason">The reason (<c>Missing</c> or <c>Expired</c>); empty when there is no gap.</param>
public readonly record struct CertificationGap(bool IsGap, string Reason)
{
    /// <summary>No gap: the worker holds the required certification and it is valid.</summary>
    public static CertificationGap None { get; } = new(false, "");
}
