namespace MentalHealth.Application.Security;

public sealed record JwtTokenSubject(
    Guid UserId,
    string PhoneNumber,
    IReadOnlyCollection<string> Roles,
    Guid? SubjectId,
    Guid? PractitionerId);

public sealed record IssuedJwtToken(string Value, DateTimeOffset ExpiresAt);

public interface IJwtTokenService
{
    IssuedJwtToken Issue(JwtTokenSubject subject);
}
