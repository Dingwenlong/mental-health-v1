using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Application.Audit;
using MentalHealth.Application.Consultations;
using MentalHealth.Domain.Audit;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.FollowUps;
using MentalHealth.Domain.Shared;
using MentalHealth.Application.Security;

namespace MentalHealth.Application.FollowUps;

public sealed class RescheduleFollowUpHandler(
    IFollowUpRepository followUps,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public Task<FollowUpTask> RescheduleAsync(
        ConsultationActor actor,
        Guid taskId,
        Guid availabilitySlotId,
        string reason,
        CancellationToken cancellationToken) =>
        ScheduleAsync(
            actor,
            taskId,
            availabilitySlotId,
            reason,
            "FollowUpRescheduled",
            cancellationToken);

    public Task<FollowUpTask> ReassignAsync(
        ConsultationActor actor,
        Guid taskId,
        Guid availabilitySlotId,
        string reason,
        CancellationToken cancellationToken) =>
        ScheduleAsync(
            actor,
            taskId,
            availabilitySlotId,
            reason,
            "FollowUpReassigned",
            cancellationToken);

    public async Task<FollowUpTask> CancelAsync(
        ConsultationActor actor,
        Guid taskId,
        string reason,
        CancellationToken cancellationToken)
    {
        _ = actor.RequireDoctor();
        var task = await RequireTaskAsync(taskId, cancellationToken);
        task.Cancel(reason, clock.UtcNow);
        AddAudit(actor.UserId, "FollowUpCancelled", task.Id, reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return task;
    }

    public async Task<FollowUpTask> CompleteAsync(
        ConsultationActor actor,
        Guid taskId,
        string reason,
        CancellationToken cancellationToken)
    {
        _ = actor.RequireDoctor();
        var task = await RequireTaskAsync(taskId, cancellationToken);
        task.Complete(reason, clock.UtcNow);
        AddAudit(actor.UserId, "FollowUpCompleted", task.Id, reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return task;
    }

    private async Task<FollowUpTask> ScheduleAsync(
        ConsultationActor actor,
        Guid taskId,
        Guid availabilitySlotId,
        string reason,
        string action,
        CancellationToken cancellationToken)
    {
        _ = actor.RequireDoctor();
        var task = await RequireTaskAsync(taskId, cancellationToken);
        var candidate = await followUps.FindCandidateAsync(
            availabilitySlotId,
            cancellationToken)
            ?? throw new DomainException("FOLLOW_UP_SLOT_NOT_QUALIFIED");
        if (!candidate.Active || candidate.Role != PractitionerRole.Doctor)
        {
            throw new DomainException("FOLLOW_UP_SLOT_NOT_QUALIFIED");
        }

        task.Reschedule(
            candidate.PractitionerId,
            candidate.AvailabilitySlotId,
            candidate.StartAt,
            reason,
            clock.UtcNow);
        AddAudit(actor.UserId, action, task.Id, reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return task;
    }

    private async Task<FollowUpTask> RequireTaskAsync(
        Guid taskId,
        CancellationToken cancellationToken) =>
        await followUps.FindFollowUpAsync(taskId, cancellationToken)
            ?? throw new DomainException("FOLLOW_UP_NOT_FOUND");

    private void AddAudit(
        Guid actorUserId,
        string action,
        Guid taskId,
        string reason) =>
        auditTrail.Add(AuditEvent.Create(
            actorUserId,
            action,
            "FollowUpTask",
            taskId,
            clock.UtcNow,
            reason));
}

public sealed class FollowUpQueryHandler(IFollowUpRepository followUps)
{
    public Task<IReadOnlyList<FollowUpTask>> HandleAsync(
        ConsultationActor actor,
        CancellationToken cancellationToken)
    {
        if (actor.Roles.Contains(
            AppRoles.User,
            StringComparer.Ordinal)
            && actor.SubjectId is { } subjectId)
        {
            return followUps.ListForSubjectAsync(subjectId, cancellationToken);
        }

        var practitionerId = actor.RequireDoctor();
        return followUps.ListForAssigneeAsync(practitionerId, cancellationToken);
    }
}
