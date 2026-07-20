using FactoryOS.Persistence.Configuration;
using FactoryOS.Persistence.ValueConversion;
using Microsoft.EntityFrameworkCore;

namespace FactoryOS.Persistence.Conventions;

/// <summary>
/// The platform-wide EF Core model conventions applied by <see cref="Context.FactoryOsDbContext"/>. They are safe and
/// additive — an explicit mapping in an entity configuration always overrides a convention — so applying them to the
/// shared base context does not disturb any module's existing model:
/// <list type="bullet">
///   <item><description>Every <see cref="DateTime"/> is stored and read back as UTC.</description></item>
///   <item><description><see cref="decimal"/> columns default to a fixed precision and scale.</description></item>
///   <item><description>The FactoryOS strongly-typed identifiers map to their underlying primitive.</description></item>
/// </list>
/// </summary>
public static class FactoryOsConventions
{
    /// <summary>Applies the FactoryOS conventions to a model-configuration builder.</summary>
    /// <param name="configurationBuilder">The model-configuration builder.</param>
    public static void Apply(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<decimal>()
            .HavePrecision(PersistenceConstants.DecimalPrecision, PersistenceConstants.DecimalScale);

        configurationBuilder.Properties<Shared.Identifiers.TenantId>().HaveConversion<TenantIdConverter>();
        configurationBuilder.Properties<Shared.Identifiers.UserId>().HaveConversion<UserIdConverter>();
        configurationBuilder.Properties<Shared.Identifiers.MachineId>().HaveConversion<MachineIdConverter>();
        configurationBuilder.Properties<Shared.Identifiers.FactoryId>().HaveConversion<FactoryIdConverter>();
        configurationBuilder.Properties<Shared.Identifiers.OrganizationId>().HaveConversion<OrganizationIdConverter>();
        configurationBuilder.Properties<Shared.Identifiers.PlantId>().HaveConversion<PlantIdConverter>();
        configurationBuilder.Properties<Shared.Identifiers.LineId>().HaveConversion<LineIdConverter>();
        configurationBuilder.Properties<Shared.Identifiers.WorkCenterId>().HaveConversion<WorkCenterIdConverter>();
        configurationBuilder.Properties<Shared.Identifiers.CorrelationId>().HaveConversion<CorrelationIdConverter>();
    }
}
