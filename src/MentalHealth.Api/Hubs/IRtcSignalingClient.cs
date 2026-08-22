namespace MentalHealth.Api.Hubs;

public interface IRtcSignalingClient
{
    Task OfferReceived(RtcDescriptionDto description);

    Task AnswerReceived(RtcDescriptionDto description);

    Task IceCandidateReceived(RtcIceCandidateDto candidate);
}

public sealed record RtcDescriptionDto(Guid SessionId, string Sdp);

public sealed record RtcIceCandidateDto(
    Guid SessionId,
    string Candidate,
    string? SdpMid,
    int? SdpMLineIndex);
