using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MentalHealth.Infrastructure.Identity;

public sealed class JwtTokenService(
    IOptions<JwtOptions> options,
    IClock clock) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public IssuedJwtToken Issue(JwtTokenSubject subject, JwtTokenScope scope)
    {
        var now = clock.UtcNow;
        var lifetime = scope == JwtTokenScope.Api
            ? _options.AccessTokenMinutes
            : _options.MfaSetupTokenMinutes;
        var expiresAt = now.AddMinutes(lifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, subject.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, subject.Email),
            new(ClaimTypes.Email, subject.Email),
            new("scope", scope == JwtTokenScope.Api ? "api" : "mfa_setup")
        };

        claims.AddRange(subject.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        if (subject.SubjectId is { } subjectId)
        {
            claims.Add(new Claim("subject_id", subjectId.ToString()));
        }

        if (subject.PractitionerId is { } practitionerId)
        {
            claims.Add(new Claim("practitioner_id", practitionerId.ToString()));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            now.UtcDateTime,
            expiresAt.UtcDateTime,
            credentials);

        return new IssuedJwtToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt);
    }
}
