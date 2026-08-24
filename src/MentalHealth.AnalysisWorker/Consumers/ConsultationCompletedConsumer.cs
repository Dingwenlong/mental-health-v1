using System.Text.Json;
using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Application.Analysis;
using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.Consultations;
using MentalHealth.Infrastructure.Outbox;

namespace MentalHealth.AnalysisWorker.Consumers;

public sealed class ConsultationCompletedConsumer(
    RequestAnalysisHandler requests,
    ITranscriptionProvider transcriptionProvider)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task ConsumeAsync(
        OutboxLease lease,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var completed = JsonSerializer.Deserialize<ConsultationCompletedDomainEvent>(
            lease.Payload,
            JsonOptions);
        if (completed is null
            || completed.SessionId == Guid.Empty
            || completed.SessionId != lease.AggregateId)
        {
            throw new ProviderException("OUTBOX_PAYLOAD_INVALID");
        }

        var job = await requests.HandleAsync(completed.SessionId, cancellationToken);
        if (job.Status is AnalysisJobStatus.Ready
            or AnalysisJobStatus.Processing
            or AnalysisJobStatus.Completed)
        {
            return;
        }

        var transcript = await transcriptionProvider.GetAsync(
            new TranscriptionRequest(
                completed.SessionId,
                "manual",
                SuppliedText: null,
                job.TranscriptRevision),
            cancellationToken);
        if (transcript is null)
        {
            await requests.MarkNeedsManualAsync(
                completed.SessionId,
                "TRANSCRIPT_REQUIRED",
                cancellationToken);
            return;
        }

        await requests.UseTranscriptAsync(
            completed.SessionId,
            transcript.Revision,
            cancellationToken);
    }

    public Task RecordFailureAsync(
        Guid sessionId,
        string failureCode,
        bool terminal,
        CancellationToken cancellationToken) =>
        requests.RecordFailureAsync(
            sessionId,
            failureCode,
            terminal,
            cancellationToken);
}
