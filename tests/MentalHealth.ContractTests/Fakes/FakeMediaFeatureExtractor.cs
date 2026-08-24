using MentalHealth.Application.Abstractions.Providers;

namespace MentalHealth.ContractTests.Fakes;

internal sealed class FakeMediaFeatureExtractor : IMediaFeatureExtractor
{
    public string Modality => "audio";

    public Task<FeatureExtractionResult> ExtractAsync(
        FeatureExtractionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.ObjectKey))
        {
            throw new ProviderException("INVALID_OBJECT_KEY");
        }

        return Task.FromResult(new FeatureExtractionResult(
            Success: true,
            Modality,
            [new FeatureObservation("speech_ratio", 0.5, 1, "audio:0-1s", "fake-v1")],
            FailureCode: null,
            []));
    }
}
