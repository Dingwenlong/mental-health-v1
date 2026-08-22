using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.FollowUps;

public sealed record FollowUpProposedDomainEvent(
    Guid EventId,
    Guid FollowUpTaskId,
    Guid SubjectId,
    Guid AssessmentId,
    DateTimeOffset OccurredAt) : IDomainEvent;
