using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Security;
using MentalHealth.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace MentalHealth.IntegrationTests.Auth;

public sealed class PhoneJwtTests
{
    [Fact]
    public void Issued_api_token_contains_phone_and_never_email()
    {
        var userId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var subjectId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var practitionerId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var service = new JwtTokenService(
            Options.Create(new JwtOptions
            {
                Issuer = "test-issuer",
                Audience = "test-audience",
                SigningKey = "synthetic-test-signing-key-with-at-least-32-bytes"
            }),
            new FixedClock(DateTimeOffset.Parse("2026-08-25T00:00:00+00:00")));

        var issued = service.Issue(new JwtTokenSubject(
            userId,
            "+8613800138000",
            [AppRoles.User],
            subjectId,
            practitionerId));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(issued.Value);

        Assert.Contains(token.Claims, claim =>
            claim.Type == JwtRegisteredClaimNames.Sub && claim.Value == userId.ToString());
        Assert.Contains(token.Claims, claim =>
            claim.Type == ClaimTypes.NameIdentifier && claim.Value == userId.ToString());
        Assert.Contains(token.Claims, claim =>
            claim.Type == "phone_number" && claim.Value == "+8613800138000");
        Assert.Contains(token.Claims, claim =>
            claim.Type == "scope" && claim.Value == "api");
        Assert.Contains(token.Claims, claim =>
            claim.Type == ClaimTypes.Role && claim.Value == AppRoles.User);
        Assert.Contains(token.Claims, claim =>
            claim.Type == "subject_id" && claim.Value == subjectId.ToString());
        Assert.Contains(token.Claims, claim =>
            claim.Type == "practitioner_id" && claim.Value == practitionerId.ToString());
        Assert.DoesNotContain(token.Claims, claim =>
            claim.Type is JwtRegisteredClaimNames.Email or ClaimTypes.Email);
    }

    [Fact]
    public void Issuing_api_token_normalizes_a_national_phone_number()
    {
        var service = CreateService();

        var issued = service.Issue(new JwtTokenSubject(
            Guid.NewGuid(),
            "13800138000",
            [AppRoles.User],
            null,
            null));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(issued.Value);

        Assert.Contains(token.Claims, claim =>
            claim.Type == "phone_number" && claim.Value == "+8613800138000");
    }

    [Theory]
    [InlineData("")]
    [InlineData("+85291234567")]
    [InlineData("138 0013 8000")]
    public void Issuing_token_with_an_invalid_phone_number_is_rejected(string phoneNumber)
    {
        var service = CreateService();

        Assert.Throws<ArgumentException>(() => service.Issue(new JwtTokenSubject(
            Guid.NewGuid(),
            phoneNumber,
            [AppRoles.User],
            null,
            null)));
    }

    private static JwtTokenService CreateService() => new(
        Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningKey = "synthetic-test-signing-key-with-at-least-32-bytes"
        }),
        new FixedClock(DateTimeOffset.Parse("2026-08-25T00:00:00+00:00")));

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
