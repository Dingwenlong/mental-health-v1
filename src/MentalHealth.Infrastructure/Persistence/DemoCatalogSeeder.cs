using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Domain.Consultations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MentalHealth.Infrastructure.Persistence;

public sealed class DemoCatalogSeeder(
    MentalHealthDbContext db,
    IConfiguration configuration,
    IClock clock)
{
    public static readonly Guid HumanChatFreePlanId =
        Guid.Parse("30000000-0000-0000-0000-000000000001");

    public static readonly Guid HumanVideoPaidPlanId =
        Guid.Parse("30000000-0000-0000-0000-000000000002");

    public static readonly Guid AiChatFreePlanId =
        Guid.Parse("30000000-0000-0000-0000-000000000003");

    public static readonly Guid AiChatPaidPlanId =
        Guid.Parse("30000000-0000-0000-0000-000000000004");

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>("CatalogSeed:Enabled"))
        {
            return;
        }

        var plans = new[]
        {
            ServicePlan.Create(
                HumanChatFreePlanId,
                "真人文字咨询（免费）",
                ConsultationKind.Human,
                ConsultationChannel.Chat,
                PlanPaymentMode.Free,
                0,
                "CNY",
                50,
                clock.UtcNow),
            ServicePlan.Create(
                HumanVideoPaidPlanId,
                "真人视频咨询（模拟收费）",
                ConsultationKind.Human,
                ConsultationChannel.Video,
                PlanPaymentMode.DemoPaid,
                29900,
                "CNY",
                50,
                clock.UtcNow),
            ServicePlan.Create(
                AiChatFreePlanId,
                "AI 文字咨询（免费）",
                ConsultationKind.AiVirtual,
                ConsultationChannel.Chat,
                PlanPaymentMode.Free,
                0,
                "CNY",
                30,
                clock.UtcNow),
            ServicePlan.Create(
                AiChatPaidPlanId,
                "AI 文字咨询（模拟收费）",
                ConsultationKind.AiVirtual,
                ConsultationChannel.Chat,
                PlanPaymentMode.DemoPaid,
                9900,
                "CNY",
                30,
                clock.UtcNow)
        };

        var planIds = plans.Select(plan => plan.Id).ToArray();
        var existingPlans = await db.ServicePlans
            .Where(plan => planIds.Contains(plan.Id))
            .ToDictionaryAsync(plan => plan.Id, cancellationToken);
        foreach (var plan in plans)
        {
            if (!existingPlans.TryGetValue(plan.Id, out var existing))
            {
                db.ServicePlans.Add(plan);
                continue;
            }

            var legacyName = plan.Id switch
            {
                var id when id == HumanChatFreePlanId => "真人文字咨询（免费演示）",
                var id when id == AiChatFreePlanId => "AI 文字咨询（免费演示）",
                _ => null
            };
            if (!string.Equals(existing.Name, legacyName, StringComparison.Ordinal))
            {
                continue;
            }

            existing.Update(
                plan.Name,
                existing.Kind,
                existing.Channel,
                existing.PaymentMode,
                existing.PriceInMinorUnits,
                existing.Currency,
                existing.DurationMinutes,
                clock.UtcNow);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
