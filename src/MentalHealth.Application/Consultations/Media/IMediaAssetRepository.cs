using MentalHealth.Domain.Consultations;

namespace MentalHealth.Application.Consultations.Media;

public interface IMediaAssetRepository
{
    Task<MediaAsset?> FindAsync(
        Guid mediaAssetId,
        CancellationToken cancellationToken);

    Task<MediaAsset?> FindByCreationKeyAsync(
        Guid sessionId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MediaAsset>> ListCleanupCandidatesAsync(
        DateTimeOffset expiresBefore,
        int maximumCount,
        CancellationToken cancellationToken);

    void Add(MediaAsset mediaAsset);
}
