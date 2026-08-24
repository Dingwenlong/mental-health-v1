using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Application.Audit;
using MentalHealth.Application.Consents;
using MentalHealth.Application.Catalog;
using MentalHealth.Application.Consultations;
using MentalHealth.Application.Consultations.Media;
using MentalHealth.Application.Analysis;
using MentalHealth.Application.FollowUps;
using MentalHealth.Application.DataRights;
using MentalHealth.Domain.Audit;
using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.Consents;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.FollowUps;
using MentalHealth.Domain.DataRights;
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
        IAnalysisRepository,
        IRiskRuleSetRepository,
        IRiskAssessmentRepository,
        IObservationCaseRepository,
        IFollowUpRepository,
        IDataRightsRepository
{
    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (Database.CurrentTransaction is not null)
        {
            await action(cancellationToken);
            return;
        }

        await using var transaction = await Database.BeginTransactionAsync(
            cancellationToken);
        await action(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

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

    public DbSet<RiskRuleSet> RiskRuleSets => Set<RiskRuleSet>();

    public DbSet<RiskAssessment> RiskAssessments => Set<RiskAssessment>();

    public DbSet<ObservationCase> ObservationCases => Set<ObservationCase>();

    public DbSet<ClinicalReview> ClinicalReviews => Set<ClinicalReview>();

    public DbSet<DemoDataDeletion> DemoDataDeletions => Set<DemoDataDeletion>();

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

    public Task<RiskRuleSet?> FindRuleSetAsync(
        string version,
        CancellationToken cancellationToken) =>
        RiskRuleSets.SingleOrDefaultAsync(
            rule => rule.Version == version,
            cancellationToken);

    public Task<RiskRuleSet?> FindActiveRuleSetAsync(
        CancellationToken cancellationToken) =>
        RiskRuleSets.SingleOrDefaultAsync(
            rule => rule.Active,
            cancellationToken);

    public async Task<IReadOnlyList<RiskRuleSet>> ListRuleSetsAsync(
        CancellationToken cancellationToken) =>
        await RiskRuleSets
            .AsNoTracking()
            .OrderByDescending(rule => rule.Active)
            .ThenByDescending(rule => rule.CreatedAt)
            .ThenBy(rule => rule.Version)
            .ToArrayAsync(cancellationToken);

    public Task<RiskAssessment?> FindLatestAssessmentAsync(
        Guid sessionId,
        CancellationToken cancellationToken) =>
        RiskAssessments
            .Include(assessment => assessment.Evidence)
            .Where(assessment => assessment.SessionId == sessionId)
            .OrderByDescending(assessment => assessment.CreatedAt)
            .ThenByDescending(assessment => assessment.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<RiskAssessment?> FindAssessmentByIdAsync(
        Guid assessmentId,
        CancellationToken cancellationToken) =>
        RiskAssessments
            .Include(assessment => assessment.Evidence)
            .SingleOrDefaultAsync(
                assessment => assessment.Id == assessmentId,
                cancellationToken);

    public Task<RiskAssessment?> FindAssessmentAsync(
        Guid sessionId,
        string ruleSetVersion,
        int? transcriptRevision,
        CancellationToken cancellationToken) =>
        RiskAssessments
            .Include(assessment => assessment.Evidence)
            .SingleOrDefaultAsync(
                assessment => assessment.SessionId == sessionId
                    && assessment.RuleSetVersion == ruleSetVersion
                    && assessment.TranscriptRevision == transcriptRevision,
                cancellationToken);

    public void Add(RiskRuleSet ruleSet) => RiskRuleSets.Add(ruleSet);

    public void Add(RiskAssessment assessment) => RiskAssessments.Add(assessment);

    public Task<ObservationCase?> FindObservationByAssessmentAsync(
        Guid assessmentId,
        CancellationToken cancellationToken) =>
        ObservationCases.SingleOrDefaultAsync(
            item => item.AssessmentId == assessmentId,
            cancellationToken);

    public Task<ObservationCase?> FindObservationCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken) =>
        ObservationCases.SingleOrDefaultAsync(
            item => item.Id == caseId,
            cancellationToken);

    public async Task<IReadOnlyList<ObservationCase>> ListCasesAsync(
        RiskLevel? level,
        ObservationCaseStatus? status,
        CancellationToken cancellationToken)
    {
        var query = ObservationCases.AsNoTracking().AsQueryable();
        if (level is { } exactLevel)
        {
            query = query.Where(item => item.CurrentLevel == exactLevel);
        }

        if (status is { } exactStatus)
        {
            query = query.Where(item => item.Status == exactStatus);
        }

        return await query
            .OrderBy(item => item.CurrentLevel == RiskLevel.Crisis
                ? 0
                : item.CurrentLevel == RiskLevel.L3
                    ? 1
                    : item.CurrentLevel == RiskLevel.L2 ? 2 : 3)
            .ThenBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClinicalReview>> ListReviewsAsync(
        Guid caseId,
        CancellationToken cancellationToken) =>
        await ClinicalReviews
            .AsNoTracking()
            .Where(review => review.ObservationCaseId == caseId)
            .OrderBy(review => review.ReviewedAt)
            .ThenBy(review => review.Id)
            .ToArrayAsync(cancellationToken);

    public void Add(ObservationCase observationCase) =>
        ObservationCases.Add(observationCase);

    public void Add(ClinicalReview review) => ClinicalReviews.Add(review);

    public Task<FollowUpTask?> FindFollowUpByAssessmentAsync(
        Guid assessmentId,
        CancellationToken cancellationToken) =>
        FollowUpTasks.SingleOrDefaultAsync(
            task => task.AssessmentId == assessmentId,
            cancellationToken);

    public Task<FollowUpTask?> FindFollowUpAsync(
        Guid taskId,
        CancellationToken cancellationToken) =>
        FollowUpTasks.SingleOrDefaultAsync(
            task => task.Id == taskId,
            cancellationToken);

    public async Task<IReadOnlyList<FollowUpCandidate>> ListCandidatesAsync(
        DateTimeOffset now,
        DateTimeOffset deadline,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from slot in AvailabilitySlots.AsNoTracking()
            join practitioner in Practitioners.AsNoTracking()
                on slot.PractitionerId equals practitioner.Id
            where slot.Active
                && practitioner.Active
                && practitioner.Role == PractitionerRole.Doctor
                && slot.StartAt >= now
                && slot.StartAt <= deadline
                && !FollowUpTasks.Any(task =>
                    task.AvailabilitySlotId == slot.Id
                    && task.Status != FollowUpStatus.Completed
                    && task.Status != FollowUpStatus.Cancelled)
            select new
            {
                Slot = slot,
                practitioner.Role,
                Incomplete = FollowUpTasks.Count(task =>
                    task.AssigneeId == practitioner.Id
                    && task.Status != FollowUpStatus.Completed
                    && task.Status != FollowUpStatus.Cancelled)
            }).ToArrayAsync(cancellationToken);
        return rows.Select(row => new FollowUpCandidate(
            row.Slot.Id,
            row.Slot.PractitionerId,
            row.Role,
            true,
            row.Slot.StartAt,
            row.Slot.EndAt,
            row.Incomplete)).ToArray();
    }

    public async Task<FollowUpCandidate?> FindCandidateAsync(
        Guid availabilitySlotId,
        CancellationToken cancellationToken)
    {
        var row = await (
            from slot in AvailabilitySlots.AsNoTracking()
            join practitioner in Practitioners.AsNoTracking()
                on slot.PractitionerId equals practitioner.Id
            where slot.Id == availabilitySlotId
            select new
            {
                Slot = slot,
                practitioner.Role,
                Active = slot.Active
                    && practitioner.Active
                    && !FollowUpTasks.Any(task =>
                        task.AvailabilitySlotId == slot.Id
                        && task.Status != FollowUpStatus.Completed
                        && task.Status != FollowUpStatus.Cancelled),
                Incomplete = FollowUpTasks.Count(task =>
                    task.AssigneeId == practitioner.Id
                    && task.Status != FollowUpStatus.Completed
                    && task.Status != FollowUpStatus.Cancelled)
            }).SingleOrDefaultAsync(cancellationToken);
        return row is null
            ? null
            : new FollowUpCandidate(
                row.Slot.Id,
                row.Slot.PractitionerId,
                row.Role,
                row.Active,
                row.Slot.StartAt,
                row.Slot.EndAt,
                row.Incomplete);
    }

    public async Task<IReadOnlyList<FollowUpTask>> ListForSubjectAsync(
        Guid subjectId,
        CancellationToken cancellationToken) =>
        await FollowUpTasks
            .AsNoTracking()
            .Where(task => task.SubjectId == subjectId)
            .OrderBy(task => task.DueAt ?? task.Deadline)
            .ThenBy(task => task.Id)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<FollowUpTask>> ListForAssigneeAsync(
        Guid practitionerId,
        CancellationToken cancellationToken) =>
        await FollowUpTasks
            .AsNoTracking()
            .Where(task => task.AssigneeId == practitionerId)
            .OrderBy(task => task.DueAt ?? task.Deadline)
            .ThenBy(task => task.Id)
            .ToArrayAsync(cancellationToken);

    public void Add(FollowUpTask task) => FollowUpTasks.Add(task);

    public async Task<SubjectDataSnapshot> ReadSubjectDataAsync(
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        var sessions = await ConsultationSessions
            .AsNoTracking()
            .Where(session => session.SubjectId == subjectId)
            .OrderBy(session => session.ScheduledAt)
            .ThenBy(session => session.Id)
            .ToArrayAsync(cancellationToken);
        var sessionIds = sessions.Select(session => session.Id).ToArray();
        var messages = sessionIds.Length == 0
            ? []
            : await Messages
                .AsNoTracking()
                .Where(message => sessionIds.Contains(message.SessionId))
                .OrderBy(message => message.SessionId)
                .ThenBy(message => message.Sequence)
                .ToArrayAsync(cancellationToken);
        var transcripts = sessionIds.Length == 0
            ? []
            : await ManualTranscripts
                .AsNoTracking()
                .Where(transcript => sessionIds.Contains(transcript.SessionId))
                .OrderBy(transcript => transcript.SessionId)
                .ThenBy(transcript => transcript.Revision)
                .ToArrayAsync(cancellationToken);
        var consents = await ConsentRecords
            .AsNoTracking()
            .Where(consent => consent.SubjectId == subjectId)
            .OrderBy(consent => consent.GrantedAt)
            .ToArrayAsync(cancellationToken);
        var assessments = await RiskAssessments
            .AsNoTracking()
            .Where(assessment => assessment.SubjectId == subjectId)
            .OrderBy(assessment => assessment.CreatedAt)
            .ToArrayAsync(cancellationToken);
        var followUps = await FollowUpTasks
            .AsNoTracking()
            .Where(task => task.SubjectId == subjectId)
            .OrderBy(task => task.ProposedAt)
            .ToArrayAsync(cancellationToken);

        return new SubjectDataSnapshot(
            subjectId,
            sessions.Select(session => new SubjectConsultationExport(
                session.Id,
                session.Kind.ToString(),
                session.Channel.ToString(),
                session.Status.ToString(),
                session.ScheduledAt,
                session.StartedAt,
                session.CompletedAt)).ToArray(),
            messages.Select(message => new SubjectMessageExport(
                message.Id,
                message.SessionId,
                message.SenderKind.ToString(),
                message.Text,
                message.Sequence,
                message.SentAt)).ToArray(),
            transcripts.Select(transcript => new SubjectTranscriptExport(
                transcript.SessionId,
                transcript.Revision,
                transcript.Source.ToString(),
                transcript.Text,
                transcript.Sha256,
                transcript.CreatedAt)).ToArray(),
            consents.Select(consent => new SubjectConsentExport(
                consent.Id,
                consent.Kind,
                consent.TextVersion,
                consent.GrantedAt,
                consent.WithdrawnAt)).ToArray(),
            assessments.Select(assessment => new SubjectAssessmentExport(
                assessment.Id,
                assessment.SessionId,
                assessment.Score,
                assessment.Level.ToString(),
                assessment.Confidence,
                assessment.IsCrisis,
                assessment.CreatedAt)).ToArray(),
            followUps.Select(task => new SubjectFollowUpExport(
                task.Id,
                task.AssessmentId,
                task.Status.ToString(),
                task.DueAt,
                task.Deadline,
                task.CompletedAt,
                task.CancelledAt)).ToArray());
    }

    public async Task<SubjectMediaReference?> FindOwnedMediaAsync(
        Guid subjectId,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        var asset = await MediaAssets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                media => media.Id == assetId
                    && media.SubjectId == subjectId
                    && media.Status == MediaAssetStatus.Completed,
                cancellationToken);
        return asset is null ? null : ToMediaReference(asset);
    }

    public async Task<IReadOnlyList<SubjectMediaReference>> ListSubjectMediaAsync(
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        var media = await MediaAssets
            .AsNoTracking()
            .Where(asset => asset.SubjectId == subjectId)
            .OrderBy(asset => asset.CapturedAt)
            .ThenBy(asset => asset.Id)
            .ToArrayAsync(cancellationToken);
        return media.Select(ToMediaReference).ToArray();
    }

    public Task<DemoDataDeletion?> FindDeletionAsync(
        Guid subjectId,
        CancellationToken cancellationToken) =>
        DemoDataDeletions.SingleOrDefaultAsync(
            deletion => deletion.SubjectId == subjectId,
            cancellationToken);

    public void Add(DemoDataDeletion deletion) =>
        DemoDataDeletions.Add(deletion);

    public async Task DeleteSubjectDataAsync(
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        var sessionIds = await ConsultationSessions
            .Where(session => session.SubjectId == subjectId)
            .Select(session => session.Id)
            .ToArrayAsync(cancellationToken);
        var assessmentIds = await RiskAssessments
            .Where(assessment => assessment.SubjectId == subjectId)
            .Select(assessment => assessment.Id)
            .ToArrayAsync(cancellationToken);
        var observationIds = assessmentIds.Length == 0
            ? []
            : await ObservationCases
                .Where(item => assessmentIds.Contains(item.AssessmentId))
                .Select(item => item.Id)
                .ToArrayAsync(cancellationToken);
        var followUpIds = await FollowUpTasks
            .Where(task => task.SubjectId == subjectId)
            .Select(task => task.Id)
            .ToArrayAsync(cancellationToken);

        if (assessmentIds.Length > 0)
        {
            await ClinicalReviews
                .Where(review => assessmentIds.Contains(review.AssessmentId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (observationIds.Length > 0)
        {
            await ObservationCases
                .Where(item => observationIds.Contains(item.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await FollowUpTasks
            .Where(task => task.SubjectId == subjectId)
            .ExecuteDeleteAsync(cancellationToken);
        if (sessionIds.Length > 0)
        {
            await AnalysisJobs
                .Where(job => sessionIds.Contains(job.SessionId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await RiskAssessments
            .Where(assessment => assessment.SubjectId == subjectId)
            .ExecuteDeleteAsync(cancellationToken);
        if (sessionIds.Length > 0)
        {
            await ManualTranscripts
                .Where(transcript => sessionIds.Contains(transcript.SessionId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await MediaAssets
            .Where(asset => asset.SubjectId == subjectId)
            .ExecuteDeleteAsync(cancellationToken);
        if (sessionIds.Length > 0)
        {
            await Messages
                .Where(message => sessionIds.Contains(message.SessionId))
                .ExecuteDeleteAsync(cancellationToken);
            await OutboxMessages
                .Where(message => sessionIds.Contains(message.AggregateId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (followUpIds.Length > 0)
        {
            await OutboxMessages
                .Where(message => followUpIds.Contains(message.AggregateId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await ConsultationSessions
            .Where(session => session.SubjectId == subjectId)
            .ExecuteDeleteAsync(cancellationToken);
        await DemoOrders
            .Where(order => order.SubjectId == subjectId)
            .ExecuteDeleteAsync(cancellationToken);
        await ConsentRecords
            .Where(consent => consent.SubjectId == subjectId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MediaAsset>> ListRetentionCandidatesAsync(
        DateTimeOffset capturedBefore,
        int maximumCount,
        CancellationToken cancellationToken) =>
        await MediaAssets
            .Where(asset => asset.IsDemo
                && asset.Status == MediaAssetStatus.Completed
                && asset.ObjectKey != null
                && asset.CapturedAt < capturedBefore)
            .OrderBy(asset => asset.CapturedAt)
            .ThenBy(asset => asset.Id)
            .Take(maximumCount)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<SafeAuditRecord>> ListAuditAsync(
        int maximumCount,
        CancellationToken cancellationToken)
    {
        var allowedActions = new[]
        {
            "ConsentGranted",
            "ConsentWithdrawn",
            "RecordViewed",
            "RiskReviewed",
            "FollowUpRescheduled",
            "DemoDataDeleted"
        };
        return await AuditEvents
            .AsNoTracking()
            .Where(audit => allowedActions.Contains(audit.Action))
            .OrderByDescending(audit => audit.OccurredAt)
            .ThenByDescending(audit => audit.Id)
            .Take(maximumCount)
            .Select(audit => new SafeAuditRecord(
                audit.OccurredAt,
                audit.ActorUserId,
                audit.Action,
                audit.ResourceId,
                audit.Reason))
            .ToArrayAsync(cancellationToken);
    }

    private static SubjectMediaReference ToMediaReference(MediaAsset asset) =>
        new(
            asset.Id,
            asset.SubjectId,
            asset.ContentType,
            asset.ObjectKey,
            asset.ExpectedChunks,
            asset.CapturedAt);
}
