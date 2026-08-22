namespace MentalHealth.Application.Abstractions.Providers;

public enum PaymentStatus
{
    Confirmed,
    Declined
}

public sealed record PaymentRequest(
    Guid OrderId,
    long AmountInMinorUnits,
    string Currency,
    string IdempotencyKey);

public sealed record PaymentConfirmation(
    Guid OrderId,
    string ProviderReference,
    PaymentStatus Status,
    DateTimeOffset ConfirmedAt);

public interface IPaymentGateway
{
    Task<PaymentConfirmation> ConfirmAsync(
        PaymentRequest request,
        CancellationToken cancellationToken);
}
