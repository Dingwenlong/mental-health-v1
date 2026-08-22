using MentalHealth.Application.Consultations.Media;

namespace MentalHealth.Api.Services;

public sealed class MediaUploadCleanupWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<MediaUploadCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var cleanup = scope.ServiceProvider
                    .GetRequiredService<ExpiredUploadCleanupHandler>();
                var expired = await cleanup.HandleAsync(stoppingToken);
                if (expired > 0)
                {
                    logger.LogInformation(
                        "Expired {ExpiredUploadCount} unfinished media uploads",
                        expired);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    "Media upload cleanup failed with {ExceptionType}",
                    exception.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
