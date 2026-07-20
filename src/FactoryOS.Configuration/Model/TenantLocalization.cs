namespace FactoryOS.Configuration.Model;

/// <summary>Per-tenant localization: language, time zone and unit system.</summary>
/// <param name="Language">An IETF/ISO language tag (e.g. <c>tr</c> or <c>en</c>).</param>
/// <param name="TimeZone">An IANA time-zone identifier (e.g. <c>Europe/Istanbul</c>).</param>
/// <param name="Units">The unit system used for measurements.</param>
public sealed record TenantLocalization(string Language, string TimeZone, UnitSystem Units);
