using MentalHealth.Application.Security;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Application.Consultations;

public sealed record ConsultationActor(
    Guid UserId,
    Guid? SubjectId,
    Guid? PractitionerId,
    IReadOnlyCollection<string> Roles)
{
    public Guid RequireOwnedSubject()
    {
        if (UserId == Guid.Empty
            || SubjectId is not { } subjectId
            || !Roles.Contains(AppRoles.User, StringComparer.Ordinal))
        {
            throw new DomainException("FORBIDDEN_RESOURCE");
        }

        return subjectId;
    }

    public MessageSenderKind RequireSessionAccess(ConsultationSession session)
    {
        if (Roles.Contains(AppRoles.User, StringComparer.Ordinal)
            && SubjectId == session.SubjectId)
        {
            return MessageSenderKind.User;
        }

        if (Roles.Contains(AppRoles.Counselor, StringComparer.Ordinal)
            && PractitionerId is { } practitionerId
            && session.AssignedPractitionerId == practitionerId)
        {
            return MessageSenderKind.Practitioner;
        }

        throw new DomainException("FORBIDDEN_RESOURCE");
    }
}
