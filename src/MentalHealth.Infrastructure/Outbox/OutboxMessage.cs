using System.Text.Json;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private OutboxMessage()
    {
    }

    public OutboxMessage(
        Guid id,
        string type,
        DateTimeOffset occurredAt,
        string payload)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Outbox id is required.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        Id = id;
        Type = type;
        OccurredAt = occurredAt;
        CreatedAt = occurredAt;
        Payload = payload;
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset? ProcessedAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    internal static OutboxMessage FromDomainEvent(IDomainEvent domainEvent)
    {
        var eventName = domainEvent.GetType().Name;
        var type = eventName.EndsWith("DomainEvent", StringComparison.Ordinal)
            ? eventName[..^"DomainEvent".Length]
            : eventName;
        var payload = JsonSerializer.Serialize(
            domainEvent,
            domainEvent.GetType(),
            JsonOptions);

        return new OutboxMessage(
            domainEvent.EventId,
            type,
            domainEvent.OccurredAt,
            payload);
    }
}
