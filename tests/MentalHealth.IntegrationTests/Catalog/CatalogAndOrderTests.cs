using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MentalHealth.IntegrationTests.Auth;
using MentalHealth.Infrastructure.Identity;
using MentalHealth.Infrastructure.Persistence;
using MentalHealth.Infrastructure.Providers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealth.IntegrationTests.Catalog;

[Collection(AuthApiCollection.Name)]
public sealed class CatalogAndOrderTests(AuthApiFixture fixture)
{
    [Theory]
    [InlineData(0, "Confirmed")]
    [InlineData(19900, "AwaitingDemoPayment")]
    public async Task Order_state_depends_on_configured_demo_price(
        long priceInMinorUnits,
        string expectedStatus)
    {
        var planId = await CreatePlanAsync(priceInMinorUnits);

        using var user = await fixture.CreateTrustedApiClientForAsync(
            "user@demo.local");
        var orderResponse = await user.PostAsJsonAsync(
            "/api/v1/orders",
            new
            {
                planId,
                idempotencyKey = $"order-{priceInMinorUnits}-{Guid.NewGuid():N}"
            });

        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        using var order = await JsonDocument.ParseAsync(
            await orderResponse.Content.ReadAsStreamAsync());
        Assert.Equal(
            expectedStatus,
            order.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Demo_paid_order_confirms_only_after_explicit_action()
    {
        var planId = await CreatePlanAsync(19900);
        using var user = await fixture.CreateTrustedApiClientForAsync(
            "user@demo.local");
        var created = await user.PostAsJsonAsync(
            "/api/v1/orders",
            new
            {
                planId,
                idempotencyKey = $"paid-{Guid.NewGuid():N}"
            });
        using var createdBody = await JsonDocument.ParseAsync(
            await created.Content.ReadAsStreamAsync());
        var orderId = createdBody.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(
            "AwaitingDemoPayment",
            createdBody.RootElement.GetProperty("status").GetString());

        var gateway = fixture.Services.GetRequiredService<DemoPaymentGateway>();
        var confirmationsBefore = gateway.ConfirmationCount;

        var confirmed = await user.PostAsync(
            $"/api/v1/orders/{orderId}/confirm",
            content: null);
        var repeated = await user.PostAsync(
            $"/api/v1/orders/{orderId}/confirm",
            content: null);

        confirmed.EnsureSuccessStatusCode();
        repeated.EnsureSuccessStatusCode();
        using var confirmedBody = await JsonDocument.ParseAsync(
            await confirmed.Content.ReadAsStreamAsync());
        Assert.Equal(
            "Confirmed",
            confirmedBody.RootElement.GetProperty("status").GetString());
        Assert.StartsWith(
            "demo-",
            confirmedBody.RootElement.GetProperty("paymentReference").GetString());
        Assert.Equal(confirmationsBefore + 1, gateway.ConfirmationCount);

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
        var auditActions = await db.AuditEvents
            .Where(audit => audit.ResourceId == orderId)
            .Select(audit => audit.Action)
            .ToArrayAsync();
        Assert.Contains("OrderCreated", auditActions);
        Assert.Contains("DemoPaymentConfirmed", auditActions);
    }

    [Fact]
    public async Task Repeated_order_idempotency_key_returns_same_order()
    {
        var planId = await CreatePlanAsync(0);
        var key = $"repeat-{Guid.NewGuid():N}";
        using var user = await fixture.CreateTrustedApiClientForAsync(
            "user@demo.local");

        var first = await user.PostAsJsonAsync(
            "/api/v1/orders",
            new { planId, idempotencyKey = key });
        var second = await user.PostAsJsonAsync(
            "/api/v1/orders",
            new { planId, idempotencyKey = key });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        using var firstBody = await JsonDocument.ParseAsync(
            await first.Content.ReadAsStreamAsync());
        using var secondBody = await JsonDocument.ParseAsync(
            await second.Content.ReadAsStreamAsync());
        Assert.Equal(
            firstBody.RootElement.GetProperty("id").GetGuid(),
            secondBody.RootElement.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Ai_video_plan_is_rejected()
    {
        using var admin = await fixture.CreateTrustedApiClientForAsync(
            "admin@demo.local");

        var response = await admin.PostAsJsonAsync(
            "/api/v1/admin/catalog/plans",
            PlanRequest(
                $"AI 视频-{Guid.NewGuid():N}",
                "Ai",
                "Video",
                0));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var problem = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        Assert.Equal(
            "PLAN_COMBINATION_UNSUPPORTED",
            problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Overlapping_practitioner_slot_is_rejected_but_adjacent_slot_is_allowed()
    {
        using var admin = await fixture.CreateTrustedApiClientForAsync(
            "admin@demo.local");
        var practitioner = await admin.PostAsJsonAsync(
            "/api/v1/admin/catalog/practitioners",
            new
            {
                displayName = $"测试咨询师-{Guid.NewGuid():N}",
                role = "Counselor"
            });
        Assert.Equal(HttpStatusCode.Created, practitioner.StatusCode);
        using var practitionerBody = await JsonDocument.ParseAsync(
            await practitioner.Content.ReadAsStreamAsync());
        var practitionerId = practitionerBody.RootElement.GetProperty("id").GetGuid();
        var startAt = DateTimeOffset.UtcNow.AddDays(1);
        var endAt = startAt.AddMinutes(50);

        var first = await admin.PostAsJsonAsync(
            $"/api/v1/admin/catalog/practitioners/{practitionerId}/slots",
            new { startAt, endAt });
        using var firstBody = await JsonDocument.ParseAsync(
            await first.Content.ReadAsStreamAsync());
        var firstSlotId = firstBody.RootElement.GetProperty("id").GetGuid();
        var overlapping = await admin.PostAsJsonAsync(
            $"/api/v1/admin/catalog/practitioners/{practitionerId}/slots",
            new
            {
                startAt = startAt.AddMinutes(25),
                endAt = endAt.AddMinutes(25)
            });
        var adjacent = await admin.PostAsJsonAsync(
            $"/api/v1/admin/catalog/practitioners/{practitionerId}/slots",
            new
            {
                startAt = endAt,
                endAt = endAt.AddMinutes(50)
            });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, overlapping.StatusCode);
        using var problem = await JsonDocument.ParseAsync(
            await overlapping.Content.ReadAsStreamAsync());
        Assert.Equal(
            "AVAILABILITY_SLOT_CONFLICT",
            problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Created, adjacent.StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await admin.DeleteAsync(
                $"/api/v1/admin/catalog/practitioners/{practitionerId}/slots/{firstSlotId}"))
            .StatusCode);
    }

    [Fact]
    public async Task User_cannot_change_catalog()
    {
        using var user = await fixture.CreateTrustedApiClientForAsync(
            "user@demo.local");

        var response = await user.PostAsJsonAsync(
            "/api/v1/admin/catalog/plans",
            PlanRequest(
                $"越权套餐-{Guid.NewGuid():N}",
                "Human",
                "Chat",
                0));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var problem = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        Assert.Equal(
            "FORBIDDEN_RESOURCE",
            problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Catalog_lists_active_records_and_hides_deactivated_records()
    {
        using var admin = await fixture.CreateTrustedApiClientForAsync(
            "admin@demo.local");
        var planId = await CreatePlanAsync(0, admin);
        var practitioner = await admin.PostAsJsonAsync(
            "/api/v1/admin/catalog/practitioners",
            new
            {
                displayName = $"目录咨询师-{Guid.NewGuid():N}",
                role = "Counselor"
            });
        using var practitionerBody = await JsonDocument.ParseAsync(
            await practitioner.Content.ReadAsStreamAsync());
        var practitionerId = practitionerBody.RootElement.GetProperty("id").GetGuid();

        using var user = await fixture.CreateTrustedApiClientForAsync(
            "user@demo.local");
        Assert.True(await CatalogContainsAsync(user, "plans", planId));
        Assert.True(await CatalogContainsAsync(
            user,
            "practitioners",
            practitionerId));

        var renamed = await admin.PutAsJsonAsync(
            $"/api/v1/admin/catalog/plans/{planId}",
            PlanRequest(
                $"已更新-{Guid.NewGuid():N}",
                "Human",
                "Chat",
                0));
        renamed.EnsureSuccessStatusCode();
        var practitionerUpdated = await admin.PutAsJsonAsync(
            $"/api/v1/admin/catalog/practitioners/{practitionerId}",
            new
            {
                displayName = $"已更新咨询师-{Guid.NewGuid():N}",
                role = "Doctor"
            });
        practitionerUpdated.EnsureSuccessStatusCode();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await admin.DeleteAsync(
                $"/api/v1/admin/catalog/practitioners/{practitionerId}"))
            .StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await admin.DeleteAsync($"/api/v1/admin/catalog/plans/{planId}"))
            .StatusCode);

        Assert.False(await CatalogContainsAsync(user, "plans", planId));
        Assert.False(await CatalogContainsAsync(
            user,
            "practitioners",
            practitionerId));

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
        var planActions = await db.AuditEvents
            .Where(audit => audit.ResourceId == planId)
            .Select(audit => audit.Action)
            .ToArrayAsync();
        var practitionerActions = await db.AuditEvents
            .Where(audit => audit.ResourceId == practitionerId)
            .Select(audit => audit.Action)
            .ToArrayAsync();
        Assert.Contains("ServicePlanCreated", planActions);
        Assert.Contains("ServicePlanUpdated", planActions);
        Assert.Contains("ServicePlanDeactivated", planActions);
        Assert.Contains("PractitionerCreated", practitionerActions);
        Assert.Contains("PractitionerUpdated", practitionerActions);
        Assert.Contains("PractitionerDeactivated", practitionerActions);
    }

    [Fact]
    public async Task Seeded_staff_account_points_to_same_catalog_practitioner()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var counselor = await users.FindByEmailAsync("counselor@demo.local");
        var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();

        Assert.NotNull(counselor?.PractitionerId);
        Assert.True(await db.Practitioners.AnyAsync(
            practitioner => practitioner.Id == counselor.PractitionerId
                && practitioner.Active));
    }

    [Fact]
    public async Task Seeded_catalog_has_four_supported_demo_plans()
    {
        using var user = await fixture.CreateTrustedApiClientForAsync(
            "user@demo.local");

        var response = await user.GetAsync("/api/v1/catalog/plans");
        response.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        var seededIds = new HashSet<Guid>
        {
            DemoCatalogSeeder.HumanChatFreePlanId,
            DemoCatalogSeeder.HumanVideoPaidPlanId,
            DemoCatalogSeeder.AiChatFreePlanId,
            DemoCatalogSeeder.AiChatPaidPlanId
        };
        var seededPlans = body.RootElement.EnumerateArray()
            .Where(plan => seededIds.Contains(plan.GetProperty("id").GetGuid()))
            .ToArray();

        Assert.Equal(4, seededPlans.Length);
        Assert.Equal(
            2,
            seededPlans.Count(
                plan => plan.GetProperty("kind").GetString() == "Ai"));
        Assert.DoesNotContain(
            seededPlans,
            plan => plan.GetProperty("kind").GetString() == "Ai"
                && plan.GetProperty("channel").GetString() == "Video");
    }

    [Fact]
    public async Task Seeded_staff_role_cannot_diverge_from_login_role()
    {
        using var admin = await fixture.CreateTrustedApiClientForAsync(
            "admin@demo.local");

        var response = await admin.PutAsJsonAsync(
            $"/api/v1/admin/catalog/practitioners/{IdentitySeeder.DemoCounselorId}",
            new
            {
                displayName = "演示咨询师",
                role = "Doctor"
            });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        Assert.Equal(
            "PRACTITIONER_ROLE_LOCKED",
            body.RootElement.GetProperty("code").GetString());
    }

    private async Task<Guid> CreatePlanAsync(
        long priceInMinorUnits,
        HttpClient? existingAdmin = null)
    {
        var ownsClient = existingAdmin is null;
        var admin = existingAdmin ?? await fixture.CreateTrustedApiClientForAsync(
            "admin@demo.local");
        try
        {
            var planResponse = await admin.PostAsJsonAsync(
                "/api/v1/admin/catalog/plans",
                PlanRequest(
                    $"测试套餐-{priceInMinorUnits}-{Guid.NewGuid():N}",
                    "Human",
                    "Chat",
                    priceInMinorUnits));
            Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);
            using var plan = await JsonDocument.ParseAsync(
                await planResponse.Content.ReadAsStreamAsync());
            return plan.RootElement.GetProperty("id").GetGuid();
        }
        finally
        {
            if (ownsClient)
            {
                admin.Dispose();
            }
        }
    }

    private static object PlanRequest(
        string name,
        string kind,
        string channel,
        long priceInMinorUnits) => new
        {
            name,
            kind,
            channel,
            paymentMode = priceInMinorUnits == 0 ? "Free" : "DemoPaid",
            priceInMinorUnits,
            currency = "CNY",
            durationMinutes = 50
        };

    private static async Task<bool> CatalogContainsAsync(
        HttpClient client,
        string resource,
        Guid id)
    {
        var response = await client.GetAsync($"/api/v1/catalog/{resource}");
        response.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        return body.RootElement.EnumerateArray()
            .Any(item => item.GetProperty("id").GetGuid() == id);
    }
}
