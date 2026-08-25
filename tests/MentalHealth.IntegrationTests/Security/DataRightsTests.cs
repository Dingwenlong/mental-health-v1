using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Application.DataRights;
using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.DataRights;
using MentalHealth.Infrastructure.Persistence;
using MentalHealth.Infrastructure.Storage;
using MentalHealth.IntegrationTests.Auth;
using MentalHealth.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MentalHealth.IntegrationTests.Security;

[Collection(AuthApiCollection.Name)]
public sealed class DataRightsTests(AuthApiFixture fixture)
{
    [Fact]
    public async Task Export_contains_only_the_current_subject_and_excludes_raw_media_by_default()
    {
        using var current = await ConsultationScenario.StartVideoAsync(fixture);
        using var other = await ConsultationScenario.StartVideoAsync(fixture);
        var currentMarker = $"current-subject-{Guid.NewGuid():N}";
        var otherMarker = $"other-subject-{Guid.NewGuid():N}";
        await SeedMessageTranscriptAndMediaAsync(current, currentMarker, DateTimeOffset.UtcNow);
        await SeedMessageTranscriptAndMediaAsync(other, otherMarker, DateTimeOffset.UtcNow);

        using var response = await current.User.Client.GetAsync(
            "/api/v1/data-rights/export");

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        await using var archiveBytes = new MemoryStream(
            await response.Content.ReadAsByteArrayAsync());
        using var archive = new ZipArchive(archiveBytes, ZipArchiveMode.Read);
        var names = archive.Entries.Select(entry => entry.FullName).ToArray();
        Assert.Contains("subject.json", names);
        Assert.Contains("messages.json", names);
        Assert.Contains(
            names,
            name => name.StartsWith("transcripts/", StringComparison.Ordinal));
        Assert.DoesNotContain(
            names,
            name => name.StartsWith("media/", StringComparison.Ordinal));
        var text = await ReadAllTextAsync(archive);
        Assert.Contains(currentMarker, text, StringComparison.Ordinal);
        Assert.DoesNotContain(otherMarker, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Raw_media_export_requires_a_second_confirmation()
    {
        using var setup = await ConsultationScenario.StartVideoAsync(fixture);
        var marker = $"confirmed-media-{Guid.NewGuid():N}";
        await SeedMessageTranscriptAndMediaAsync(setup, marker, DateTimeOffset.UtcNow);

        using var missingConfirmation = await setup.User.Client.GetAsync(
            "/api/v1/data-rights/export?includeRawMedia=true");
        using var confirmed = await setup.User.Client.GetAsync(
            "/api/v1/data-rights/export?includeRawMedia=true&confirmRawMedia=true");

        Assert.Equal(
            HttpStatusCode.UnprocessableEntity,
            missingConfirmation.StatusCode);
        confirmed.EnsureSuccessStatusCode();
        await using var archiveBytes = new MemoryStream(
            await confirmed.Content.ReadAsByteArrayAsync());
        using var archive = new ZipArchive(archiveBytes, ZipArchiveMode.Read);
        Assert.Contains(
            archive.Entries,
            entry => entry.FullName.StartsWith("media/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Demo_delete_never_touches_file_outside_demo_root()
    {
        using var setup = await ConsultationScenario.StartVideoAsync(fixture);
        var marker = $"delete-subject-{Guid.NewGuid():N}";
        var asset = await SeedMessageTranscriptAndMediaAsync(
            setup,
            marker,
            DateTimeOffset.UtcNow);
        var options = fixture.Services.GetRequiredService<
            IOptions<LocalObjectStorageOptions>>().Value;
        var sentinelPath = Path.Combine(
            Path.GetFullPath(options.RootPath),
            $"outside-demo-{Guid.NewGuid():N}.sentinel");
        await File.WriteAllTextAsync(sentinelPath, "must remain");

        using var response = await setup.User.Client.DeleteAsync(
            "/api/v1/data-rights/demo-data");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(File.Exists(sentinelPath));
        await using var verifyScope = fixture.Services.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
        Assert.False(await db.ConsultationSessions.AnyAsync(
            session => session.SubjectId == setup.User.SubjectId));
        Assert.False(await db.MediaAssets.AnyAsync(
            media => media.SubjectId == setup.User.SubjectId));
        Assert.True(await db.AuditEvents.AnyAsync(
            audit => audit.ActorUserId == setup.User.UserId
                && audit.Action == "DemoDataDeleted"));
        var deletion = await db.DemoDataDeletions.AsNoTracking().SingleAsync(
            item => item.SubjectId == setup.User.SubjectId);
        Assert.Equal(DemoDataDeletionStatus.Deleted, deletion.Status);
        Assert.NotNull(deletion.DeletedAt);
        var storage = verifyScope.ServiceProvider.GetRequiredService<IObjectStorage>();
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await using var _ = await storage.OpenReadAsync(
                asset.ObjectKey!,
                CancellationToken.None);
        });
    }

    [Fact]
    public async Task Retention_removes_only_demo_media_older_than_thirty_days()
    {
        using var setup = await ConsultationScenario.StartVideoAsync(fixture);
        var oldAsset = await SeedMessageTranscriptAndMediaAsync(
            setup,
            $"old-media-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(-31));
        var recentAsset = await SeedMessageTranscriptAndMediaAsync(
            setup,
            $"recent-media-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(-29));
        var nonDemoAsset = await SeedMessageTranscriptAndMediaAsync(
            setup,
            $"non-demo-media-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(-31));
        await using (var setupScope = fixture.Services.CreateAsyncScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
            await setupDb.MediaAssets
                .Where(asset => asset.Id == nonDemoAsset.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(asset => asset.IsDemo, false));
        }

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var retention = scope.ServiceProvider.GetRequiredService<DemoRetentionHandler>();
            Assert.Equal(1, await retention.HandleAsync(CancellationToken.None));
        }

        await using var verifyScope = fixture.Services.CreateAsyncScope();
        var storage = verifyScope.ServiceProvider.GetRequiredService<IObjectStorage>();
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await using var _ = await storage.OpenReadAsync(
                oldAsset.ObjectKey!,
                CancellationToken.None);
        });
        await using var recent = await storage.OpenReadAsync(
            recentAsset.ObjectKey!,
            CancellationToken.None);
        Assert.True(recent.CanRead);
        await using var nonDemo = await storage.OpenReadAsync(
            nonDemoAsset.ObjectKey!,
            CancellationToken.None);
        Assert.True(nonDemo.CanRead);
        var db = verifyScope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
        var oldRow = await db.MediaAssets.AsNoTracking().SingleAsync(
            asset => asset.Id == oldAsset.Id);
        var recentRow = await db.MediaAssets.AsNoTracking().SingleAsync(
            asset => asset.Id == recentAsset.Id);
        var nonDemoRow = await db.MediaAssets.AsNoTracking().SingleAsync(
            asset => asset.Id == nonDemoAsset.Id);
        Assert.Equal(MediaAssetStatus.Purged, oldRow.Status);
        Assert.NotNull(oldRow.RawMediaDeletedAt);
        Assert.Null(oldRow.ObjectKey);
        Assert.Equal(MediaAssetStatus.Completed, recentRow.Status);
        Assert.NotNull(recentRow.ObjectKey);
        Assert.False(nonDemoRow.IsDemo);
        Assert.Equal(MediaAssetStatus.Completed, nonDemoRow.Status);
        Assert.NotNull(nonDemoRow.ObjectKey);
    }

    [Fact]
    public async Task Audit_is_admin_only_and_returns_no_sensitive_content_fields()
    {
        using var setup = await ConsultationScenario.StartVideoAsync(fixture);
        using var denied = await setup.User.Client.GetAsync(
            "/api/v1/data-rights/audit");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        using var admin = await fixture.CreateTrustedApiClientForAsync(
            "123@qq.com");
        using var response = await admin.GetAsync("/api/v1/data-rights/audit");
        response.EnsureSuccessStatusCode();
        var records = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, records.ValueKind);
        Assert.NotEmpty(records.EnumerateArray());
        var allowedProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "occurredAt",
            "actorUserId",
            "action",
            "resourceId",
            "reason"
        };
        foreach (var record in records.EnumerateArray())
        {
            Assert.All(
                record.EnumerateObject(),
                property => Assert.Contains(property.Name, allowedProperties));
        }
    }

    private async Task<MediaAsset> SeedMessageTranscriptAndMediaAsync(
        StartedConsultation setup,
        string marker,
        DateTimeOffset capturedAt)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
        var sequence = await db.Messages.CountAsync(
            message => message.SessionId == setup.SessionId) + 1;
        db.Messages.Add(Message.Create(
            setup.SessionId,
            setup.User.UserId,
            MessageSenderKind.User,
            marker,
            $"export-message-{Guid.NewGuid():N}",
            sequence,
            capturedAt));
        var revision = await db.ManualTranscripts.CountAsync(
            transcript => transcript.SessionId == setup.SessionId) + 1;
        db.ManualTranscripts.Add(ManualTranscript.Create(
            setup.SessionId,
            revision,
            TranscriptSource.ManualUpload,
            $"transcript-{marker}",
            capturedAt));

        var asset = MediaAsset.Create(
            setup.SessionId,
            setup.User.SubjectId,
            setup.User.UserId,
            "video/webm",
            1,
            $"data-rights-{Guid.NewGuid():N}",
            capturedAt);
        var objectKey = $"demo/{setup.User.SubjectId:N}/media/{asset.Id:N}.media";
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes(marker));
        var stored = await storage.PutAsync(
            new ObjectWriteRequest(objectKey, "video/webm"),
            content,
            CancellationToken.None);
        asset.Complete(
            objectKey,
            stored.Sha256,
            stored.Length,
            $"data-rights-complete-{Guid.NewGuid():N}",
            capturedAt);
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync();
        return asset;
    }

    private static async Task<string> ReadAllTextAsync(ZipArchive archive)
    {
        var builder = new StringBuilder();
        foreach (var entry in archive.Entries.Where(entry =>
                     entry.FullName.EndsWith(".json", StringComparison.Ordinal)
                     || entry.FullName.EndsWith(".txt", StringComparison.Ordinal)))
        {
            await using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            builder.Append(await reader.ReadToEndAsync());
        }

        return builder.ToString();
    }
}
