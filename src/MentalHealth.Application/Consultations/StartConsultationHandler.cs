using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Application.Consultations;

public sealed class StartConsultationHandler(
    IConsultationRepository consultations,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<ConsultationSession> HandleAsync(
        ConsultationActor actor,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await consultations.FindAsync(sessionId, cancellationToken)
            ?? throw new DomainException("SESSION_NOT_FOUND");
        actor.RequireSessionAccess(session);
        session.Start(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return session;
    }
}
