namespace FactoryOS.Shared.Identifiers;

/// <summary>A correlation identifier that threads a single logical operation across service and log boundaries.</summary>
/// <param name="Value">The underlying identifier string.</param>
public readonly record struct CorrelationId(string Value)
{
    /// <summary>Creates a new random correlation identifier.</summary>
    /// <returns>A new <see cref="CorrelationId"/>.</returns>
    public static CorrelationId New() => new(Guid.NewGuid().ToString("N"));

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Identifies a tenant (a factory) — the isolation boundary every operation runs within.</summary>
/// <param name="Value">The underlying identifier.</param>
public readonly record struct TenantId(Guid Value)
{
    /// <summary>Creates a new tenant identifier.</summary>
    /// <returns>A new <see cref="TenantId"/>.</returns>
    public static TenantId New() => new(Guid.NewGuid());

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a user.</summary>
/// <param name="Value">The underlying identifier.</param>
public readonly record struct UserId(Guid Value)
{
    /// <summary>Creates a new user identifier.</summary>
    /// <returns>A new <see cref="UserId"/>.</returns>
    public static UserId New() => new(Guid.NewGuid());

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a machine or asset on the shop floor.</summary>
/// <param name="Value">The underlying identifier.</param>
public readonly record struct MachineId(Guid Value)
{
    /// <summary>Creates a new machine identifier.</summary>
    /// <returns>A new <see cref="MachineId"/>.</returns>
    public static MachineId New() => new(Guid.NewGuid());

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a factory as a physical site.</summary>
/// <param name="Value">The underlying identifier.</param>
public readonly record struct FactoryId(Guid Value)
{
    /// <summary>Creates a new factory identifier.</summary>
    /// <returns>A new <see cref="FactoryId"/>.</returns>
    public static FactoryId New() => new(Guid.NewGuid());

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies an organization (a customer tenant may span several).</summary>
/// <param name="Value">The underlying identifier.</param>
public readonly record struct OrganizationId(Guid Value)
{
    /// <summary>Creates a new organization identifier.</summary>
    /// <returns>A new <see cref="OrganizationId"/>.</returns>
    public static OrganizationId New() => new(Guid.NewGuid());

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a plant within a factory.</summary>
/// <param name="Value">The underlying identifier.</param>
public readonly record struct PlantId(Guid Value)
{
    /// <summary>Creates a new plant identifier.</summary>
    /// <returns>A new <see cref="PlantId"/>.</returns>
    public static PlantId New() => new(Guid.NewGuid());

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a production line.</summary>
/// <param name="Value">The underlying identifier.</param>
public readonly record struct LineId(Guid Value)
{
    /// <summary>Creates a new line identifier.</summary>
    /// <returns>A new <see cref="LineId"/>.</returns>
    public static LineId New() => new(Guid.NewGuid());

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a work center within a line.</summary>
/// <param name="Value">The underlying identifier.</param>
public readonly record struct WorkCenterId(Guid Value)
{
    /// <summary>Creates a new work-center identifier.</summary>
    /// <returns>A new <see cref="WorkCenterId"/>.</returns>
    public static WorkCenterId New() => new(Guid.NewGuid());

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>A key into a localization resource, decoupling code from translated strings.</summary>
/// <param name="Value">The resource key.</param>
public readonly record struct LocalizationKey(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>A stable, machine-readable error code, distinct from any human-readable message.</summary>
/// <param name="Value">The code string.</param>
public readonly record struct ErrorCode(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;
}
