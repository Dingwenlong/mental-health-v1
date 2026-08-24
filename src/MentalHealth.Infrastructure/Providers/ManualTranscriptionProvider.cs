using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Application.Analysis;

namespace MentalHealth.Infrastructure.Providers;

public sealed class ManualTranscriptionProvider(IManualTranscriptReader transcripts)
    : ITranscriptionProvider
{
    public async Task<TranscriptDocument?> GetAsync(
        TranscriptionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.SessionId == Guid.Empty)
        {
            throw new ProviderException("TRANSCRIPT_REFERENCE_INVALID");
        }

        if (string.IsNullOrWhiteSpace(request.ObjectKey))
        {
            throw new ProviderException("TRANSCRIPT_REQUIRED");
        }

        var transcript = await transcripts.FindAsync(
            request.SessionId,
            request.Revision,
            cancellationToken);
        return transcript is null
            ? null
            : new TranscriptDocument(
                transcript.SessionId,
                transcript.Revision,
                transcript.Source.ToString(),
                transcript.Text,
                transcript.Sha256,
                "zh-CN",
                IsManual: true,
                Segments: []);
    }
}
