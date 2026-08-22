using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.FollowUps;

public sealed record FollowUpScheduledDomainEvent(
    Guid EventId,
    Guid FollowUpTaskId,
    Guid AssigneeId,
    DateTimeOffset DueAt,
    DateTimeOffset OccurredAt) : IDomainEvent;
