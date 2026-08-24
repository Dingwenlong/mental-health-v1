using System.Text.Json;
using MentalHealth.AnalysisWorker.Consumers;
using MentalHealth.AnalysisWorker.Pipeline;
using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Application.Analysis;
using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.Consents;
using MentalHealth.Domain.Consultations;
using MentalHealth.Application.Consultations;
using MentalHealth.Application.Security;
using MentalHealth.Infrastructure.Outbox;
using MentalHealth.Infrastructure.Persistence;
using MentalHealth.Infrastructure.Providers;
using MentalHealth.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MentalHealth.IntegrationTests.Analysis;

[Collection(PersistenceCollection.Name)]
public sealed class WorkerIdempotencyTests(PersistenceFixture fixture)
{
    private readonly MutableClock _clock = new(
        DateTimeOffset.Parse("2026-08-24T02:00:00+00:00"));

    [Fact]
    public async Task Same_consultation_event_creates_one_analysis_job()
    {
        var sessionId = Guid.NewGuid();
        await InsertCompletedEventsAsync(sessionId, count: 2);

        await using var db = fixture.CreateDbContext();
        var pipeline = CreatePipeline(db, new ManualTranscriptionProvider(db));

        await pipeline.RunOneBatchAsync(CancellationToken.None);

        Assert.Equal(1, await db.AnalysisJobs.CountAsync(job => job.SessionId == sessionId));
    }

    [Fact]
    public async Task Concurrent_requests_create_one_analysis_job_without_an_error()
    {
        var sessionId = Guid.NewGuid();
        await using var firstDb = fixture.CreateDbContext();
        await using var secondDb = fixture.CreateDbContext();
        var first = new RequestAnalysisHandler(firstDb, firstDb, _clock);
        var second = new RequestAnalysisHandler(secondDb, secondDb, _clock);

        var jobs = await Task.WhenAll(
            first.HandleAsync(sessionId, CancellationToken.None),
            second.HandleAsync(sessionId, CancellationToken.None));

        await using var verification = fixture.CreateDbContext();
        Assert.Equal(jobs[0].Id, jobs[1].Id);
        Assert.Equal(
            1,
            await verification.AnalysisJobs.CountAsync(job => job.SessionId == sessionId));
    }

    [Fact]
    public async Task Missing_manual_transcript_moves_job_to_needs_manual()
    {
        var sessionId = Guid.NewGuid();
        await InsertCompletedEventsAsync(sessionId, count: 1);

        await using var db = fixture.CreateDbContext();
        var pipeline = CreatePipeline(db, new ManualTranscriptionProvider(db));

        await pipeline.RunOneBatchAsync(CancellationToken.None);

        var job = await db.AnalysisJobs.SingleAsync(item => item.SessionId == sessionId);
        Assert.Equal(AnalysisJobStatus.NeedsManual, job.Status);
        Assert.Equal("TRANSCRIPT_REQUIRED", job.FailureCode);
    }

    [Fact]
    public async Task Expired_lease_can_be_taken_by_another_worker()
    {
        var sessionId = Guid.NewGuid();
        await InsertCompletedEventsAsync(sessionId, count: 1);
        var factory = new FixtureDbContextFactory(fixture);
        var reader = new PostgresOutboxReader(factory, _clock);

        var first = await reader.LeaseBatchAsync(
            "worker-a", 100, TimeSpan.FromMinutes(1), CancellationToken.None);
        Assert.Contains(first, item => item.AggregateId == sessionId);

        var immediate = await reader.LeaseBatchAsync(
            "worker-b", 100, TimeSpan.FromMinutes(1), CancellationToken.None);
        Assert.DoesNotContain(immediate, item => item.AggregateId == sessionId);

        _clock.Advance(TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(1)));
        var recovered = await reader.LeaseBatchAsync(
            "worker-b", 100, TimeSpan.FromMinutes(1), CancellationToken.None);
        Assert.Contains(recovered, item => item.AggregateId == sessionId);
    }

    [Fact]
    public async Task Third_worker_failure_moves_job_to_needs_manual()
    {
        var sessionId = Guid.NewGuid();
        await InsertCompletedEventsAsync(sessionId, count: 1);

        await using var db = fixture.CreateDbContext();
        var pipeline = CreatePipeline(db, new FailingTranscriptionProvider());

        await pipeline.RunOneBatchAsync(CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(11));
        await pipeline.RunOneBatchAsync(CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(31));
        await pipeline.RunOneBatchAsync(CancellationToken.None);

        var job = await db.AnalysisJobs.SingleAsync(item => item.SessionId == sessionId);
        var message = await db.OutboxMessages.SingleAsync(item => item.AggregateId == sessionId);
        Assert.Equal(AnalysisJobStatus.NeedsManual, job.Status);
        Assert.Equal("TRANSCRIPTION_TEMPORARY", job.FailureCode);
        Assert.Equal(3, message.Attempts);
        Assert.NotNull(message.ProcessedAt);
    }

    [Fact]
    public async Task Cancellation_does_not_count_as_a_worker_failure()
    {
        var sessionId = Guid.NewGuid();
        await InsertCompletedEventsAsync(sessionId, count: 1);
        await using var db = fixture.CreateDbContext();
        var pipeline = CreatePipeline(db, new ManualTranscriptionProvider(db));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pipeline.RunOneBatchAsync(cancelled.Token));

        var message = await db.OutboxMessages.SingleAsync(item => item.AggregateId == sessionId);
        Assert.Equal(0, message.Attempts);
        Assert.Null(message.ProcessedAt);
    }

    [Fact]
    public async Task Correction_creates_a_new_revision_and_keeps_the_original()
    {
        var subjectId = Guid.NewGuid();
        var session = ConsultationSession.Create(
            subjectId,
            ConsultationKind.Human,
            ConsultationChannel.Chat);
        session.RequestConsent();
        session.Schedule(
            new HashSet<ConsentKind> { ConsentKind.Service, ConsentKind.AiAnalysis },
            _clock.UtcNow.AddMinutes(1));
        session.Start(_clock.UtcNow.AddMinutes(1));
        session.Complete(_clock.UtcNow.AddMinutes(30));

        await using var db = fixture.CreateDbContext();
        db.ConsultationSessions.Add(session);
        await db.SaveChangesAsync();
        var actor = new ConsultationActor(
            Guid.NewGuid(),
            subjectId,
            PractitionerId: null,
            Roles: [AppRoles.User]);
        var handler = new SaveManualTranscriptHandler(
            new SessionAccessService(db),
            db,
            db,
            _clock);

        var first = await handler.HandleAsync(
            actor,
            session.Id,
            "ManualUpload",
            "第一版人工转写。",
            CancellationToken.None);
        var second = await handler.HandleAsync(
            actor,
            session.Id,
            "ManualCorrection",
            "第二版人工校对。",
            CancellationToken.None);

        var saved = await db.ManualTranscripts
            .AsNoTracking()
            .Where(item => item.SessionId == session.Id)
            .OrderBy(item => item.Revision)
            .ToArrayAsync();
        var job = await db.AnalysisJobs.SingleAsync(item => item.SessionId == session.Id);
        Assert.Equal([1, 2], saved.Select(item => item.Revision));
        Assert.Equal("第一版人工转写。", saved[0].Text);
        Assert.Equal("第二版人工校对。", saved[1].Text);
        Assert.NotEqual(first.Sha256, second.Sha256);
        Assert.Equal(2, job.TranscriptRevision);
        Assert.Equal(AnalysisJobStatus.Ready, job.Status);
    }

    private AnalysisPipeline CreatePipeline(
        MentalHealthDbContext db,
        ITranscriptionProvider provider)
    {
        var handler = new RequestAnalysisHandler(db, db, _clock);
        var consumer = new ConsultationCompletedConsumer(handler, provider);
        var reader = new PostgresOutboxReader(new FixtureDbContextFactory(fixture), _clock);
        return new AnalysisPipeline(reader, consumer, "integration-worker");
    }

    private async Task InsertCompletedEventsAsync(Guid sessionId, int count)
    {
        await using var db = fixture.CreateDbContext();
        for (var index = 0; index < count; index++)
        {
            var eventMessage = new ConsultationCompletedDomainEvent(
                Guid.NewGuid(),
                sessionId,
                Guid.NewGuid(),
                _clock.UtcNow);
            db.OutboxMessages.Add(new OutboxMessage(
                eventMessage.EventId,
                "ConsultationCompleted",
                eventMessage.OccurredAt,
                JsonSerializer.Serialize(eventMessage, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                sessionId));
        }

        await db.SaveChangesAsync();
    }

    private sealed class MutableClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = utcNow;

        public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
    }

    private sealed class FixtureDbContextFactory(PersistenceFixture fixture)
        : IDbContextFactory<MentalHealthDbContext>
    {
        public MentalHealthDbContext CreateDbContext() => fixture.CreateDbContext();

        public Task<MentalHealthDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }

    private sealed class FailingTranscriptionProvider : ITranscriptionProvider
    {
        public Task<TranscriptDocument?> GetAsync(
            TranscriptionRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new ProviderException("TRANSCRIPTION_TEMPORARY");
        }
    }
}
