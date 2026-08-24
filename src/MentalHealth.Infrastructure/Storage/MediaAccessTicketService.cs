using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.DataRights;
using MentalHealth.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace MentalHealth.Infrastructure.Storage;

public sealed class MediaAccessTicketService : IMediaAccessTicketService
{
    private static readonly TimeSpan MaximumLifetime = TimeSpan.FromMinutes(5);
    private readonly byte[] key;
    private readonly IClock clock;

    public MediaAccessTicketService(
        IOptions<JwtOptions> jwtOptions,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(jwtOptions);
        this.clock = clock;
        var rootKey = Encoding.UTF8.GetBytes(jwtOptions.Value.SigningKey);
        key = HMACSHA256.HashData(
            rootKey,
            "mental-health-v1/media-access-ticket/v1"u8.ToArray());
        CryptographicOperations.ZeroMemory(rootKey);
    }

    public string Create(
        Guid subjectId,
        Guid assetId,
        DateTimeOffset expiresAt)
    {
        if (subjectId == Guid.Empty || assetId == Guid.Empty)
        {
            throw new ArgumentException("Media ticket references are required.");
        }

        var utcExpiry = expiresAt.ToUniversalTime();
        if (utcExpiry > clock.UtcNow.Add(MaximumLifetime))
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresAt),
                "Media ticket lifetime cannot exceed five minutes.");
        }

        var payload = FormattableString.Invariant(
            $"{subjectId:N}.{assetId:N}.{utcExpiry.ToUnixTimeSeconds()}");
        var signature = HMACSHA256.HashData(
            key,
            Encoding.UTF8.GetBytes(payload));
        return $"{payload}.{Base64UrlEncode(signature)}";
    }

    public bool Validate(string ticket, Guid subjectId, Guid assetId)
    {
        if (string.IsNullOrWhiteSpace(ticket)
            || subjectId == Guid.Empty
            || assetId == Guid.Empty)
        {
            return false;
        }

        var parts = ticket.Split('.', StringSplitOptions.None);
        if (parts.Length != 4
            || !Guid.TryParseExact(parts[0], "N", out var ticketSubjectId)
            || !Guid.TryParseExact(parts[1], "N", out var ticketAssetId)
            || !long.TryParse(
                parts[2],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var expirySeconds)
            || ticketSubjectId != subjectId
            || ticketAssetId != assetId)
        {
            return false;
        }

        DateTimeOffset expiresAt;
        byte[] actualSignature;
        try
        {
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(expirySeconds);
            actualSignature = Base64UrlDecode(parts[3]);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }

        var now = clock.UtcNow;
        if (expiresAt <= now || expiresAt > now.Add(MaximumLifetime))
        {
            return false;
        }

        var payload = string.Join('.', parts.AsSpan(0, 3).ToArray());
        var expectedSignature = HMACSHA256.HashData(
            key,
            Encoding.UTF8.GetBytes(payload));
        return actualSignature.Length == expectedSignature.Length
            && CryptographicOperations.FixedTimeEquals(
                actualSignature,
                expectedSignature);
    }

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch
        {
            0 => string.Empty,
            2 => "==",
            3 => "=",
            _ => throw new FormatException("Invalid Base64Url value.")
        };
        return Convert.FromBase64String(padded);
    }
}
