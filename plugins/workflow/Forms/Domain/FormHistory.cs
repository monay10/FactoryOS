namespace FactoryOS.Plugins.Forms.Engine.Domain;

/// <summary>A single audit entry recording something that happened to a form instance.</summary>
/// <param name="OccurredOnUtc">When the entry was recorded.</param>
/// <param name="Action">A short action name (e.g. <c>opened</c>, <c>draft-saved</c>, <c>submitted</c>).</param>
/// <param name="Actor">Who caused it, if known.</param>
/// <param name="Detail">Optional additional detail.</param>
public sealed record FormHistoryEntry(DateTimeOffset OccurredOnUtc, string Action, string? Actor, string? Detail);

/// <summary>An append-only audit trail of what happened to a form instance, in order.</summary>
public sealed class FormHistory
{
    private readonly List<FormHistoryEntry> _entries = [];

    /// <summary>Gets the recorded entries in order.</summary>
    public IReadOnlyList<FormHistoryEntry> Entries => _entries;

    /// <summary>Appends an entry.</summary>
    /// <param name="entry">The entry to append.</param>
    public void Append(FormHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Add(entry);
    }
}
