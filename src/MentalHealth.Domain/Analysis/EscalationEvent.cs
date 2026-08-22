using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.Analysis;

public sealed record EscalationRequestedDomainEvent(
    Guid EventId,
    Guid SessionId,
    Guid SubjectId,
    string RuleId,
    DateTimeOffset OccurredAt) : IDomainEvent;
