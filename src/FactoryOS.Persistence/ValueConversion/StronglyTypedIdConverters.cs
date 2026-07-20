using FactoryOS.Shared.Identifiers;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FactoryOS.Persistence.ValueConversion;

/// <summary>Persists a <see cref="TenantId"/> as its underlying <see cref="Guid"/>.</summary>
public sealed class TenantIdConverter : ValueConverter<TenantId, Guid>
{
    /// <summary>Initializes a new instance of the <see cref="TenantIdConverter"/> class.</summary>
    public TenantIdConverter()
        : base(id => id.Value, value => new TenantId(value))
    {
    }
}

/// <summary>Persists a <see cref="UserId"/> as its underlying <see cref="Guid"/>.</summary>
public sealed class UserIdConverter : ValueConverter<UserId, Guid>
{
    /// <summary>Initializes a new instance of the <see cref="UserIdConverter"/> class.</summary>
    public UserIdConverter()
        : base(id => id.Value, value => new UserId(value))
    {
    }
}

/// <summary>Persists a <see cref="MachineId"/> as its underlying <see cref="Guid"/>.</summary>
public sealed class MachineIdConverter : ValueConverter<MachineId, Guid>
{
    /// <summary>Initializes a new instance of the <see cref="MachineIdConverter"/> class.</summary>
    public MachineIdConverter()
        : base(id => id.Value, value => new MachineId(value))
    {
    }
}

/// <summary>Persists a <see cref="FactoryId"/> as its underlying <see cref="Guid"/>.</summary>
public sealed class FactoryIdConverter : ValueConverter<FactoryId, Guid>
{
    /// <summary>Initializes a new instance of the <see cref="FactoryIdConverter"/> class.</summary>
    public FactoryIdConverter()
        : base(id => id.Value, value => new FactoryId(value))
    {
    }
}

/// <summary>Persists an <see cref="OrganizationId"/> as its underlying <see cref="Guid"/>.</summary>
public sealed class OrganizationIdConverter : ValueConverter<OrganizationId, Guid>
{
    /// <summary>Initializes a new instance of the <see cref="OrganizationIdConverter"/> class.</summary>
    public OrganizationIdConverter()
        : base(id => id.Value, value => new OrganizationId(value))
    {
    }
}

/// <summary>Persists a <see cref="PlantId"/> as its underlying <see cref="Guid"/>.</summary>
public sealed class PlantIdConverter : ValueConverter<PlantId, Guid>
{
    /// <summary>Initializes a new instance of the <see cref="PlantIdConverter"/> class.</summary>
    public PlantIdConverter()
        : base(id => id.Value, value => new PlantId(value))
    {
    }
}

/// <summary>Persists a <see cref="LineId"/> as its underlying <see cref="Guid"/>.</summary>
public sealed class LineIdConverter : ValueConverter<LineId, Guid>
{
    /// <summary>Initializes a new instance of the <see cref="LineIdConverter"/> class.</summary>
    public LineIdConverter()
        : base(id => id.Value, value => new LineId(value))
    {
    }
}

/// <summary>Persists a <see cref="WorkCenterId"/> as its underlying <see cref="Guid"/>.</summary>
public sealed class WorkCenterIdConverter : ValueConverter<WorkCenterId, Guid>
{
    /// <summary>Initializes a new instance of the <see cref="WorkCenterIdConverter"/> class.</summary>
    public WorkCenterIdConverter()
        : base(id => id.Value, value => new WorkCenterId(value))
    {
    }
}

/// <summary>Persists a <see cref="CorrelationId"/> as its underlying string.</summary>
public sealed class CorrelationIdConverter : ValueConverter<CorrelationId, string>
{
    /// <summary>Initializes a new instance of the <see cref="CorrelationIdConverter"/> class.</summary>
    public CorrelationIdConverter()
        : base(id => id.Value, value => new CorrelationId(value))
    {
    }
}
