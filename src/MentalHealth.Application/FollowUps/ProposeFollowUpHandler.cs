using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.FollowUps;

namespace MentalHealth.Application.FollowUps;

public interface IFollowUpRepository
{
    Task<FollowUpTask?> FindFollowUpByAssessmentAsync(
        Guid assessmentId,
        CancellationToken cancellationToken);

    Task<FollowUpTask?> FindFollowUpAsync(
        Guid taskId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FollowUpCandidate>> ListCandidatesAsync(
        DateTimeOffset now,
        DateTimeOffset deadline,
        CancellationToken cancellationToken);

    Task<FollowUpCandidate?> FindCandidateAsync(
        Guid availabilitySlotId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FollowUpTask>> ListForSubjectAsync(
        Guid subjectId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FollowUpTask>> ListForAssigneeAsync(
        Guid practitionerId,
        CancellationToken cancellationToken);

    void Add(FollowUpTask task);
}

public sealed class ProposeFollowUpHandler(
    IFollowUpRepository followUps,
    FollowUpScheduler scheduler,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<FollowUpTask?> HandleAsync(
        ObservationCase observationCase,
        RiskAssessment assessment,
        Guid? originalPractitionerId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observationCase);
        ArgumentNullException.ThrowIfNull(assessment);
        var existing = await followUps.FindFollowUpByAssessmentAsync(
            assessment.Id,
            cancellationToken);
        if (existing is not null)
        {
            observationCase.LinkFollowUp(existing, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var now = clock.UtcNow;
        var request = new FollowUpScheduleRequest(
            assessment.Level,
            assessment.IsCrisis,
            originalPractitionerId);
        var deadline = scheduler.GetDeadline(request, now);
        var candidates = deadline is { } exact
            ? await followUps.ListCandidatesAsync(
                now,
                exact,
                cancellationToken)
            : [];
        var proposal = scheduler.Propose(
            request,
            candidates,
            now);
        if (!proposal.IsRequired)
        {
            return null;
        }

        var task = FollowUpTask.FromProposal(
            assessment.SubjectId,
            assessment.Id,
            proposal,
            now);
        followUps.Add(task);
        observationCase.LinkFollowUp(task, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return task;
    }

}
