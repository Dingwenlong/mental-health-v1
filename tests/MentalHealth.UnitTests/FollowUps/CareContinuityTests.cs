using MentalHealth.Domain.Care;
using MentalHealth.Domain.FollowUps;
using MentalHealth.Domain.Shared;

namespace MentalHealth.UnitTests.FollowUps;

public sealed class CareContinuityTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Daily_entry_validates_values_and_keeps_identity_when_edited()
    {
        var entry = DailyCheckIn.Create(Guid.NewGuid(), CareDate.Today(Now), 3, 7.5m, "今天出去散步了。", Now);
        var id = entry.Id;
        entry.Update(4, 8, null, Now.AddMinutes(1));
        Assert.Equal(id, entry.Id);
        Assert.Null(entry.Note);
        Assert.Throws<DomainException>(() => entry.Update(0, 8, null, Now));
        Assert.Throws<DomainException>(() => entry.Update(3, 25, null, Now));
        Assert.Throws<DomainException>(() => DailyCheckIn.Create(Guid.NewGuid(), CareDate.Today(Now).AddDays(1), 3, 8, null, Now));
        Assert.Throws<DomainException>(() => entry.Update(3, 8, new string('x', 501), Now));
    }

    [Fact]
    public void Plan_is_immutable_after_publish_and_feedback_does_not_complete_follow_up()
    {
        var followUp = FollowUpTask.Schedule(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(3), Now);
        var plan = CarePlan.Create(followUp.SubjectId, followUp.Id, followUp.AssigneeId!.Value, "这周的安排", "request-1", Now);
        plan.ReplaceDraft("这周的安排", [new CareTaskInput("CheckIn", null, CareDate.Today(Now).AddDays(1))], Now);
        plan.Publish(Now);
        Assert.Throws<DomainException>(() => plan.ReplaceDraft("改动", [], Now));
        var task = Assert.Single(plan.Tasks);
        plan.RecordFeedback(task.Id, "Done", "已经记录。", true, Now);
        plan.RecordFeedback(task.Id, "Done", "已经记录。", true, Now);
        Assert.Equal(CarePlanStatus.Completed, plan.Status);
        Assert.Equal(FollowUpStatus.Scheduled, followUp.Status);
        Assert.Throws<DomainException>(() => plan.RecordFeedback(task.Id, "Skipped", null, true, Now));
    }

    [Fact]
    public void Draft_and_cancelled_plans_reject_feedback_and_require_acknowledgement()
    {
        var plan = CarePlan.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "练习安排", "request-2", Now);
        plan.ReplaceDraft("练习安排", [new CareTaskInput("Exercise", "grounding", CareDate.Today(Now))], Now);
        var task = Assert.Single(plan.Tasks);
        Assert.Throws<DomainException>(() => plan.RecordFeedback(task.Id, "Done", null, true, Now));
        plan.Publish(Now);
        Assert.Throws<DomainException>(() => plan.RecordFeedback(task.Id, "Done", null, false, Now));
        plan.Cancel(Now);
        Assert.Throws<DomainException>(() => plan.RecordFeedback(task.Id, "Done", null, true, Now));
    }

    [Fact]
    public void Sharing_tracks_assignment_generation_even_when_doctor_is_reassigned_back()
    {
        var doctor = Guid.NewGuid();
        var task = FollowUpTask.Schedule(Guid.NewGuid(), Guid.NewGuid(), doctor, Now.AddDays(2), Now);
        var grant = SharingGrant.Create(task.SubjectId, task.Id, doctor, task.AssignmentVersion, Now);
        Assert.True(grant.Matches(task));
        task.Reschedule(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(3), "调整医生", Now);
        Assert.False(grant.Matches(task));
        task.Reschedule(doctor, Guid.NewGuid(), Now.AddDays(4), "恢复安排", Now);
        Assert.False(grant.Matches(task));
    }
}
