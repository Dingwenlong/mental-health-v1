using System.Text.RegularExpressions;

namespace MentalHealth.Application.Security;

public static class PhoneNumberNormalizer
{
    private static readonly Regex MainlandChinaPattern = new(
        "^(?:1[3-9]\\d{9}|\\+861[3-9]\\d{9})$",
        RegexOptions.CultureInvariant);

    public static bool TryNormalizeMainlandChina(
        string? value,
        out string normalized)
    {
        normalized = string.Empty;
        if (value is null || !MainlandChinaPattern.IsMatch(value))
        {
            return false;
        }

        normalized = value[0] == '+' ? value : $"+86{value}";
        return true;
    }

    public static string ToMainlandChinaDomestic(string value)
    {
        if (!TryNormalizeMainlandChina(value, out var normalized))
        {
            throw new ArgumentException("A valid mainland China phone number is required.", nameof(value));
        }

        return normalized[3..];
    }
}
