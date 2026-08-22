using MentalHealth.Domain.Consents;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.Shared;

namespace MentalHealth.UnitTests.Consultations;

public sealed class ConsultationSessionTests
{
    private static readonly DateTimeOffset ScheduledAt =
        DateTimeOffset.Parse("2026-08-22T09:00:00+08:00");

    private static readonly IReadOnlySet<ConsentKind> AllConsents =
        new HashSet<ConsentKind>
        {
            ConsentKind.Service,
            ConsentKind.Recording,
            ConsentKind.AiAnalysis
        };

    [Theory]
    [InlineData(ConsentKind.Service)]
    [InlineData(ConsentKind.Recording)]
    [InlineData(ConsentKind.AiAnalysis)]
    public void Schedule_requires_each_video_consent(ConsentKind missingConsent)
    {
        var session = ConsultationSession.Create(
            Guid.NewGuid(),
            ConsultationKind.Human,
            ConsultationChannel.Video);
        session.RequestConsent();
        var consents = AllConsents
            .Where(consent => consent != missingConsent)
            .ToHashSet();

        var exception = Assert.Throws<DomainException>(
            () => session.Schedule(consents, DateTimeOffset.Parse("2026-08-22T09:00:00+08:00")));

        Assert.Equal("CONSENT_REQUIRED", exception.Message);
    }

    [Fact]
    public void Schedule_allows_chat_without_recording_consent()
    {
        var session = ConsultationSession.Create(
            Guid.NewGuid(),
            ConsultationKind.AiVirtual,
            ConsultationChannel.Chat);
        session.RequestConsent();
        var consents = new HashSet<ConsentKind>
        {
            ConsentKind.Service,
            ConsentKind.AiAnalysis
        };

        session.Schedule(consents, ScheduledAt);

        Assert.Equal(ConsultationStatus.Scheduled, session.Status);
        Assert.Equal(ScheduledAt, session.ScheduledAt);
    }

    [Theory]
    [InlineData(ConsultationStatus.AwaitingConsent)]
    [InlineData(ConsultationStatus.Scheduled)]
    [InlineData(ConsultationStatus.InProgress)]
    [InlineData(ConsultationStatus.Completed)]
    [InlineData(ConsultationStatus.Cancelled)]
    public void RequestConsent_rejects_every_state_except_draft(ConsultationStatus status)
    {
        var session = CreateInState(status);

        var exception = Assert.Throws<DomainException>(session.RequestConsent);

        Assert.Equal("INVALID_SESSION_STATE", exception.Code);
    }

    [Theory]
    [InlineData(ConsultationStatus.Draft)]
    [InlineData(ConsultationStatus.Scheduled)]
    [InlineData(ConsultationStatus.InProgress)]
    [InlineData(ConsultationStatus.Completed)]
    [InlineData(ConsultationStatus.Cancelled)]
    public void Schedule_rejects_every_state_except_awaiting_consent(ConsultationStatus status)
    {
        var session = CreateInState(status);

        var exception = Assert.Throws<DomainException>(
            () => session.Schedule(AllConsents, ScheduledAt));

        Assert.Equal("INVALID_SESSION_STATE", exception.Code);
    }

    [Fact]
    public void Start_moves_scheduled_session_to_in_progress()
    {
        var session = CreateInState(ConsultationStatus.Scheduled);
        var startedAt = ScheduledAt.AddMinutes(5);

        session.Start(startedAt);

        Assert.Equal(ConsultationStatus.InProgress, session.Status);
        Assert.Equal(startedAt, session.StartedAt);
    }

    [Theory]
    [InlineData(ConsultationStatus.Draft)]
    [InlineData(ConsultationStatus.AwaitingConsent)]
    [InlineData(ConsultationStatus.InProgress)]
    [InlineData(ConsultationStatus.Completed)]
    [InlineData(ConsultationStatus.Cancelled)]
    public void Start_rejects_every_state_except_scheduled(ConsultationStatus status)
    {
        var session = CreateInState(status);

        var exception = Assert.Throws<DomainException>(() => session.Start(ScheduledAt));

        Assert.Equal("INVALID_SESSION_STATE", exception.Code);
    }

    [Fact]
    public void Complete_moves_in_progress_session_to_completed()
    {
        var session = CreateInState(ConsultationStatus.InProgress);
        var completedAt = ScheduledAt.AddHours(1);

        session.Complete(completedAt);

        Assert.Equal(ConsultationStatus.Completed, session.Status);
        Assert.Equal(completedAt, session.CompletedAt);
    }

    [Theory]
    [InlineData(ConsultationStatus.Draft)]
    [InlineData(ConsultationStatus.AwaitingConsent)]
    [InlineData(ConsultationStatus.Scheduled)]
    [InlineData(ConsultationStatus.Completed)]
    [InlineData(ConsultationStatus.Cancelled)]
    public void Complete_rejects_every_state_except_in_progress(ConsultationStatus status)
    {
        var session = CreateInState(status);

        var exception = Assert.Throws<DomainException>(() => session.Complete(ScheduledAt));

        Assert.Equal("INVALID_SESSION_STATE", exception.Code);
    }

    [Theory]
    [InlineData(ConsultationStatus.Draft)]
    [InlineData(ConsultationStatus.AwaitingConsent)]
    [InlineData(ConsultationStatus.Scheduled)]
    public void Cancel_is_allowed_before_session_starts(ConsultationStatus status)
    {
        var session = CreateInState(status);
        var cancelledAt = ScheduledAt.AddMinutes(10);

        session.Cancel(cancelledAt);

        Assert.Equal(ConsultationStatus.Cancelled, session.Status);
        Assert.Equal(cancelledAt, session.CancelledAt);
    }

    [Theory]
    [InlineData(ConsultationStatus.InProgress)]
    [InlineData(ConsultationStatus.Completed)]
    [InlineData(ConsultationStatus.Cancelled)]
    public void Cancel_rejects_started_or_terminal_sessions(ConsultationStatus status)
    {
        var session = CreateInState(status);

        var exception = Assert.Throws<DomainException>(() => session.Cancel(ScheduledAt));

        Assert.Equal("INVALID_SESSION_STATE", exception.Code);
    }

    [Fact]
    public void Complete_adds_a_consultation_completed_event()
    {
        var session = CreateInState(ConsultationStatus.InProgress);
        var completedAt = ScheduledAt.AddHours(1);

        session.Complete(completedAt);

        var domainEvent = Assert.Single(
            session.DomainEvents.OfType<ConsultationCompletedDomainEvent>());
        Assert.NotEqual(Guid.Empty, domainEvent.EventId);
        Assert.Equal(session.Id, domainEvent.SessionId);
        Assert.Equal(session.SubjectId, domainEvent.SubjectId);
        Assert.Equal(completedAt, domainEvent.OccurredAt);
    }

    [Fact]
    public void Complete_with_same_idempotency_key_does_not_add_another_event()
    {
        var session = CreateInState(ConsultationStatus.InProgress);
        var completedAt = ScheduledAt.AddHours(1);

        Assert.True(session.Complete(completedAt, "complete-001"));
        session.ClearDomainEvents();

        Assert.False(session.Complete(completedAt, "complete-001"));
        Assert.Empty(session.DomainEvents);
        Assert.Equal("complete-001", session.CompletionIdempotencyKey);
    }

    [Fact]
    public void Complete_with_different_idempotency_key_is_rejected()
    {
        var session = CreateInState(ConsultationStatus.InProgress);
        session.Complete(ScheduledAt.AddHours(1), "complete-001");

        var exception = Assert.Throws<DomainException>(
            () => session.Complete(ScheduledAt.AddHours(1), "complete-002"));

        Assert.Equal("IDEMPOTENCY_CONFLICT", exception.Code);
    }

    private static ConsultationSession CreateInState(ConsultationStatus status)
    {
        var session = ConsultationSession.Create(
            Guid.NewGuid(),
            ConsultationKind.Human,
            ConsultationChannel.Video);

        switch (status)
        {
            case ConsultationStatus.Draft:
                break;
            case ConsultationStatus.AwaitingConsent:
                session.RequestConsent();
                break;
            case ConsultationStatus.Scheduled:
                MoveToScheduled(session);
                break;
            case ConsultationStatus.InProgress:
                MoveToScheduled(session);
                session.Start(ScheduledAt);
                break;
            case ConsultationStatus.Completed:
                MoveToScheduled(session);
                session.Start(ScheduledAt);
                session.Complete(ScheduledAt.AddHours(1));
                break;
            case ConsultationStatus.Cancelled:
                session.Cancel(ScheduledAt);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        return session;
    }

    private static void MoveToScheduled(ConsultationSession session)
    {
        session.RequestConsent();
        session.Schedule(AllConsents, ScheduledAt);
    }
}
