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

    public IssuedJwtToken Issue(JwtTokenSubject subject) =>
        Issue(subject, JwtTokenScope.Api);

    public IssuedJwtToken Issue(JwtTokenSubject subject, JwtTokenScope scope)
    {
        if (!PhoneNumberNormalizer.TryNormalizeMainlandChina(
                subject.PhoneNumber,
                out var phoneNumber))
        {
            throw new ArgumentException(
                "A valid mainland China phone number is required.",
                nameof(subject));
        }

        var now = clock.UtcNow;
        var lifetime = scope == JwtTokenScope.Api
            ? _options.AccessTokenMinutes
            : _options.MfaSetupTokenMinutes;
        var expiresAt = now.AddMinutes(lifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, subject.UserId.ToString()),
            new("phone_number", phoneNumber),
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
