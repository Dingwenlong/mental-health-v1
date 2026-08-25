using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using MentalHealth.Application.Security;
using MentalHealth.Infrastructure.Persistence;
using MentalHealth.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealth.IntegrationTests.Auth;

[Collection(AuthApiCollection.Name)]
public sealed class LoginAndMfaTests(AuthApiFixture fixture)
{
    [Fact]
    public async Task Local_admin_origin_receives_CORS_preflight_headers()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Options,
            "/api/v1/auth/login");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        using var response = await fixture.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal(
            "http://localhost:5173",
            response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Isolated_doctor_login_without_totp_returns_mfa_required_problem()
    {
        var user = await CreatePasswordUserAsync(AppRoles.Doctor, requiresMfa: true);
        var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                email = user.Email,
                password = user.Password
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var problem = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        Assert.Equal("MFA_REQUIRED", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Isolated_regular_user_login_returns_api_access_token()
    {
        var user = await CreatePasswordUserAsync(AppRoles.User, requiresMfa: false);
        var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                email = user.Email,
                password = user.Password
            });

        response.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        Assert.Equal(2, body.RootElement.GetProperty("accessToken")
            .GetString()!
            .Count(character => character == '.'));
    }

    [Fact]
    public async Task Wrong_password_does_not_return_mfa_setup_token()
    {
        var user = await CreatePasswordUserAsync(AppRoles.Doctor, requiresMfa: true);
        var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                email = user.Email,
                password = "Wrong-password-2026!"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        Assert.Equal(
            "INVALID_CREDENTIALS",
            body.RootElement.GetProperty("code").GetString());
        Assert.False(body.RootElement.TryGetProperty("setupToken", out _));
    }

    [Fact]
    public async Task Isolated_admin_mfa_setup_token_cannot_access_business_api()
    {
        var user = await CreatePasswordUserAsync(
            AppRoles.OperationsAdmin,
            requiresMfa: true);
        var login = await fixture.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                email = user.Email,
                password = user.Password
            });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        using var loginBody = await JsonDocument.ParseAsync(
            await login.Content.ReadAsStreamAsync());
        Assert.Equal(
            "MFA_REQUIRED",
            loginBody.RootElement.GetProperty("code").GetString());
        var setupToken = loginBody.RootElement.GetProperty("setupToken").GetString()!;

        using var setupClient = fixture.CreateClientWithBearer(setupToken);
        var businessResponse = await setupClient.PostAsJsonAsync(
            "/api/v1/consents",
            new
            {
                kind = "Service",
                textVersion = "service-v1"
            });

        Assert.Equal(HttpStatusCode.Forbidden, businessResponse.StatusCode);
        using var problem = await JsonDocument.ParseAsync(
            await businessResponse.Content.ReadAsStreamAsync());
        Assert.Equal(
            "FORBIDDEN_RESOURCE",
            problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Isolated_doctor_can_set_up_totp_then_login()
    {
        var user = await CreatePasswordUserAsync(AppRoles.Doctor, requiresMfa: true);
        var login = await fixture.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                email = user.Email,
                password = user.Password
            });
        using var loginBody = await JsonDocument.ParseAsync(
            await login.Content.ReadAsStreamAsync());
        var setupToken = loginBody.RootElement.GetProperty("setupToken").GetString()!;

        using var setupClient = fixture.CreateClientWithBearer(setupToken);
        var setup = await setupClient.PostAsJsonAsync(
            "/api/v1/auth/mfa/setup",
            new { });
        setup.EnsureSuccessStatusCode();
        using var setupBody = await JsonDocument.ParseAsync(
            await setup.Content.ReadAsStreamAsync());
        var manualKey = setupBody.RootElement.GetProperty("manualKey").GetString()!;

        var code = GenerateTotp(manualKey, DateTimeOffset.UtcNow);
        var confirm = await setupClient.PostAsJsonAsync(
            "/api/v1/auth/mfa/setup",
            new { totpCode = code });
        confirm.EnsureSuccessStatusCode();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
            Assert.Equal(1, await db.AuditEvents.CountAsync(
                audit => audit.Action == "MfaEnabled"
                    && audit.ResourceType == "AppUser"));
        }

        var completedLogin = await fixture.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                email = user.Email,
                password = user.Password,
                totpCode = code
            });

        completedLogin.EnsureSuccessStatusCode();
    }

    private async Task<PasswordTestUser> CreatePasswordUserAsync(
        string role,
        bool requiresMfa)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"legacy-login-{suffix}@example.test";
        const string password = "Synthetic-password-2026!";
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            PhoneNumber = AuthApiFixture.CreateSyntheticPhoneNumber(
                $"legacy-login:{suffix}"),
            RequiresMfa = requiresMfa
        };
        EnsureSucceeded(await userManager.CreateAsync(user, password));
        EnsureSucceeded(await userManager.AddToRoleAsync(user, role));
        return new PasswordTestUser(email, password);
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(
                "; ",
                result.Errors.Select(error => error.Description)));
        }
    }

    private sealed record PasswordTestUser(string Email, string Password);

    private static string GenerateTotp(string base32Secret, DateTimeOffset now)
    {
        var secret = DecodeBase32(base32Secret);
        var counter = (ulong)(now.ToUnixTimeSeconds() / 30);
        Span<byte> counterBytes = stackalloc byte[8];
        for (var index = counterBytes.Length - 1; index >= 0; index--)
        {
            counterBytes[index] = (byte)(counter & 0xff);
            counter >>= 8;
        }

        var hash = HMACSHA1.HashData(secret, counterBytes);
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    private static byte[] DecodeBase32(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new List<byte>();
        var buffer = 0;
        var bits = 0;

        foreach (var character in value.TrimEnd('=').ToUpperInvariant())
        {
            var index = alphabet.IndexOf(character);
            if (index < 0)
            {
                continue;
            }

            buffer = (buffer << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                output.Add((byte)(buffer >> bits));
                buffer &= (1 << bits) - 1;
            }
        }

        return output.ToArray();
    }
}
