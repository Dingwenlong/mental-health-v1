using MentalHealth.Domain.Consultations;

namespace MentalHealth.Application.Consultations;

public sealed record PresenceChange(
    Guid SessionId,
    Guid UserId,
    MessageSenderKind Kind,
    bool Online);

public interface IPresenceStore
{
    Task<bool> JoinAsync(
        Guid sessionId,
        Guid userId,
        MessageSenderKind kind,
        string connectionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PresenceChange>> LeaveConnectionAsync(
        string connectionId,
        CancellationToken cancellationToken);
}
