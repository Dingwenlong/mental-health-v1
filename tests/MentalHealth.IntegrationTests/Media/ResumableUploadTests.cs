using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Application.Consultations.Media;
using MentalHealth.Domain.Consultations;
using MentalHealth.Infrastructure.Persistence;
using MentalHealth.IntegrationTests.Auth;
using MentalHealth.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealth.IntegrationTests.Media;

[Collection(AuthApiCollection.Name)]
public sealed class ResumableUploadTests(AuthApiFixture fixture)
{
    [Fact]
    public async Task Repeated_chunk_is_idempotent_and_completed_hash_must_match()
    {
        using var setup = await ConsultationScenario.StartVideoAsync(fixture);
        var uploadId = await CreateUploadAsync(
            setup.User.Client,
            setup.SessionId,
            expectedChunks: 2);
        var marker = Guid.NewGuid().ToString("N");
        var firstText = $"synthetic-private-media-first-{marker}";
        var laterText = $"synthetic-private-media-later-{marker}";
        var firstBytes = Encoding.UTF8.GetBytes(firstText);
        var laterBytes = Encoding.UTF8.GetBytes(laterText);
        var combinedBytes = Encoding.UTF8.GetBytes(firstText + laterText);
        fixture.ClearCapturedLogs();

        using var laterChunk = await PutChunkAsync(
            setup.User.Client,
            uploadId,
            1,
            laterBytes);
        Assert.Equal(HttpStatusCode.Created, laterChunk.StatusCode);
        using var firstChunk = await PutChunkAsync(
            setup.User.Client,
            uploadId,
            0,
            firstBytes);
        Assert.Equal(HttpStatusCode.Created, firstChunk.StatusCode);
        using var repeated = await PutChunkAsync(
            setup.User.Client,
            uploadId,
            0,
            firstBytes);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);

        using var wrongHash = await CompleteAsync(
            setup.User.Client,
            uploadId,
            "wrong",
            "complete-wrong");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongHash.StatusCode);
        Assert.Equal(
            "MEDIA_HASH_MISMATCH",
            await ConsultationScenario.ReadProblemCodeAsync(wrongHash));

        var expectedHash = Convert.ToHexStringLower(
            SHA256.HashData(combinedBytes));
        using var completed = await CompleteAsync(
            setup.User.Client,
            uploadId,
            expectedHash,
            "complete-correct");
        completed.EnsureSuccessStatusCode();
        var completedBody = await ConsultationScenario.ReadJsonAsync(completed);
        Assert.Equal("Completed", completedBody.GetProperty("status").GetString());
        Assert.Equal(expectedHash, completedBody.GetProperty("sha256").GetString());
        Assert.Equal(combinedBytes.LongLength, completedBody.GetProperty("length").GetInt64());
        Assert.False(completedBody.TryGetProperty("objectKey", out _));

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
            var asset = await db.MediaAssets
                .AsNoTracking()
                .SingleAsync(item => item.Id == uploadId);
            Assert.NotNull(asset.ObjectKey);
            var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
            await using var stored = await storage.OpenReadAsync(
                asset.ObjectKey,
                CancellationToken.None);
            using var reader = new StreamReader(
                stored,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: false);
            Assert.Equal(firstText + laterText, await reader.ReadToEndAsync());
            await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            {
                await using var _ = await storage.OpenReadAsync(
                    $"pending-media/{uploadId:N}/chunks/000000",
                    CancellationToken.None);
            });
        }
        Assert.DoesNotContain(
            fixture.CapturedLogs,
            entry => entry.Message.Contains(firstText, StringComparison.Ordinal)
                || entry.Message.Contains(laterText, StringComparison.Ordinal));

        using var repeatedComplete = await CompleteAsync(
            setup.User.Client,
            uploadId,
            expectedHash,
            "complete-correct");
        Assert.Equal(HttpStatusCode.OK, repeatedComplete.StatusCode);
        var repeatedBody = await ConsultationScenario.ReadJsonAsync(repeatedComplete);
        Assert.Equal(
            completedBody.GetProperty("id").GetGuid(),
            repeatedBody.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Create_upload_reuses_the_same_idempotency_key()
    {
        using var setup = await ConsultationScenario.StartVideoAsync(fixture);
        var idempotencyKey = $"same-upload-{Guid.NewGuid():N}";
        using var first = await PostCreateUploadAsync(
            setup.User.Client,
            setup.SessionId,
            2,
            idempotencyKey);
        using var repeated = await PostCreateUploadAsync(
            setup.User.Client,
            setup.SessionId,
            2,
            idempotencyKey);
        using var conflict = await PostCreateUploadAsync(
            setup.User.Client,
            setup.SessionId,
            3,
            idempotencyKey);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Equal(
            (await ConsultationScenario.ReadJsonAsync(first)).GetProperty("id").GetGuid(),
            (await ConsultationScenario.ReadJsonAsync(repeated)).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(
            "IDEMPOTENCY_CONFLICT",
            await ConsultationScenario.ReadProblemCodeAsync(conflict));

        using var completeSession = await setup.User.Client.PostAsJsonAsync(
            $"/api/v1/consultations/{setup.SessionId}/complete",
            new { idempotencyKey = $"finish-{Guid.NewGuid():N}" });
        completeSession.EnsureSuccessStatusCode();
        using var repeatedAfterSession = await PostCreateUploadAsync(
            setup.User.Client,
            setup.SessionId,
            2,
            idempotencyKey);
        using var newAfterSession = await PostCreateUploadAsync(
            setup.User.Client,
            setup.SessionId,
            2,
            $"new-after-session-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.OK, repeatedAfterSession.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, newAfterSession.StatusCode);
        Assert.Equal(
            "INVALID_SESSION_STATE",
            await ConsultationScenario.ReadProblemCodeAsync(newAfterSession));
    }

    [Fact]
    public async Task Missing_conflicting_and_out_of_range_chunks_have_stable_codes()
    {
        using var setup = await ConsultationScenario.StartVideoAsync(fixture);
        var uploadId = await CreateUploadAsync(
            setup.User.Client,
            setup.SessionId,
            expectedChunks: 2);
        using var chunk = await PutChunkAsync(
            setup.User.Client,
            uploadId,
            1,
            "def"u8.ToArray());
        chunk.EnsureSuccessStatusCode();

        using var missing = await CompleteAsync(
            setup.User.Client,
            uploadId,
            Convert.ToHexStringLower(SHA256.HashData("def"u8.ToArray())),
            "missing-chunk");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
        Assert.Equal(
            "MEDIA_CHUNK_MISSING",
            await ConsultationScenario.ReadProblemCodeAsync(missing));

        using var conflict = await PutChunkAsync(
            setup.User.Client,
            uploadId,
            1,
            "different"u8.ToArray());
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(
            "MEDIA_CHUNK_CONFLICT",
            await ConsultationScenario.ReadProblemCodeAsync(conflict));

        using var invalidIndex = await PutChunkAsync(
            setup.User.Client,
            uploadId,
            2,
            "x"u8.ToArray());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidIndex.StatusCode);
        Assert.Equal(
            "INVALID_CHUNK_INDEX",
            await ConsultationScenario.ReadProblemCodeAsync(invalidIndex));
    }

    [Fact]
    public async Task Another_user_cannot_write_or_complete_the_upload()
    {
        using var setup = await ConsultationScenario.StartVideoAsync(fixture);
        using var intruder = await ConsultationScenario.CreateUserAsync(fixture);
        var uploadId = await CreateUploadAsync(
            setup.User.Client,
            setup.SessionId,
            expectedChunks: 1);

        using var write = await PutChunkAsync(
            intruder.Client,
            uploadId,
            0,
            "abc"u8.ToArray());
        using var complete = await CompleteAsync(
            intruder.Client,
            uploadId,
            Convert.ToHexStringLower(SHA256.HashData("abc"u8.ToArray())),
            "intruder-complete");

        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Equal(
            "FORBIDDEN_RESOURCE",
            await ConsultationScenario.ReadProblemCodeAsync(write));
        Assert.Equal(HttpStatusCode.Forbidden, complete.StatusCode);
        Assert.Equal(
            "FORBIDDEN_RESOURCE",
            await ConsultationScenario.ReadProblemCodeAsync(complete));
    }

    [Fact]
    public async Task Expired_upload_chunks_are_removed_after_twenty_four_hours()
    {
        using var setup = await ConsultationScenario.StartVideoAsync(fixture);
        var uploadId = await CreateUploadAsync(
            setup.User.Client,
            setup.SessionId,
            expectedChunks: 1);
        using var chunk = await PutChunkAsync(
            setup.User.Client,
            uploadId,
            0,
            "expired"u8.ToArray());
        chunk.EnsureSuccessStatusCode();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
            await db.MediaAssets
                .Where(asset => asset.Id == uploadId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    asset => asset.UploadExpiresAt,
                    DateTimeOffset.UtcNow.AddMinutes(-1)));
            var cleanup = scope.ServiceProvider
                .GetRequiredService<ExpiredUploadCleanupHandler>();
            Assert.Equal(1, await cleanup.HandleAsync(CancellationToken.None));
        }

        await using var verifyScope = fixture.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider
            .GetRequiredService<MentalHealthDbContext>();
        var expired = await verifyDb.MediaAssets
            .AsNoTracking()
            .SingleAsync(asset => asset.Id == uploadId);
        Assert.Equal(MediaAssetStatus.Expired, expired.Status);
        Assert.NotNull(expired.ChunksDeletedAt);
        var storage = verifyScope.ServiceProvider.GetRequiredService<IObjectStorage>();
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await using var _ = await storage.OpenReadAsync(
                $"pending-media/{uploadId:N}/chunks/000000",
                CancellationToken.None);
        });
    }

    private static async Task<Guid> CreateUploadAsync(
        HttpClient client,
        Guid sessionId,
        int expectedChunks)
    {
        using var response = await PostCreateUploadAsync(
            client,
            sessionId,
            expectedChunks,
            $"upload-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ConsultationScenario.ReadJsonAsync(response))
            .GetProperty("id")
            .GetGuid();
    }

    private static Task<HttpResponseMessage> PostCreateUploadAsync(
        HttpClient client,
        Guid sessionId,
        int expectedChunks,
        string idempotencyKey) =>
        client.PostAsJsonAsync(
            "/api/v1/uploads",
            new
            {
                sessionId,
                contentType = "video/webm",
                expectedChunks,
                idempotencyKey
            });

    private static Task<HttpResponseMessage> PutChunkAsync(
        HttpClient client,
        Guid uploadId,
        int index,
        byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(
            "application/octet-stream");
        return client.PutAsync(
            $"/api/v1/uploads/{uploadId}/chunks/{index}",
            content);
    }

    private static Task<HttpResponseMessage> CompleteAsync(
        HttpClient client,
        Guid uploadId,
        string expectedSha256,
        string idempotencyKey) =>
        client.PostAsJsonAsync(
            $"/api/v1/uploads/{uploadId}/complete",
            new { expectedSha256, idempotencyKey });
}
