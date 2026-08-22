using MentalHealth.Api.Authorization;
using MentalHealth.Application.Consultations;
using MentalHealth.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MentalHealth.Api.Hubs;

[Authorize]
public sealed class ConsultationHub(
    SendMessageHandler messages,
    SessionAccessService access,
    IPresenceStore presence) : Hub<IConsultationClient>
{
    public async Task JoinSession(Guid sessionId)
    {
        var actor = RequireActor();
        try
        {
            var permitted = await access.DemandAsync(
                actor,
                sessionId,
                Context.ConnectionAborted);
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                GroupName(sessionId),
                Context.ConnectionAborted);
            var becameOnline = await presence.JoinAsync(
                sessionId,
                actor.UserId,
                permitted.SenderKind,
                Context.ConnectionId,
                Context.ConnectionAborted);
            if (becameOnline)
            {
                await Clients.Group(GroupName(sessionId)).PresenceChanged(
                    new PresenceDto(
                        actor.UserId,
                        permitted.SenderKind.ToString(),
                        true));
            }
        }
        catch (DomainException exception)
        {
            throw new HubException(exception.Code);
        }
    }

    public async Task<ChatMessageDto> SendMessage(
        Guid sessionId,
        string text,
        string clientMessageId)
    {
        var actor = RequireActor();
        try
        {
            var result = await messages.HandleAsync(
                actor,
                sessionId,
                text,
                clientMessageId,
                Context.ConnectionAborted);
            var dto = ChatMessageDto.From(result.Message);
            if (result.Created)
            {
                await Clients.Group(GroupName(sessionId)).MessageReceived(dto);
            }

            return dto;
        }
        catch (DomainException exception)
        {
            throw new HubException(exception.Code);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var changes = await presence.LeaveConnectionAsync(
            Context.ConnectionId,
            CancellationToken.None);
        foreach (var change in changes)
        {
            await Clients.Group(GroupName(change.SessionId))
                .PresenceChanged(PresenceDto.From(change));
        }

        await base.OnDisconnectedAsync(exception);
    }

    private ConsultationActor RequireActor() =>
        Context.User?.ToConsultationActor()
        ?? throw new HubException("FORBIDDEN_RESOURCE");

    private static string GroupName(Guid sessionId) =>
        $"consultation:{sessionId:N}";
}
