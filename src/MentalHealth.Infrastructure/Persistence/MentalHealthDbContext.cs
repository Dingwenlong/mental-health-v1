using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Application.Audit;
using MentalHealth.Application.Consents;
using MentalHealth.Domain.Audit;
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
        IUnitOfWork
{
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<ConsultationSession> ConsultationSessions => Set<ConsultationSession>();

    public DbSet<FollowUpTask> FollowUpTasks => Set<FollowUpTask>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

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
}
