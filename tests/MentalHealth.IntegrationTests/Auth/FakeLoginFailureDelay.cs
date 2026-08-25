using System.Collections.Concurrent;
using MentalHealth.Application.Security;

namespace MentalHealth.IntegrationTests.Auth;

public sealed class FakeLoginFailureDelay : ILoginFailureDelay
{
    private readonly ConcurrentQueue<TimeSpan> _targets = new();

    public IReadOnlyList<TimeSpan> Targets => _targets.ToArray();

    public Task DelayAsync(
        TimeSpan targetDelay,
        long startedTimestamp,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _targets.Enqueue(targetDelay);
        return Task.CompletedTask;
    }

    public void Reset()
    {
        while (_targets.TryDequeue(out _))
        {
        }
    }
}
