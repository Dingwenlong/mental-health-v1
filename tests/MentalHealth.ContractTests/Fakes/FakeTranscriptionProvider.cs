using MentalHealth.Application.Abstractions.Providers;

namespace MentalHealth.ContractTests.Fakes;

internal sealed class FakeTranscriptionProvider : ITranscriptionProvider
{
    public Task<TranscriptDocument?> GetAsync(
        TranscriptionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.ObjectKey))
        {
            throw new ProviderException("TRANSCRIPT_REQUIRED");
        }

        TranscriptDocument? document = string.IsNullOrWhiteSpace(request.SuppliedText)
            ? null
            : new TranscriptDocument(
                request.SuppliedText,
                "zh-CN",
                IsManual: true,
                Segments: []);
        return Task.FromResult(document);
    }
}
