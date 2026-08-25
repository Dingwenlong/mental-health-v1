namespace MentalHealth.Application.Security;

public interface ILoginChallengeStore
{
    Task<PhoneLoginTicket> CreatePreChallengeAsync(
        PhoneLoginPreChallenge preChallenge,
        CancellationToken cancellationToken = default);

    Task<PhoneLoginPreChallenge?> TakePreChallengeAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task<RateLimitDecision> CheckBootstrapRateAsync(
        string sourceIp,
        CancellationToken cancellationToken = default);

    Task<RateLimitDecision> CheckSmsSendRateAsync(
        string nationalPhoneNumber,
        string sourceIp,
        CancellationToken cancellationToken = default);

    Task<PhoneLoginTicket> CreateChallengeAsync(
        PhoneLoginChallengeDraft challenge,
        CancellationToken cancellationToken = default);

    Task<PhoneLoginChallenge?> GetChallengeForDispatchAsync(
        string challengeId,
        CancellationToken cancellationToken = default);

    Task<VerificationLease> TryAcquireVerificationAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task ReleaseVerificationLeaseAsync(
        string challengeId,
        string leaseId,
        CancellationToken cancellationToken = default);

    Task<ChallengeConsumption> ConsumeChallengeAsync(
        string challengeId,
        string leaseId,
        CancellationToken cancellationToken = default);
}
