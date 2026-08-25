namespace MentalHealth.Infrastructure.Identity;

public sealed class PhoneLoginAccountOptions
{
    public const string SectionName = "PhoneLogin:Accounts";

    public string? ClientPhone { get; init; }

    public string? AdminPhone { get; init; }
}
