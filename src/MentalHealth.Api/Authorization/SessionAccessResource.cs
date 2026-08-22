namespace MentalHealth.Api.Authorization;

public sealed record SessionAccessResource(
    Guid SubjectId,
    Guid AssignedPractitionerId,
    Guid RiskReviewerId,
    bool RequiresDoctorReview);
