using MentalHealth.Application.Security;
using StackExchange.Redis;

namespace MentalHealth.IntegrationTests.Auth;

public sealed class ControllableLoginChallengeStore(ILoginChallengeStore inner)
    : ILoginChallengeStore
{
    public bool Unavailable { get; set; }

    public Task<PhoneLoginTicket> CreatePreChallengeAsync(PhoneLoginPreChallengeDraft value, CancellationToken token = default) => Invoke(() => inner.CreatePreChallengeAsync(value, token));
    public Task<PhoneLoginPreChallenge?> TakePreChallengeAsync(string value, CancellationToken token = default) => Invoke(() => inner.TakePreChallengeAsync(value, token));
    public Task<RateLimitDecision> CheckBootstrapRateAsync(string value, CancellationToken token = default) => Invoke(() => inner.CheckBootstrapRateAsync(value, token));
    public Task<RateLimitDecision> CheckSmsSendRateAsync(string phone, string ip, CancellationToken token = default) => Invoke(() => inner.CheckSmsSendRateAsync(phone, ip, token));
    public Task<PhoneLoginTicket> CreateChallengeAsync(PhoneLoginChallengeDraft value, CancellationToken token = default) => Invoke(() => inner.CreateChallengeAsync(value, token));
    public Task<PhoneLoginChallenge?> GetChallengeForDispatchAsync(string value, CancellationToken token = default) => Invoke(() => inner.GetChallengeForDispatchAsync(value, token));
    public Task<VerificationLease> TryAcquireVerificationAsync(string value, CancellationToken token = default) => Invoke(() => inner.TryAcquireVerificationAsync(value, token));
    public Task ReleaseVerificationLeaseAsync(string id, string lease, CancellationToken token = default) => Invoke(() => inner.ReleaseVerificationLeaseAsync(id, lease, token));
    public Task<ChallengeConsumption> ConsumeChallengeAsync(string id, string lease, CancellationToken token = default) => Invoke(() => inner.ConsumeChallengeAsync(id, lease, token));
    public Task<bool> AcknowledgeAndDeleteSmsDispatchAsync(string id, CancellationToken token = default) => Invoke(() => inner.AcknowledgeAndDeleteSmsDispatchAsync(id, token));
    public Task<SmsDispatchLease> TryAcquireSmsDispatchAsync(string id, CancellationToken token = default) => Invoke(() => inner.TryAcquireSmsDispatchAsync(id, token));
    public Task<bool> CompleteSmsDispatchAsync(string id, string lease, CancellationToken token = default) => Invoke(() => inner.CompleteSmsDispatchAsync(id, lease, token));
    public Task<SmsDispatchFailureState> FailSmsDispatchAsync(string id, string lease, bool terminal, CancellationToken token = default) => Invoke(() => inner.FailSmsDispatchAsync(id, lease, terminal, token));

    private Task<T> Invoke<T>(Func<Task<T>> action)
    {
        ThrowIfUnavailable();
        return action();
    }

    private Task Invoke(Func<Task> action)
    {
        ThrowIfUnavailable();
        return action();
    }

    private void ThrowIfUnavailable()
    {
        if (Unavailable)
        {
            throw new RedisConnectionException(
                ConnectionFailureType.UnableToConnect,
                CommandFlags.None,
                "Synthetic Redis outage.",
                null,
                CommandStatus.Unknown);
        }
    }
}
