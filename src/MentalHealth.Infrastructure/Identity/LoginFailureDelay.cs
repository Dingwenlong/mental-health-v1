using MentalHealth.Application.Security;

namespace MentalHealth.Infrastructure.Identity;

public sealed class LoginFailureDelay(TimeProvider timeProvider) : ILoginFailureDelay
{
    public Task DelayAsync(
        TimeSpan targetDelay,
        long startedTimestamp,
        CancellationToken cancellationToken)
    {
        var remaining = targetDelay - timeProvider.GetElapsedTime(startedTimestamp);
        return remaining > TimeSpan.Zero
            ? Task.Delay(remaining, timeProvider, cancellationToken)
            : Task.CompletedTask;
    }
}
