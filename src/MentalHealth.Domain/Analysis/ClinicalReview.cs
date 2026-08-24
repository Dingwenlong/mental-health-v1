using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.Analysis;

public sealed class ClinicalReview
{
    private ClinicalReview()
    {
    }

    private ClinicalReview(
        Guid observationCaseId,
        Guid assessmentId,
        Guid reviewerId,
        RiskLevel reviewedLevel,
        string reason,
        DateTimeOffset reviewedAt)
    {
        Id = Guid.NewGuid();
        ObservationCaseId = observationCaseId;
        AssessmentId = assessmentId;
        ReviewerId = reviewerId;
        ReviewedLevel = reviewedLevel;
        Reason = reason;
        ReviewedAt = reviewedAt;
    }

    public Guid Id { get; private set; }

    public Guid ObservationCaseId { get; private set; }

    public Guid AssessmentId { get; private set; }

    public Guid ReviewerId { get; private set; }

    public RiskLevel ReviewedLevel { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public DateTimeOffset ReviewedAt { get; private set; }

    public static ClinicalReview Create(
        Guid observationCaseId,
        Guid assessmentId,
        Guid reviewerId,
        RiskLevel reviewedLevel,
        string reason,
        DateTimeOffset reviewedAt)
    {
        if (observationCaseId == Guid.Empty
            || assessmentId == Guid.Empty
            || reviewerId == Guid.Empty
            || !Enum.IsDefined(reviewedLevel))
        {
            throw new DomainException("CLINICAL_REVIEW_VALUE_INVALID");
        }

        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason)
            || normalizedReason.Length > 1000)
        {
            throw new DomainException("REVIEW_REASON_REQUIRED");
        }

        return new ClinicalReview(
            observationCaseId,
            assessmentId,
            reviewerId,
            reviewedLevel,
            normalizedReason,
            reviewedAt);
    }
}
