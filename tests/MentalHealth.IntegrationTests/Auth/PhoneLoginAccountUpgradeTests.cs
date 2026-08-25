using MentalHealth.Infrastructure.Identity;
using MentalHealth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MentalHealth.IntegrationTests.Auth;

[Collection(AuthApiCollection.Name)]
public sealed class PhoneLoginAccountUpgradeTests(AuthApiFixture fixture)
{
    [Fact]
    public void Startup_allows_disabled_aliyun_phone_login_without_private_configuration()
    {
        Assert.NotNull(fixture.Services.GetRequiredService<UserManager<AppUser>>());
    }

    [Fact]
    public async Task Startup_upgrades_public_accounts_without_password_or_totp()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var client = await users.FindByEmailAsync("abc@qq.com");
        var admin = await users.FindByEmailAsync("123@qq.com");

        Assert.Equal("+8613800138001", client!.PhoneNumber);
        Assert.Equal(client.PhoneNumber, client.UserName);
        Assert.Null(client.PasswordHash);
        Assert.False(client.TwoFactorEnabled);
        Assert.False(client.EmailConfirmed);
        Assert.Equal("+8613900139002", admin!.PhoneNumber);
        Assert.Equal(admin.PhoneNumber, admin.UserName);
        Assert.Null(admin.PasswordHash);
        Assert.False(admin.TwoFactorEnabled);
        Assert.False(admin.EmailConfirmed);
    }

    [Fact]
    public async Task Startup_keeps_practitioner_test_accounts_without_phone_numbers_or_passwords()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var counselor = await users.FindByEmailAsync("counselor@demo.local");
        var doctor = await users.FindByEmailAsync("doctor@demo.local");

        Assert.Null(counselor!.PhoneNumber);
        Assert.Null(counselor.PasswordHash);
        Assert.Null(doctor!.PhoneNumber);
        Assert.Null(doctor.PasswordHash);
    }

    [Fact]
    public async Task Repeating_the_upgrade_does_not_change_already_upgraded_accounts()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var upgrader = scope.ServiceProvider.GetRequiredService<PhoneLoginAccountUpgrader>();
        var client = await users.FindByEmailAsync("abc@qq.com");
        var admin = await users.FindByEmailAsync("123@qq.com");
        var clientSecurityStamp = client!.SecurityStamp;
        var adminSecurityStamp = admin!.SecurityStamp;

        await upgrader.UpgradeAsync();

        Assert.Equal(clientSecurityStamp, client.SecurityStamp);
        Assert.Equal(adminSecurityStamp, admin.SecurityStamp);
    }

    [Fact]
    public async Task Startup_seed_and_upgrade_preserve_changed_contact_emails_and_confirmed_phones()
    {
        var startupFixture = new AuthApiFixture();
        await startupFixture.InitializeAsync();
        try
        {
            Guid clientId;
            Guid adminId;
            int userCount;
            await using (var setupScope = startupFixture.Services.CreateAsyncScope())
            {
                var db = setupScope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
                var client = await db.Users.SingleAsync(
                    user => user.PhoneNumber == "+8613800138001");
                var admin = await db.Users.SingleAsync(
                    user => user.PhoneNumber == "+8613900139002");
                clientId = client.Id;
                adminId = admin.Id;
                userCount = await db.Users.CountAsync();

                client.Email = "client-contact@example.test";
                client.NormalizedEmail = "CLIENT-CONTACT@EXAMPLE.TEST";
                client.PhoneNumberConfirmed = true;
                admin.Email = null;
                admin.NormalizedEmail = null;
                admin.PhoneNumberConfirmed = true;
                await db.SaveChangesAsync();
            }

            await using (var startupScope = startupFixture.Services.CreateAsyncScope())
            {
                await startupScope.ServiceProvider
                    .GetRequiredService<IdentitySeeder>()
                    .SeedAsync();
                await startupScope.ServiceProvider
                    .GetRequiredService<PhoneLoginAccountUpgrader>()
                    .UpgradeAsync();
            }

            await using var assertScope = startupFixture.Services.CreateAsyncScope();
            var assertDb = assertScope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
            var clientAfter = await assertDb.Users.SingleAsync(user => user.Id == clientId);
            var adminAfter = await assertDb.Users.SingleAsync(user => user.Id == adminId);
            Assert.Equal(userCount, await assertDb.Users.CountAsync());
            Assert.Equal("client-contact@example.test", clientAfter.Email);
            Assert.True(clientAfter.PhoneNumberConfirmed);
            Assert.Null(adminAfter.Email);
            Assert.True(adminAfter.PhoneNumberConfirmed);
        }
        finally
        {
            await startupFixture.DisposeAsync();
        }
    }

    [Fact]
    public Task Startup_rejects_matching_public_phone_numbers_without_upgrading_either_account() =>
        AssertInvalidStartupDoesNotUpgradeAccountsAsync("13800138001", "13800138001");

    [Fact]
    public Task Startup_rejects_an_invalid_public_phone_number_without_upgrading_either_account() =>
        AssertInvalidStartupDoesNotUpgradeAccountsAsync("invalid-phone", "13900139002");

    [Fact]
    public Task Startup_rejects_a_missing_public_phone_number_without_upgrading_either_account() =>
        AssertInvalidStartupDoesNotUpgradeAccountsAsync(null, "13900139002");

    [Fact]
    public async Task Startup_rejects_enabled_aliyun_phone_login_without_private_configuration()
    {
        var enabledFixture = new AuthApiFixture(
            "13800138001",
            "13900139002",
            aliyunPhoneLoginEnabled: true);
        try
        {
            await Assert.ThrowsAsync<OptionsValidationException>(enabledFixture.InitializeAsync);
        }
        finally
        {
            await enabledFixture.DisposeAsync();
        }
    }

    private static async Task AssertInvalidStartupDoesNotUpgradeAccountsAsync(
        string? clientPhone,
        string? adminPhone)
    {
        var invalidFixture = new AuthApiFixture(clientPhone, adminPhone);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                invalidFixture.InitializeAsync);
            var phones = await invalidFixture.ReadPublicAccountPhonesAsync();
            Assert.Null(phones.ClientPhone);
            Assert.Null(phones.AdminPhone);
        }
        finally
        {
            await invalidFixture.DisposeAsync();
        }
    }
}
