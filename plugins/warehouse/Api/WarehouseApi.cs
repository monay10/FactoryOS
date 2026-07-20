using FactoryOS.Gateway.Endpoints;
using FactoryOS.Gateway.Tenancy;
using FactoryOS.Plugins.Warehouse.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FactoryOS.Plugins.Warehouse.Api;

/// <summary>
/// The Warehouse read API — exposes the tenant's stock read model through the gateway under <c>/m/warehouse/*</c>
/// so a wall dashboard, a stock controller or an AI agent can read on-hand levels and what is at or below its
/// reorder point without touching the ERP connectors that feed movements. It reads only; the ledger is fed
/// exclusively by <c>StockMovementRecorded</c> and <c>ItemReorderPointDefined</c> on the bus. The tenant comes
/// from the ambient <see cref="ITenantContext"/> the gateway resolves at the edge, and the reorder threshold
/// used for the low-stock view falls back to the configured default — behaviour varies by configuration, never
/// by customer.
/// </summary>
internal sealed class WarehouseApi : IModuleApi
{
    private readonly IStockLedger _ledger;
    private readonly WarehouseOptions _options;

    public WarehouseApi(IStockLedger ledger, WarehouseOptions options)
    {
        _ledger = ledger;
        _options = options;
    }

    public string ModuleKey => WarehousePlugin.PluginKey;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/stock", ([FromServices] ITenantContext context, bool? belowReorder) =>
                Results.Ok(new StockResponse(context.Tenant, Project(_ledger.ForTenant(context.Tenant), belowReorder ?? false))))
            .RequireTenant()
            .WithName("GetStockLevels");

        endpoints.MapGet("/summary", ([FromServices] ITenantContext context) =>
                Results.Ok(Summarize(context.Tenant, _ledger.ForTenant(context.Tenant))))
            .RequireTenant()
            .WithName("GetStockSummary");
    }

    /// <summary>The reorder point actually in force for an item: its own, else the configured positive default.</summary>
    private decimal? EffectiveReorderPoint(StockLevel level)
    {
        if (level.ReorderPoint is { } explicitPoint)
        {
            return explicitPoint;
        }

        return _options.DefaultReorderPoint > 0m ? _options.DefaultReorderPoint : null;
    }

    private static bool IsBelow(decimal onHand, decimal? reorderPoint) =>
        reorderPoint is { } point && point > 0m && onHand <= point;

    private List<StockView> Project(IReadOnlyCollection<StockLevel> levels, bool belowReorderOnly)
    {
        IEnumerable<StockView> rows = levels.Select(level =>
        {
            var point = EffectiveReorderPoint(level);
            return new StockView(level.WarehouseId, level.Sku, level.OnHand, point, IsBelow(level.OnHand, point));
        });

        if (belowReorderOnly)
        {
            rows = rows.Where(r => r.BelowReorder);
        }

        return rows
            .OrderBy(r => r.WarehouseId, StringComparer.Ordinal)
            .ThenBy(r => r.Sku, StringComparer.Ordinal)
            .ToList();
    }

    private StockSummaryResponse Summarize(string tenant, IReadOnlyCollection<StockLevel> levels)
    {
        var belowReorder = levels.Count(level => IsBelow(level.OnHand, EffectiveReorderPoint(level)));
        return new StockSummaryResponse(tenant, levels.Count, belowReorder);
    }
}

/// <summary>A tenant's stock levels, projected for reading.</summary>
/// <param name="Tenant">The tenant the stock belongs to.</param>
/// <param name="Items">The per-item levels, ordered by warehouse then SKU.</param>
internal sealed record StockResponse(string Tenant, IReadOnlyList<StockView> Items);

/// <summary>One item's on-hand level flattened for a dashboard row.</summary>
/// <param name="WarehouseId">The warehouse or location.</param>
/// <param name="Sku">The Standard Model SKU.</param>
/// <param name="OnHand">The current on-hand quantity.</param>
/// <param name="ReorderPoint">The reorder point in force (explicit, else the configured default), or <see langword="null"/>.</param>
/// <param name="BelowReorder">Whether on-hand is at or below the reorder point.</param>
internal sealed record StockView(
    string WarehouseId,
    string Sku,
    decimal OnHand,
    decimal? ReorderPoint,
    bool BelowReorder);

/// <summary>A tenant's stock rollup.</summary>
/// <param name="Tenant">The tenant summarized.</param>
/// <param name="TrackedItems">How many items are tracked.</param>
/// <param name="BelowReorder">How many items are at or below their reorder point.</param>
internal sealed record StockSummaryResponse(string Tenant, int TrackedItems, int BelowReorder);
