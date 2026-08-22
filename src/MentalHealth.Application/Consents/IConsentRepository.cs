using MentalHealth.Domain.Consents;

namespace MentalHealth.Application.Consents;

public interface IConsentRepository
{
    Task<ConsentRecord?> FindActiveAsync(
        Guid subjectId,
        ConsentKind kind,
        CancellationToken cancellationToken);

    Task<ConsentRecord?> FindActiveByIdAsync(
        Guid subjectId,
        Guid consentId,
        CancellationToken cancellationToken);

    void Add(ConsentRecord consent);
}
