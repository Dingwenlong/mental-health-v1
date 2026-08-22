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

    public FollowUpStatus Status { get; private set; }

    public DateTimeOffset ProposedAt { get; private set; }

    public DateTimeOffset? ScheduledAt { get; private set; }

    public DateTimeOffset? DueAt { get; private set; }

    public DateTimeOffset? BecameDueAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? OverdueAt { get; private set; }

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

    public void Schedule(
        Guid assigneeId,
        DateTimeOffset dueAt,
        DateTimeOffset now)
    {
        EnsureStatus(FollowUpStatus.Proposed);

        if (dueAt <= now)
        {
            throw new DomainException("FOLLOW_UP_DUE_AT_INVALID");
        }

        AssigneeId = assigneeId;
        DueAt = dueAt;
        ScheduledAt = now;
        Status = FollowUpStatus.Scheduled;
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
}
