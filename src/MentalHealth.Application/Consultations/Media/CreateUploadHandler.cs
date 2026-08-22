using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Application.Consultations.Media;

public sealed class CreateUploadHandler(
    IMediaAssetRepository mediaAssets,
    MediaSessionAccessService access,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<CreateUploadResult> HandleAsync(
        ConsultationActor actor,
        Guid sessionId,
        string contentType,
        int expectedChunks,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var permitted = await access.DemandExistingAsync(
            actor,
            sessionId,
            cancellationToken);
        var normalizedKey = MediaAsset.NormalizeIdempotencyKey(idempotencyKey);
        var normalizedContentType = MediaAsset.NormalizeContentType(contentType);

        var existing = await mediaAssets.FindByCreationKeyAsync(
            sessionId,
            normalizedKey,
            cancellationToken);
        if (existing is not null)
        {
            if (existing.CreatedByUserId != actor.UserId
                || existing.ExpectedChunks != expectedChunks
                || !string.Equals(
                    existing.ContentType,
                    normalizedContentType,
                    StringComparison.Ordinal))
            {
                throw new DomainException("IDEMPOTENCY_CONFLICT");
            }

            return new CreateUploadResult(existing, false);
        }

        if (permitted.Session.Status != ConsultationStatus.InProgress)
        {
            throw new DomainException("INVALID_SESSION_STATE");
        }

        var asset = MediaAsset.Create(
            sessionId,
            permitted.Session.SubjectId,
            actor.UserId,
            normalizedContentType,
            expectedChunks,
            normalizedKey,
            clock.UtcNow);
        mediaAssets.Add(asset);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CreateUploadResult(asset, true);
    }
}
