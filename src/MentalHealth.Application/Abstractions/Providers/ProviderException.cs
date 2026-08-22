namespace MentalHealth.Application.Abstractions.Providers;

public sealed class ProviderException : Exception
{
    public ProviderException(
        string code,
        string? message = null,
        Exception? innerException = null)
        : base(message ?? code, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}
