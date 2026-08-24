using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.Consultations;

namespace MentalHealth.Domain.FollowUps;

public sealed record FollowUpScheduleRequest(
    RiskLevel Level,
    bool IsCrisis,
    Guid? OriginalPractitionerId);

public sealed record FollowUpCandidate(
    Guid AvailabilitySlotId,
    Guid PractitionerId,
    PractitionerRole Role,
    bool Active,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    int IncompleteTaskCount);

public sealed record FollowUpProposal(
    bool IsRequired,
    bool IsScheduled,
    Guid? AvailabilitySlotId,
    Guid? PractitionerId,
    DateTimeOffset? DueAt,
    DateTimeOffset? Deadline,
    string? ConflictCode)
{
    public static FollowUpProposal NotRequired() => new(
        false,
        false,
        null,
        null,
        null,
        null,
        null);

    public static FollowUpProposal Conflict(
        DateTimeOffset deadline,
        string code) => new(
            true,
            false,
            null,
            null,
            null,
            deadline,
            code);

    public static FollowUpProposal Scheduled(
        FollowUpCandidate candidate,
        DateTimeOffset deadline) => new(
            true,
            true,
            candidate.AvailabilitySlotId,
            candidate.PractitionerId,
            candidate.StartAt,
            deadline,
            null);
}
