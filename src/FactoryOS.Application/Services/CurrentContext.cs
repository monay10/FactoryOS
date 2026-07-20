using FactoryOS.Domain.Abstractions;
using FactoryOS.Shared.Identifiers;

namespace FactoryOS.Application.Services;

/// <summary>
/// The application-facing clock. Extends the domain <see cref="IDateTimeProvider"/> (reused, not duplicated) with the
/// calendar concepts application code frequently needs.
/// </summary>
public interface IApplicationClock : IDateTimeProvider
{
    /// <summary>Gets the current date in UTC.</summary>
    DateOnly Today { get; }
}

/// <summary>The user the current request runs as.</summary>
public interface ICurrentUser
{
    /// <summary>Gets a value indicating whether the caller is authenticated.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Gets the caller's user identifier, when authenticated.</summary>
    UserId? UserId { get; }

    /// <summary>Gets the caller's user name, when authenticated.</summary>
    string? UserName { get; }

    /// <summary>Gets the permissions the caller holds.</summary>
    IReadOnlyCollection<string> Permissions { get; }

    /// <summary>Determines whether the caller holds a permission (wildcard-aware is the implementation's concern).</summary>
    /// <param name="permission">The permission key to check.</param>
    /// <returns><see langword="true"/> when the caller holds the permission.</returns>
    bool HasPermission(string permission);
}

/// <summary>The tenant the current request runs within.</summary>
public interface ICurrentTenant
{
    /// <summary>Gets a value indicating whether a tenant was resolved for the request.</summary>
    bool HasTenant { get; }

    /// <summary>Gets the resolved tenant, or <see langword="null"/> when none was resolved.</summary>
    string? Tenant { get; }
}

/// <summary>The factory (site) scope of the current request, when applicable.</summary>
public interface ICurrentFactory
{
    /// <summary>Gets the current factory identifier, when in scope.</summary>
    FactoryId? FactoryId { get; }
}

/// <summary>The plant scope of the current request, when applicable.</summary>
public interface ICurrentPlant
{
    /// <summary>Gets the current plant identifier, when in scope.</summary>
    PlantId? PlantId { get; }
}

/// <summary>The work-center scope of the current request, when applicable.</summary>
public interface ICurrentWorkCenter
{
    /// <summary>Gets the current work-center identifier, when in scope.</summary>
    WorkCenterId? WorkCenterId { get; }
}
