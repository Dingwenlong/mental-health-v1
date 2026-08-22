using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Application.Audit;
using MentalHealth.Application.Consents;
using MentalHealth.Application.Catalog;
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
        IUnitOfWork,
        ICatalogRepository,
        IOrderRepository
{
    public DbSet<Practitioner> Practitioners => Set<Practitioner>();

    public DbSet<AvailabilitySlot> AvailabilitySlots => Set<AvailabilitySlot>();

    public DbSet<ServicePlan> ServicePlans => Set<ServicePlan>();

    public DbSet<DemoOrder> DemoOrders => Set<DemoOrder>();

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
}
