namespace MentalHealth.Application.Security;

public sealed class PhoneLoginProviderException(string code)
    : Exception(code)
{
    public string Code { get; } = code;
}
