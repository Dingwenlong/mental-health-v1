using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.FollowUps;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.Analysis;

public enum ObservationCaseStatus
{
    Open,
    Reviewed,
    Closed
}

public static class ObservationPolicy
{
    public static bool ShouldOpen(ConsultationKind kind, RiskLevel level)
    {
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(level))
        {
            throw new DomainException("OBSERVATION_POLICY_INPUT_INVALID");
        }

        return level is RiskLevel.L2 or RiskLevel.L3 or RiskLevel.Crisis;
    }
}

public sealed class ObservationCase
{
    private ObservationCase()
    {
    }

    private ObservationCase(
        Guid assessmentId,
        Guid sessionId,
        Guid subjectId,
        ConsultationKind consultationKind,
        RiskLevel level,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        AssessmentId = assessmentId;
        SessionId = sessionId;
        SubjectId = subjectId;
        ConsultationKind = consultationKind;
        OriginalLevel = level;
        CurrentLevel = level;
        Status = ObservationCaseStatus.Open;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid AssessmentId { get; private set; }

    public Guid SessionId { get; private set; }

    public Guid SubjectId { get; private set; }

    public ConsultationKind ConsultationKind { get; private set; }

    public RiskLevel OriginalLevel { get; private set; }

    public RiskLevel CurrentLevel { get; private set; }

    public ObservationCaseStatus Status { get; private set; }

    public Guid? LatestReviewId { get; private set; }

    public Guid? FollowUpTaskId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static ObservationCase Open(
        RiskAssessment assessment,
        ConsultationKind consultationKind,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        if (!ObservationPolicy.ShouldOpen(consultationKind, assessment.Level))
        {
            throw new DomainException("OBSERVATION_NOT_REQUIRED");
        }

        return new ObservationCase(
            assessment.Id,
            assessment.SessionId,
            assessment.SubjectId,
            consultationKind,
            assessment.Level,
            now);
    }

    public void ApplyReview(ClinicalReview review)
    {
        ArgumentNullException.ThrowIfNull(review);
        if (review.ObservationCaseId != Id || review.AssessmentId != AssessmentId)
        {
            throw new DomainException("CLINICAL_REVIEW_REFERENCE_INVALID");
        }

        CurrentLevel = review.ReviewedLevel;
        LatestReviewId = review.Id;
        Status = ObservationCaseStatus.Reviewed;
        UpdatedAt = review.ReviewedAt;
    }

    public void LinkFollowUp(FollowUpTask task, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (task.AssessmentId != AssessmentId || task.SubjectId != SubjectId)
        {
            throw new DomainException("FOLLOW_UP_REFERENCE_INVALID");
        }

        if (FollowUpTaskId is { } existing && existing != task.Id)
        {
            throw new DomainException("FOLLOW_UP_ALREADY_LINKED");
        }

        FollowUpTaskId = task.Id;
        UpdatedAt = now;
    }
}
