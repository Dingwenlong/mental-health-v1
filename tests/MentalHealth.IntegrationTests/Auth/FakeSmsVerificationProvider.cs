using System.Collections.Concurrent;
using MentalHealth.Application.Security;

namespace MentalHealth.IntegrationTests.Auth;

public sealed class FakeSmsVerificationProvider : ISmsVerificationProvider
{
    public const string ValidCode = "246810";

    private readonly ConcurrentDictionary<string, string> _phonesByOutId = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _sent = new();
    private int _temporaryFailuresRemaining;
    private int _sendAttempts;

    public int SendAttempts => Volatile.Read(ref _sendAttempts);

    public int TemporaryFailuresRemaining
    {
        get => Volatile.Read(ref _temporaryFailuresRemaining);
        set => Volatile.Write(ref _temporaryFailuresRemaining, value);
    }

    public Task SendAsync(
        string nationalPhoneNumber,
        string outId,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _sendAttempts);
        if (Interlocked.Decrement(ref _temporaryFailuresRemaining) >= 0)
        {
            throw new PhoneLoginProviderException("SMS_PROVIDER_UNAVAILABLE");
        }

        _phonesByOutId[outId] = nationalPhoneNumber;
        _sent.GetOrAdd(
            outId,
            static _ => new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
        return Task.CompletedTask;
    }

    public Task<bool> CheckAsync(
        string nationalPhoneNumber,
        string outId,
        string code,
        CancellationToken cancellationToken) =>
        Task.FromResult(
            code == ValidCode
            && _phonesByOutId.TryGetValue(outId, out var sentPhone)
            && sentPhone == nationalPhoneNumber);

    public async Task WaitUntilSentAsync(
        string outId,
        TimeSpan? timeout = null)
    {
        var completion = _sent.GetOrAdd(
            outId,
            static _ => new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously));
        await completion.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(5));
    }
}
