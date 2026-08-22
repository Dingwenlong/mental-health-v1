using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MentalHealth.IntegrationTests.Auth;
using MentalHealth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealth.IntegrationTests.Consents;

[Collection(AuthApiCollection.Name)]
public sealed class ConsentTests(AuthApiFixture fixture)
{
    [Fact]
    public async Task User_can_record_and_withdraw_recording_consent()
    {
        using var client = await LoginAsUserAsync();

        var record = await client.PostAsJsonAsync(
            "/api/v1/consents",
            new
            {
                kind = "Recording",
                textVersion = "recording-v1"
            });

        Assert.Equal(HttpStatusCode.Created, record.StatusCode);
        using var recordedBody = await JsonDocument.ParseAsync(
            await record.Content.ReadAsStreamAsync());
        var consentId = recordedBody.RootElement.GetProperty("id").GetGuid();
        Assert.EndsWith(consentId.ToString(), record.Headers.Location!.ToString());
        Assert.Equal(
            "Recording",
            recordedBody.RootElement.GetProperty("kind").GetString());
        Assert.True(recordedBody.RootElement.GetProperty("active").GetBoolean());

        var withdraw = await client.DeleteAsync($"/api/v1/consents/{consentId}");

        Assert.Equal(HttpStatusCode.NoContent, withdraw.StatusCode);

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
        var saved = await db.ConsentRecords.SingleAsync(
            consent => consent.Id == consentId);
        var auditActions = await db.AuditEvents
            .Where(audit => audit.ResourceId == consentId)
            .OrderBy(audit => audit.OccurredAt)
            .Select(audit => audit.Action)
            .ToArrayAsync();

        Assert.NotNull(saved.WithdrawnAt);
        Assert.Equal(["ConsentGranted", "ConsentWithdrawn"], auditActions);
    }

    [Fact]
    public async Task Model_training_consent_is_disabled()
    {
        using var client = await LoginAsUserAsync();

        var response = await client.PostAsJsonAsync(
            "/api/v1/consents",
            new
            {
                kind = "ModelTraining",
                textVersion = "model-training-v1"
            });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        Assert.Equal(
            "CONSENT_TYPE_DISABLED",
            body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Blank_consent_text_version_is_rejected()
    {
        using var client = await LoginAsUserAsync();

        var response = await client.PostAsJsonAsync(
            "/api/v1/consents",
            new
            {
                kind = "AiAnalysis",
                textVersion = "   "
            });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        Assert.Equal(
            "INVALID_CONSENT_TEXT_VERSION",
            body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Anonymous_user_cannot_record_consent()
    {
        var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/consents",
            new
            {
                kind = "Recording",
                textVersion = "recording-v1"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Counselor_cannot_record_a_users_consent()
    {
        using var client = await LoginAsync("counselor@demo.local");

        var response = await client.PostAsJsonAsync(
            "/api/v1/consents",
            new
            {
                kind = "Service",
                textVersion = "service-v1"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        Assert.Equal(
            "FORBIDDEN_RESOURCE",
            body.RootElement.GetProperty("code").GetString());
    }

    private async Task<HttpClient> LoginAsUserAsync()
    {
        return await LoginAsync("user@demo.local");
    }

    private async Task<HttpClient> LoginAsync(string email)
    {
        var login = await fixture.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                email,
                password = AuthApiFixture.InitialPassword
            });
        login.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(
            await login.Content.ReadAsStreamAsync());
        return fixture.CreateClientWithBearer(
            body.RootElement.GetProperty("accessToken").GetString()!);
    }
}
