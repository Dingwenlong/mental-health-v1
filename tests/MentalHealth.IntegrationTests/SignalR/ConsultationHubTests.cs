using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MentalHealth.Application.Consultations;
using MentalHealth.Application.Security;
using MentalHealth.Infrastructure.Identity;
using MentalHealth.Infrastructure.Persistence;
using MentalHealth.IntegrationTests.Auth;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealth.IntegrationTests.SignalR;

[Collection(AuthApiCollection.Name)]
public sealed class ConsultationHubTests(AuthApiFixture fixture)
{
    [Fact]
    public async Task Assigned_user_and_counselor_receive_one_persisted_message()
    {
        var setup = await CreateStartedSessionAsync();
        var counselorToken = await fixture.IssueTrustedApiTokenForAsync(
            "counselor@demo.local");
        await using var user = CreateConnection(setup.UserToken);
        await using var counselor = CreateConnection(counselorToken);
        var received = new TaskCompletionSource<ChatMessageDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var counselorOnline = new TaskCompletionSource<PresenceEnvelope>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var counselorOffline = new TaskCompletionSource<PresenceEnvelope>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var messageCount = 0;
        counselor.On<ChatMessageDto>("MessageReceived", message =>
        {
            Interlocked.Increment(ref messageCount);
            received.TrySetResult(message);
        });
        user.On<PresenceEnvelope>("PresenceChanged", presence =>
        {
            if (presence.Kind == "Practitioner" && presence.Online)
            {
                counselorOnline.TrySetResult(presence);
            }
            else if (presence.Kind == "Practitioner" && !presence.Online)
            {
                counselorOffline.TrySetResult(presence);
            }
        });

        await user.StartAsync();
        await counselor.StartAsync();
        await user.InvokeAsync("JoinSession", setup.SessionId);
        await counselor.InvokeAsync("JoinSession", setup.SessionId);
        Assert.True((await counselorOnline.Task.WaitAsync(
            TimeSpan.FromSeconds(5))).Online);

        var text = $"合成实时消息-{Guid.NewGuid():N}";
        var first = await user.InvokeAsync<ChatMessageDto>(
            "SendMessage",
            setup.SessionId,
            text,
            "client-001");
        var repeated = await user.InvokeAsync<ChatMessageDto>(
            "SendMessage",
            setup.SessionId,
            text,
            "client-001");
        var eventMessage = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(first.Id, repeated.Id);
        Assert.Equal("client-001", eventMessage.ClientMessageId);
        Assert.Equal(1, eventMessage.Sequence);
        await Task.Delay(250);
        Assert.Equal(1, Volatile.Read(ref messageCount));

        await counselor.StopAsync();
        Assert.False((await counselorOffline.Task.WaitAsync(
            TimeSpan.FromSeconds(5))).Online);

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
        Assert.Equal(
            1,
            await db.Messages.CountAsync(message =>
                message.SessionId == setup.SessionId));
    }

    [Fact]
    public async Task Another_user_cannot_join_the_consultation_group()
    {
        var setup = await CreateStartedSessionAsync();
        var intruder = await CreateUserAsync();
        await using var connection = CreateConnection(intruder.Token);
        await connection.StartAsync();

        var exception = await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync("JoinSession", setup.SessionId));

        Assert.Contains("FORBIDDEN_RESOURCE", exception.Message);
    }

    [Fact]
    public async Task Message_history_returns_only_sequences_after_the_cursor()
    {
        var setup = await CreateStartedSessionAsync();
        await using var connection = CreateConnection(setup.UserToken);
        await connection.StartAsync();
        await connection.InvokeAsync("JoinSession", setup.SessionId);
        await connection.InvokeAsync<ChatMessageDto>(
            "SendMessage",
            setup.SessionId,
            "第一条合成消息",
            "history-001");
        await connection.InvokeAsync<ChatMessageDto>(
            "SendMessage",
            setup.SessionId,
            "第二条合成消息",
            "history-002");

        using var response = await setup.UserClient.GetAsync(
            $"/api/v1/consultations/{setup.SessionId}/messages?afterSequence=1");

        response.EnsureSuccessStatusCode();
        var body = await ReadJsonAsync(response);
        var message = Assert.Single(body.EnumerateArray());
        Assert.Equal(2, message.GetProperty("sequence").GetInt32());
    }

    private HubConnection CreateConnection(string token) =>
        new HubConnectionBuilder()
            .WithUrl(
                "http://localhost/hubs/chat",
                options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                    options.HttpMessageHandlerFactory = _ => fixture.CreateServerHandler();
                    options.Transports = HttpTransportType.LongPolling;
                })
            .Build();

    private async Task<SessionSetup> CreateStartedSessionAsync()
    {
        var user = await CreateUserAsync();
        await GrantAsync(user.Client, "Service");
        await GrantAsync(user.Client, "AiAnalysis");
        using var orderResponse = await user.Client.PostAsJsonAsync(
            "/api/v1/orders",
            new
            {
                planId = DemoCatalogSeeder.HumanChatFreePlanId,
                idempotencyKey = $"task9-order-{Guid.NewGuid():N}"
            });
        orderResponse.EnsureSuccessStatusCode();
        var orderId = (await ReadJsonAsync(orderResponse)).GetProperty("id").GetGuid();
        using var createResponse = await user.Client.PostAsJsonAsync(
            "/api/v1/consultations",
            new
            {
                orderId,
                assignedPractitionerId = IdentitySeeder.DemoCounselorId,
                scheduledAt = DateTimeOffset.UtcNow.AddMinutes(5),
                idempotencyKey = $"task9-session-{Guid.NewGuid():N}"
            });
        createResponse.EnsureSuccessStatusCode();
        var sessionId = (await ReadJsonAsync(createResponse)).GetProperty("id").GetGuid();
        using var startResponse = await user.Client.PostAsJsonAsync(
            $"/api/v1/consultations/{sessionId}/start",
            new { });
        startResponse.EnsureSuccessStatusCode();
        return new SessionSetup(sessionId, user.Token, user.Client);
    }

    private async Task<TestUser> CreateUserAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"task9-{suffix}@example.test";
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            SubjectId = Guid.NewGuid()
        };
        EnsureSucceeded(await userManager.CreateAsync(
            user,
            $"Synthetic-task9-password-{suffix}!"));
        EnsureSucceeded(await userManager.AddToRoleAsync(user, AppRoles.User));
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var token = tokenService.Issue(
            new JwtTokenSubject(
                user.Id,
                email,
                [AppRoles.User],
                user.SubjectId,
                null)).Value;
        return new TestUser(token, fixture.CreateClientWithBearer(token));
    }

    private static async Task GrantAsync(HttpClient client, string kind)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/consents",
            new { kind, textVersion = "task9-v1" });
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created);
    }

    private static async Task<JsonElement> ReadJsonAsync(
        HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(
                "; ",
                result.Errors.Select(error => error.Description)));
        }
    }

    private sealed record SessionSetup(
        Guid SessionId,
        string UserToken,
        HttpClient UserClient);

    private sealed record TestUser(string Token, HttpClient Client);

    private sealed record PresenceEnvelope(Guid UserId, string Kind, bool Online);
}
