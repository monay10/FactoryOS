using System.Collections.Concurrent;

namespace FactoryOS.Identity.Sessions;

/// <summary>Persists authenticated sessions.</summary>
public interface ISessionStore
{
    /// <summary>Adds a session.</summary>
    /// <param name="session">The session to store.</param>
    void Add(ApplicationSession session);

    /// <summary>Finds a session by identifier.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The session, or <see langword="null"/> when not found.</returns>
    ApplicationSession? Find(Guid sessionId);

    /// <summary>Finds every session belonging to a user.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns>The user's sessions.</returns>
    IReadOnlyCollection<ApplicationSession> FindByUser(Guid userId);
}

/// <summary>An in-memory <see cref="ISessionStore"/> for development and tests.</summary>
public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<Guid, ApplicationSession> _sessions = new();

    /// <inheritdoc />
    public void Add(ApplicationSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _sessions[session.Id] = session;
    }

    /// <inheritdoc />
    public ApplicationSession? Find(Guid sessionId) =>
        _sessions.TryGetValue(sessionId, out var session) ? session : null;

    /// <inheritdoc />
    public IReadOnlyCollection<ApplicationSession> FindByUser(Guid userId) =>
        _sessions.Values.Where(session => session.UserId == userId).ToArray();
}
