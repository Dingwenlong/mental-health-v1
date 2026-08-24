using System.Net;
using System.Net.Http.Json;
using MentalHealth.Domain.Analysis;
using MentalHealth.Infrastructure.Persistence;
using MentalHealth.IntegrationTests.Auth;
using MentalHealth.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealth.IntegrationTests.Analysis;

[Collection(AuthApiCollection.Name)]
public sealed class TranscriptApiTests(AuthApiFixture fixture)
{
    [Fact]
    public async Task Completed_session_accepts_upload_then_correction_without_overwrite()
    {
        using var started = await ConsultationScenario.StartVideoAsync(fixture);
        using var complete = await started.User.Client.PostAsJsonAsync(
            $"/api/v1/consultations/{started.SessionId}/complete",
            new { idempotencyKey = $"complete-{Guid.NewGuid():N}" });
        complete.EnsureSuccessStatusCode();

        using var firstResponse = await started.User.Client.PostAsJsonAsync(
            $"/api/v1/consultations/{started.SessionId}/transcript",
            new { source = "ManualUpload", text = "第一版人工转写。" });
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var secondResponse = await started.User.Client.PostAsJsonAsync(
            $"/api/v1/consultations/{started.SessionId}/transcript",
            new { source = "ManualCorrection", text = "第二版人工校对。" });
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var response = await ConsultationScenario.ReadJsonAsync(secondResponse);
        Assert.Equal(2, response.GetProperty("revision").GetInt32());
        Assert.Equal("ManualCorrection", response.GetProperty("source").GetString());

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
        var documents = await db.ManualTranscripts
            .AsNoTracking()
            .Where(item => item.SessionId == started.SessionId)
            .OrderBy(item => item.Revision)
            .ToArrayAsync();
        var job = await db.AnalysisJobs
            .AsNoTracking()
            .SingleAsync(item => item.SessionId == started.SessionId);
        Assert.Equal([1, 2], documents.Select(item => item.Revision));
        Assert.Equal("第一版人工转写。", documents[0].Text);
        Assert.Equal("第二版人工校对。", documents[1].Text);
        Assert.Equal(2, job.TranscriptRevision);
        Assert.Equal(AnalysisJobStatus.Ready, job.Status);
    }
}
