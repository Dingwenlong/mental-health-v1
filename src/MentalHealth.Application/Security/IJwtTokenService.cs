namespace MentalHealth.Application.Security;

public enum JwtTokenScope
{
    Api,
    MfaSetup
}

public sealed record JwtTokenSubject(
    Guid UserId,
    string Email,
    IReadOnlyCollection<string> Roles,
    Guid? SubjectId,
    Guid? PractitionerId);

public sealed record IssuedJwtToken(string Value, DateTimeOffset ExpiresAt);

public interface IJwtTokenService
{
    IssuedJwtToken Issue(JwtTokenSubject subject, JwtTokenScope scope);
}
