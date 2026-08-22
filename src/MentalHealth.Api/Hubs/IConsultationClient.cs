using MentalHealth.Application.Consultations;

namespace MentalHealth.Api.Hubs;

public interface IConsultationClient
{
    Task MessageReceived(ChatMessageDto message);

    Task PresenceChanged(PresenceDto presence);
}

public sealed record PresenceDto(Guid UserId, string Kind, bool Online)
{
    public static PresenceDto From(PresenceChange change) => new(
        change.UserId,
        change.Kind.ToString(),
        change.Online);
}
