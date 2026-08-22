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
            Modality,
            [new ExtractedFeature("speechRatio", 0.5, 1)],
            []));
    }
}
