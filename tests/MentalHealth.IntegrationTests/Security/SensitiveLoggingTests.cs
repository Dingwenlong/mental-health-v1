using System.Net.Http.Json;
using MentalHealth.IntegrationTests.Auth;
using MentalHealth.IntegrationTests.Support;

namespace MentalHealth.IntegrationTests.Security;

[Collection(AuthApiCollection.Name)]
public sealed class SensitiveLoggingTests(AuthApiFixture fixture)
{
    [Fact]
    public async Task Request_logs_exclude_message_text_and_media_ticket()
    {
        using var setup = await ConsultationScenario.StartVideoAsync(fixture);
        var messageMarker = $"private-message-{Guid.NewGuid():N}";
        var ticketMarker = $"private-ticket-{Guid.NewGuid():N}";
        fixture.ClearCapturedLogs();

        using var messageResponse = await setup.User.Client.PostAsJsonAsync(
            $"/api/v1/consultations/{setup.SessionId}/messages",
            new
            {
                text = messageMarker,
                clientMessageId = $"logging-{Guid.NewGuid():N}"
            });
        messageResponse.EnsureSuccessStatusCode();
        using var mediaResponse = await setup.User.Client.GetAsync(
            $"/api/v1/media/{Guid.NewGuid()}/content?ticket={ticketMarker}");

        Assert.DoesNotContain(
            fixture.CapturedLogs,
            entry => entry.Message.Contains(messageMarker, StringComparison.Ordinal)
                || entry.Message.Contains(ticketMarker, StringComparison.Ordinal));
    }
}
