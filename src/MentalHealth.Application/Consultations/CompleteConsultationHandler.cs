using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Application.Consultations;

public sealed class CompleteConsultationHandler(
    IConsultationRepository consultations,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<ConsultationSession> HandleAsync(
        ConsultationActor actor,
        Guid sessionId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var session = await consultations.FindAsync(sessionId, cancellationToken)
            ?? throw new DomainException("SESSION_NOT_FOUND");
        actor.RequireSessionAccess(session);
        if (session.Complete(clock.UtcNow, idempotencyKey))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return session;
    }
}
