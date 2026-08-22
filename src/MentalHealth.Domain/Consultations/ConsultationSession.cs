using MentalHealth.Domain.Consents;
using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.Consultations;

public sealed class ConsultationSession : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private ConsultationSession()
    {
    }

    private ConsultationSession(
        Guid subjectId,
        ConsultationKind kind,
        ConsultationChannel channel,
        Guid? orderId = null,
        Guid? assignedPractitionerId = null,
        string? creationIdempotencyKey = null)
    {
        Id = Guid.NewGuid();
        SubjectId = subjectId;
        Kind = kind;
        Channel = channel;
        OrderId = orderId;
        AssignedPractitionerId = assignedPractitionerId;
        CreationIdempotencyKey = creationIdempotencyKey;
    }

    public Guid Id { get; private set; }

    public Guid SubjectId { get; private set; }

    public Guid? OrderId { get; private set; }

    public Guid? AssignedPractitionerId { get; private set; }

    public string? CreationIdempotencyKey { get; private set; }

    public string? CompletionIdempotencyKey { get; private set; }

    public ConsultationKind Kind { get; private set; }

    public ConsultationChannel Channel { get; private set; }

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

    public static ConsultationSession CreateAuthorized(
        Guid subjectId,
        Guid orderId,
        Guid? assignedPractitionerId,
        ConsultationKind kind,
        ConsultationChannel channel,
        string idempotencyKey)
    {
        if (subjectId == Guid.Empty || orderId == Guid.Empty)
        {
            throw new DomainException("CONSULTATION_REFERENCE_INVALID");
        }

        var normalizedKey = idempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedKey) || normalizedKey.Length > 100)
        {
            throw new DomainException("IDEMPOTENCY_KEY_INVALID");
        }

        return new ConsultationSession(
            subjectId,
            kind,
            channel,
            orderId,
            assignedPractitionerId,
            normalizedKey);
    }

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
        CompleteCore(now);
    }

    public bool Complete(DateTimeOffset now, string idempotencyKey)
    {
        var normalizedKey = idempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedKey) || normalizedKey.Length > 100)
        {
            throw new DomainException("IDEMPOTENCY_KEY_INVALID");
        }

        if (Status == ConsultationStatus.Completed)
        {
            if (string.Equals(
                CompletionIdempotencyKey,
                normalizedKey,
                StringComparison.Ordinal))
            {
                return false;
            }

            throw new DomainException("IDEMPOTENCY_CONFLICT");
        }

        EnsureStatus(ConsultationStatus.InProgress);
        CompletionIdempotencyKey = normalizedKey;
        CompleteCore(now);
        return true;
    }

    private void CompleteCore(DateTimeOffset now)
    {
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

    public void RequestEscalation(
        Guid eventId,
        string ruleId,
        DateTimeOffset occurredAt)
    {
        EnsureStatus(ConsultationStatus.InProgress);
        var normalizedRuleId = ruleId?.Trim();
        if (eventId == Guid.Empty || string.IsNullOrWhiteSpace(normalizedRuleId))
        {
            throw new DomainException("ESCALATION_INVALID");
        }

        if (_domainEvents.Any(domainEvent => domainEvent.EventId == eventId))
        {
            return;
        }

        _domainEvents.Add(new EscalationRequestedDomainEvent(
            eventId,
            Id,
            SubjectId,
            normalizedRuleId,
            occurredAt));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    private void EnsureStatus(ConsultationStatus expectedStatus)
    {
        if (Status != expectedStatus)
        {
            throw new DomainException("INVALID_SESSION_STATE");
        }
    }
}
