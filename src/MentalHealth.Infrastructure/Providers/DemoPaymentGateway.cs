using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Providers;

namespace MentalHealth.Infrastructure.Providers;

public sealed class DemoPaymentGateway(IClock clock) : IPaymentGateway
{
    private readonly object _gate = new();
    private readonly Dictionary<string, StoredConfirmation> _confirmations =
        new(StringComparer.Ordinal);

    public int ConfirmationCount { get; private set; }

    public Task<PaymentConfirmation> ConfirmAsync(
        PaymentRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            throw new ProviderException("IDEMPOTENCY_KEY_REQUIRED");
        }

        if (request.AmountInMinorUnits <= 0)
        {
            throw new ProviderException("PAYMENT_AMOUNT_INVALID");
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            throw new ProviderException("PAYMENT_CURRENCY_INVALID");
        }

        var fingerprint = string.Join(
            '|',
            request.OrderId.ToString("N"),
            request.AmountInMinorUnits,
            request.Currency.ToUpperInvariant());

        lock (_gate)
        {
            if (_confirmations.TryGetValue(
                request.IdempotencyKey,
                out var existing))
            {
                if (!string.Equals(
                    existing.Fingerprint,
                    fingerprint,
                    StringComparison.Ordinal))
                {
                    throw new ProviderException("IDEMPOTENCY_KEY_CONFLICT");
                }

                return Task.FromResult(existing.Confirmation);
            }

            var confirmation = new PaymentConfirmation(
                request.OrderId,
                $"demo-{request.OrderId:N}",
                PaymentStatus.Confirmed,
                clock.UtcNow);
            _confirmations.Add(
                request.IdempotencyKey,
                new StoredConfirmation(fingerprint, confirmation));
            ConfirmationCount++;
            return Task.FromResult(confirmation);
        }
    }

    private sealed record StoredConfirmation(
        string Fingerprint,
        PaymentConfirmation Confirmation);
}
