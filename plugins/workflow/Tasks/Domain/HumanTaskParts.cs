namespace FactoryOS.Plugins.Workflow.Tasks.Domain;

/// <summary>A comment left on a human task, either internal or public.</summary>
/// <param name="Id">The comment id.</param>
/// <param name="Author">Who wrote it.</param>
/// <param name="Text">The comment text.</param>
/// <param name="Visibility">Who may see it.</param>
/// <param name="CreatedOnUtc">When it was written.</param>
public sealed record HumanTaskComment(
    Guid Id, string Author, string Text, CommentVisibility Visibility, DateTimeOffset CreatedOnUtc);

/// <summary>
/// A reference to a file attached to a human task. The engine stores only the reference (a storage key or URI)
/// and metadata — never the bytes; the actual content lives in the platform's object storage.
/// </summary>
/// <param name="Id">The attachment id.</param>
/// <param name="FileName">The display file name.</param>
/// <param name="StorageKey">The object-storage key or URI the content lives at.</param>
/// <param name="ContentType">The MIME type.</param>
/// <param name="SizeBytes">The content size in bytes, if known.</param>
/// <param name="AddedBy">Who attached it.</param>
/// <param name="AddedOnUtc">When it was attached.</param>
public sealed record HumanTaskAttachment(
    Guid Id,
    string FileName,
    string StorageKey,
    string? ContentType,
    long? SizeBytes,
    string? AddedBy,
    DateTimeOffset AddedOnUtc);

/// <summary>
/// The deadline policy of a human task: either a fixed instant or a duration measured from creation. Resolved
/// to a concrete due instant when the task is created.
/// </summary>
public sealed record HumanTaskDeadline
{
    private HumanTaskDeadline(TimeSpan? duration, DateTimeOffset? dueUtc)
    {
        Duration = duration;
        DueUtc = dueUtc;
    }

    /// <summary>Gets the duration from creation, when the deadline is relative.</summary>
    public TimeSpan? Duration { get; }

    /// <summary>Gets the fixed due instant, when the deadline is absolute.</summary>
    public DateTimeOffset? DueUtc { get; }

    /// <summary>Creates a deadline a fixed duration after creation.</summary>
    /// <param name="duration">The duration from creation.</param>
    /// <returns>The deadline.</returns>
    public static HumanTaskDeadline In(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A deadline duration must be positive.");
        }

        return new HumanTaskDeadline(duration, null);
    }

    /// <summary>Creates a deadline at a fixed instant.</summary>
    /// <param name="dueUtc">The due instant.</param>
    /// <returns>The deadline.</returns>
    public static HumanTaskDeadline At(DateTimeOffset dueUtc) => new(null, dueUtc);

    /// <summary>Resolves the deadline to a concrete instant given when the task was created.</summary>
    /// <param name="createdOnUtc">When the task was created.</param>
    /// <returns>The due instant.</returns>
    public DateTimeOffset Resolve(DateTimeOffset createdOnUtc) =>
        DueUtc ?? createdOnUtc + Duration!.Value;
}

/// <summary>
/// A reminder policy: fire a nudge a fixed duration before the task's deadline. Resolved to a fire instant
/// when the task is created and its deadline is known.
/// </summary>
/// <param name="Before">How long before the deadline to fire.</param>
public sealed record HumanTaskReminder(TimeSpan Before)
{
    /// <summary>Resolves the reminder to the instant it should fire.</summary>
    /// <param name="deadlineUtc">The task's deadline.</param>
    /// <returns>The fire instant.</returns>
    public DateTimeOffset ResolveFireInstant(DateTimeOffset deadlineUtc) => deadlineUtc - Before;
}

/// <summary>
/// An escalation policy: a fixed duration after the deadline, hand the task to another principal. Resolved to
/// a due instant when the task's deadline is known.
/// </summary>
/// <param name="After">How long after the deadline to escalate.</param>
/// <param name="To">Who to escalate the task to.</param>
public sealed record HumanTaskEscalation(TimeSpan After, HumanTaskAssignment To)
{
    /// <summary>Resolves the escalation to the instant it becomes due.</summary>
    /// <param name="deadlineUtc">The task's deadline.</param>
    /// <returns>The due instant.</returns>
    public DateTimeOffset ResolveDueInstant(DateTimeOffset deadlineUtc) => deadlineUtc + After;
}

/// <summary>The decision recorded when a human task is completed or rejected.</summary>
/// <param name="Outcome">The approve/reject/done outcome.</param>
/// <param name="DecidedBy">Who decided.</param>
/// <param name="Comment">An optional decision comment.</param>
/// <param name="Variables">Values passed back to the workflow as the activity outcome.</param>
public sealed record HumanTaskDecision(
    HumanTaskOutcome Outcome,
    string? DecidedBy,
    string? Comment,
    IReadOnlyDictionary<string, object?> Variables)
{
    /// <summary>Creates an approval decision.</summary>
    /// <param name="decidedBy">Who approved.</param>
    /// <param name="variables">Optional outcome variables.</param>
    /// <returns>The decision.</returns>
    public static HumanTaskDecision Approve(
        string? decidedBy = null, IReadOnlyDictionary<string, object?>? variables = null) =>
        new(HumanTaskOutcome.Approved, decidedBy, null, variables ?? EmptyVariables);

    /// <summary>Creates a rejection decision.</summary>
    /// <param name="decidedBy">Who rejected.</param>
    /// <param name="comment">An optional reason.</param>
    /// <param name="variables">Optional outcome variables.</param>
    /// <returns>The decision.</returns>
    public static HumanTaskDecision Reject(
        string? decidedBy = null,
        string? comment = null,
        IReadOnlyDictionary<string, object?>? variables = null) =>
        new(HumanTaskOutcome.Rejected, decidedBy, comment, variables ?? EmptyVariables);

    /// <summary>An empty variable map.</summary>
    public static IReadOnlyDictionary<string, object?> EmptyVariables { get; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);
}
