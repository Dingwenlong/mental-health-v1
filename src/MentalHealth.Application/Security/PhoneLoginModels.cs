namespace MentalHealth.Application.Security;

public sealed record PhoneLoginPreChallenge(
    string NationalPhoneNumber,
    Guid? UserId,
    string Client,
    string SceneId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

public sealed record PhoneLoginChallengeDraft(
    string NationalPhoneNumber,
    Guid? UserId,
    string Client,
    string SceneId,
    DateTimeOffset SentAt,
    DateTimeOffset ExpiresAt);

public sealed record PhoneLoginChallenge(
    string ChallengeId,
    string NationalPhoneNumber,
    Guid? UserId,
    string Client,
    string SceneId,
    string OutId,
    DateTimeOffset SentAt,
    DateTimeOffset ExpiresAt,
    int FailedAttempts);

public sealed record PhoneLoginTicket(
    string Id,
    string Token,
    DateTimeOffset ExpiresAt);

public readonly record struct RateLimitDecision(
    bool IsAllowed,
    int RetryAfterSeconds)
{
    public static RateLimitDecision Allowed => new(true, 0);

    public static RateLimitDecision Denied(int retryAfterSeconds) =>
        new(false, retryAfterSeconds);
}

public sealed record VerificationLease(
    bool IsAcquired,
    string? LeaseId,
    PhoneLoginChallenge? Challenge);

public readonly record struct ChallengeConsumption(
    bool WasConsumed,
    Guid? UserId);
