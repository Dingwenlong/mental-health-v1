using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Application.Audit;
using MentalHealth.Application.Consultations;
using MentalHealth.Application.DataRights;
using MentalHealth.Application.Security;
using MentalHealth.Domain.Audit;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.DataRights;

namespace MentalHealth.UnitTests.DataRights;

public sealed class DeleteDemoSubjectHandlerTests
{
    [Fact]
    public async Task Storage_failure_leaves_deletion_pending_for_retry()
    {
        var now = new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.Zero);
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var repository = new RecordingDataRightsRepository(
            new SubjectMediaReference(
                Guid.NewGuid(),
                subjectId,
                "video/webm",
                $"demo/{subjectId:N}/media/recording.webm",
                1,
                now));
        var unitOfWork = new ImmediateUnitOfWork();
        var audit = new RecordingAuditTrail();
        var handler = new DeleteDemoSubjectHandler(
            repository,
            new FailingObjectStorage(),
            unitOfWork,
            audit,
            new FixedClock(now));
        var actor = new ConsultationActor(
            userId,
            subjectId,
            null,
            [AppRoles.User]);

        await Assert.ThrowsAsync<IOException>(() =>
            handler.HandleAsync(actor, CancellationToken.None));

        Assert.NotNull(repository.Deletion);
        Assert.Equal(
            DemoDataDeletionStatus.DeletionPending,
            repository.Deletion.Status);
        Assert.Null(repository.Deletion.DeletedAt);
        Assert.False(repository.SubjectRowsDeleted);
        Assert.Empty(audit.Events);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    private sealed class RecordingDataRightsRepository(SubjectMediaReference media)
        : IDataRightsRepository
    {
        public DemoDataDeletion? Deletion { get; private set; }

        public bool SubjectRowsDeleted { get; private set; }

        public void Add(DemoDataDeletion deletion) => Deletion = deletion;

        public Task<DemoDataDeletion?> FindDeletionAsync(
            Guid subjectId,
            CancellationToken cancellationToken) => Task.FromResult(Deletion);

        public Task<IReadOnlyList<SubjectMediaReference>> ListSubjectMediaAsync(
            Guid subjectId,
            CancellationToken cancellationToken) => Task.FromResult<
                IReadOnlyList<SubjectMediaReference>>([media]);

        public Task DeleteSubjectDataAsync(
            Guid subjectId,
            CancellationToken cancellationToken)
        {
            SubjectRowsDeleted = true;
            return Task.CompletedTask;
        }

        public Task<SubjectDataSnapshot> ReadSubjectDataAsync(
            Guid subjectId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SubjectMediaReference?> FindOwnedMediaAsync(
            Guid subjectId,
            Guid assetId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<MediaAsset>> ListRetentionCandidatesAsync(
            DateTimeOffset capturedBefore,
            int maximumCount,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<SafeAuditRecord>> ListAuditAsync(
            int maximumCount,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FailingObjectStorage : IObjectStorage
    {
        public Task<StoredObject> PutAsync(
            ObjectWriteRequest request,
            Stream content,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(
            string objectKey,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DeleteAsync(
            string objectKey,
            CancellationToken cancellationToken) => throw new IOException("storage unavailable");
    }

    private sealed class ImmediateUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount += 1;
            return Task.FromResult(1);
        }

        public Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default) => action(cancellationToken);
    }

    private sealed class RecordingAuditTrail : IAuditTrail
    {
        public List<AuditEvent> Events { get; } = [];

        public void Add(AuditEvent auditEvent) => Events.Add(auditEvent);
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
