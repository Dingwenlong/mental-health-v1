using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.Consents;

public sealed class ConsentRecord
{
    private ConsentRecord()
    {
    }

    private ConsentRecord(
        Guid subjectId,
        ConsentKind kind,
        string textVersion,
        Guid grantedByUserId,
        DateTimeOffset grantedAt)
    {
        if (subjectId == Guid.Empty || grantedByUserId == Guid.Empty)
        {
            throw new DomainException("CONSENT_ACTOR_INVALID");
        }

        if (string.IsNullOrWhiteSpace(textVersion) || textVersion.Length > 64)
        {
            throw new DomainException("CONSENT_TEXT_VERSION_INVALID");
        }

        Id = Guid.NewGuid();
        SubjectId = subjectId;
        Kind = kind;
        TextVersion = textVersion.Trim();
        GrantedByUserId = grantedByUserId;
        GrantedAt = grantedAt;
    }

    public Guid Id { get; private set; }

    public Guid SubjectId { get; private set; }

    public ConsentKind Kind { get; private set; }

    public string TextVersion { get; private set; } = string.Empty;

    public Guid GrantedByUserId { get; private set; }

    public DateTimeOffset GrantedAt { get; private set; }

    public Guid? WithdrawnByUserId { get; private set; }

    public DateTimeOffset? WithdrawnAt { get; private set; }

    public bool Active => WithdrawnAt is null;

    public static ConsentRecord Grant(
        Guid subjectId,
        ConsentKind kind,
        string textVersion,
        Guid grantedByUserId,
        DateTimeOffset grantedAt) =>
        new(subjectId, kind, textVersion, grantedByUserId, grantedAt);

    public void Withdraw(Guid withdrawnByUserId, DateTimeOffset withdrawnAt)
    {
        if (!Active)
        {
            throw new DomainException("CONSENT_ALREADY_WITHDRAWN");
        }

        if (withdrawnByUserId == Guid.Empty || withdrawnAt < GrantedAt)
        {
            throw new DomainException("CONSENT_WITHDRAWAL_INVALID");
        }

        WithdrawnByUserId = withdrawnByUserId;
        WithdrawnAt = withdrawnAt;
    }
}
