namespace FactoryOS.Plugins.DeliveryHealth.Domain;

/// <summary>
/// The delivery tally for one transport within a tenant — how many notification deliveries were attempted on it and
/// how many succeeded or failed. The read model a UI or an AI agent queries to judge a transport's health.
/// </summary>
/// <param name="Transport">The transport the tally is for (for example <c>webhook</c> or <c>log</c>).</param>
/// <param name="Attempts">The total number of delivery attempts recorded.</param>
/// <param name="Delivered">The number of attempts that succeeded.</param>
/// <param name="Failed">The number of attempts that failed.</param>
public readonly record struct TransportHealth(string Transport, int Attempts, int Delivered, int Failed);
