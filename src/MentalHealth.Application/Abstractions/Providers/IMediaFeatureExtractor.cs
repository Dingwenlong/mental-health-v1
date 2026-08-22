namespace MentalHealth.Application.Abstractions.Providers;

public sealed record FeatureExtractionRequest(
    Guid SessionId,
    string ObjectKey,
    string ContentType,
    string? TranscriptText);

public sealed record ExtractedFeature(string Name, double Value, double Quality);

public sealed record FeatureExtractionResult(
    string Modality,
    IReadOnlyList<ExtractedFeature> Features,
    IReadOnlyList<string> Warnings);

public interface IMediaFeatureExtractor
{
    string Modality { get; }

    Task<FeatureExtractionResult> ExtractAsync(
        FeatureExtractionRequest request,
        CancellationToken cancellationToken);
}
