using MentalHealth.Domain.Consultations;

namespace MentalHealth.Application.Catalog;

public sealed record ServicePlanDto(
    Guid Id,
    string Name,
    string Kind,
    string Channel,
    string PaymentMode,
    long PriceInMinorUnits,
    string Currency,
    int DurationMinutes,
    bool Active)
{
    public static ServicePlanDto From(ServicePlan plan) => new(
        plan.Id,
        plan.Name,
        plan.Kind.ToString(),
        plan.Channel.ToString(),
        plan.PaymentMode.ToString(),
        plan.PriceInMinorUnits,
        plan.Currency,
        plan.DurationMinutes,
        plan.Active);
}

public sealed record AvailabilitySlotDto(
    Guid Id,
    Guid PractitionerId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    bool Active)
{
    public static AvailabilitySlotDto From(AvailabilitySlot slot) => new(
        slot.Id,
        slot.PractitionerId,
        slot.StartAt,
        slot.EndAt,
        slot.Active);
}

public sealed record PractitionerDto(
    Guid Id,
    string DisplayName,
    string Role,
    bool Active,
    IReadOnlyList<AvailabilitySlotDto> AvailabilitySlots);

public sealed record DemoOrderDto(
    Guid Id,
    Guid PlanId,
    long AmountInMinorUnits,
    string Currency,
    string Status,
    string? PaymentReference,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt)
{
    public static DemoOrderDto From(DemoOrder order) => new(
        order.Id,
        order.PlanId,
        order.AmountInMinorUnits,
        order.Currency,
        order.Status.ToString(),
        order.PaymentReference,
        order.CreatedAt,
        order.ConfirmedAt);
}
