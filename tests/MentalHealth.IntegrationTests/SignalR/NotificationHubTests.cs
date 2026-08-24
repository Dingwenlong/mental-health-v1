using System.Net.Http.Json;
using MentalHealth.IntegrationTests.Auth;
using MentalHealth.IntegrationTests.Support;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace MentalHealth.IntegrationTests.SignalR;

[Collection(AuthApiCollection.Name)]
public sealed class NotificationHubTests(AuthApiFixture fixture)
{
    [Fact]
    public async Task Consultation_owner_receives_analysis_status_after_transcript_is_saved()
    {
        using var started = await ConsultationScenario.StartVideoAsync(fixture);
        using var completed = await started.User.Client.PostAsJsonAsync(
            $"/api/v1/consultations/{started.SessionId}/complete",
            new { idempotencyKey = $"notify-complete-{Guid.NewGuid():N}" });
        completed.EnsureSuccessStatusCode();
        await using var connection = CreateConnection(started.User.Token);
        var received = new TaskCompletionSource<AnalysisStatusEnvelope>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<AnalysisStatusEnvelope>(
            "AnalysisStatusChanged",
            update => received.TrySetResult(update));
        await connection.StartAsync();
        await connection.InvokeAsync("WatchSession", started.SessionId);

        using var transcript = await started.User.Client.PostAsJsonAsync(
            $"/api/v1/consultations/{started.SessionId}/transcript",
            new
            {
                source = "ManualUpload",
                text = "这是通知测试使用的合成转写，不含真实资料。"
            });

        transcript.EnsureSuccessStatusCode();
        var update = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(started.SessionId, update.SessionId);
        Assert.Equal("Queued", update.Status);
        Assert.Equal(1, update.TranscriptRevision);
    }

    private HubConnection CreateConnection(string token) =>
        new HubConnectionBuilder()
            .WithUrl(
                "http://localhost/hubs/notifications",
                options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                    options.HttpMessageHandlerFactory = _ => fixture.CreateServerHandler();
                    options.Transports = HttpTransportType.LongPolling;
                })
            .Build();

    private sealed record AnalysisStatusEnvelope(
        Guid SessionId,
        string Status,
        int? TranscriptRevision);
}
