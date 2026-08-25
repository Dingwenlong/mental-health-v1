using MentalHealth.Infrastructure.Identity;

namespace MentalHealth.UnitTests.Security;

public sealed class AliyunPhoneLoginOptionsTests
{
    [Fact]
    public void Complete_configuration_is_valid()
    {
        Assert.True(CreateValidOptions().IsValid());
    }

    [Theory]
    [InlineData(nameof(AliyunPhoneLoginOptions.Prefix))]
    [InlineData(nameof(AliyunPhoneLoginOptions.AdminSceneId))]
    [InlineData(nameof(AliyunPhoneLoginOptions.AndroidSceneId))]
    [InlineData(nameof(AliyunPhoneLoginOptions.CaptchaEkey))]
    [InlineData(nameof(AliyunPhoneLoginOptions.AccessKeyId))]
    [InlineData(nameof(AliyunPhoneLoginOptions.AccessKeySecret))]
    [InlineData(nameof(AliyunPhoneLoginOptions.SmsSignName))]
    [InlineData(nameof(AliyunPhoneLoginOptions.SmsTemplateCode))]
    public void Missing_required_configuration_is_invalid(string propertyName)
    {
        var options = CreateValidOptions();
        typeof(AliyunPhoneLoginOptions).GetProperty(propertyName)!.SetValue(options, string.Empty);

        Assert.False(options.IsValid());
    }

    private static AliyunPhoneLoginOptions CreateValidOptions() => new()
    {
        Prefix = "xfkdn8",
        AdminSceneId = "1lae8yfm",
        AndroidSceneId = "e20maaxh",
        CaptchaEkey = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=",
        AccessKeyId = "test-access-key-id",
        AccessKeySecret = "test-access-key-secret",
        SmsSignName = "test-sign",
        SmsTemplateCode = "test-template"
    };
}
