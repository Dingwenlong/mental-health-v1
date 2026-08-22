using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Application.Abstractions.Providers;

namespace MentalHealth.Application.Consultations.Media;

public sealed class ExpiredUploadCleanupHandler(
    IMediaAssetRepository mediaAssets,
    IObjectStorage storage,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private const int BatchSize = 100;

    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var candidates = await mediaAssets.ListCleanupCandidatesAsync(
            now,
            BatchSize,
            cancellationToken);
        var expiredCount = 0;
        foreach (var asset in candidates)
        {
            if (asset.ExpireIfDue(now))
            {
                expiredCount += 1;
            }
        }

        if (expiredCount > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        foreach (var asset in candidates)
        {
            for (var index = 0; index < asset.ExpectedChunks; index += 1)
            {
                await storage.DeleteAsync(
                    MediaStorageKeys.Chunk(asset.Id, index),
                    cancellationToken);
            }

            asset.MarkExpiredChunksDeleted(now);
        }

        if (candidates.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return expiredCount;
    }
}
