using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MentalHealth.Application.Security;
using MentalHealth.IntegrationTests.Auth;
using MentalHealth.Infrastructure.Identity;
using MentalHealth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealth.IntegrationTests.Consultations;

[Collection(AuthApiCollection.Name)]
public sealed class ConsultationLifecycleTests(AuthApiFixture fixture)
{
    [Fact]
    public async Task Completed_chat_persists_one_message_and_enqueues_analysis_once()
    {
        using var user = await CreateUserClientAsync();
        await GrantAsync(user, "Service");
        await GrantAsync(user, "AiAnalysis");
        var orderId = await CreateOrderAsync(
            user,
            DemoCatalogSeeder.HumanChatFreePlanId);
        var createKey = $"consultation-{Guid.NewGuid():N}";

        using var created = await CreateConsultationAsync(
            user,
            orderId,
            IdentitySeeder.DemoCounselorId,
            createKey);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var sessionId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        using var repeatedCreate = await CreateConsultationAsync(
            user,
            orderId,
            IdentitySeeder.DemoCounselorId,
            createKey);
        Assert.Equal(HttpStatusCode.OK, repeatedCreate.StatusCode);
        Assert.Equal(
            sessionId,
            (await ReadJsonAsync(repeatedCreate)).GetProperty("id").GetGuid());

        using var started = await user.PostAsJsonAsync(
            $"/api/v1/consultations/{sessionId}/start",
            new { });
        started.EnsureSuccessStatusCode();
        Assert.Equal(
            "InProgress",
            (await ReadJsonAsync(started)).GetProperty("status").GetString());

        var messageText = $"合成测试消息-{Guid.NewGuid():N}";
        fixture.ClearCapturedLogs();
        using var firstMessage = await user.PostAsJsonAsync(
            $"/api/v1/consultations/{sessionId}/messages",
            new { text = messageText, clientMessageId = "msg-001" });
        Assert.Equal(HttpStatusCode.Created, firstMessage.StatusCode);
        var firstMessageBody = await ReadJsonAsync(firstMessage);

        using var repeatedMessage = await user.PostAsJsonAsync(
            $"/api/v1/consultations/{sessionId}/messages",
            new { text = messageText, clientMessageId = "msg-001" });
        Assert.Equal(HttpStatusCode.OK, repeatedMessage.StatusCode);
        Assert.Equal(
            firstMessageBody.GetProperty("id").GetGuid(),
            (await ReadJsonAsync(repeatedMessage)).GetProperty("id").GetGuid());

        using var listed = await user.GetAsync(
            $"/api/v1/consultations/{sessionId}/messages");
        listed.EnsureSuccessStatusCode();
        var messages = await ReadJsonAsync(listed);
        var persistedMessage = Assert.Single(messages.EnumerateArray());
        Assert.Equal(1, persistedMessage.GetProperty("sequence").GetInt32());
        Assert.Equal(messageText, persistedMessage.GetProperty("text").GetString());

        using var counselor = await fixture.CreateTrustedApiClientForAsync(
            "counselor@demo.local");
        using var counselorView = await counselor.GetAsync(
            $"/api/v1/consultations/{sessionId}/messages");
        counselorView.EnsureSuccessStatusCode();

        using var admin = await fixture.CreateTrustedApiClientForAsync(
            "123@qq.com");
        using var adminView = await admin.GetAsync(
            $"/api/v1/consultations/{sessionId}/messages");
        Assert.Equal(HttpStatusCode.Forbidden, adminView.StatusCode);

        var completeKey = $"complete-{Guid.NewGuid():N}";
        using var completed = await user.PostAsJsonAsync(
            $"/api/v1/consultations/{sessionId}/complete",
            new { idempotencyKey = completeKey });
        completed.EnsureSuccessStatusCode();
        using var repeatedComplete = await user.PostAsJsonAsync(
            $"/api/v1/consultations/{sessionId}/complete",
            new { idempotencyKey = completeKey });
        repeatedComplete.EnsureSuccessStatusCode();

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
        var messageCount = await db.Database
            .SqlQueryRaw<int>(
                "SELECT count(*)::int AS \"Value\" FROM messages WHERE session_id = {0}",
                sessionId)
            .SingleAsync();
        var completionCount = await db.Database
            .SqlQueryRaw<int>(
                "SELECT count(*)::int AS \"Value\" FROM outbox_messages WHERE aggregate_id = {0} AND type = 'ConsultationCompleted'",
                sessionId)
            .SingleAsync();
        Assert.Equal(1, messageCount);
        Assert.Equal(1, completionCount);

        var messageLogs = fixture.CapturedLogs
            .Where(entry => entry.Category.EndsWith(
                "MessagesController",
                StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(messageLogs);
        Assert.DoesNotContain(
            messageLogs,
            entry => entry.Message.Contains(messageText, StringComparison.Ordinal));
        Assert.Contains(
            messageLogs,
            entry => entry.Message.Contains("Length", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Missing_analysis_consent_rejects_consultation_creation()
    {
        using var user = await CreateUserClientAsync();
        await GrantAsync(user, "Service");
        var orderId = await CreateOrderAsync(
            user,
            DemoCatalogSeeder.HumanChatFreePlanId);

        using var response = await CreateConsultationAsync(
            user,
            orderId,
            IdentitySeeder.DemoCounselorId,
            $"consultation-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(
            "CONSENT_REQUIRED",
            (await ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Unconfirmed_demo_paid_order_rejects_consultation_creation()
    {
        using var user = await CreateUserClientAsync();
        await GrantAsync(user, "Service");
        await GrantAsync(user, "AiAnalysis");
        var orderId = await CreateOrderAsync(
            user,
            DemoCatalogSeeder.AiChatPaidPlanId);

        using var response = await CreateConsultationAsync(
            user,
            orderId,
            null,
            $"consultation-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(
            "ORDER_NOT_CONFIRMED",
            (await ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Message_before_start_is_rejected_without_persisting_text()
    {
        using var user = await CreateUserClientAsync();
        await GrantAsync(user, "Service");
        await GrantAsync(user, "AiAnalysis");
        var orderId = await CreateOrderAsync(
            user,
            DemoCatalogSeeder.HumanChatFreePlanId);
        using var created = await CreateConsultationAsync(
            user,
            orderId,
            IdentitySeeder.DemoCounselorId,
            $"consultation-{Guid.NewGuid():N}");
        created.EnsureSuccessStatusCode();
        var sessionId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        using var response = await user.PostAsJsonAsync(
            $"/api/v1/consultations/{sessionId}/messages",
            new { text = "不应保存的合成消息", clientMessageId = "too-early" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(
            "INVALID_SESSION_STATE",
            (await ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Another_user_cannot_read_session_messages()
    {
        using var owner = await CreateUserClientAsync();
        await GrantAsync(owner, "Service");
        await GrantAsync(owner, "AiAnalysis");
        var orderId = await CreateOrderAsync(
            owner,
            DemoCatalogSeeder.HumanChatFreePlanId);
        using var created = await CreateConsultationAsync(
            owner,
            orderId,
            IdentitySeeder.DemoCounselorId,
            $"consultation-{Guid.NewGuid():N}");
        created.EnsureSuccessStatusCode();
        var sessionId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();
        using var otherUser = await CreateUserClientAsync();

        using var response = await otherUser.GetAsync(
            $"/api/v1/consultations/{sessionId}/messages");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<HttpClient> CreateUserClientAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"task8-{suffix}@example.test";
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            PhoneNumber = "+8613900000003",
            SubjectId = Guid.NewGuid()
        };
        EnsureSucceeded(await userManager.CreateAsync(
            user,
            $"Synthetic-task8-password-{suffix}!"));
        EnsureSucceeded(await userManager.AddToRoleAsync(user, AppRoles.User));

        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var token = tokenService.Issue(
            new JwtTokenSubject(
                user.Id,
                user.PhoneNumber!,
                [AppRoles.User],
                user.SubjectId,
                null));
        return fixture.CreateClientWithBearer(token.Value);
    }

    private static async Task GrantAsync(HttpClient user, string kind)
    {
        using var response = await user.PostAsJsonAsync(
            "/api/v1/consents",
            new { kind, textVersion = "task8-v1" });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<Guid> CreateOrderAsync(
        HttpClient user,
        Guid planId)
    {
        using var response = await user.PostAsJsonAsync(
            "/api/v1/orders",
            new
            {
                planId,
                idempotencyKey = $"task8-order-{Guid.NewGuid():N}"
            });
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> CreateConsultationAsync(
        HttpClient user,
        Guid orderId,
        Guid? assignedPractitionerId,
        string idempotencyKey) => user.PostAsJsonAsync(
            "/api/v1/consultations",
            new
            {
                orderId,
                assignedPractitionerId,
                scheduledAt = DateTimeOffset.UtcNow.AddMinutes(5),
                idempotencyKey
            });

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
}
