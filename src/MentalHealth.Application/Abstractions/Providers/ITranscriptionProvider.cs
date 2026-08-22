namespace MentalHealth.Application.Abstractions.Providers;

public sealed record TranscriptionRequest(
    Guid SessionId,
    string ObjectKey,
    string? SuppliedText);

public sealed record TranscriptSegment(
    int Index,
    TimeSpan StartsAt,
    TimeSpan EndsAt,
    string Text);

public sealed record TranscriptDocument(
    string Text,
    string Language,
    bool IsManual,
    IReadOnlyList<TranscriptSegment> Segments);

public interface ITranscriptionProvider
{
    Task<TranscriptDocument?> GetAsync(
        TranscriptionRequest request,
        CancellationToken cancellationToken);
}
