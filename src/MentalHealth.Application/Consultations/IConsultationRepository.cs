using MentalHealth.Domain.Consultations;

namespace MentalHealth.Application.Consultations;

public sealed record MessageAppendResult(Message Message, bool Created);

public interface IConsultationRepository
{
    Task<ConsultationSession?> FindAsync(
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<ConsultationSession?> FindByCreationKeyAsync(
        Guid subjectId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<ConsultationSession?> FindByOrderAsync(
        Guid subjectId,
        Guid orderId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Message>> ListMessagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<MessageAppendResult> AppendMessageAsync(
        Guid sessionId,
        Guid senderUserId,
        MessageSenderKind senderKind,
        string text,
        string clientMessageId,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken);

    void Add(ConsultationSession session);
}
