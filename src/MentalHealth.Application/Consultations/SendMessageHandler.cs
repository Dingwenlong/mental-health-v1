using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Application.Consultations;

public sealed class SendMessageHandler(
    IConsultationRepository consultations,
    SessionAccessService access,
    IClock clock)
{
    public async Task<MessageAppendResult> HandleAsync(
        ConsultationActor actor,
        Guid sessionId,
        string text,
        string clientMessageId,
        CancellationToken cancellationToken)
    {
        var permitted = await access.DemandAsync(
            actor,
            sessionId,
            cancellationToken);
        if (permitted.Session.Status != ConsultationStatus.InProgress)
        {
            throw new DomainException("INVALID_SESSION_STATE");
        }

        return await consultations.AppendMessageAsync(
            sessionId,
            actor.UserId,
            permitted.SenderKind,
            text,
            clientMessageId,
            clock.UtcNow,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Message>> ListAsync(
        ConsultationActor actor,
        Guid sessionId,
        int afterSequence,
        CancellationToken cancellationToken)
    {
        if (afterSequence < 0)
        {
            throw new DomainException("MESSAGE_CURSOR_INVALID");
        }

        await access.DemandAsync(actor, sessionId, cancellationToken);
        return await consultations.ListMessagesAsync(
            sessionId,
            afterSequence,
            cancellationToken);
    }
}
