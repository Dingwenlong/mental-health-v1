using MentalHealth.Domain.Consents;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.Consultations;

public sealed class ConsultationSession
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private ConsultationSession(
        Guid subjectId,
        ConsultationKind kind,
        ConsultationChannel channel)
    {
        Id = Guid.NewGuid();
        SubjectId = subjectId;
        Kind = kind;
        Channel = channel;
    }

    public Guid Id { get; }

    public Guid SubjectId { get; }

    public ConsultationKind Kind { get; }

    public ConsultationChannel Channel { get; }

    public ConsultationStatus Status { get; private set; }

    public DateTimeOffset? ScheduledAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public static ConsultationSession Create(
        Guid subjectId,
        ConsultationKind kind,
        ConsultationChannel channel) => new(subjectId, kind, channel);

    public void RequestConsent()
    {
        EnsureStatus(ConsultationStatus.Draft);
        Status = ConsultationStatus.AwaitingConsent;
    }

    public void Schedule(IReadOnlySet<ConsentKind> consents, DateTimeOffset startsAt)
    {
        var requiredConsents = Channel == ConsultationChannel.Video
            ? new[] { ConsentKind.Service, ConsentKind.Recording, ConsentKind.AiAnalysis }
            : new[] { ConsentKind.Service, ConsentKind.AiAnalysis };

        if (requiredConsents.Any(required => !consents.Contains(required)))
        {
            throw new DomainException("CONSENT_REQUIRED");
        }

        EnsureStatus(ConsultationStatus.AwaitingConsent);
        Status = ConsultationStatus.Scheduled;
        ScheduledAt = startsAt;
    }

    public void Start(DateTimeOffset now)
    {
        EnsureStatus(ConsultationStatus.Scheduled);
        Status = ConsultationStatus.InProgress;
        StartedAt = now;
    }

    public void Complete(DateTimeOffset now)
    {
        EnsureStatus(ConsultationStatus.InProgress);
        Status = ConsultationStatus.Completed;
        CompletedAt = now;
        _domainEvents.Add(new ConsultationCompletedDomainEvent(
            Guid.NewGuid(),
            Id,
            SubjectId,
            now));
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status is not (ConsultationStatus.Draft
            or ConsultationStatus.AwaitingConsent
            or ConsultationStatus.Scheduled))
        {
            throw new DomainException("INVALID_SESSION_STATE");
        }

        Status = ConsultationStatus.Cancelled;
        CancelledAt = now;
    }

    private void EnsureStatus(ConsultationStatus expectedStatus)
    {
        if (Status != expectedStatus)
        {
            throw new DomainException("INVALID_SESSION_STATE");
        }
    }
}
