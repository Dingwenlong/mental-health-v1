namespace MentalHealth.Application.Abstractions.Providers;

public sealed record FeatureExtractionRequest(
    Guid SessionId,
    string ObjectKey,
    string ContentType,
    string? TranscriptText);

public sealed record FeatureObservation(
    string Code,
    double Value,
    double Quality,
    string SourceRange,
    string ExtractorVersion);

public sealed record FeatureExtractionResult(
    bool Success,
    string Modality,
    IReadOnlyList<FeatureObservation> Observations,
    string? FailureCode,
    IReadOnlyList<string> Warnings);

public interface IMediaFeatureExtractor
{
    string Modality { get; }

    Task<FeatureExtractionResult> ExtractAsync(
        FeatureExtractionRequest request,
        CancellationToken cancellationToken);
}
