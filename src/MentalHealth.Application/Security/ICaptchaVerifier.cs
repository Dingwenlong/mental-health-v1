namespace MentalHealth.Application.Security;

public interface ICaptchaVerifier
{
    Task<bool> VerifyAsync(
        string sceneId,
        string captchaVerifyParam,
        CancellationToken cancellationToken);
}
