using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.FollowUps;
using MentalHealth.Domain.Shared;

namespace MentalHealth.UnitTests.FollowUps;

public sealed class FollowUpSchedulerTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-08-22T09:00:00+08:00");
    private readonly FollowUpScheduler _scheduler = new();

    [Theory]
    [InlineData(ConsultationKind.AiVirtual, RiskLevel.L2, true)]
    [InlineData(ConsultationKind.AiVirtual, RiskLevel.L1, false)]
    [InlineData(ConsultationKind.Human, RiskLevel.L3, true)]
    [InlineData(ConsultationKind.Human, RiskLevel.Crisis, true)]
    [InlineData(ConsultationKind.Human, RiskLevel.L0, false)]
    public void L2_or_higher_creates_observation_case(
        ConsultationKind kind,
        RiskLevel level,
        bool expected)
    {
        Assert.Equal(expected, ObservationPolicy.ShouldOpen(kind, level));
    }

    [Fact]
    public void L3_prefers_original_doctor_before_24_hour_deadline()
    {
        var originalDoctorId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var otherDoctorId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var proposal = _scheduler.Propose(
            new FollowUpScheduleRequest(RiskLevel.L3, false, originalDoctorId),
            [
                Candidate(otherDoctorId, Now.AddHours(2), incomplete: 0),
                Candidate(originalDoctorId, Now.AddHours(20), incomplete: 5)
            ],
            Now);

        Assert.True(proposal.IsScheduled);
        Assert.Equal(originalDoctorId, proposal.PractitionerId);
        Assert.True(proposal.DueAt <= Now.AddHours(24));
        Assert.Equal(Now.AddHours(24), proposal.Deadline);
    }

    [Fact]
    public void Same_start_time_prefers_lowest_load_then_practitioner_id()
    {
        var firstId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var start = Now.AddHours(4);

        var lowestLoad = _scheduler.Propose(
            new FollowUpScheduleRequest(RiskLevel.L2, false, null),
            [Candidate(firstId, start, 3), Candidate(secondId, start, 1)],
            Now);
        var stableTie = _scheduler.Propose(
            new FollowUpScheduleRequest(RiskLevel.L2, false, null),
            [Candidate(secondId, start, 1), Candidate(firstId, start, 1)],
            Now);

        Assert.Equal(secondId, lowestLoad.PractitionerId);
        Assert.Equal(firstId, stableTie.PractitionerId);
    }

    [Fact]
    public void Counselor_inactive_and_after_deadline_slots_are_rejected()
    {
        var proposal = _scheduler.Propose(
            new FollowUpScheduleRequest(RiskLevel.L3, false, null),
            [
                Candidate(Guid.NewGuid(), Now.AddHours(2), 0, PractitionerRole.Counselor),
                Candidate(Guid.NewGuid(), Now.AddHours(3), 0, active: false),
                Candidate(Guid.NewGuid(), Now.AddHours(25), 0)
            ],
            Now);

        Assert.False(proposal.IsScheduled);
        Assert.True(proposal.IsRequired);
        Assert.Equal("NO_QUALIFIED_SLOT_BEFORE_SLA", proposal.ConflictCode);
        Assert.Equal(Now.AddHours(24), proposal.Deadline);
    }

    [Fact]
    public void Crisis_enters_manual_queue_immediately_when_no_slot_exists_now()
    {
        var proposal = _scheduler.Propose(
            new FollowUpScheduleRequest(RiskLevel.Crisis, true, null),
            [Candidate(Guid.NewGuid(), Now.AddMinutes(5), 0)],
            Now);

        Assert.True(proposal.IsRequired);
        Assert.False(proposal.IsScheduled);
        Assert.Equal(Now, proposal.Deadline);
        Assert.Equal("NO_QUALIFIED_SLOT_BEFORE_SLA", proposal.ConflictCode);
    }

    [Fact]
    public void L0_does_not_create_a_follow_up()
    {
        var proposal = _scheduler.Propose(
            new FollowUpScheduleRequest(RiskLevel.L0, false, null),
            [Candidate(Guid.NewGuid(), Now.AddHours(1), 0)],
            Now);

        Assert.False(proposal.IsRequired);
        Assert.False(proposal.IsScheduled);
        Assert.Null(proposal.Deadline);
    }

    [Fact]
    public void Conflict_proposal_stays_in_manual_queue_with_deadline()
    {
        var proposal = FollowUpProposal.Conflict(
            Now.AddHours(24),
            "NO_QUALIFIED_SLOT_BEFORE_SLA");

        var task = FollowUpTask.FromProposal(
            Guid.NewGuid(),
            Guid.NewGuid(),
            proposal,
            Now);

        Assert.Equal(FollowUpStatus.Proposed, task.Status);
        Assert.Equal(Now.AddHours(24), task.Deadline);
        Assert.Equal("NO_QUALIFIED_SLOT_BEFORE_SLA", task.ConflictCode);
        Assert.Null(task.AssigneeId);
    }

    [Fact]
    public void Manual_changes_require_reason_and_cannot_cross_deadline()
    {
        var assigneeId = Guid.NewGuid();
        var task = FollowUpTask.FromProposal(
            Guid.NewGuid(),
            Guid.NewGuid(),
            FollowUpProposal.Scheduled(
                Candidate(assigneeId, Now.AddHours(4), 0),
                Now.AddHours(24)),
            Now);

        Assert.Equal(
            "FOLLOW_UP_REASON_REQUIRED",
            Assert.Throws<DomainException>(() => task.Reschedule(
                assigneeId,
                Guid.NewGuid(),
                Now.AddHours(6),
                " ",
                Now.AddMinutes(1))).Code);
        Assert.Equal(
            "FOLLOW_UP_DUE_AT_INVALID",
            Assert.Throws<DomainException>(() => task.Reschedule(
                assigneeId,
                Guid.NewGuid(),
                Now.AddHours(25),
                "用户要求改期",
                Now.AddMinutes(1))).Code);
        Assert.Equal(
            "FOLLOW_UP_REASON_REQUIRED",
            Assert.Throws<DomainException>(() => task.Cancel("", Now)).Code);
        Assert.Equal(
            "FOLLOW_UP_REASON_REQUIRED",
            Assert.Throws<DomainException>(() => task.Complete("", Now)).Code);
    }

    [Fact]
    public void Cancel_and_complete_keep_distinct_terminal_states()
    {
        var cancelled = ScheduledTask();
        var completed = ScheduledTask();

        cancelled.Cancel("用户要求取消", Now.AddMinutes(1));
        completed.Complete("已经完成回访", Now.AddMinutes(1));

        Assert.Equal(FollowUpStatus.Cancelled, cancelled.Status);
        Assert.Equal(Now.AddMinutes(1), cancelled.CancelledAt);
        Assert.Equal(FollowUpStatus.Completed, completed.Status);
        Assert.Equal(Now.AddMinutes(1), completed.CompletedAt);
    }

    private static FollowUpTask ScheduledTask() => FollowUpTask.FromProposal(
        Guid.NewGuid(),
        Guid.NewGuid(),
        FollowUpProposal.Scheduled(
            Candidate(Guid.NewGuid(), Now.AddHours(4), 0),
            Now.AddHours(24)),
        Now);

    private static FollowUpCandidate Candidate(
        Guid practitionerId,
        DateTimeOffset startAt,
        int incomplete,
        PractitionerRole role = PractitionerRole.Doctor,
        bool active = true) => new(
            Guid.NewGuid(),
            practitionerId,
            role,
            active,
            startAt,
            startAt.AddMinutes(30),
            incomplete);
}
