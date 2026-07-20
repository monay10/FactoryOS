namespace FactoryOS.Persistence.Auditing;

/// <summary>A default <see cref="ICurrentActorProvider"/> that attributes changes to the system.</summary>
public sealed class SystemActorProvider : ICurrentActorProvider
{
    /// <summary>The system actor identifier.</summary>
    public const string System = "system";

    /// <inheritdoc />
    public string? ActorId => System;
}
