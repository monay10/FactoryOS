using FactoryOS.Identity.Authorization.Context;
using FactoryOS.Identity.Authorization.Evaluation;
using FactoryOS.Identity.Authorization.Model;
using FactoryOS.Identity.Authorization.Services;

namespace FactoryOS.Identity.Authorization.Handlers;

/// <summary>Handles a specific kind of <see cref="AuthorizationRequirement"/> against an authorization context.</summary>
public interface IAuthorizationHandler
{
    /// <summary>Determines whether this handler evaluates the given requirement.</summary>
    /// <param name="requirement">The requirement.</param>
    /// <returns><see langword="true"/> when this handler can evaluate it.</returns>
    bool CanHandle(AuthorizationRequirement requirement);

    /// <summary>Evaluates the requirement against the context.</summary>
    /// <param name="context">The authorization context.</param>
    /// <param name="requirement">The requirement to evaluate.</param>
    /// <returns>The authorization result.</returns>
    AuthorizationResult Handle(AuthorizationContext context, AuthorizationRequirement requirement);
}

/// <summary>Evaluates a <see cref="PermissionRequirement"/> against the context's permissions.</summary>
public sealed class PermissionAuthorizationHandler : IAuthorizationHandler
{
    private readonly IPermissionEvaluator _evaluator;

    /// <summary>Initializes a new instance of the <see cref="PermissionAuthorizationHandler"/> class.</summary>
    /// <param name="evaluator">The permission evaluator.</param>
    public PermissionAuthorizationHandler(IPermissionEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        _evaluator = evaluator;
    }

    /// <inheritdoc />
    public bool CanHandle(AuthorizationRequirement requirement) => requirement is PermissionRequirement;

    /// <inheritdoc />
    public AuthorizationResult Handle(AuthorizationContext context, AuthorizationRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        var permission = ((PermissionRequirement)requirement).Permission;

        return _evaluator.Evaluate(context.Permissions, permission)
            ? AuthorizationResult.Success()
            : AuthorizationResult.Fail($"The '{permission}' permission is required.");
    }
}

/// <summary>Evaluates a <see cref="RoleRequirement"/> against the context's roles (with inheritance).</summary>
public sealed class RoleAuthorizationHandler : IAuthorizationHandler
{
    private readonly IRoleService _roles;

    /// <summary>Initializes a new instance of the <see cref="RoleAuthorizationHandler"/> class.</summary>
    /// <param name="roles">The role service.</param>
    public RoleAuthorizationHandler(IRoleService roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        _roles = roles;
    }

    /// <inheritdoc />
    public bool CanHandle(AuthorizationRequirement requirement) => requirement is RoleRequirement;

    /// <inheritdoc />
    public AuthorizationResult Handle(AuthorizationContext context, AuthorizationRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        var role = ((RoleRequirement)requirement).Role;

        return _roles.IsInRole(context.Roles, role)
            ? AuthorizationResult.Success()
            : AuthorizationResult.Fail($"The '{role}' role is required.");
    }
}

/// <summary>
/// Evaluates a <see cref="PolicyRequirement"/> by resolving the named policy and checking the context's
/// permissions against it (all permissions, or any one, per the policy).
/// </summary>
public sealed class PolicyAuthorizationHandler : IAuthorizationHandler
{
    private readonly IPolicyProvider _policies;
    private readonly IPermissionEvaluator _evaluator;

    /// <summary>Initializes a new instance of the <see cref="PolicyAuthorizationHandler"/> class.</summary>
    /// <param name="policies">The policy provider.</param>
    /// <param name="evaluator">The permission evaluator.</param>
    public PolicyAuthorizationHandler(IPolicyProvider policies, IPermissionEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(evaluator);
        _policies = policies;
        _evaluator = evaluator;
    }

    /// <inheritdoc />
    public bool CanHandle(AuthorizationRequirement requirement) => requirement is PolicyRequirement;

    /// <inheritdoc />
    public AuthorizationResult Handle(AuthorizationContext context, AuthorizationRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        var name = ((PolicyRequirement)requirement).PolicyName;

        var policy = _policies.GetPolicy(name);
        if (policy is null)
        {
            return AuthorizationResult.Fail($"The '{name}' policy is not defined.");
        }

        if (policy.Permissions.Count == 0)
        {
            return AuthorizationResult.Success();
        }

        var satisfied = policy.RequireAll
            ? policy.Permissions.All(permission => _evaluator.Evaluate(context.Permissions, permission))
            : policy.Permissions.Any(permission => _evaluator.Evaluate(context.Permissions, permission));

        return satisfied
            ? AuthorizationResult.Success()
            : AuthorizationResult.Fail($"The '{name}' policy is not satisfied.");
    }
}
