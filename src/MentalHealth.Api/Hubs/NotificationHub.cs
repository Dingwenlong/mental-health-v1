using MentalHealth.Api.Authorization;
using MentalHealth.Application.Consultations;
using MentalHealth.Application.Security;
using MentalHealth.Domain.FollowUps;
using MentalHealth.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MentalHealth.Api.Hubs;

public interface INotificationClient
{
    Task AnalysisStatusChanged(AnalysisStatusChangedNotification notification);

    Task RiskCaseChanged(RiskCaseChangedNotification notification);

    Task FollowUpChanged(FollowUpChangedNotification notification);
}

public sealed record AnalysisStatusChangedNotification(
    Guid SessionId,
    string Status,
    int? TranscriptRevision);

public sealed record RiskCaseChangedNotification(
    Guid CaseId,
    string CurrentLevel,
    string Status);

public sealed record FollowUpChangedNotification(
    Guid TaskId,
    string Status,
    DateTimeOffset? DueAt,
    string? ConflictCode);

[Authorize]
public sealed class NotificationHub(SessionAccessService access)
    : Hub<INotificationClient>
{
    public override async Task OnConnectedAsync()
    {
        var actor = RequireActor();
        if (actor.SubjectId is { } subjectId)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                NotificationGroups.Subject(subjectId),
                Context.ConnectionAborted);
        }

        if (actor.Roles.Contains(AppRoles.Doctor, StringComparer.Ordinal)
            || actor.Roles.Contains(AppRoles.OperationsAdmin, StringComparer.Ordinal))
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                NotificationGroups.Clinical,
                Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }

    public async Task WatchSession(Guid sessionId)
    {
        try
        {
            await access.DemandAsync(
                RequireActor(),
                sessionId,
                Context.ConnectionAborted);
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                NotificationGroups.Session(sessionId),
                Context.ConnectionAborted);
        }
        catch (DomainException exception)
        {
            throw new HubException(exception.Code);
        }
    }

    private ConsultationActor RequireActor() =>
        Context.User?.ToConsultationActor()
        ?? throw new HubException("FORBIDDEN_RESOURCE");
}

public sealed class NotificationPublisher(
    IHubContext<NotificationHub, INotificationClient> hub)
{
    public Task AnalysisStatusChangedAsync(
        Guid sessionId,
        string status,
        int? transcriptRevision,
        CancellationToken cancellationToken) =>
        hub.Clients.Group(NotificationGroups.Session(sessionId))
            .AnalysisStatusChanged(new AnalysisStatusChangedNotification(
                sessionId,
                status,
                transcriptRevision));

    public Task RiskCaseChangedAsync(
        Guid caseId,
        string currentLevel,
        string status,
        CancellationToken cancellationToken) =>
        hub.Clients.Group(NotificationGroups.Clinical)
            .RiskCaseChanged(new RiskCaseChangedNotification(
                caseId,
                currentLevel,
                status));

    public async Task FollowUpChangedAsync(
        FollowUpTask task,
        CancellationToken cancellationToken)
    {
        var notification = new FollowUpChangedNotification(
            task.Id,
            task.Status.ToString(),
            task.DueAt,
            task.ConflictCode);
        await Task.WhenAll(
            hub.Clients.Group(NotificationGroups.Subject(task.SubjectId))
                .FollowUpChanged(notification),
            hub.Clients.Group(NotificationGroups.Clinical)
                .FollowUpChanged(notification));
    }
}

internal static class NotificationGroups
{
    public const string Clinical = "notifications:clinical";

    public static string Session(Guid sessionId) =>
        $"notifications:session:{sessionId:N}";

    public static string Subject(Guid subjectId) =>
        $"notifications:subject:{subjectId:N}";
}
