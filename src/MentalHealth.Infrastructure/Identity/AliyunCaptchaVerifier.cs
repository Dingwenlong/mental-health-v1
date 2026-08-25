using AlibabaCloud.OpenApiClient.Models;
using AlibabaCloud.SDK.Captcha20230305.Models;
using MentalHealth.Application.Security;
using Microsoft.Extensions.Options;

namespace MentalHealth.Infrastructure.Identity;

public sealed class AliyunCaptchaVerifier(IOptions<AliyunPhoneLoginOptions> options) : ICaptchaVerifier
{
    private readonly AliyunPhoneLoginOptions _options = options.Value;

    public async Task<bool> VerifyAsync(
        string sceneId,
        string captchaVerifyParam,
        CancellationToken cancellationToken)
    {
        ThrowIfDisabled();
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var client = new AlibabaCloud.SDK.Captcha20230305.Client(CreateCaptchaConfig());
            var response = await client.VerifyIntelligentCaptchaAsync(
                new VerifyIntelligentCaptchaRequest
                {
                    SceneId = sceneId,
                    CaptchaVerifyParam = captchaVerifyParam
                });
            cancellationToken.ThrowIfCancellationRequested();
            return response.Body?.Result?.VerifyResult == true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            throw new PhoneLoginProviderException("CAPTCHA_PROVIDER_UNAVAILABLE");
        }
    }

    private Config CreateCaptchaConfig() => new()
    {
        AccessKeyId = _options.AccessKeyId,
        AccessKeySecret = _options.AccessKeySecret,
        RegionId = "cn-shanghai",
        Endpoint = "captcha.cn-shanghai.aliyuncs.com"
    };

    private void ThrowIfDisabled()
    {
        if (!_options.Enabled)
        {
            throw new PhoneLoginProviderException("PHONE_LOGIN_DISABLED");
        }
    }
}
