namespace MentalHealth.Infrastructure.Identity;

public sealed class AliyunPhoneLoginOptions
{
    public const string SectionName = "PhoneLogin:Aliyun";

    public string Prefix { get; set; } = string.Empty;

    public string AdminSceneId { get; set; } = string.Empty;

    public string AndroidSceneId { get; set; } = string.Empty;

    public string CaptchaEkey { get; set; } = string.Empty;

    public string AccessKeyId { get; set; } = string.Empty;

    public string AccessKeySecret { get; set; } = string.Empty;

    public string SmsSignName { get; set; } = string.Empty;

    public string SmsTemplateCode { get; set; } = string.Empty;

    public bool IsValid()
    {
        if (new[]
            {
                Prefix,
                AdminSceneId,
                AndroidSceneId,
                CaptchaEkey,
                AccessKeyId,
                AccessKeySecret,
                SmsSignName,
                SmsTemplateCode
            }.Any(string.IsNullOrWhiteSpace))
        {
            return false;
        }

        try
        {
            return Convert.FromBase64String(CaptchaEkey).Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
