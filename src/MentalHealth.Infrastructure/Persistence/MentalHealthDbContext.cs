using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Application.Audit;
using MentalHealth.Application.Consents;
using MentalHealth.Application.Catalog;
using MentalHealth.Application.Consultations;
using MentalHealth.Application.Consultations.Media;
using MentalHealth.Application.Analysis;
using MentalHealth.Domain.Audit;
using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.Consents;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.FollowUps;
using MentalHealth.Domain.Shared;
using MentalHealth.Infrastructure.Outbox;
using MentalHealth.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MentalHealth.Infrastructure.Persistence;

public sealed class MentalHealthDbContext(DbContextOptions<MentalHealthDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options),
        IConsentRepository,
        IAuditTrail,
        IUnitOfWork,
        ICatalogRepository,
        IOrderRepository,
        IConsultationRepository,
        IMediaAssetRepository,
        IAnalysisRepository
{
    public DbSet<Practitioner> Practitioners => Set<Practitioner>();

    public DbSet<AvailabilitySlot> AvailabilitySlots => Set<AvailabilitySlot>();

    public DbSet<ServicePlan> ServicePlans => Set<ServicePlan>();

    public DbSet<DemoOrder> DemoOrders => Set<DemoOrder>();

    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<ConsultationSession> ConsultationSessions => Set<ConsultationSession>();

    public DbSet<FollowUpTask> FollowUpTasks => Set<FollowUpTask>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<AnalysisJob> AnalysisJobs => Set<AnalysisJob>();

    public DbSet<ManualTranscript> ManualTranscripts => Set<ManualTranscript>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MentalHealthDbContext).Assembly);
    }

    internal void EnqueueDomainEvents()
    {
        ChangeTracker.DetectChanges();
        var domainEvents = ChangeTracker
            .Entries<IHasDomainEvents>()
            .SelectMany(entry => entry.Entity.DomainEvents)
            .ToArray();

        foreach (var domainEvent in domainEvents)
        {
            if (OutboxMessages.Local.All(message => message.Id != domainEvent.EventId))
            {
                OutboxMessages.Add(OutboxMessage.FromDomainEvent(domainEvent));
            }
        }
    }

    internal void ClearDomainEvents()
    {
        foreach (var entry in ChangeTracker.Entries<IHasDomainEvents>())
        {
            entry.Entity.ClearDomainEvents();
        }
    }

    public Task<ConsentRecord?> FindActiveAsync(
        Guid subjectId,
        ConsentKind kind,
        CancellationToken cancellationToken)
    {
        return ConsentRecords.SingleOrDefaultAsync(
            consent => consent.SubjectId == subjectId
                && consent.Kind == kind
                && consent.WithdrawnAt == null,
            cancellationToken);
    }

    public void Add(ConsentRecord consent) => ConsentRecords.Add(consent);

    public Task<ConsentRecord?> FindActiveByIdAsync(
        Guid subjectId,
        Guid consentId,
        CancellationToken cancellationToken)
    {
        return ConsentRecords.SingleOrDefaultAsync(
            consent => consent.Id == consentId
                && consent.SubjectId == subjectId
                && consent.WithdrawnAt == null,
            cancellationToken);
    }

    public void Add(AuditEvent auditEvent) => AuditEvents.Add(auditEvent);

    public async Task<IReadOnlyList<ServicePlan>> ListActivePlansAsync(
        CancellationToken cancellationToken)
    {
        return await ServicePlans
            .AsNoTracking()
            .Where(plan => plan.Active)
            .OrderBy(plan => plan.Name)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Practitioner>> ListActivePractitionersAsync(
        CancellationToken cancellationToken)
    {
        return await Practitioners
            .AsNoTracking()
            .Where(practitioner => practitioner.Active)
            .OrderBy(practitioner => practitioner.DisplayName)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AvailabilitySlot>> ListActiveSlotsAsync(
        DateTimeOffset endingAfter,
        CancellationToken cancellationToken)
    {
        return await AvailabilitySlots
            .AsNoTracking()
            .Where(slot => slot.Active && slot.EndAt > endingAfter)
            .OrderBy(slot => slot.StartAt)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AvailabilitySlot>> ListActiveSlotsAsync(
        Guid practitionerId,
        CancellationToken cancellationToken)
    {
        return await AvailabilitySlots
            .Where(slot => slot.PractitionerId == practitionerId && slot.Active)
            .ToArrayAsync(cancellationToken);
    }

    public Task<ServicePlan?> FindPlanAsync(
        Guid planId,
        CancellationToken cancellationToken) =>
        ServicePlans.SingleOrDefaultAsync(
            plan => plan.Id == planId,
            cancellationToken);

    public Task<Practitioner?> FindPractitionerAsync(
        Guid practitionerId,
        CancellationToken cancellationToken) =>
        Practitioners.SingleOrDefaultAsync(
            practitioner => practitioner.Id == practitionerId,
            cancellationToken);

    public Task<AvailabilitySlot?> FindSlotAsync(
        Guid practitionerId,
        Guid slotId,
        CancellationToken cancellationToken) =>
        AvailabilitySlots.SingleOrDefaultAsync(
            slot => slot.Id == slotId
                && slot.PractitionerId == practitionerId
                && slot.Active,
            cancellationToken);

    public Task<bool> HasSlotOverlapAsync(
        Guid practitionerId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        CancellationToken cancellationToken) =>
        AvailabilitySlots.AnyAsync(
            slot => slot.PractitionerId == practitionerId
                && slot.Active
                && slot.StartAt < endAt
                && startAt < slot.EndAt,
            cancellationToken);

    public Task<bool> IsPractitionerLinkedToAccountAsync(
        Guid practitionerId,
        CancellationToken cancellationToken) =>
        Users.AnyAsync(
            user => user.PractitionerId == practitionerId,
            cancellationToken);

    public void Add(ServicePlan plan) => ServicePlans.Add(plan);

    public void Add(Practitioner practitioner) => Practitioners.Add(practitioner);

    public void Add(AvailabilitySlot slot) => AvailabilitySlots.Add(slot);

    public Task<DemoOrder?> FindByIdempotencyKeyAsync(
        Guid subjectId,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        DemoOrders.SingleOrDefaultAsync(
            order => order.SubjectId == subjectId
                && order.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public Task<DemoOrder?> FindAsync(
        Guid subjectId,
        Guid orderId,
        CancellationToken cancellationToken) =>
        DemoOrders.SingleOrDefaultAsync(
            order => order.Id == orderId && order.SubjectId == subjectId,
            cancellationToken);

    public void Add(DemoOrder order) => DemoOrders.Add(order);

    public Task<ConsultationSession?> FindAsync(
        Guid sessionId,
        CancellationToken cancellationToken) =>
        ConsultationSessions.SingleOrDefaultAsync(
            session => session.Id == sessionId,
            cancellationToken);

    public Task<ConsultationSession?> FindByCreationKeyAsync(
        Guid subjectId,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        ConsultationSessions.SingleOrDefaultAsync(
            session => session.SubjectId == subjectId
                && session.CreationIdempotencyKey == idempotencyKey,
            cancellationToken);

    public Task<ConsultationSession?> FindByOrderAsync(
        Guid subjectId,
        Guid orderId,
        CancellationToken cancellationToken) =>
        ConsultationSessions.SingleOrDefaultAsync(
            session => session.SubjectId == subjectId
                && session.OrderId == orderId,
            cancellationToken);

    public async Task<IReadOnlyList<Message>> ListMessagesAsync(
        Guid sessionId,
        int afterSequence,
        CancellationToken cancellationToken) =>
        await Messages
            .AsNoTracking()
            .Where(message => message.SessionId == sessionId
                && message.Sequence > afterSequence)
            .OrderBy(message => message.Sequence)
            .ToArrayAsync(cancellationToken);

    public async Task<MessageAppendResult> AppendMessageAsync(
        Guid sessionId,
        Guid senderUserId,
        MessageSenderKind senderKind,
        string text,
        string clientMessageId,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken)
    {
        await using var transaction = await Database.BeginTransactionAsync(
            cancellationToken);
        var session = await ConsultationSessions
            .FromSqlInterpolated(
                $"SELECT * FROM consultation_sessions WHERE id = {sessionId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("SESSION_NOT_FOUND");
        var normalizedClientMessageId = clientMessageId.Trim();
        var existing = await Messages.SingleOrDefaultAsync(
            message => message.SessionId == sessionId
                && message.ClientMessageId == normalizedClientMessageId,
            cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new MessageAppendResult(existing, false);
        }

        if (session.Status != ConsultationStatus.InProgress)
        {
            throw new DomainException("INVALID_SESSION_STATE");
        }

        var lastSequence = await Messages
            .Where(message => message.SessionId == sessionId)
            .Select(message => (int?)message.Sequence)
            .MaxAsync(cancellationToken) ?? 0;
        var message = Message.Create(
            sessionId,
            senderUserId,
            senderKind,
            text,
            normalizedClientMessageId,
            checked(lastSequence + 1),
            sentAt);
        Messages.Add(message);
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new MessageAppendResult(message, true);
    }

    public void Add(ConsultationSession session) =>
        ConsultationSessions.Add(session);

    Task<MediaAsset?> IMediaAssetRepository.FindAsync(
        Guid mediaAssetId,
        CancellationToken cancellationToken) =>
        MediaAssets.SingleOrDefaultAsync(
            asset => asset.Id == mediaAssetId,
            cancellationToken);

    Task<MediaAsset?> IMediaAssetRepository.FindByCreationKeyAsync(
        Guid sessionId,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        MediaAssets.SingleOrDefaultAsync(
            asset => asset.SessionId == sessionId
                && asset.CreationIdempotencyKey == idempotencyKey,
            cancellationToken);

    async Task<IReadOnlyList<MediaAsset>>
        IMediaAssetRepository.ListCleanupCandidatesAsync(
            DateTimeOffset expiresBefore,
            int maximumCount,
            CancellationToken cancellationToken) =>
        await MediaAssets
            .Where(asset => (asset.Status == MediaAssetStatus.Expired
                    && asset.ChunksDeletedAt == null)
                || (asset.Status == MediaAssetStatus.Uploading
                    && asset.UploadExpiresAt <= expiresBefore))
            .OrderBy(asset => asset.UploadExpiresAt)
            .Take(maximumCount)
            .ToArrayAsync(cancellationToken);

    void IMediaAssetRepository.Add(MediaAsset mediaAsset) =>
        MediaAssets.Add(mediaAsset);

    public async Task<AnalysisJob> GetOrCreateJobAsync(
        Guid sessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var tracked = AnalysisJobs.Local.SingleOrDefault(
            job => job.SessionId == sessionId);
        if (tracked is not null)
        {
            return tracked;
        }

        var requested = AnalysisJob.Request(sessionId, now);
        var status = requested.Status.ToString();
        await Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO analysis_jobs (
                id, session_id, status, attempts, created_at, updated_at)
            VALUES (
                {{requested.Id}}, {{requested.SessionId}}, {{status}},
                {{requested.Attempts}}, {{requested.CreatedAt}}, {{requested.UpdatedAt}})
            ON CONFLICT (session_id) DO NOTHING
            """, cancellationToken);
        return await AnalysisJobs.SingleAsync(
            job => job.SessionId == sessionId,
            cancellationToken);
    }

    public async Task<int> GetLatestTranscriptRevisionAsync(
        Guid sessionId,
        CancellationToken cancellationToken) =>
        await ManualTranscripts
            .Where(document => document.SessionId == sessionId)
            .Select(document => (int?)document.Revision)
            .MaxAsync(cancellationToken) ?? 0;

    public Task<ManualTranscript?> FindAsync(
        Guid sessionId,
        int? revision,
        CancellationToken cancellationToken)
    {
        var documents = ManualTranscripts
            .AsNoTracking()
            .Where(document => document.SessionId == sessionId);
        return revision is { } exact
            ? documents.SingleOrDefaultAsync(
                document => document.Revision == exact,
                cancellationToken)
            : documents
                .OrderByDescending(document => document.Revision)
                .FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(ManualTranscript transcript) => ManualTranscripts.Add(transcript);
}
