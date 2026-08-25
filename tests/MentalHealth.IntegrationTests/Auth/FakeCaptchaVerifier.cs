using MentalHealth.Application.Security;

namespace MentalHealth.IntegrationTests.Auth;

public sealed class FakeCaptchaVerifier : ICaptchaVerifier
{
    public const string ValidParam = "synthetic-captcha-pass";

    private int _attempts;
    private string _acceptedParam = ValidParam;

    public int Attempts => Volatile.Read(ref _attempts);

    public bool ProviderUnavailable { get; set; }

    public void Accept(string captchaVerifyParam) =>
        _acceptedParam = captchaVerifyParam;

    public void Reset()
    {
        Volatile.Write(ref _attempts, 0);
        ProviderUnavailable = false;
        _acceptedParam = ValidParam;
    }

    public Task<bool> VerifyAsync(
        string sceneId,
        string captchaVerifyParam,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _attempts);
        if (ProviderUnavailable)
        {
            throw new PhoneLoginProviderException("CAPTCHA_PROVIDER_UNAVAILABLE");
        }

        return Task.FromResult(captchaVerifyParam == _acceptedParam);
    }
}
