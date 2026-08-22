namespace MentalHealth.Application.Abstractions.Providers;

public sealed record NotificationMessage(
    Guid RecipientId,
    string Type,
    IReadOnlyDictionary<string, string> Data,
    string IdempotencyKey);

public interface INotificationSender
{
    Task SendAsync(
        NotificationMessage message,
        CancellationToken cancellationToken);
}
