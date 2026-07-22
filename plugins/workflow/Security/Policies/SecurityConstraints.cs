using System.Globalization;
using System.Net;
using FactoryOS.Plugins.Workflow.Security.Domain;

namespace FactoryOS.Plugins.Workflow.Security.Policies;

/// <summary>
/// A rule only applies inside a window of the working week.
/// <para>
/// The window is expressed in a fixed offset from UTC rather than a named zone, for the same reason the SLA
/// engine's calendars are: a factory floor's "shift" is a wall-clock fact about one site, and a named zone
/// would make a decision depend on which machine evaluated it and which version of the zone database it had.
/// </para>
/// </summary>
/// <param name="Key">The constraint key, named in a denial.</param>
/// <param name="From">When the window opens, inclusive.</param>
/// <param name="To">When it closes, exclusive; earlier than <paramref name="From"/> means it crosses midnight.</param>
/// <param name="Days">The days it applies on; empty means every day.</param>
/// <param name="UtcOffset">The site's fixed offset from UTC.</param>
public sealed record TimeWindowConstraint(
    string Key, TimeOnly From, TimeOnly To, IReadOnlyList<DayOfWeek> Days, TimeSpan UtcOffset)
    : SecurityConstraint(Key)
{
    /// <summary>Creates a window covering the standard working week at a site.</summary>
    /// <param name="key">The constraint key.</param>
    /// <param name="from">When the window opens.</param>
    /// <param name="to">When it closes.</param>
    /// <param name="utcOffset">The site's offset from UTC.</param>
    /// <returns>The constraint.</returns>
    public static TimeWindowConstraint WorkingWeek(
        string key, TimeOnly from, TimeOnly to, TimeSpan utcOffset) =>
        new(
            key, from, to,
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            utcOffset);

    /// <inheritdoc />
    public override bool IsSatisfiedBy(SecurityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var local = context.RequestedOnUtc.ToOffset(UtcOffset);
        if (Days.Count > 0 && !Days.Contains(local.DayOfWeek))
        {
            return false;
        }

        var time = TimeOnly.FromTimeSpan(local.TimeOfDay);

        // A window that closes earlier than it opens runs through midnight — a night shift is not a mistake.
        return From <= To
            ? time >= From && time < To
            : time >= From || time < To;
    }

    /// <inheritdoc />
    public override string Describe()
    {
        var days = Days.Count == 0
            ? "any day"
            : string.Join(", ", Days.Select(day => day.ToString()));
        return $"between {From:HH\\:mm} and {To:HH\\:mm} (UTC{UtcOffset:hh\\:mm}) on {days}";
    }
}

/// <summary>
/// A rule only applies to requests from a set of networks, given in CIDR notation (<c>10.4.0.0/16</c>) or as
/// single addresses.
/// <para>
/// A request whose origin is <b>unknown</b> does not satisfy the constraint. That is the important case: an
/// unknown address is not evidence of being on the plant network, and treating it as one would make the
/// constraint trivially bypassable by anything that omits the header it was read from.
/// </para>
/// </summary>
public sealed record IpRangeConstraint : SecurityConstraint
{
    private readonly List<(IPAddress Network, int Prefix)> _ranges = [];

    /// <summary>Initializes a new instance of the <see cref="IpRangeConstraint"/> class.</summary>
    /// <param name="key">The constraint key.</param>
    /// <param name="ranges">The permitted networks, in CIDR notation or as single addresses.</param>
    public IpRangeConstraint(string key, params string[] ranges)
        : base(key)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        foreach (var range in ranges)
        {
            _ranges.Add(Parse(range));
        }
    }

    /// <summary>Gets the permitted networks, as written.</summary>
    public IReadOnlyList<string> Ranges =>
        _ranges.Select(range => $"{range.Network}/{range.Prefix}").ToArray();

    /// <inheritdoc />
    public override bool IsSatisfiedBy(SecurityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.NetworkAddress is null || !IPAddress.TryParse(context.NetworkAddress, out var address))
        {
            return false;
        }

        return _ranges.Any(range => Contains(range.Network, range.Prefix, address));
    }

    /// <inheritdoc />
    public override string Describe() => $"from {string.Join(", ", Ranges)}";

    private static (IPAddress Network, int Prefix) Parse(string range)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(range);

        var parts = range.Split('/');
        if (!IPAddress.TryParse(parts[0], out var network))
        {
            throw new FormatException($"'{range}' is not a valid address or CIDR range.");
        }

        var bits = network.GetAddressBytes().Length * 8;
        if (parts.Length == 1)
        {
            return (network, bits);
        }

        if (parts.Length != 2
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var prefix)
            || prefix < 0
            || prefix > bits)
        {
            throw new FormatException($"'{range}' is not a valid CIDR range.");
        }

        return (network, prefix);
    }

    private static bool Contains(IPAddress network, int prefix, IPAddress address)
    {
        // Comparing an IPv4 address against an IPv6 range (or the reverse) is not a near miss, it is a
        // different question; answering "no" is the only honest result.
        if (network.AddressFamily != address.AddressFamily)
        {
            return false;
        }

        var networkBytes = network.GetAddressBytes();
        var addressBytes = address.GetAddressBytes();
        var wholeBytes = prefix / 8;
        var remainingBits = prefix % 8;

        for (var index = 0; index < wholeBytes; index++)
        {
            if (networkBytes[index] != addressBytes[index])
            {
                return false;
            }
        }

        if (remainingBits == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (networkBytes[wholeBytes] & mask) == (addressBytes[wholeBytes] & mask);
    }
}

/// <summary>Where an attribute constraint reads its value from.</summary>
public enum SecurityAttributeSource
{
    /// <summary>An attribute of the resource being acted on.</summary>
    Resource = 0,

    /// <summary>A fact about the request that is neither the principal nor the resource.</summary>
    Environment = 1,

    /// <summary>A claim the principal presented.</summary>
    Claim = 2,
}

/// <summary>
/// A rule only applies when an attribute has a particular value — the workhorse of attribute-based
/// authorization. "Only while the order is in draft", "only on the line the request names".
/// </summary>
/// <param name="Key">The constraint key.</param>
/// <param name="Source">Where to read the attribute from.</param>
/// <param name="Name">The attribute or claim name.</param>
/// <param name="Expected">The value it must have.</param>
public sealed record AttributeConstraint(
    string Key, SecurityAttributeSource Source, string Name, string Expected)
    : SecurityConstraint(Key)
{
    /// <inheritdoc />
    public override bool IsSatisfiedBy(SecurityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var actual = Source switch
        {
            SecurityAttributeSource.Resource => context.Resource[Name],
            SecurityAttributeSource.Environment => context[Name],
            _ => context.Principal.FindFirst(Name),
        };

        return actual is not null && string.Equals(actual, Expected, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override string Describe() =>
        $"{Source.ToString().ToLowerInvariant()} '{Name}' must be '{Expected}'";
}

/// <summary>
/// A rule only applies when a claim the principal presents matches an attribute of the resource — the shape
/// almost every real attribute-based policy takes.
/// <para>
/// "A supervisor may approve orders <i>on their own site</i>" cannot be written as a role, and cannot be
/// written as a fixed value either, because the value differs per person. It is a comparison between the two
/// sides of the request, which is what this expresses.
/// </para>
/// </summary>
/// <param name="Key">The constraint key.</param>
/// <param name="ClaimType">The claim to read from the principal.</param>
/// <param name="ResourceAttribute">The attribute to read from the resource.</param>
public sealed record ClaimMatchesResourceConstraint(string Key, string ClaimType, string ResourceAttribute)
    : SecurityConstraint(Key)
{
    /// <inheritdoc />
    public override bool IsSatisfiedBy(SecurityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var claim = context.Principal.FindFirst(ClaimType);
        var attribute = context.Resource[ResourceAttribute];

        // Neither side may be absent. A missing claim compared against a missing attribute is two unknowns,
        // and calling that a match would grant on the strength of knowing nothing.
        return claim is not null
            && attribute is not null
            && string.Equals(claim, attribute, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override string Describe() =>
        $"claim '{ClaimType}' must match resource '{ResourceAttribute}'";
}

/// <summary>
/// A rule only applies to the principal that owns the resource instance — "you may cancel the run you started".
/// </summary>
/// <param name="Key">The constraint key.</param>
public sealed record ResourceOwnerConstraint(string Key) : SecurityConstraint(Key)
{
    /// <inheritdoc />
    public override bool IsSatisfiedBy(SecurityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Resource.Owner is { } owner
            && string.Equals(owner, context.Principal.Subject, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override string Describe() => "the principal must own the resource";
}

/// <summary>
/// A rule only applies inside a scope — a site, a module. The tenant is checked separately and structurally;
/// this narrows <i>within</i> a tenant, which is the part that is a policy decision rather than an invariant.
/// </summary>
/// <param name="Key">The constraint key.</param>
/// <param name="Scope">The scope the request must fall inside.</param>
public sealed record ScopeConstraint(string Key, SecurityScope Scope) : SecurityConstraint(Key)
{
    /// <inheritdoc />
    public override bool IsSatisfiedBy(SecurityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Scope.Contains(context.Scope);
    }

    /// <inheritdoc />
    public override string Describe() =>
        $"within {Scope.Tenant}/{Scope.Organization ?? "*"}/{Scope.Module ?? "*"}";
}
