namespace FactoryOS.Persistence.Auditing;

/// <summary>Supplies the identifier of the actor responsible for the current unit of work, for audit.</summary>
public interface ICurrentActorProvider
{
    /// <summary>Gets the current actor identifier, or <see langword="null"/> when acting as the system.</summary>
    string? ActorId { get; }
}
