using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Application.Consultations;

public sealed record SessionAccess(
    ConsultationSession Session,
    MessageSenderKind SenderKind);

public sealed class SessionAccessService(IConsultationRepository consultations)
{
    public async Task<SessionAccess> DemandAsync(
        ConsultationActor actor,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await consultations.FindAsync(sessionId, cancellationToken)
            ?? throw new DomainException("SESSION_NOT_FOUND");
        return new SessionAccess(session, actor.RequireSessionAccess(session));
    }
}
