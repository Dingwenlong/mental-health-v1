using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MentalHealth.Application.Security;
using MentalHealth.Infrastructure.Identity;
using MentalHealth.Infrastructure.Persistence;
using MentalHealth.IntegrationTests.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealth.IntegrationTests.Support;

internal sealed record SyntheticUser(
    Guid UserId,
    Guid SubjectId,
    string Token,
    HttpClient Client) : IDisposable
{
    public void Dispose() => Client.Dispose();
}

internal sealed record StartedConsultation(
    Guid SessionId,
    SyntheticUser User,
    string CounselorToken) : IDisposable
{
    public void Dispose() => User.Dispose();
}

internal static class ConsultationScenario
{
    public static async Task<StartedConsultation> StartVideoAsync(
        AuthApiFixture fixture)
    {
        var user = await CreateUserAsync(fixture);
        try
        {
            foreach (var kind in new[] { "Service", "Recording", "AiAnalysis" })
            {
                await GrantAsync(user.Client, kind);
            }

            using var orderResponse = await user.Client.PostAsJsonAsync(
                "/api/v1/orders",
                new
                {
                    planId = DemoCatalogSeeder.HumanVideoPaidPlanId,
                    idempotencyKey = $"video-order-{Guid.NewGuid():N}"
                });
            orderResponse.EnsureSuccessStatusCode();
            var orderId = (await ReadJsonAsync(orderResponse))
                .GetProperty("id")
                .GetGuid();
            using var confirmResponse = await user.Client.PostAsJsonAsync(
                $"/api/v1/orders/{orderId}/confirm",
                new { });
            confirmResponse.EnsureSuccessStatusCode();

            using var createResponse = await user.Client.PostAsJsonAsync(
                "/api/v1/consultations",
                new
                {
                    orderId,
                    assignedPractitionerId = IdentitySeeder.DemoCounselorId,
                    scheduledAt = DateTimeOffset.UtcNow.AddMinutes(5),
                    idempotencyKey = $"video-session-{Guid.NewGuid():N}"
                });
            createResponse.EnsureSuccessStatusCode();
            var sessionId = (await ReadJsonAsync(createResponse))
                .GetProperty("id")
                .GetGuid();
            using var startResponse = await user.Client.PostAsJsonAsync(
                $"/api/v1/consultations/{sessionId}/start",
                new { });
            startResponse.EnsureSuccessStatusCode();
            var counselorToken = await fixture.IssueTrustedApiTokenForAsync(
                "counselor@demo.local");
            return new StartedConsultation(sessionId, user, counselorToken);
        }
        catch
        {
            user.Dispose();
            throw;
        }
    }

    public static async Task<SyntheticUser> CreateUserAsync(AuthApiFixture fixture)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"media-{suffix}@example.test";
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            PhoneNumber = AuthApiFixture.CreateSyntheticPhoneNumber(
                $"consultation-scenario:{suffix}"),
            SubjectId = Guid.NewGuid()
        };
        EnsureSucceeded(await userManager.CreateAsync(
            user,
            $"Synthetic-media-password-{suffix}!"));
        EnsureSucceeded(await userManager.AddToRoleAsync(user, AppRoles.User));
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var token = tokenService.Issue(
            new JwtTokenSubject(
                user.Id,
                user.PhoneNumber!,
                [AppRoles.User],
                user.SubjectId,
                null)).Value;
        return new SyntheticUser(
            user.Id,
            user.SubjectId.Value,
            token,
            fixture.CreateClientWithBearer(token));
    }

    public static async Task<JsonElement> ReadJsonAsync(
        HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    public static async Task<string> ReadProblemCodeAsync(
        HttpResponseMessage response) =>
        (await ReadJsonAsync(response)).GetProperty("code").GetString()
        ?? throw new InvalidOperationException("Problem code is missing.");

    private static async Task GrantAsync(HttpClient client, string kind)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/consents",
            new { kind, textVersion = "media-v1" });
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created);
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
}
