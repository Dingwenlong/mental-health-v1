using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Application.Audit;
using MentalHealth.Domain.Audit;
using MentalHealth.Domain.Consents;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Application.Consents;

public sealed record RecordConsentResult(ConsentRecord Consent, bool Created);

public sealed class RecordConsentHandler(
    IConsentRepository consents,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<RecordConsentResult> RecordAsync(
        Guid subjectId,
        Guid actorUserId,
        ConsentKind kind,
        string textVersion,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(textVersion) || textVersion.Length > 64)
        {
            throw new DomainException("CONSENT_TEXT_VERSION_INVALID");
        }

        if (kind == ConsentKind.ModelTraining)
        {
            throw new DomainException("CONSENT_TYPE_DISABLED");
        }

        var normalizedTextVersion = textVersion.Trim();
        var active = await consents.FindActiveAsync(
            subjectId,
            kind,
            cancellationToken);
        if (active is not null)
        {
            if (string.Equals(
                active.TextVersion,
                normalizedTextVersion,
                StringComparison.Ordinal))
            {
                return new RecordConsentResult(active, false);
            }

            throw new DomainException("ACTIVE_CONSENT_EXISTS");
        }

        var consent = ConsentRecord.Grant(
            subjectId,
            kind,
            normalizedTextVersion,
            actorUserId,
            clock.UtcNow);
        consents.Add(consent);
        auditTrail.Add(AuditEvent.Create(
            actorUserId,
            "ConsentGranted",
            nameof(ConsentRecord),
            consent.Id,
            clock.UtcNow));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new RecordConsentResult(consent, true);
    }

    public async Task<bool> WithdrawAsync(
        Guid subjectId,
        Guid actorUserId,
        Guid consentId,
        CancellationToken cancellationToken)
    {
        var active = await consents.FindActiveByIdAsync(
            subjectId,
            consentId,
            cancellationToken);
        if (active is null)
        {
            return false;
        }

        active.Withdraw(actorUserId, clock.UtcNow);
        auditTrail.Add(AuditEvent.Create(
            actorUserId,
            "ConsentWithdrawn",
            nameof(ConsentRecord),
            active.Id,
            clock.UtcNow));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
