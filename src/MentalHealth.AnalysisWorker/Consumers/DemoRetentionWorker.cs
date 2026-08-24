using MentalHealth.Application.DataRights;

namespace MentalHealth.AnalysisWorker.Consumers;

public sealed class DemoRetentionWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<DemoRetentionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var retention = scope.ServiceProvider
                    .GetRequiredService<DemoRetentionHandler>();
                var deletedCount = await retention.HandleAsync(stoppingToken);
                logger.LogInformation(
                    "Demo media retention completed {DeletedCount}",
                    deletedCount);
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Demo media retention failed with {ExceptionType}",
                    exception.GetType().Name);
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
