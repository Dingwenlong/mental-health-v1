using MentalHealth.Application.Abstractions.Providers;

namespace MentalHealth.ContractTests.Fakes;

internal sealed class FakeNotificationSender : INotificationSender
{
    private readonly Dictionary<string, string> _fingerprints = new(StringComparer.Ordinal);

    public int DeliveryCount { get; private set; }

    public Task SendAsync(
        NotificationMessage message,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(message.IdempotencyKey))
        {
            throw new ProviderException("IDEMPOTENCY_KEY_REQUIRED");
        }

        var fingerprint = BuildFingerprint(message);
        if (_fingerprints.TryGetValue(message.IdempotencyKey, out var existing))
        {
            if (!string.Equals(existing, fingerprint, StringComparison.Ordinal))
            {
                throw new ProviderException("IDEMPOTENCY_KEY_CONFLICT");
            }

            return Task.CompletedTask;
        }

        _fingerprints.Add(message.IdempotencyKey, fingerprint);
        DeliveryCount++;
        return Task.CompletedTask;
    }

    private static string BuildFingerprint(NotificationMessage message)
    {
        var data = string.Join(
            "&",
            message.Data
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => $"{item.Key}={item.Value}"));
        return $"{message.RecipientId:N}|{message.Type}|{data}";
    }
}
