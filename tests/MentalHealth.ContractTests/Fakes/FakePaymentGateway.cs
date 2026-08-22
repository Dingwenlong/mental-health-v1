using MentalHealth.Application.Abstractions.Providers;

namespace MentalHealth.ContractTests.Fakes;

internal sealed class FakePaymentGateway : IPaymentGateway
{
    private static readonly DateTimeOffset ConfirmedAt =
        DateTimeOffset.Parse("2026-08-22T01:00:00+00:00");

    private readonly Dictionary<string, StoredConfirmation> _confirmations =
        new(StringComparer.Ordinal);

    public int ChargeCount { get; private set; }

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

        var fingerprint = $"{request.OrderId:N}|{request.AmountInMinorUnits}|{request.Currency}";
        if (_confirmations.TryGetValue(request.IdempotencyKey, out var existing))
        {
            if (!string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                throw new ProviderException("IDEMPOTENCY_KEY_CONFLICT");
            }

            return Task.FromResult(existing.Confirmation);
        }

        var confirmation = new PaymentConfirmation(
            request.OrderId,
            $"demo-{request.OrderId:N}",
            PaymentStatus.Confirmed,
            ConfirmedAt);
        _confirmations.Add(
            request.IdempotencyKey,
            new StoredConfirmation(fingerprint, confirmation));
        ChargeCount++;
        return Task.FromResult(confirmation);
    }

    private sealed record StoredConfirmation(
        string Fingerprint,
        PaymentConfirmation Confirmation);
}
