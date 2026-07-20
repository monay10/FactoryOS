namespace FactoryOS.Plugins.Warehouse.Domain;

/// <summary>The on-hand level before and after a movement was applied.</summary>
/// <param name="Previous">The on-hand quantity before the movement.</param>
/// <param name="Current">The on-hand quantity after the movement.</param>
public readonly record struct StockChange(decimal Previous, decimal Current);
