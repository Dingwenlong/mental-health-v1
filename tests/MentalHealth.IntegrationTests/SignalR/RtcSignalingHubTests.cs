using System.Net.Http.Json;
using MentalHealth.IntegrationTests.Auth;
using MentalHealth.IntegrationTests.Support;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace MentalHealth.IntegrationTests.SignalR;

[Collection(AuthApiCollection.Name)]
public sealed class RtcSignalingHubTests(AuthApiFixture fixture)
{
    [Fact]
    public async Task Offer_answer_and_ice_are_relayed_only_to_the_other_peer()
    {
        using var setup = await ConsultationScenario.StartVideoAsync(fixture);
        await using var mobile = CreateConnection(setup.User.Token);
        await using var web = CreateConnection(setup.CounselorToken);
        var offerAtWeb = Completion<RtcDescriptionEnvelope>();
        var offerAtMobile = Completion<RtcDescriptionEnvelope>();
        var answerAtMobile = Completion<RtcDescriptionEnvelope>();
        var iceAtWeb = Completion<RtcIceCandidateEnvelope>();
        var offerCountAtWeb = 0;
        web.On<RtcDescriptionEnvelope>(
            "OfferReceived",
            value =>
            {
                Interlocked.Increment(ref offerCountAtWeb);
                offerAtWeb.TrySetResult(value);
            });
        mobile.On<RtcDescriptionEnvelope>(
            "OfferReceived",
            value => offerAtMobile.TrySetResult(value));
        mobile.On<RtcDescriptionEnvelope>(
            "AnswerReceived",
            value => answerAtMobile.TrySetResult(value));
        web.On<RtcIceCandidateEnvelope>(
            "IceCandidateReceived",
            value => iceAtWeb.TrySetResult(value));

        await mobile.StartAsync();
        await web.StartAsync();
        await mobile.InvokeAsync("JoinRoom", setup.SessionId);
        await web.InvokeAsync("JoinRoom", setup.SessionId);
        fixture.ClearCapturedLogs();
        var offer = $"synthetic-offer-{Guid.NewGuid():N}";
        var answer = $"synthetic-answer-{Guid.NewGuid():N}";
        var candidate = $"synthetic-candidate-{Guid.NewGuid():N}";

        await mobile.InvokeAsync("RelayOffer", setup.SessionId, offer);
        await web.InvokeAsync("RelayAnswer", setup.SessionId, answer);
        await mobile.InvokeAsync(
            "RelayIceCandidate",
            setup.SessionId,
            candidate,
            "video",
            0);

        Assert.Equal(offer, (await WaitAsync(offerAtWeb.Task)).Sdp);
        Assert.Equal(answer, (await WaitAsync(answerAtMobile.Task)).Sdp);
        var receivedCandidate = await WaitAsync(iceAtWeb.Task);
        Assert.Equal(candidate, receivedCandidate.Candidate);
        Assert.Equal("video", receivedCandidate.SdpMid);
        Assert.Equal(0, receivedCandidate.SdpMLineIndex);
        await Task.Delay(250);
        Assert.False(offerAtMobile.Task.IsCompleted);
        await web.InvokeAsync("LeaveRoom", setup.SessionId);
        await mobile.InvokeAsync(
            "RelayOffer",
            setup.SessionId,
            $"synthetic-offer-after-leave-{Guid.NewGuid():N}");
        await Task.Delay(250);
        Assert.Equal(1, Volatile.Read(ref offerCountAtWeb));
        Assert.DoesNotContain(
            fixture.CapturedLogs,
            entry => entry.Message.Contains(offer, StringComparison.Ordinal)
                || entry.Message.Contains(answer, StringComparison.Ordinal)
                || entry.Message.Contains(candidate, StringComparison.Ordinal));
        Assert.Contains(
            fixture.CapturedLogs,
            entry => entry.Message.Contains("Offer", StringComparison.Ordinal));
        Assert.Contains(
            fixture.CapturedLogs,
            entry => entry.Message.Contains("Answer", StringComparison.Ordinal));
        Assert.Contains(
            fixture.CapturedLogs,
            entry => entry.Message.Contains("IceCandidate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Another_user_cannot_join_or_relay_to_the_room()
    {
        using var setup = await ConsultationScenario.StartVideoAsync(fixture);
        using var intruder = await ConsultationScenario.CreateUserAsync(fixture);
        await using var connection = CreateConnection(intruder.Token);
        await connection.StartAsync();

        var joinException = await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync("JoinRoom", setup.SessionId));
        var relayException = await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync(
                "RelayOffer",
                setup.SessionId,
                "synthetic-offer"));

        Assert.Contains("FORBIDDEN_RESOURCE", joinException.Message);
        Assert.Contains("FORBIDDEN_RESOURCE", relayException.Message);
    }

    [Fact]
    public async Task Relay_is_rejected_after_the_consultation_is_completed()
    {
        using var setup = await ConsultationScenario.StartVideoAsync(fixture);
        await using var connection = CreateConnection(setup.User.Token);
        await connection.StartAsync();
        await connection.InvokeAsync("JoinRoom", setup.SessionId);
        using var completeResponse = await setup.User.Client.PostAsJsonAsync(
            $"/api/v1/consultations/{setup.SessionId}/complete",
            new { idempotencyKey = $"rtc-complete-{Guid.NewGuid():N}" });
        completeResponse.EnsureSuccessStatusCode();

        var exception = await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync(
                "RelayOffer",
                setup.SessionId,
                "synthetic-offer"));

        Assert.Contains("INVALID_SESSION_STATE", exception.Message);
    }

    private HubConnection CreateConnection(string token) =>
        new HubConnectionBuilder()
            .WithUrl(
                "http://localhost/hubs/rtc",
                options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                    options.HttpMessageHandlerFactory = _ => fixture.CreateServerHandler();
                    options.Transports = HttpTransportType.LongPolling;
                })
            .Build();

    private static TaskCompletionSource<T> Completion<T>() => new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task<T> WaitAsync<T>(Task<T> task) =>
        await task.WaitAsync(TimeSpan.FromSeconds(5));

    private sealed record RtcDescriptionEnvelope(Guid SessionId, string Sdp);

    private sealed record RtcIceCandidateEnvelope(
        Guid SessionId,
        string Candidate,
        string? SdpMid,
        int? SdpMLineIndex);
}
