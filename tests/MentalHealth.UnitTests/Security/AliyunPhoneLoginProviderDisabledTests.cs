using MentalHealth.Application.Security;
using MentalHealth.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace MentalHealth.UnitTests.Security;

public sealed class AliyunPhoneLoginProviderDisabledTests
{
    [Fact]
    public Task Disabled_captcha_provider_fails_closed_without_a_cloud_call() =>
        AssertDisabledAsync(
            new AliyunCaptchaVerifier(Options.Create(new AliyunPhoneLoginOptions { Enabled = false }))
                .VerifyAsync("scene", "captcha", CancellationToken.None));

    [Fact]
    public Task Disabled_sms_send_fails_closed_without_a_cloud_call() =>
        AssertDisabledAsync(
            new AliyunSmsVerificationProvider(Options.Create(new AliyunPhoneLoginOptions { Enabled = false }))
                .SendAsync("13800138000", "challenge", CancellationToken.None));

    [Fact]
    public Task Disabled_sms_check_fails_closed_without_a_cloud_call() =>
        AssertDisabledAsync(
            new AliyunSmsVerificationProvider(Options.Create(new AliyunPhoneLoginOptions { Enabled = false }))
                .CheckAsync("13800138000", "challenge", "123456", CancellationToken.None));

    private static async Task AssertDisabledAsync(Task operation)
    {
        var exception = await Assert.ThrowsAsync<PhoneLoginProviderException>(() => operation);
        Assert.Equal("PHONE_LOGIN_DISABLED", exception.Code);
    }
}
