using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Domain.Consultations;
using MentalHealth.Infrastructure.Persistence;
using MentalHealth.Infrastructure.Storage;
using MentalHealth.IntegrationTests.Auth;
using MentalHealth.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealth.IntegrationTests.Security;

[Collection(AuthApiCollection.Name)]
public sealed class MediaAccessTicketTests(AuthApiFixture fixture)
{
    [Fact]
    public async Task Expired_or_other_subject_media_ticket_is_denied()
    {
        using var setup = await ConsultationScenario.StartVideoAsync(fixture);
        using var otherSubject = await ConsultationScenario.CreateUserAsync(fixture);
        var marker = $"synthetic-media-{Guid.NewGuid():N}";
        var asset = await SeedCompletedMediaAsync(setup, marker, DateTimeOffset.UtcNow);

        await using var scope = fixture.Services.CreateAsyncScope();
        var tickets = scope.ServiceProvider.GetRequiredService<MediaAccessTicketService>();
        var now = DateTimeOffset.UtcNow;
        var otherSubjectTicket = tickets.Create(
            setup.User.SubjectId,
            asset.Id,
            now.AddMinutes(5));
        var expiredTicket = tickets.Create(
            setup.User.SubjectId,
            asset.Id,
            now.AddMinutes(-1));

        using var otherSubjectResponse = await otherSubject.Client.GetAsync(
            $"/api/v1/media/{asset.Id}/content?ticket={Uri.EscapeDataString(otherSubjectTicket)}");
        using var expiredResponse = await setup.User.Client.GetAsync(
            $"/api/v1/media/{asset.Id}/content?ticket={Uri.EscapeDataString(expiredTicket)}");

        Assert.Equal(HttpStatusCode.Forbidden, otherSubjectResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, expiredResponse.StatusCode);
    }

    [Fact]
    public async Task Current_owner_can_read_media_with_a_five_minute_ticket()
    {
        using var setup = await ConsultationScenario.StartVideoAsync(fixture);
        var marker = $"synthetic-media-{Guid.NewGuid():N}";
        var asset = await SeedCompletedMediaAsync(setup, marker, DateTimeOffset.UtcNow);

        var requestedAt = DateTimeOffset.UtcNow;
        using var ticketResponse = await setup.User.Client.PostAsync(
            $"/api/v1/media/{asset.Id}/ticket",
            content: null);
        ticketResponse.EnsureSuccessStatusCode();
        var ticketBody = await ticketResponse.Content.ReadFromJsonAsync<JsonElement>();
        var ticket = ticketBody.GetProperty("ticket").GetString();
        var expiresAt = ticketBody.GetProperty("expiresAt").GetDateTimeOffset();
        Assert.NotNull(ticket);
        Assert.InRange(
            expiresAt,
            requestedAt.AddMinutes(4).AddSeconds(50),
            DateTimeOffset.UtcNow.AddMinutes(5));

        using var response = await setup.User.Client.GetAsync(
            $"/api/v1/media/{asset.Id}/content?ticket={Uri.EscapeDataString(ticket!)}");

        response.EnsureSuccessStatusCode();
        Assert.Equal("video/webm", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(marker, await response.Content.ReadAsStringAsync());
    }

    private async Task<MediaAsset> SeedCompletedMediaAsync(
        StartedConsultation setup,
        string marker,
        DateTimeOffset capturedAt)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        var asset = MediaAsset.Create(
            setup.SessionId,
            setup.User.SubjectId,
            setup.User.UserId,
            "video/webm",
            1,
            $"ticket-test-{Guid.NewGuid():N}",
            capturedAt);
        var objectKey = $"demo/{setup.User.SubjectId:N}/media/{asset.Id:N}.media";
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes(marker));
        var stored = await storage.PutAsync(
            new ObjectWriteRequest(objectKey, "video/webm"),
            content,
            CancellationToken.None);
        asset.Complete(
            objectKey,
            stored.Sha256,
            stored.Length,
            $"ticket-complete-{Guid.NewGuid():N}",
            capturedAt);
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync();
        return asset;
    }
}
