using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.Audit;

public sealed class AuditEvent
{
    private AuditEvent()
    {
    }

    private AuditEvent(
        Guid actorUserId,
        string action,
        string resourceType,
        Guid resourceId,
        DateTimeOffset occurredAt)
    {
        if (actorUserId == Guid.Empty
            || resourceId == Guid.Empty
            || string.IsNullOrWhiteSpace(action)
            || action.Length > 64
            || string.IsNullOrWhiteSpace(resourceType)
            || resourceType.Length > 64)
        {
            throw new DomainException("AUDIT_EVENT_INVALID");
        }

        Id = Guid.NewGuid();
        ActorUserId = actorUserId;
        Action = action;
        ResourceType = resourceType;
        ResourceId = resourceId;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }

    public Guid ActorUserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string ResourceType { get; private set; } = string.Empty;

    public Guid ResourceId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static AuditEvent Create(
        Guid actorUserId,
        string action,
        string resourceType,
        Guid resourceId,
        DateTimeOffset occurredAt) =>
        new(actorUserId, action, resourceType, resourceId, occurredAt);
}
