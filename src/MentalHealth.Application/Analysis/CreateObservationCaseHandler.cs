using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Application.Consultations;
using MentalHealth.Application.FollowUps;
using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Application.Analysis;

public interface IObservationCaseRepository
{
    Task<ObservationCase?> FindObservationByAssessmentAsync(
        Guid assessmentId,
        CancellationToken cancellationToken);

    Task<ObservationCase?> FindObservationCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ObservationCase>> ListCasesAsync(
        RiskLevel? level,
        ObservationCaseStatus? status,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ClinicalReview>> ListReviewsAsync(
        Guid caseId,
        CancellationToken cancellationToken);

    void Add(ObservationCase observationCase);

    void Add(ClinicalReview review);
}

public sealed class CreateObservationCaseHandler(
    IObservationCaseRepository observationCases,
    IConsultationRepository consultations,
    ProposeFollowUpHandler followUps,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<ObservationCase?> HandleAsync(
        RiskAssessment assessment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        var session = await consultations.FindAsync(
            assessment.SessionId,
            cancellationToken)
            ?? throw new DomainException("SESSION_NOT_FOUND");
        if (!ObservationPolicy.ShouldOpen(session.Kind, assessment.Level))
        {
            return null;
        }

        var observationCase = await observationCases.FindObservationByAssessmentAsync(
            assessment.Id,
            cancellationToken);
        if (observationCase is null)
        {
            observationCase = ObservationCase.Open(assessment, session.Kind, clock.UtcNow);
            observationCases.Add(observationCase);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await followUps.HandleAsync(
            observationCase,
            assessment,
            session.AssignedPractitionerId,
            cancellationToken);
        return observationCase;
    }
}
