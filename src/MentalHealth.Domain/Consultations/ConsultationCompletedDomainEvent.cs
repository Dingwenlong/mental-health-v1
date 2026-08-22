using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.Consultations;

public sealed record ConsultationCompletedDomainEvent(
    Guid EventId,
    Guid SessionId,
    Guid SubjectId,
    DateTimeOffset OccurredAt) : IDomainEvent;
