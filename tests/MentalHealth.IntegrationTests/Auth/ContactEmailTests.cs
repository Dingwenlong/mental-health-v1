using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MentalHealth.Contracts.Common;
using MentalHealth.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealth.IntegrationTests.Auth;

[Collection(AuthApiCollection.Name)]
public sealed class ContactEmailTests(AuthApiFixture fixture)
{
    [Fact]
    public async Task Current_account_can_read_update_and_clear_only_its_contact_email()
    {
        await fixture.ResetContactEmailsAsync();
        try
        {
            using var client = await fixture.CreateTrustedApiClientForAsync("abc@qq.com");
            using var admin = await fixture.CreateTrustedApiClientForAsync("123@qq.com");
            var clientEmail = $"client-{Guid.NewGuid():N}@example.com";
            var adminEmail = $"admin-{Guid.NewGuid():N}@example.com";

            using var clientUpdate = await client.PutAsJsonAsync(
                "/api/v1/account/contact-email",
                new { email = $"  {clientEmail}  " });
            using var adminUpdate = await admin.PutAsJsonAsync(
                "/api/v1/account/contact-email",
                new { email = adminEmail });
            Assert.Equal(HttpStatusCode.NoContent, clientUpdate.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, adminUpdate.StatusCode);

            Assert.Equal(clientEmail, await ReadEmailAsync(client));
            Assert.Equal(adminEmail, await ReadEmailAsync(admin));
            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                Assert.False((await users.FindByEmailAsync(clientEmail))!.EmailConfirmed);
            }

            using var clear = await client.PutAsJsonAsync<object?>(
                "/api/v1/account/contact-email",
                new { email = (string?)null });
            Assert.Equal(HttpStatusCode.NoContent, clear.StatusCode);
            Assert.Null(await ReadEmailAsync(client));
            Assert.Equal(adminEmail, await ReadEmailAsync(admin));
        }
        finally
        {
            await fixture.ResetContactEmailsAsync();
        }
    }

    [Fact]
    public async Task Invalid_contact_email_returns_422_without_changing_account()
    {
        await fixture.ResetContactEmailsAsync();
        using var client = await fixture.CreateTrustedApiClientForAsync("abc@qq.com");
        var before = await ReadEmailAsync(client);
        using var response = await client.PutAsJsonAsync(
            "/api/v1/account/contact-email",
            new { email = "Display Name <person@example.test>" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var problem = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        Assert.Equal(
            ApiProblemCodes.ContactEmailInvalid,
            problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(before, await ReadEmailAsync(client));
    }

    [Fact]
    public async Task Contact_email_requires_an_api_token()
    {
        using var response = await fixture.Client.GetAsync(
            "/api/v1/account/contact-email");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<string?> ReadEmailAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/account/contact-email");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContactEmailResponse>())!.Email;
    }
}

public sealed record ContactEmailResponse(string? Email);
