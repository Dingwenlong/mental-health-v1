using MentalHealth.Api.Authorization;
using MentalHealth.Application.Consultations;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MentalHealth.Api.Hubs;

[Authorize]
public sealed class RtcSignalingHub(
    SessionAccessService access,
    ILogger<RtcSignalingHub> logger)
    : Hub<IRtcSignalingClient>
{
    private const int MaximumDescriptionLength = 128 * 1024;
    private const int MaximumCandidateLength = 8 * 1024;
    private const int MaximumMidLength = 128;
    private const string JoinedRoomsKey = "rtc-joined-rooms";

    public async Task JoinRoom(Guid sessionId)
    {
        try
        {
            await DemandActiveVideoSessionAsync(sessionId);
            foreach (var joinedSessionId in JoinedRooms
                .Where(joinedSessionId => joinedSessionId != sessionId)
                .ToArray())
            {
                await Groups.RemoveFromGroupAsync(
                    Context.ConnectionId,
                    Room(joinedSessionId),
                    Context.ConnectionAborted);
                JoinedRooms.Remove(joinedSessionId);
            }

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                Room(sessionId),
                Context.ConnectionAborted);
            JoinedRooms.Add(sessionId);
        }
        catch (DomainException exception)
        {
            throw new HubException(exception.Code);
        }
    }

    public async Task LeaveRoom(Guid sessionId)
    {
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            Room(sessionId),
            Context.ConnectionAborted);
        JoinedRooms.Remove(sessionId);
    }

    public async Task RelayOffer(Guid sessionId, string sdp)
    {
        await RelayDescriptionAsync(
            sessionId,
            sdp,
            "Offer",
            dto => Clients.OthersInGroup(Room(sessionId)).OfferReceived(dto));
    }

    public async Task RelayAnswer(Guid sessionId, string sdp)
    {
        await RelayDescriptionAsync(
            sessionId,
            sdp,
            "Answer",
            dto => Clients.OthersInGroup(Room(sessionId)).AnswerReceived(dto));
    }

    public async Task RelayIceCandidate(
        Guid sessionId,
        string candidate,
        string? sdpMid,
        int? sdpMLineIndex)
    {
        try
        {
            await DemandRelayAccessAsync(sessionId);
            if (string.IsNullOrWhiteSpace(candidate)
                || candidate.Length > MaximumCandidateLength
                || sdpMid?.Length > MaximumMidLength
                || sdpMLineIndex is < 0 or > 32)
            {
                throw new DomainException("RTC_ICE_INVALID");
            }

            await Clients.OthersInGroup(Room(sessionId)).IceCandidateReceived(
                new RtcIceCandidateDto(
                    sessionId,
                    candidate,
                    sdpMid,
                    sdpMLineIndex));
            logger.LogInformation(
                "Relayed RTC signal {SignalType} for session {SessionId}",
                "IceCandidate",
                sessionId);
        }
        catch (DomainException exception)
        {
            throw new HubException(exception.Code);
        }
    }

    private async Task RelayDescriptionAsync(
        Guid sessionId,
        string sdp,
        string signalType,
        Func<RtcDescriptionDto, Task> relay)
    {
        try
        {
            await DemandRelayAccessAsync(sessionId);
            if (string.IsNullOrWhiteSpace(sdp)
                || sdp.Length > MaximumDescriptionLength)
            {
                throw new DomainException("RTC_DESCRIPTION_INVALID");
            }

            await relay(new RtcDescriptionDto(sessionId, sdp));
            logger.LogInformation(
                "Relayed RTC signal {SignalType} for session {SessionId}",
                signalType,
                sessionId);
        }
        catch (DomainException exception)
        {
            throw new HubException(exception.Code);
        }
    }

    private async Task DemandRelayAccessAsync(Guid sessionId)
    {
        await DemandActiveVideoSessionAsync(sessionId);
        if (!JoinedRooms.Contains(sessionId))
        {
            throw new DomainException("RTC_ROOM_NOT_JOINED");
        }
    }

    private async Task DemandActiveVideoSessionAsync(Guid sessionId)
    {
        var actor = Context.User?.ToConsultationActor()
            ?? throw new DomainException("FORBIDDEN_RESOURCE");
        var permitted = await access.DemandAsync(
            actor,
            sessionId,
            Context.ConnectionAborted);
        if (permitted.Session.Status != ConsultationStatus.InProgress)
        {
            throw new DomainException("INVALID_SESSION_STATE");
        }

        if (permitted.Session.Channel != ConsultationChannel.Video)
        {
            throw new DomainException("VIDEO_SESSION_REQUIRED");
        }
    }

    private HashSet<Guid> JoinedRooms
    {
        get
        {
            if (Context.Items.TryGetValue(JoinedRoomsKey, out var value)
                && value is HashSet<Guid> rooms)
            {
                return rooms;
            }

            var created = new HashSet<Guid>();
            Context.Items[JoinedRoomsKey] = created;
            return created;
        }
    }

    private static string Room(Guid sessionId) => $"rtc:{sessionId:N}";
}
