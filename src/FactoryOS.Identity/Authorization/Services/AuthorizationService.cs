using FactoryOS.Identity.Authorization.Context;
using FactoryOS.Identity.Authorization.Handlers;
using FactoryOS.Identity.Authorization.Model;

namespace FactoryOS.Identity.Authorization.Services;

/// <summary>The authorization foundation entry point: evaluates requirements and policies against a context.</summary>
public interface IAuthorizationService
{
    /// <summary>Evaluates a single requirement.</summary>
    /// <param name="context">The authorization context.</param>
    /// <param name="requirement">The requirement to evaluate.</param>
    /// <returns>The authorization result.</returns>
    AuthorizationResult Authorize(AuthorizationContext context, AuthorizationRequirement requirement);

    /// <summary>Evaluates a named policy.</summary>
    /// <param name="context">The authorization context.</param>
    /// <param name="policyName">The policy name.</param>
    /// <returns>The authorization result.</returns>
    AuthorizationResult Authorize(AuthorizationContext context, string policyName);

    /// <summary>Determines whether the context is granted a permission.</summary>
    /// <param name="context">The authorization context.</param>
    /// <param name="permission">The permission key.</param>
    /// <returns><see langword="true"/> when the permission is granted.</returns>
    bool IsGranted(AuthorizationContext context, string permission);
}

/// <summary>
/// Default <see cref="IAuthorizationService"/> dispatching each requirement to the first registered
/// <see cref="IAuthorizationHandler"/> that can evaluate it.
/// </summary>
public sealed class AuthorizationService : IAuthorizationService
{
    private readonly IReadOnlyList<IAuthorizationHandler> _handlers;

    /// <summary>Initializes a new instance of the <see cref="AuthorizationService"/> class.</summary>
    /// <param name="handlers">The registered authorization handlers.</param>
    public AuthorizationService(IEnumerable<IAuthorizationHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        _handlers = handlers.ToArray();
    }

    /// <inheritdoc />
    public AuthorizationResult Authorize(AuthorizationContext context, AuthorizationRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        var handler = _handlers.FirstOrDefault(candidate => candidate.CanHandle(requirement));
        return handler is null
            ? AuthorizationResult.Fail($"No handler is registered for '{requirement.GetType().Name}'.")
            : handler.Handle(context, requirement);
    }

    /// <inheritdoc />
    public AuthorizationResult Authorize(AuthorizationContext context, string policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        return Authorize(context, new PolicyRequirement(policyName));
    }

    /// <inheritdoc />
    public bool IsGranted(AuthorizationContext context, string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        return Authorize(context, new PermissionRequirement(permission)).Succeeded;
    }
}
