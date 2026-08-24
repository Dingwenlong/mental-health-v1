using MentalHealth.AnalysisWorker.Consumers;
using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Infrastructure.Outbox;

namespace MentalHealth.AnalysisWorker.Pipeline;

public sealed class AnalysisPipeline(
    PostgresOutboxReader outbox,
    ConsultationCompletedConsumer consumer,
    string workerId)
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);

    public async Task<int> RunOneBatchAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var leases = await outbox.LeaseBatchAsync(
            workerId,
            maximumCount: 20,
            LeaseDuration,
            cancellationToken);

        foreach (var lease in leases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await consumer.ConsumeAsync(lease, cancellationToken);
                await outbox.MarkProcessedAsync(lease.Id, workerId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var failureCode = exception is ProviderException providerException
                    ? providerException.Code
                    : "ANALYSIS_WORKER_FAILED";
                var terminal = lease.Attempts + 1 >= 3;
                await consumer.RecordFailureAsync(
                    lease.AggregateId,
                    failureCode,
                    terminal,
                    cancellationToken);
                await outbox.RecordFailureAsync(
                    lease.Id,
                    workerId,
                    failureCode,
                    cancellationToken);
            }
        }

        return leases.Count;
    }
}
