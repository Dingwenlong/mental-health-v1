using MentalHealth.Application.Consents;
using MentalHealth.Domain.Consents;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Application.Consultations.Media;

public sealed class MediaSessionAccessService(
    SessionAccessService sessions,
    IConsentRepository consents)
{
    public async Task<SessionAccess> DemandExistingAsync(
        ConsultationActor actor,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var access = await DemandCoreAsync(actor, sessionId, cancellationToken);
        if (access.Session.Status is not (
            ConsultationStatus.InProgress or ConsultationStatus.Completed))
        {
            throw new DomainException("INVALID_SESSION_STATE");
        }

        return access;
    }

    private async Task<SessionAccess> DemandCoreAsync(
        ConsultationActor actor,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var access = await sessions.DemandAsync(actor, sessionId, cancellationToken);
        if (access.Session.Channel != ConsultationChannel.Video)
        {
            throw new DomainException("VIDEO_SESSION_REQUIRED");
        }

        if (await consents.FindActiveAsync(
            access.Session.SubjectId,
            ConsentKind.Recording,
            cancellationToken) is null)
        {
            throw new DomainException("CONSENT_REQUIRED");
        }

        return access;
    }
}
