namespace FactoryOS.Plugins.Workflow.Approvals.Domain;

/// <summary>A comment left on an approval.</summary>
/// <param name="Id">The comment id.</param>
/// <param name="Author">Who wrote it.</param>
/// <param name="Text">The comment text.</param>
/// <param name="CreatedOnUtc">When it was written.</param>
public sealed record ApprovalComment(Guid Id, string Author, string Text, DateTimeOffset CreatedOnUtc);

/// <summary>The deadline policy of an approval: a fixed instant or a duration from creation.</summary>
public sealed record ApprovalDeadline
{
    private ApprovalDeadline(TimeSpan? duration, DateTimeOffset? dueUtc)
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
    public static ApprovalDeadline In(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A deadline duration must be positive.");
        }

        return new ApprovalDeadline(duration, null);
    }

    /// <summary>Creates a deadline at a fixed instant.</summary>
    /// <param name="dueUtc">The due instant.</param>
    /// <returns>The deadline.</returns>
    public static ApprovalDeadline At(DateTimeOffset dueUtc) => new(null, dueUtc);

    /// <summary>Resolves the deadline to a concrete instant given when the approval was created.</summary>
    /// <param name="createdOnUtc">When the approval was created.</param>
    /// <returns>The due instant.</returns>
    public DateTimeOffset Resolve(DateTimeOffset createdOnUtc) => DueUtc ?? createdOnUtc + Duration!.Value;
}

/// <summary>A reminder policy: nudge the pending approvers a fixed duration before the deadline.</summary>
/// <param name="Before">How long before the deadline to fire.</param>
public sealed record ApprovalReminder(TimeSpan Before)
{
    /// <summary>Resolves the reminder to the instant it should fire.</summary>
    /// <param name="deadlineUtc">The approval's deadline.</param>
    /// <returns>The fire instant.</returns>
    public DateTimeOffset ResolveFireInstant(DateTimeOffset deadlineUtc) => deadlineUtc - Before;
}

/// <summary>An escalation policy: after the deadline, hand the pending steps to another approver.</summary>
/// <param name="After">How long after the deadline to escalate.</param>
/// <param name="To">Who to escalate the pending steps to.</param>
public sealed record ApprovalEscalation(TimeSpan After, ApprovalAssignment To)
{
    /// <summary>Resolves the escalation to the instant it becomes due.</summary>
    /// <param name="deadlineUtc">The approval's deadline.</param>
    /// <returns>The due instant.</returns>
    public DateTimeOffset ResolveDueInstant(DateTimeOffset deadlineUtc) => deadlineUtc + After;
}
