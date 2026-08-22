using Microsoft.EntityFrameworkCore;
using MentalHealth.Domain.Consents;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.FollowUps;
using MentalHealth.Infrastructure.Outbox;

namespace MentalHealth.IntegrationTests.Infrastructure;

[Collection(PersistenceCollection.Name)]
public sealed class OutboxAtomicityTests(PersistenceFixture fixture)
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-08-22T02:00:00+00:00");

    [Fact]
    public async Task Completing_session_and_outbox_message_commit_atomically()
    {
        var session = CreateCompletedSession();
        var eventId = session.DomainEvents.Single().EventId;

        await using (var db = fixture.CreateDbContext())
        {
            db.ConsultationSessions.Add(session);
            await db.SaveChangesAsync();
            await db.SaveChangesAsync();
        }

        await using var verification = fixture.CreateDbContext();
        var savedSession = await verification.ConsultationSessions
            .SingleAsync(item => item.Id == session.Id);
        var outbox = await verification.OutboxMessages
            .SingleAsync(item => item.Id == eventId);

        Assert.Equal(ConsultationStatus.Completed, savedSession.Status);
        Assert.Equal("ConsultationCompleted", outbox.Type);
        Assert.Contains(session.Id.ToString(), outbox.Payload);
        Assert.Empty(session.DomainEvents);
    }

    [Fact]
    public async Task Scheduling_follow_up_persists_state_and_both_domain_events()
    {
        var dueAt = Now.AddDays(1);
        var task = FollowUpTask.Schedule(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            dueAt,
            Now);
        var eventIds = task.DomainEvents.Select(domainEvent => domainEvent.EventId).ToArray();

        await using (var db = fixture.CreateDbContext())
        {
            db.FollowUpTasks.Add(task);
            await db.SaveChangesAsync();
        }

        await using var verification = fixture.CreateDbContext();
        var savedTask = await verification.FollowUpTasks
            .SingleAsync(item => item.Id == task.Id);
        var savedEventIds = await verification.OutboxMessages
            .Where(message => eventIds.Contains(message.Id))
            .Select(message => message.Id)
            .ToArrayAsync();

        Assert.Equal(FollowUpStatus.Scheduled, savedTask.Status);
        Assert.Equal(dueAt, savedTask.DueAt);
        Assert.Equal(eventIds.Order(), savedEventIds.Order());
        Assert.Empty(task.DomainEvents);
    }

    [Fact]
    public async Task Database_failure_rolls_back_session_and_outbox_together()
    {
        var session = CreateCompletedSession();
        var eventId = session.DomainEvents.Single().EventId;
        var duplicateOutboxId = Guid.NewGuid();

        await using (var seed = fixture.CreateDbContext())
        {
            seed.OutboxMessages.Add(new OutboxMessage(
                duplicateOutboxId,
                "ExistingTestEvent",
                Now,
                "{}"));
            await seed.SaveChangesAsync();
        }

        await using (var db = fixture.CreateDbContext())
        {
            db.ConsultationSessions.Add(session);
            db.OutboxMessages.Add(new OutboxMessage(
                duplicateOutboxId,
                "DuplicateTestEvent",
                Now,
                "{}"));

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using var verification = fixture.CreateDbContext();
        Assert.False(await verification.ConsultationSessions
            .AnyAsync(item => item.Id == session.Id));
        Assert.False(await verification.OutboxMessages
            .AnyAsync(item => item.Id == eventId));
        Assert.Equal(1, await verification.OutboxMessages
            .CountAsync(item => item.Id == duplicateOutboxId));
        Assert.NotEmpty(session.DomainEvents);
    }

    private static ConsultationSession CreateCompletedSession()
    {
        var session = ConsultationSession.Create(
            Guid.NewGuid(),
            ConsultationKind.Human,
            ConsultationChannel.Chat);
        session.RequestConsent();
        session.Schedule(
            new HashSet<ConsentKind>
            {
                ConsentKind.Service,
                ConsentKind.AiAnalysis
            },
            Now.AddMinutes(5));
        session.Start(Now.AddMinutes(5));
        session.Complete(Now.AddMinutes(30));
        return session;
    }
}
