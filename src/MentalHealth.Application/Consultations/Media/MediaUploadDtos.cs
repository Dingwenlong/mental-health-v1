using MentalHealth.Domain.Consultations;

namespace MentalHealth.Application.Consultations.Media;

public sealed record MediaUploadDto(
    Guid Id,
    Guid SessionId,
    string ContentType,
    int ExpectedChunks,
    string Status,
    string? Sha256,
    long? Length,
    DateTimeOffset CapturedAt,
    DateTimeOffset UploadExpiresAt,
    DateTimeOffset? CompletedAt)
{
    public static MediaUploadDto From(MediaAsset asset) => new(
        asset.Id,
        asset.SessionId,
        asset.ContentType,
        asset.ExpectedChunks,
        asset.Status.ToString(),
        asset.Sha256,
        asset.Length,
        asset.CapturedAt,
        asset.UploadExpiresAt,
        asset.CompletedAt);
}

public sealed record ChunkWriteResult(
    int Index,
    bool Created,
    string Sha256,
    long Length);

public sealed record CreateUploadResult(MediaAsset Asset, bool Created);

public sealed record CompleteUploadResult(MediaAsset Asset, bool Completed);
