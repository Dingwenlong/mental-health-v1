using MentalHealth.Domain.FollowUps;
using MentalHealth.Domain.Shared;

namespace MentalHealth.UnitTests.FollowUps;

public sealed class FollowUpTaskTests
{
    private static readonly Guid SubjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AssessmentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AssigneeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-08-22T09:00:00+08:00");
    private static readonly DateTimeOffset DueAt = Now.AddHours(72);

    [Fact]
    public void Propose_creates_a_proposed_task_and_event()
    {
        var task = FollowUpTask.Propose(SubjectId, AssessmentId, Now);

        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.Equal(SubjectId, task.SubjectId);
        Assert.Equal(AssessmentId, task.AssessmentId);
        Assert.Equal(FollowUpStatus.Proposed, task.Status);
        Assert.Equal(Now, task.ProposedAt);
        Assert.Null(task.AssigneeId);
        Assert.Null(task.DueAt);

        var domainEvent = Assert.Single(
            task.DomainEvents.OfType<FollowUpProposedDomainEvent>());
        Assert.NotEqual(Guid.Empty, domainEvent.EventId);
        Assert.Equal(task.Id, domainEvent.FollowUpTaskId);
        Assert.Equal(SubjectId, domainEvent.SubjectId);
        Assert.Equal(AssessmentId, domainEvent.AssessmentId);
        Assert.Equal(Now, domainEvent.OccurredAt);
    }

    [Fact]
    public void Schedule_factory_creates_a_scheduled_task()
    {
        var task = FollowUpTask.Schedule(
            SubjectId,
            AssessmentId,
            AssigneeId,
            DueAt,
            Now);

        Assert.Equal(FollowUpStatus.Scheduled, task.Status);
        Assert.Equal(AssigneeId, task.AssigneeId);
        Assert.Equal(DueAt, task.DueAt);
        Assert.Equal(Now, task.ScheduledAt);
        Assert.Single(task.DomainEvents.OfType<FollowUpProposedDomainEvent>());
        var scheduled = Assert.Single(
            task.DomainEvents.OfType<FollowUpScheduledDomainEvent>());
        Assert.Equal(task.Id, scheduled.FollowUpTaskId);
        Assert.Equal(AssigneeId, scheduled.AssigneeId);
        Assert.Equal(DueAt, scheduled.DueAt);
        Assert.Equal(Now, scheduled.OccurredAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Schedule_requires_due_time_after_scheduling_time(int dueOffsetMinutes)
    {
        var task = FollowUpTask.Propose(SubjectId, AssessmentId, Now);

        var exception = Assert.Throws<DomainException>(
            () => task.Schedule(AssigneeId, Now.AddMinutes(dueOffsetMinutes), Now));

        Assert.Equal("FOLLOW_UP_DUE_AT_INVALID", exception.Code);
        Assert.Equal(FollowUpStatus.Proposed, task.Status);
    }

    [Theory]
    [InlineData(FollowUpStatus.Scheduled)]
    [InlineData(FollowUpStatus.Due)]
    [InlineData(FollowUpStatus.Completed)]
    [InlineData(FollowUpStatus.Overdue)]
    public void Schedule_rejects_every_state_except_proposed(FollowUpStatus status)
    {
        var task = CreateInState(status);

        var exception = Assert.Throws<DomainException>(
            () => task.Schedule(AssigneeId, DueAt.AddDays(1), Now));

        Assert.Equal("INVALID_FOLLOW_UP_STATE", exception.Code);
    }

    [Fact]
    public void MarkDue_moves_scheduled_task_to_due_at_deadline()
    {
        var task = CreateInState(FollowUpStatus.Scheduled);

        task.MarkDue(DueAt);

        Assert.Equal(FollowUpStatus.Due, task.Status);
        Assert.Equal(DueAt, task.BecameDueAt);
    }

    [Fact]
    public void MarkDue_rejects_time_before_deadline()
    {
        var task = CreateInState(FollowUpStatus.Scheduled);

        var exception = Assert.Throws<DomainException>(
            () => task.MarkDue(DueAt.AddTicks(-1)));

        Assert.Equal("FOLLOW_UP_NOT_DUE", exception.Code);
        Assert.Equal(FollowUpStatus.Scheduled, task.Status);
    }

    [Theory]
    [InlineData(FollowUpStatus.Proposed)]
    [InlineData(FollowUpStatus.Due)]
    [InlineData(FollowUpStatus.Completed)]
    [InlineData(FollowUpStatus.Overdue)]
    public void MarkDue_rejects_every_state_except_scheduled(FollowUpStatus status)
    {
        var task = CreateInState(status);

        var exception = Assert.Throws<DomainException>(() => task.MarkDue(DueAt));

        Assert.Equal("INVALID_FOLLOW_UP_STATE", exception.Code);
    }

    [Fact]
    public void Complete_moves_due_task_to_completed()
    {
        var task = CreateInState(FollowUpStatus.Due);
        var completedAt = DueAt.AddMinutes(15);

        task.Complete(completedAt);

        Assert.Equal(FollowUpStatus.Completed, task.Status);
        Assert.Equal(completedAt, task.CompletedAt);
    }

    [Theory]
    [InlineData(FollowUpStatus.Proposed)]
    [InlineData(FollowUpStatus.Scheduled)]
    [InlineData(FollowUpStatus.Completed)]
    [InlineData(FollowUpStatus.Overdue)]
    public void Complete_rejects_every_state_except_due(FollowUpStatus status)
    {
        var task = CreateInState(status);

        var exception = Assert.Throws<DomainException>(() => task.Complete(DueAt));

        Assert.Equal("INVALID_FOLLOW_UP_STATE", exception.Code);
    }

    [Fact]
    public void MarkOverdue_moves_due_task_to_overdue_after_deadline()
    {
        var task = CreateInState(FollowUpStatus.Due);
        var overdueAt = DueAt.AddTicks(1);

        task.MarkOverdue(overdueAt);

        Assert.Equal(FollowUpStatus.Overdue, task.Status);
        Assert.Equal(overdueAt, task.OverdueAt);
    }

    [Fact]
    public void MarkOverdue_rejects_deadline_instant()
    {
        var task = CreateInState(FollowUpStatus.Due);

        var exception = Assert.Throws<DomainException>(() => task.MarkOverdue(DueAt));

        Assert.Equal("FOLLOW_UP_NOT_OVERDUE", exception.Code);
        Assert.Equal(FollowUpStatus.Due, task.Status);
    }

    [Theory]
    [InlineData(FollowUpStatus.Proposed)]
    [InlineData(FollowUpStatus.Scheduled)]
    [InlineData(FollowUpStatus.Completed)]
    [InlineData(FollowUpStatus.Overdue)]
    public void MarkOverdue_rejects_every_state_except_due(FollowUpStatus status)
    {
        var task = CreateInState(status);

        var exception = Assert.Throws<DomainException>(
            () => task.MarkOverdue(DueAt.AddMinutes(1)));

        Assert.Equal("INVALID_FOLLOW_UP_STATE", exception.Code);
    }

    private static FollowUpTask CreateInState(FollowUpStatus status)
    {
        var task = FollowUpTask.Propose(SubjectId, AssessmentId, Now);

        switch (status)
        {
            case FollowUpStatus.Proposed:
                break;
            case FollowUpStatus.Scheduled:
                MoveToScheduled(task);
                break;
            case FollowUpStatus.Due:
                MoveToDue(task);
                break;
            case FollowUpStatus.Completed:
                MoveToDue(task);
                task.Complete(DueAt.AddMinutes(1));
                break;
            case FollowUpStatus.Overdue:
                MoveToDue(task);
                task.MarkOverdue(DueAt.AddMinutes(1));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        return task;
    }

    private static void MoveToScheduled(FollowUpTask task)
    {
        task.Schedule(AssigneeId, DueAt, Now);
    }

    private static void MoveToDue(FollowUpTask task)
    {
        MoveToScheduled(task);
        task.MarkDue(DueAt);
    }
}
