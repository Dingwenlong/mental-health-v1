using MentalHealth.Domain.Consultations;

namespace MentalHealth.Application.Consultations;

public sealed record ConsultationDto(
    Guid Id,
    Guid SubjectId,
    Guid OrderId,
    Guid? AssignedPractitionerId,
    string Kind,
    string Channel,
    string Status,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt)
{
    public static ConsultationDto From(ConsultationSession session) => new(
        session.Id,
        session.SubjectId,
        session.OrderId ?? Guid.Empty,
        session.AssignedPractitionerId,
        session.Kind == ConsultationKind.AiVirtual ? "Ai" : "Human",
        session.Channel.ToString(),
        session.Status.ToString(),
        session.ScheduledAt,
        session.StartedAt,
        session.CompletedAt);
}

public sealed record ChatMessageDto(
    Guid Id,
    Guid SessionId,
    string SenderKind,
    string ClientMessageId,
    int Sequence,
    string Text,
    DateTimeOffset SentAt)
{
    public static ChatMessageDto From(Message message) => new(
        message.Id,
        message.SessionId,
        message.SenderKind.ToString(),
        message.ClientMessageId,
        message.Sequence,
        message.Text,
        message.SentAt);
}
