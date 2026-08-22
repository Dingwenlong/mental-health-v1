using System.Collections.Concurrent;
using MentalHealth.Application.Abstractions.Providers;
using Microsoft.Extensions.Logging;

namespace MentalHealth.Infrastructure.Providers;

public sealed class LocalNotificationSender(
    ILogger<LocalNotificationSender> logger) : INotificationSender
{
    private readonly ConcurrentDictionary<string, string> _fingerprints =
        new(StringComparer.Ordinal);
    private int _deliveryCount;

    public int DeliveryCount => Volatile.Read(ref _deliveryCount);

    public Task SendAsync(
        NotificationMessage message,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (message.RecipientId == Guid.Empty
            || string.IsNullOrWhiteSpace(message.Type))
        {
            throw new ProviderException("NOTIFICATION_INVALID");
        }

        if (string.IsNullOrWhiteSpace(message.IdempotencyKey))
        {
            throw new ProviderException("IDEMPOTENCY_KEY_REQUIRED");
        }

        var fingerprint = BuildFingerprint(message);
        while (true)
        {
            if (_fingerprints.TryGetValue(message.IdempotencyKey, out var existing))
            {
                if (!string.Equals(existing, fingerprint, StringComparison.Ordinal))
                {
                    throw new ProviderException("IDEMPOTENCY_KEY_CONFLICT");
                }

                return Task.CompletedTask;
            }

            if (_fingerprints.TryAdd(message.IdempotencyKey, fingerprint))
            {
                Interlocked.Increment(ref _deliveryCount);
                logger.LogWarning("Recorded one local notification.");
                return Task.CompletedTask;
            }
        }
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
