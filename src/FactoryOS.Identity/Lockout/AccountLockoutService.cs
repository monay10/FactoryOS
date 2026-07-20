using System.Collections.Concurrent;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Identity.Configuration;
using Microsoft.Extensions.Options;

namespace FactoryOS.Identity.Lockout;

/// <summary>The lockout state of an account: its consecutive failure count and the instant it unlocks.</summary>
/// <param name="FailedAttempts">The number of consecutive failures recorded.</param>
/// <param name="LockedUntilUtc">The instant the lockout ends, or <see langword="null"/> when not locked.</param>
public sealed record LockoutState(int FailedAttempts, DateTimeOffset? LockedUntilUtc);

/// <summary>Persists per-user login-attempt state for the lockout policy.</summary>
public interface ILoginAttemptStore
{
    /// <summary>Reads the current lockout state for a user.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns>The stored state, or a zeroed state when the user has no recorded failures.</returns>
    LockoutState Get(Guid userId);

    /// <summary>Stores the lockout state for a user.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="state">The state to store.</param>
    void Set(Guid userId, LockoutState state);

    /// <summary>Clears any stored state for a user.</summary>
    /// <param name="userId">The user identifier.</param>
    void Clear(Guid userId);
}

/// <summary>An in-memory <see cref="ILoginAttemptStore"/> for development and tests.</summary>
public sealed class InMemoryLoginAttemptStore : ILoginAttemptStore
{
    private readonly ConcurrentDictionary<Guid, LockoutState> _states = new();

    /// <inheritdoc />
    public LockoutState Get(Guid userId) =>
        _states.TryGetValue(userId, out var state) ? state : new LockoutState(0, null);

    /// <inheritdoc />
    public void Set(Guid userId, LockoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _states[userId] = state;
    }

    /// <inheritdoc />
    public void Clear(Guid userId) => _states.TryRemove(userId, out _);
}

/// <summary>Evaluates and mutates account-lockout state after authentication attempts.</summary>
public interface IAccountLockoutService
{
    /// <summary>Determines whether an account is currently locked out.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns><see langword="true"/> when the account is locked out at the current instant.</returns>
    bool IsLockedOut(Guid userId);

    /// <summary>Records an authentication failure, locking the account once the threshold is reached.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns>The resulting lockout state.</returns>
    LockoutState RecordFailure(Guid userId);

    /// <summary>Records an authentication success, clearing any accumulated failures.</summary>
    /// <param name="userId">The user identifier.</param>
    void RecordSuccess(Guid userId);
}

/// <summary>
/// Default <see cref="IAccountLockoutService"/>. Counts consecutive failures per user and locks the account
/// for the configured duration once <see cref="LockoutOptions.MaxFailedAccessAttempts"/> is reached. A
/// success resets the counter. When lockout is disabled the service is inert.
/// </summary>
public sealed class AccountLockoutService : IAccountLockoutService
{
    private readonly ILoginAttemptStore _store;
    private readonly IDateTimeProvider _clock;
    private readonly LockoutOptions _options;

    /// <summary>Initializes a new instance of the <see cref="AccountLockoutService"/> class.</summary>
    /// <param name="store">The login-attempt store.</param>
    /// <param name="clock">The clock used for lockout windows.</param>
    /// <param name="options">The identity options carrying the lockout policy.</param>
    public AccountLockoutService(ILoginAttemptStore store, IDateTimeProvider clock, IOptions<IdentityOptions> options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);

        _store = store;
        _clock = clock;
        _options = options.Value.Lockout;
    }

    /// <inheritdoc />
    public bool IsLockedOut(Guid userId)
    {
        if (!_options.Enabled)
        {
            return false;
        }

        var state = _store.Get(userId);
        return state.LockedUntilUtc is { } until && _clock.UtcNow < until;
    }

    /// <inheritdoc />
    public LockoutState RecordFailure(Guid userId)
    {
        if (!_options.Enabled)
        {
            return new LockoutState(0, null);
        }

        var attempts = _store.Get(userId).FailedAttempts + 1;
        DateTimeOffset? lockedUntil = attempts >= _options.MaxFailedAccessAttempts
            ? _clock.UtcNow.AddMinutes(_options.LockoutMinutes)
            : null;

        var state = new LockoutState(attempts, lockedUntil);
        _store.Set(userId, state);
        return state;
    }

    /// <inheritdoc />
    public void RecordSuccess(Guid userId) => _store.Clear(userId);
}
