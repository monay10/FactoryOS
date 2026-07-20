using FactoryOS.Gateway.Endpoints;
using FactoryOS.Gateway.Tenancy;
using FactoryOS.Plugins.Energy.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FactoryOS.Plugins.Energy.Api;

/// <summary>
/// The Energy read API — serves the tenant's live per-meter readings (latest value against its rolling baseline)
/// and a recent-spike feed over <c>/m/energy/*</c>, feeding the Energy dashboard declared in the manifest. It reads
/// only; the model is fed exclusively by <c>MeterReadingReceived</c> on the bus, so no consumer touches the Edge
/// Gateway or a meter directly. The tenant comes from the ambient <see cref="ITenantContext"/> resolved at the edge.
/// </summary>
internal sealed class EnergyApi : IModuleApi
{
    private readonly IEnergyReadModel _readModel;

    public EnergyApi(IEnergyReadModel readModel)
    {
        _readModel = readModel;
    }

    public string ModuleKey => EnergyPlugin.PluginKey;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/meters", ([FromServices] ITenantContext context) =>
                Results.Ok(new EnergyMetersResponse(context.Tenant, Project(_readModel.Meters(context.Tenant)))))
            .RequireTenant()
            .WithName("GetEnergyMeters");

        endpoints.MapGet("/spikes", ([FromServices] ITenantContext context, int? max) =>
                Results.Ok(new EnergySpikesResponse(
                    context.Tenant,
                    ProjectSpikes(_readModel.Spikes(context.Tenant, max is > 0 ? max.Value : 50)))))
            .RequireTenant()
            .WithName("GetEnergySpikes");

        endpoints.MapGet("/summary", ([FromServices] ITenantContext context) =>
                Results.Ok(_readModel.Summarize(context.Tenant)))
            .RequireTenant()
            .WithName("GetEnergySummary");
    }

    private static List<EnergyMeterView> Project(IReadOnlyList<EnergyMeterReading> meters) =>
        meters
            .Select(m => new EnergyMeterView(
                m.MeterId,
                m.Metric,
                m.Value,
                m.Baseline,
                DeltaPercent(m.Value, m.Baseline),
                m.Unit,
                m.ReadingAt))
            .ToList();

    private static List<EnergySpikeView> ProjectSpikes(IReadOnlyList<EnergySpikeEntry> spikes) =>
        spikes
            .Select(s => new EnergySpikeView(s.MeterId, s.Metric, s.Value, s.Baseline, s.DeltaPercent, s.Unit, s.ReadingAt))
            .ToList();

    private static decimal DeltaPercent(decimal value, decimal baseline) =>
        baseline == 0m ? 0m : decimal.Round((value - baseline) / baseline * 100m, 1, MidpointRounding.AwayFromZero);
}

/// <summary>A tenant's live per-meter readings.</summary>
/// <param name="Tenant">The tenant the readings belong to.</param>
/// <param name="Meters">The latest reading per meter, ordered by meter then metric.</param>
internal sealed record EnergyMetersResponse(string Tenant, IReadOnlyList<EnergyMeterView> Meters);

/// <summary>One meter's latest reading flattened for a dashboard row.</summary>
/// <param name="MeterId">The meter.</param>
/// <param name="Metric">The measured metric.</param>
/// <param name="Value">The latest reading value.</param>
/// <param name="Baseline">The rolling baseline the value is compared against.</param>
/// <param name="DeltaPercent">How far the value sits above (or below) the baseline, in percent.</param>
/// <param name="Unit">The unit of measure.</param>
/// <param name="ReadingAt">When the reading was taken.</param>
internal sealed record EnergyMeterView(
    string MeterId,
    string Metric,
    decimal Value,
    decimal Baseline,
    decimal DeltaPercent,
    string Unit,
    DateTimeOffset ReadingAt);

/// <summary>A tenant's recent spikes, newest first.</summary>
/// <param name="Tenant">The tenant the spikes belong to.</param>
/// <param name="Spikes">The recent spikes.</param>
internal sealed record EnergySpikesResponse(string Tenant, IReadOnlyList<EnergySpikeView> Spikes);

/// <summary>One detected spike flattened for a dashboard row.</summary>
/// <param name="MeterId">The meter.</param>
/// <param name="Metric">The measured metric.</param>
/// <param name="Value">The reading that tripped the spike.</param>
/// <param name="Baseline">The baseline it was compared against.</param>
/// <param name="DeltaPercent">How far above the baseline the value is, in percent.</param>
/// <param name="Unit">The unit of measure.</param>
/// <param name="ReadingAt">When the reading was taken.</param>
internal sealed record EnergySpikeView(
    string MeterId,
    string Metric,
    decimal Value,
    decimal Baseline,
    decimal DeltaPercent,
    string Unit,
    DateTimeOffset ReadingAt);
