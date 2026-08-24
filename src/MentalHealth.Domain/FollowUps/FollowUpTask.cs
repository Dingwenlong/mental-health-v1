using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.FollowUps;

public sealed class FollowUpTask : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private FollowUpTask()
    {
    }

    private FollowUpTask(Guid subjectId, Guid assessmentId, DateTimeOffset proposedAt)
    {
        if (subjectId == Guid.Empty || assessmentId == Guid.Empty)
        {
            throw new DomainException("FOLLOW_UP_REFERENCE_INVALID");
        }

        Id = Guid.NewGuid();
        SubjectId = subjectId;
        AssessmentId = assessmentId;
        ProposedAt = proposedAt;
        Status = FollowUpStatus.Proposed;
        _domainEvents.Add(new FollowUpProposedDomainEvent(
            Guid.NewGuid(),
            Id,
            subjectId,
            assessmentId,
            proposedAt));
    }

    public Guid Id { get; private set; }

    public Guid SubjectId { get; private set; }

    public Guid AssessmentId { get; private set; }

    public Guid? AssigneeId { get; private set; }

    public Guid? AvailabilitySlotId { get; private set; }

    public FollowUpStatus Status { get; private set; }

    public DateTimeOffset ProposedAt { get; private set; }

    public DateTimeOffset? ScheduledAt { get; private set; }

    public DateTimeOffset? DueAt { get; private set; }

    public DateTimeOffset? Deadline { get; private set; }

    public string? ConflictCode { get; private set; }

    public DateTimeOffset? BecameDueAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? OverdueAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public static FollowUpTask Propose(
        Guid subjectId,
        Guid assessmentId,
        DateTimeOffset now) => new(subjectId, assessmentId, now);

    public static FollowUpTask Schedule(
        Guid subjectId,
        Guid assessmentId,
        Guid assigneeId,
        DateTimeOffset dueAt,
        DateTimeOffset now)
    {
        var task = Propose(subjectId, assessmentId, now);
        task.Schedule(assigneeId, dueAt, now);
        return task;
    }

    public static FollowUpTask FromProposal(
        Guid subjectId,
        Guid assessmentId,
        FollowUpProposal proposal,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (!proposal.IsRequired || proposal.Deadline is not { } deadline)
        {
            throw new DomainException("FOLLOW_UP_NOT_REQUIRED");
        }

        var task = Propose(subjectId, assessmentId, now);
        task.Deadline = deadline;
        if (!proposal.IsScheduled)
        {
            if (string.IsNullOrWhiteSpace(proposal.ConflictCode))
            {
                throw new DomainException("FOLLOW_UP_PROPOSAL_INVALID");
            }

            task.ConflictCode = proposal.ConflictCode;
            return task;
        }

        if (proposal.AvailabilitySlotId is not { } availabilitySlotId
            || proposal.PractitionerId is not { } practitionerId
            || proposal.DueAt is not { } dueAt)
        {
            throw new DomainException("FOLLOW_UP_PROPOSAL_INVALID");
        }

        task.AvailabilitySlotId = availabilitySlotId;
        task.Schedule(practitionerId, dueAt, now);
        return task;
    }

    public void Schedule(
        Guid assigneeId,
        DateTimeOffset dueAt,
        DateTimeOffset now)
    {
        EnsureStatus(FollowUpStatus.Proposed);

        if (assigneeId == Guid.Empty
            || dueAt <= now
            || (Deadline is { } deadline && dueAt > deadline))
        {
            throw new DomainException("FOLLOW_UP_DUE_AT_INVALID");
        }

        AssigneeId = assigneeId;
        DueAt = dueAt;
        ScheduledAt = now;
        Status = FollowUpStatus.Scheduled;
        ConflictCode = null;
        _domainEvents.Add(new FollowUpScheduledDomainEvent(
            Guid.NewGuid(),
            Id,
            assigneeId,
            dueAt,
            now));
    }

    public void MarkDue(DateTimeOffset now)
    {
        EnsureStatus(FollowUpStatus.Scheduled);

        if (now < DueAt)
        {
            throw new DomainException("FOLLOW_UP_NOT_DUE");
        }

        Status = FollowUpStatus.Due;
        BecameDueAt = now;
    }

    public void Complete(DateTimeOffset now)
    {
        EnsureStatus(FollowUpStatus.Due);
        Status = FollowUpStatus.Completed;
        CompletedAt = now;
    }

    public void Reschedule(
        Guid assigneeId,
        Guid availabilitySlotId,
        DateTimeOffset dueAt,
        string reason,
        DateTimeOffset now)
    {
        _ = NormalizeReason(reason);
        if (availabilitySlotId == Guid.Empty
            || Status is FollowUpStatus.Completed or FollowUpStatus.Cancelled)
        {
            throw new DomainException("INVALID_FOLLOW_UP_STATE");
        }

        if (Status == FollowUpStatus.Proposed)
        {
            AvailabilitySlotId = availabilitySlotId;
            Schedule(assigneeId, dueAt, now);
            return;
        }

        if (assigneeId == Guid.Empty
            || dueAt <= now
            || (Deadline is { } deadline && dueAt > deadline))
        {
            throw new DomainException("FOLLOW_UP_DUE_AT_INVALID");
        }

        AssigneeId = assigneeId;
        AvailabilitySlotId = availabilitySlotId;
        DueAt = dueAt;
        ScheduledAt = now;
        BecameDueAt = null;
        CompletedAt = null;
        OverdueAt = null;
        ConflictCode = null;
        Status = FollowUpStatus.Scheduled;
        _domainEvents.Add(new FollowUpScheduledDomainEvent(
            Guid.NewGuid(),
            Id,
            assigneeId,
            dueAt,
            now));
    }

    public void Cancel(string reason, DateTimeOffset now)
    {
        _ = NormalizeReason(reason);
        if (Status is FollowUpStatus.Completed or FollowUpStatus.Cancelled)
        {
            throw new DomainException("INVALID_FOLLOW_UP_STATE");
        }

        Status = FollowUpStatus.Cancelled;
        CancelledAt = now;
    }

    public void Complete(string reason, DateTimeOffset now)
    {
        _ = NormalizeReason(reason);
        if (Status is not (FollowUpStatus.Scheduled
            or FollowUpStatus.Due
            or FollowUpStatus.Overdue))
        {
            throw new DomainException("INVALID_FOLLOW_UP_STATE");
        }

        Status = FollowUpStatus.Completed;
        CompletedAt = now;
    }

    public void MarkOverdue(DateTimeOffset now)
    {
        EnsureStatus(FollowUpStatus.Due);

        if (now <= DueAt)
        {
            throw new DomainException("FOLLOW_UP_NOT_OVERDUE");
        }

        Status = FollowUpStatus.Overdue;
        OverdueAt = now;
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    private void EnsureStatus(FollowUpStatus expectedStatus)
    {
        if (Status != expectedStatus)
        {
            throw new DomainException("INVALID_FOLLOW_UP_STATE");
        }
    }

    private static string NormalizeReason(string reason)
    {
        var normalized = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 1000)
        {
            throw new DomainException("FOLLOW_UP_REASON_REQUIRED");
        }

        return normalized;
    }
}
