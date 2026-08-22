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
                "真人文字咨询（免费演示）",
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
                "AI 文字咨询（免费演示）",
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
        var existingIds = await db.ServicePlans
            .Where(plan => planIds.Contains(plan.Id))
            .Select(plan => plan.Id)
            .ToArrayAsync(cancellationToken);
        foreach (var plan in plans.Where(plan => !existingIds.Contains(plan.Id)))
        {
            db.ServicePlans.Add(plan);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
