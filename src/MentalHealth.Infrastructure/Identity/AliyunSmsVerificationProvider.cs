using AlibabaCloud.OpenApiClient.Models;
using AlibabaCloud.SDK.Dypnsapi20170525.Models;
using MentalHealth.Application.Security;
using Microsoft.Extensions.Options;

namespace MentalHealth.Infrastructure.Identity;

public sealed class AliyunSmsVerificationProvider(IOptions<AliyunPhoneLoginOptions> options) : ISmsVerificationProvider
{
    private readonly AliyunPhoneLoginOptions _options = options.Value;

    public async Task SendAsync(
        string nationalPhoneNumber,
        string outId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var client = new AlibabaCloud.SDK.Dypnsapi20170525.Client(CreateSmsConfig());
            var response = await client.SendSmsVerifyCodeAsync(new SendSmsVerifyCodeRequest
            {
                CountryCode = "86",
                PhoneNumber = nationalPhoneNumber,
                OutId = outId,
                CodeLength = 6,
                CodeType = 1,
                ValidTime = 300,
                Interval = 60,
                DuplicatePolicy = 1,
                ReturnVerifyCode = false,
                SignName = _options.SmsSignName,
                TemplateCode = _options.SmsTemplateCode,
                TemplateParam = "{\"code\":\"##code##\",\"min\":\"5\"}"
            });
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.Equals(response.Body?.Code, "OK", StringComparison.Ordinal))
            {
                throw new PhoneLoginProviderException("SMS_PROVIDER_REJECTED");
            }
        }
        catch (PhoneLoginProviderException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            throw new PhoneLoginProviderException("SMS_PROVIDER_UNAVAILABLE");
        }
    }

    public async Task<bool> CheckAsync(
        string nationalPhoneNumber,
        string outId,
        string code,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var client = new AlibabaCloud.SDK.Dypnsapi20170525.Client(CreateSmsConfig());
            var response = await client.CheckSmsVerifyCodeAsync(new CheckSmsVerifyCodeRequest
            {
                CountryCode = "86",
                PhoneNumber = nationalPhoneNumber,
                OutId = outId,
                VerifyCode = code
            });
            cancellationToken.ThrowIfCancellationRequested();
            return string.Equals(response.Body?.Code, "OK", StringComparison.Ordinal)
                && string.Equals(response.Body?.Model?.VerifyResult, "PASS", StringComparison.Ordinal);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            throw new PhoneLoginProviderException("SMS_PROVIDER_UNAVAILABLE");
        }
    }

    private Config CreateSmsConfig() => new()
    {
        AccessKeyId = _options.AccessKeyId,
        AccessKeySecret = _options.AccessKeySecret,
        RegionId = "cn-hangzhou"
    };
}
