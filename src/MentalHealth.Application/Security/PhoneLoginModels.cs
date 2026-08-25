namespace MentalHealth.Application.Security;

public sealed record PhoneLoginPreChallengeDraft(
    string NationalPhoneNumber,
    Guid? UserId,
    string Client,
    string SceneId);

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
    string SceneId);

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

public enum SmsDispatchLeaseState
{
    Missing = 0,
    Acquired = 1,
    Busy = 2,
    Sent = 3,
    Terminal = 4
}

public sealed record SmsDispatchLease(
    SmsDispatchLeaseState State,
    string? LeaseId,
    PhoneLoginChallenge? Challenge,
    int Attempt);

public enum SmsDispatchFailureState
{
    LostLease = 0,
    Retryable = 1,
    Terminal = 2
}
