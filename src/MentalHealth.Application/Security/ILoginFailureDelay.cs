namespace MentalHealth.Application.Security;

public interface ILoginFailureDelay
{
    Task DelayAsync(
        TimeSpan targetDelay,
        long startedTimestamp,
        CancellationToken cancellationToken);
}
