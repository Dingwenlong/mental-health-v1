using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.Consultations;

public sealed class DemoOrder
{
    private DemoOrder()
    {
    }

    private DemoOrder(
        Guid subjectId,
        ServicePlan plan,
        string idempotencyKey,
        DateTimeOffset createdAt)
    {
        if (subjectId == Guid.Empty)
        {
            throw new DomainException("ORDER_SUBJECT_REQUIRED");
        }

        if (!plan.Active)
        {
            throw new DomainException("PLAN_NOT_AVAILABLE");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey)
            || idempotencyKey.Trim().Length > 100)
        {
            throw new DomainException("IDEMPOTENCY_KEY_INVALID");
        }

        Id = Guid.NewGuid();
        SubjectId = subjectId;
        PlanId = plan.Id;
        AmountInMinorUnits = plan.PriceInMinorUnits;
        Currency = plan.Currency;
        IdempotencyKey = idempotencyKey.Trim();
        CreatedAt = createdAt;
        Status = plan.PaymentMode == PlanPaymentMode.Free
            ? DemoOrderStatus.Confirmed
            : DemoOrderStatus.AwaitingDemoPayment;
        ConfirmedAt = Status == DemoOrderStatus.Confirmed ? createdAt : null;
    }

    public Guid Id { get; private set; }

    public Guid SubjectId { get; private set; }

    public Guid PlanId { get; private set; }

    public long AmountInMinorUnits { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public string IdempotencyKey { get; private set; } = string.Empty;

    public DemoOrderStatus Status { get; private set; }

    public string? PaymentReference { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public static DemoOrder Create(
        Guid subjectId,
        ServicePlan plan,
        string idempotencyKey,
        DateTimeOffset createdAt) =>
        new(subjectId, plan, idempotencyKey, createdAt);

    public void Confirm(string paymentReference, DateTimeOffset confirmedAt)
    {
        if (Status == DemoOrderStatus.Confirmed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(paymentReference)
            || paymentReference.Trim().Length > 128
            || confirmedAt < CreatedAt)
        {
            throw new DomainException("PAYMENT_CONFIRMATION_INVALID");
        }

        Status = DemoOrderStatus.Confirmed;
        PaymentReference = paymentReference.Trim();
        ConfirmedAt = confirmedAt;
    }
}
