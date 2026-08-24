using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Application.Audit;
using MentalHealth.Application.Consultations;
using MentalHealth.Application.Consultations.Media;
using MentalHealth.Domain.Audit;
using MentalHealth.Domain.DataRights;

namespace MentalHealth.Application.DataRights;

public sealed class DeleteDemoSubjectHandler(
    IDataRightsRepository dataRights,
    IObjectStorage storage,
    IUnitOfWork unitOfWork,
    IAuditTrail auditTrail,
    IClock clock)
{
    public async Task HandleAsync(
        ConsultationActor actor,
        CancellationToken cancellationToken)
    {
        var subjectId = actor.RequireOwnedSubject();
        DemoDataDeletion? deletion = null;
        await unitOfWork.ExecuteInTransactionAsync(async transactionToken =>
        {
            deletion = await dataRights.FindDeletionAsync(
                subjectId,
                transactionToken);
            if (deletion is null)
            {
                deletion = DemoDataDeletion.Request(
                    subjectId,
                    actor.UserId,
                    clock.UtcNow);
                dataRights.Add(deletion);
            }
            else
            {
                deletion.Retry(actor.UserId, clock.UtcNow);
            }

            await unitOfWork.SaveChangesAsync(transactionToken);
        }, cancellationToken);

        var media = await dataRights.ListSubjectMediaAsync(
            subjectId,
            cancellationToken);
        foreach (var item in media)
        {
            if (item.ObjectKey is { } objectKey)
            {
                EnsureOwnedDemoObject(subjectId, objectKey);
                await storage.DeleteAsync(objectKey, cancellationToken);
            }

            for (var index = 0; index < item.ExpectedChunks; index += 1)
            {
                await storage.DeleteAsync(
                    MediaStorageKeys.Chunk(item.AssetId, index),
                    cancellationToken);
            }
        }

        await unitOfWork.ExecuteInTransactionAsync(async transactionToken =>
        {
            await dataRights.DeleteSubjectDataAsync(subjectId, transactionToken);
            deletion!.MarkDeleted(clock.UtcNow);
            auditTrail.Add(AuditEvent.Create(
                actor.UserId,
                "DemoDataDeleted",
                "Subject",
                subjectId,
                clock.UtcNow,
                "用户确认清除演示数据"));
            await unitOfWork.SaveChangesAsync(transactionToken);
        }, cancellationToken);
    }

    private static void EnsureOwnedDemoObject(Guid subjectId, string objectKey)
    {
        var expectedPrefix = $"demo/{subjectId:N}/";
        if (!objectKey.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Unsafe demo deletion target.");
        }
    }
}

public sealed class DemoRetentionHandler(
    IDataRightsRepository dataRights,
    IObjectStorage storage,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private const int BatchSize = 100;

    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var candidates = await dataRights.ListRetentionCandidatesAsync(
            clock.UtcNow.AddDays(-30),
            BatchSize,
            cancellationToken);
        var deletedCount = 0;
        foreach (var asset in candidates)
        {
            if (asset.ObjectKey is not { } objectKey)
            {
                continue;
            }

            var expectedPrefix = $"demo/{asset.SubjectId:N}/";
            if (!objectKey.StartsWith(expectedPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Unsafe demo retention target.");
            }

            await storage.DeleteAsync(objectKey, cancellationToken);
            asset.PurgeRawMedia(clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            deletedCount += 1;
        }

        return deletedCount;
    }
}
