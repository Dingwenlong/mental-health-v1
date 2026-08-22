using System.Security.Claims;
using MentalHealth.Application.Consultations;

namespace MentalHealth.Api.Authorization;

public static class ConsultationActorClaims
{
    public static ConsultationActor? ToConsultationActor(
        this ClaimsPrincipal principal)
    {
        if (!Guid.TryParse(
            principal.FindFirstValue(ClaimTypes.NameIdentifier),
            out var userId))
        {
            return null;
        }

        var subjectId = TryGuid(principal, "subject_id");
        var practitionerId = TryGuid(principal, "practitioner_id");
        var roles = principal.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new ConsultationActor(
            userId,
            subjectId,
            practitionerId,
            roles);
    }

    private static Guid? TryGuid(
        ClaimsPrincipal principal,
        string claimType) =>
        Guid.TryParse(principal.FindFirstValue(claimType), out var value)
            ? value
            : null;
}
