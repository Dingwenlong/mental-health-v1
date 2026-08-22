using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Application.Consultations;

public sealed class SendMessageHandler(
    IConsultationRepository consultations,
    IClock clock)
{
    public async Task<MessageAppendResult> HandleAsync(
        ConsultationActor actor,
        Guid sessionId,
        string text,
        string clientMessageId,
        CancellationToken cancellationToken)
    {
        var session = await consultations.FindAsync(sessionId, cancellationToken)
            ?? throw new DomainException("SESSION_NOT_FOUND");
        var senderKind = actor.RequireSessionAccess(session);
        if (session.Status != ConsultationStatus.InProgress)
        {
            throw new DomainException("INVALID_SESSION_STATE");
        }

        return await consultations.AppendMessageAsync(
            sessionId,
            actor.UserId,
            senderKind,
            text,
            clientMessageId,
            clock.UtcNow,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Message>> ListAsync(
        ConsultationActor actor,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await consultations.FindAsync(sessionId, cancellationToken)
            ?? throw new DomainException("SESSION_NOT_FOUND");
        actor.RequireSessionAccess(session);
        return await consultations.ListMessagesAsync(sessionId, cancellationToken);
    }
}
