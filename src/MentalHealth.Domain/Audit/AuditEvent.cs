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
        DateTimeOffset occurredAt,
        string? reason)
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
        Reason = NormalizeReason(reason);
    }

    public Guid Id { get; private set; }

    public Guid ActorUserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string ResourceType { get; private set; } = string.Empty;

    public Guid ResourceId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string? Reason { get; private set; }

    public static AuditEvent Create(
        Guid actorUserId,
        string action,
        string resourceType,
        Guid resourceId,
        DateTimeOffset occurredAt,
        string? reason = null) =>
        new(actorUserId, action, resourceType, resourceId, occurredAt, reason);

    private static string? NormalizeReason(string? reason)
    {
        if (reason is null)
        {
            return null;
        }

        var normalized = reason.Trim();
        if (normalized.Length == 0 || normalized.Length > 1000)
        {
            throw new DomainException("AUDIT_REASON_INVALID");
        }

        return normalized;
    }
}
