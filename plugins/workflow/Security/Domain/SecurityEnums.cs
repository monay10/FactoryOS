namespace FactoryOS.Plugins.Workflow.Security.Domain;

/// <summary>What a rule does when it matches.</summary>
public enum SecurityEffect
{
    /// <summary>The request is refused.</summary>
    Deny = 0,

    /// <summary>The request is permitted.</summary>
    Allow = 1,
}

/// <summary>
/// Why a request was allowed or refused. Every decision names one, because an authorization system that can
/// only say "no" is one nobody can fix — and the first question after a denial is always "why?".
/// </summary>
public enum SecurityDecisionReason
{
    /// <summary>No rule matched, so the deny-by-default fell through.</summary>
    NoMatchingRule = 0,

    /// <summary>A rule granted the request.</summary>
    GrantedByRule = 1,

    /// <summary>The principal held the permission directly or through a role.</summary>
    GrantedByPermission = 2,

    /// <summary>A rule refused the request outright.</summary>
    DeniedByRule = 3,

    /// <summary>The principal held no permission covering the request.</summary>
    MissingPermission = 4,

    /// <summary>A constraint attached to the matching rule was not satisfied.</summary>
    ConstraintNotSatisfied = 5,

    /// <summary>The request named a tenant the principal does not belong to.</summary>
    TenantMismatch = 6,

    /// <summary>The principal's session is expired, revoked or unknown.</summary>
    SessionNotActive = 7,

    /// <summary>The presented token is expired, revoked or unknown.</summary>
    TokenNotValid = 8,

    /// <summary>The principal is not authenticated at all.</summary>
    NotAuthenticated = 9,
}

/// <summary>The policy styles the engine evaluates.</summary>
public enum SecurityPolicyKind
{
    /// <summary>Role-based: what a principal is allowed follows from the roles it holds.</summary>
    RoleBased = 0,

    /// <summary>Attribute-based: the decision follows from attributes of the principal, resource and context.</summary>
    AttributeBased = 1,

    /// <summary>Claim-based: the decision follows from claims the principal presents.</summary>
    ClaimBased = 2,

    /// <summary>Resource-based: the decision depends on the specific resource instance, such as its owner.</summary>
    ResourceBased = 3,

    /// <summary>Tenant-based: the decision depends on which tenant the request belongs to.</summary>
    TenantBased = 4,

    /// <summary>Time-based: the decision depends on when the request is made.</summary>
    TimeBased = 5,

    /// <summary>Network-based: the decision depends on where the request came from.</summary>
    IpBased = 6,
}

/// <summary>What kind of security violation was detected.</summary>
public enum SecurityViolationKind
{
    /// <summary>A request was refused by authorization.</summary>
    AuthorizationDenied = 0,

    /// <summary>Authentication was attempted and failed.</summary>
    AuthenticationFailed = 1,

    /// <summary>A request named a tenant the principal does not belong to.</summary>
    CrossTenantAccess = 2,

    /// <summary>A token was presented that is expired, revoked or unknown.</summary>
    InvalidToken = 3,

    /// <summary>A session was used after it stopped being active.</summary>
    ExpiredSession = 4,

    /// <summary>A principal opened more sessions than it is allowed.</summary>
    ConcurrentSessionLimit = 5,

    /// <summary>A request arrived from outside the permitted network range.</summary>
    NetworkNotPermitted = 6,

    /// <summary>A request arrived outside the permitted time window.</summary>
    OutsidePermittedTime = 7,
}

/// <summary>How much attention a violation or incident deserves.</summary>
public enum SecurityRiskLevel
{
    /// <summary>Routine; recorded for completeness.</summary>
    Informational = 0,

    /// <summary>Worth noticing, on its own or in aggregate.</summary>
    Low = 1,

    /// <summary>Worth investigating.</summary>
    Medium = 2,

    /// <summary>Worth investigating now.</summary>
    High = 3,

    /// <summary>Worth waking somebody up for.</summary>
    Critical = 4,
}

/// <summary>Where an incident is in its life.</summary>
public enum SecurityIncidentStatus
{
    /// <summary>Raised and not yet looked at.</summary>
    Open = 0,

    /// <summary>Somebody is looking at it.</summary>
    Investigating = 1,

    /// <summary>Dealt with.</summary>
    Resolved = 2,

    /// <summary>Looked at and judged not to be a problem.</summary>
    Dismissed = 3,
}

/// <summary>Why a session ended.</summary>
public enum SessionEndReason
{
    /// <summary>The session sat idle past its sliding window.</summary>
    IdleTimeout = 0,

    /// <summary>The session reached its absolute lifetime, which is never extended.</summary>
    AbsoluteTimeout = 1,

    /// <summary>Somebody revoked it — a sign-out, or an administrator.</summary>
    Revoked = 2,

    /// <summary>It was displaced by a newer session because the principal's concurrent limit was reached.</summary>
    Displaced = 3,
}
