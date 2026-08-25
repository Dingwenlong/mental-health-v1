using MentalHealth.Application.Security;

namespace MentalHealth.IntegrationTests.Auth;

public sealed class ControllableJwtTokenService(IJwtTokenService inner)
    : IJwtTokenService
{
    public const string FailureMessage = "synthetic-post-consume-jwt-failure";

    public bool ThrowOnIssue { get; set; }

    public IssuedJwtToken Issue(JwtTokenSubject subject)
    {
        if (ThrowOnIssue)
        {
            throw new InvalidOperationException(FailureMessage);
        }

        return inner.Issue(subject);
    }
}
