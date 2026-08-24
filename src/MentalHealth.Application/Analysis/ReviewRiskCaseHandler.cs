using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Application.Audit;
using MentalHealth.Application.Consultations;
using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.Audit;
using MentalHealth.Domain.Shared;
using MentalHealth.Application.FollowUps;
using MentalHealth.Domain.FollowUps;

namespace MentalHealth.Application.Analysis;

public sealed record RiskCaseDetails(
    ObservationCase ObservationCase,
    RiskAssessment Assessment,
    IReadOnlyList<ClinicalReview> Reviews,
    FollowUpTask? FollowUp);

public sealed class ReviewRiskCaseHandler(
    IObservationCaseRepository observationCases,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<ClinicalReview> HandleAsync(
        ConsultationActor actor,
        Guid caseId,
        RiskLevel reviewedLevel,
        string reason,
        CancellationToken cancellationToken)
    {
        var reviewerId = actor.RequireDoctor();
        var observationCase = await observationCases.FindObservationCaseAsync(
            caseId,
            cancellationToken)
            ?? throw new DomainException("RISK_CASE_NOT_FOUND");
        var review = ClinicalReview.Create(
            observationCase.Id,
            observationCase.AssessmentId,
            reviewerId,
            reviewedLevel,
            reason,
            clock.UtcNow);
        observationCases.Add(review);
        observationCase.ApplyReview(review);
        auditTrail.Add(AuditEvent.Create(
            actor.UserId,
            "RiskCaseReviewed",
            "ObservationCase",
            observationCase.Id,
            clock.UtcNow,
            review.Reason));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return review;
    }
}

public sealed class RiskCaseQueryHandler(
    IObservationCaseRepository observationCases,
    IRiskAssessmentRepository assessments,
    IFollowUpRepository followUps)
{
    public async Task<IReadOnlyList<RiskCaseDetails>> ListAsync(
        ConsultationActor actor,
        RiskLevel? level,
        ObservationCaseStatus? status,
        bool assignedToMe,
        CancellationToken cancellationToken)
    {
        var practitionerId = actor.RequireDoctor();
        var cases = await observationCases.ListCasesAsync(
            level,
            status,
            cancellationToken);
        var details = new List<RiskCaseDetails>(cases.Count);
        foreach (var observationCase in cases)
        {
            details.Add(await LoadAsync(observationCase, cancellationToken));
        }

        return assignedToMe
            ? details
                .Where(item => item.FollowUp?.AssigneeId == practitionerId)
                .ToArray()
            : details;
    }

    public async Task<RiskCaseDetails> GetAsync(
        ConsultationActor actor,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        _ = actor.RequireDoctor();
        var observationCase = await observationCases.FindObservationCaseAsync(
            caseId,
            cancellationToken)
            ?? throw new DomainException("RISK_CASE_NOT_FOUND");
        return await LoadAsync(observationCase, cancellationToken);
    }

    private async Task<RiskCaseDetails> LoadAsync(
        ObservationCase observationCase,
        CancellationToken cancellationToken)
    {
        var assessment = await assessments.FindAssessmentByIdAsync(
            observationCase.AssessmentId,
            cancellationToken)
            ?? throw new DomainException("RESULT_NOT_FOUND");
        var reviews = await observationCases.ListReviewsAsync(
            observationCase.Id,
            cancellationToken);
        var followUp = await followUps.FindFollowUpByAssessmentAsync(
            observationCase.AssessmentId,
            cancellationToken);
        return new RiskCaseDetails(observationCase, assessment, reviews, followUp);
    }
}
