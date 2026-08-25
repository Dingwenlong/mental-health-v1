namespace MentalHealth.Application.Security;

public interface ISmsVerificationProvider
{
    Task SendAsync(
        string nationalPhoneNumber,
        string outId,
        CancellationToken cancellationToken);

    Task<bool> CheckAsync(
        string nationalPhoneNumber,
        string outId,
        string code,
        CancellationToken cancellationToken);
}
