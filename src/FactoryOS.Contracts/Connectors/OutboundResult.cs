namespace FactoryOS.Contracts.Connectors;

/// <summary>
/// The outcome of an outbound delivery: whether it succeeded, and an optional human-readable detail (a status
/// line, an error, a message id). A value type — no failure is signalled by throwing on the happy path.
/// </summary>
/// <param name="Delivered">Whether the message was delivered.</param>
/// <param name="Detail">An optional detail about the outcome.</param>
public readonly record struct OutboundResult(bool Delivered, string? Detail)
{
    /// <summary>A successful delivery.</summary>
    /// <param name="detail">An optional success detail (for example a provider message id).</param>
    /// <returns>A delivered result.</returns>
    public static OutboundResult Ok(string? detail = null) => new(true, detail);

    /// <summary>A failed delivery.</summary>
    /// <param name="detail">Why the delivery failed.</param>
    /// <returns>A failed result.</returns>
    public static OutboundResult Failed(string detail) => new(false, detail);
}
