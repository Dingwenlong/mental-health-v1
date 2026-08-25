using MentalHealth.Application.Security;
using MentalHealth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MentalHealth.Infrastructure.Identity;

public sealed class PhoneLoginAccountUpgrader(
    MentalHealthDbContext db,
    UserManager<AppUser> userManager,
    IOptions<PhoneLoginAccountOptions> options)
{
    private const string ClientEmail = "abc@qq.com";
    private const string AdminEmail = "123@qq.com";
    private const string LegacyClientEmail = "user@demo.local";
    private const string LegacyAdminEmail = "admin@demo.local";

    public async Task UpgradeAsync(CancellationToken cancellationToken = default)
    {
        var (clientPhone, adminPhone) = NormalizeConfiguredPhones(options.Value);

        await using var transaction = await db.Database.BeginTransactionAsync(
            cancellationToken);
        var client = await FindAndLockAsync(
                clientPhone,
                ClientEmail,
                LegacyClientEmail,
                cancellationToken)
            ?? throw new InvalidOperationException("The public client account was not seeded.");
        var admin = await FindAndLockAsync(
                adminPhone,
                AdminEmail,
                LegacyAdminEmail,
                cancellationToken)
            ?? throw new InvalidOperationException("The public admin account was not seeded.");

        var clientChanged = Upgrade(client, clientPhone);
        var adminChanged = Upgrade(admin, adminPhone);
        var clientTokensRemoved = await RemoveAuthenticatorTokensAsync(
            client.Id,
            cancellationToken);
        var adminTokensRemoved = await RemoveAuthenticatorTokensAsync(
            admin.Id,
            cancellationToken);
        if (clientChanged || clientTokensRemoved)
        {
            client.SecurityStamp = Guid.NewGuid().ToString();
        }

        if (adminChanged || adminTokensRemoved)
        {
            admin.SecurityStamp = Guid.NewGuid().ToString();
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<AppUser?> FindAndLockAsync(
        string phoneNumber,
        string email,
        string legacyEmail,
        CancellationToken cancellationToken)
    {
        var byPhone = await db.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"PhoneNumber\" = {phoneNumber} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (byPhone is not null)
        {
            return byPhone;
        }

        var normalizedEmail = userManager.NormalizeEmail(email);
        var byEmail = await db.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"NormalizedEmail\" = {normalizedEmail} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (byEmail is not null)
        {
            return byEmail;
        }

        var normalizedLegacyEmail = userManager.NormalizeEmail(legacyEmail);
        return await db.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"NormalizedEmail\" = {normalizedLegacyEmail} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static (string ClientPhone, string AdminPhone) NormalizeConfiguredPhones(
        PhoneLoginAccountOptions options)
    {
        if (!PhoneNumberNormalizer.TryNormalizeMainlandChina(
                options.ClientPhone ?? string.Empty,
                out var clientPhone)
            || !PhoneNumberNormalizer.TryNormalizeMainlandChina(
                options.AdminPhone ?? string.Empty,
                out var adminPhone))
        {
            throw new InvalidOperationException(
                "PhoneLogin account phone numbers must be valid mainland China phone numbers.");
        }

        if (string.Equals(clientPhone, adminPhone, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "PhoneLogin account phone numbers must be different.");
        }

        return (clientPhone, adminPhone);
    }

    private static bool Upgrade(AppUser user, string phoneNumber)
    {
        var phoneChanged = user.PhoneNumber != phoneNumber;
        var changed = phoneChanged
            || user.UserName != phoneNumber
            || user.NormalizedUserName != phoneNumber.ToUpperInvariant()
            || user.EmailConfirmed
            || user.TwoFactorEnabled
            || user.RequiresMfa
            || user.PasswordHash is not null;
        if (!changed)
        {
            return false;
        }

        user.PhoneNumber = phoneNumber;
        if (phoneChanged)
        {
            user.PhoneNumberConfirmed = false;
        }

        user.UserName = phoneNumber;
        user.NormalizedUserName = phoneNumber.ToUpperInvariant();
        user.EmailConfirmed = false;
        user.TwoFactorEnabled = false;
        user.RequiresMfa = false;
        user.PasswordHash = null;
        return true;
    }

    private async Task<bool> RemoveAuthenticatorTokensAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var tokens = await db.Set<IdentityUserToken<Guid>>()
            .Where(token => token.UserId == userId
                && (token.Name == "AuthenticatorKey" || token.Name == "RecoveryCodes"))
            .ToArrayAsync(cancellationToken);
        db.RemoveRange(tokens);
        return tokens.Length > 0;
    }
}
