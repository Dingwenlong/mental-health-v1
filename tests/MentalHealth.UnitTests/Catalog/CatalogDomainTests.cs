using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.Shared;

namespace MentalHealth.UnitTests.Catalog;

public sealed class CatalogDomainTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-08-22T04:00:00+00:00");

    [Fact]
    public void Ai_video_plan_is_not_supported()
    {
        var exception = Assert.Throws<DomainException>(() => ServicePlan.Create(
            "AI 视频",
            ConsultationKind.AiVirtual,
            ConsultationChannel.Video,
            PlanPaymentMode.Free,
            0,
            "CNY",
            50,
            Now));

        Assert.Equal("PLAN_COMBINATION_UNSUPPORTED", exception.Code);
    }

    [Theory]
    [InlineData(PlanPaymentMode.Free, 1)]
    [InlineData(PlanPaymentMode.DemoPaid, 0)]
    [InlineData(PlanPaymentMode.DemoPaid, -1)]
    public void Payment_mode_and_price_must_match(
        PlanPaymentMode paymentMode,
        long priceInMinorUnits)
    {
        var exception = Assert.Throws<DomainException>(() => ServicePlan.Create(
            "真人文字咨询",
            ConsultationKind.Human,
            ConsultationChannel.Chat,
            paymentMode,
            priceInMinorUnits,
            "CNY",
            50,
            Now));

        Assert.Equal("PLAN_PRICE_INVALID", exception.Code);
    }

    [Fact]
    public void Availability_uses_half_open_interval()
    {
        var slot = AvailabilitySlot.Create(
            Guid.NewGuid(),
            Now,
            Now.AddMinutes(50),
            Now.AddHours(-1));

        Assert.True(slot.Overlaps(Now.AddMinutes(25), Now.AddMinutes(75)));
        Assert.False(slot.Overlaps(Now.AddMinutes(50), Now.AddMinutes(100)));
        Assert.False(slot.Overlaps(Now.AddMinutes(-50), Now));
    }

    [Fact]
    public void Availability_normalizes_local_offset_to_utc()
    {
        var localStart = DateTimeOffset.Parse("2026-08-23T12:00:00+08:00");

        var slot = AvailabilitySlot.Create(
            Guid.NewGuid(),
            localStart,
            localStart.AddMinutes(50),
            localStart.AddHours(-1));

        Assert.Equal(TimeSpan.Zero, slot.StartAt.Offset);
        Assert.Equal(
            DateTimeOffset.Parse("2026-08-23T04:00:00+00:00"),
            slot.StartAt);
    }

    [Fact]
    public void Free_order_is_confirmed_when_created()
    {
        var plan = CreatePlan(PlanPaymentMode.Free, 0);

        var order = DemoOrder.Create(
            Guid.NewGuid(),
            plan,
            "free-order",
            Now);

        Assert.Equal(DemoOrderStatus.Confirmed, order.Status);
        Assert.Equal(Now, order.ConfirmedAt);
        Assert.Null(order.PaymentReference);
    }

    [Fact]
    public void Demo_paid_order_waits_then_accepts_confirmation()
    {
        var plan = CreatePlan(PlanPaymentMode.DemoPaid, 19900);
        var order = DemoOrder.Create(
            Guid.NewGuid(),
            plan,
            "paid-order",
            Now);

        Assert.Equal(DemoOrderStatus.AwaitingDemoPayment, order.Status);

        order.Confirm("demo-reference", Now.AddMinutes(1));

        Assert.Equal(DemoOrderStatus.Confirmed, order.Status);
        Assert.Equal("demo-reference", order.PaymentReference);
        Assert.Equal(Now.AddMinutes(1), order.ConfirmedAt);
    }

    private static ServicePlan CreatePlan(
        PlanPaymentMode paymentMode,
        long priceInMinorUnits) =>
        ServicePlan.Create(
            "真人文字咨询",
            ConsultationKind.Human,
            ConsultationChannel.Chat,
            paymentMode,
            priceInMinorUnits,
            "CNY",
            50,
            Now);
}
