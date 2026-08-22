using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.FollowUps;
using MentalHealth.Domain.Shared;
using MentalHealth.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace MentalHealth.Infrastructure.Persistence;

public sealed class MentalHealthDbContext(DbContextOptions<MentalHealthDbContext> options)
    : DbContext(options)
{
    public DbSet<ConsultationSession> ConsultationSessions => Set<ConsultationSession>();

    public DbSet<FollowUpTask> FollowUpTasks => Set<FollowUpTask>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
}
