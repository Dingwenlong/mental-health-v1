using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.Consultations;

public sealed class ServicePlan
{
    private ServicePlan()
    {
    }

    private ServicePlan(
        Guid id,
        string name,
        ConsultationKind kind,
        ConsultationChannel channel,
        PlanPaymentMode paymentMode,
        long priceInMinorUnits,
        string currency,
        int durationMinutes,
        DateTimeOffset now)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("PLAN_VALUE_INVALID");
        }

        Id = id;
        Apply(
            name,
            kind,
            channel,
            paymentMode,
            priceInMinorUnits,
            currency,
            durationMinutes);
        Active = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public ConsultationKind Kind { get; private set; }

    public ConsultationChannel Channel { get; private set; }

    public PlanPaymentMode PaymentMode { get; private set; }

    public long PriceInMinorUnits { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int DurationMinutes { get; private set; }

    public bool Active { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static ServicePlan Create(
        string name,
        ConsultationKind kind,
        ConsultationChannel channel,
        PlanPaymentMode paymentMode,
        long priceInMinorUnits,
        string currency,
        int durationMinutes,
        DateTimeOffset now) =>
        new(
            Guid.NewGuid(),
            name,
            kind,
            channel,
            paymentMode,
            priceInMinorUnits,
            currency,
            durationMinutes,
            now);

    public static ServicePlan Create(
        Guid id,
        string name,
        ConsultationKind kind,
        ConsultationChannel channel,
        PlanPaymentMode paymentMode,
        long priceInMinorUnits,
        string currency,
        int durationMinutes,
        DateTimeOffset now) =>
        new(
            id,
            name,
            kind,
            channel,
            paymentMode,
            priceInMinorUnits,
            currency,
            durationMinutes,
            now);

    public void Update(
        string name,
        ConsultationKind kind,
        ConsultationChannel channel,
        PlanPaymentMode paymentMode,
        long priceInMinorUnits,
        string currency,
        int durationMinutes,
        DateTimeOffset now)
    {
        EnsureActive();
        Apply(
            name,
            kind,
            channel,
            paymentMode,
            priceInMinorUnits,
            currency,
            durationMinutes);
        UpdatedAt = now;
    }

    public void Deactivate(DateTimeOffset now)
    {
        EnsureActive();
        Active = false;
        UpdatedAt = now;
    }

    private void Apply(
        string name,
        ConsultationKind kind,
        ConsultationChannel channel,
        PlanPaymentMode paymentMode,
        long priceInMinorUnits,
        string currency,
        int durationMinutes)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
        {
            throw new DomainException("PLAN_NAME_INVALID");
        }

        if (!Enum.IsDefined(kind)
            || !Enum.IsDefined(channel)
            || !Enum.IsDefined(paymentMode))
        {
            throw new DomainException("PLAN_VALUE_INVALID");
        }

        if (kind == ConsultationKind.AiVirtual
            && channel == ConsultationChannel.Video)
        {
            throw new DomainException("PLAN_COMBINATION_UNSUPPORTED");
        }

        if (!string.Equals(currency, "CNY", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("PLAN_CURRENCY_UNSUPPORTED");
        }

        if ((paymentMode == PlanPaymentMode.Free && priceInMinorUnits != 0)
            || (paymentMode == PlanPaymentMode.DemoPaid
                && priceInMinorUnits is <= 0 or > 10_000_000))
        {
            throw new DomainException("PLAN_PRICE_INVALID");
        }

        if (durationMinutes is < 10 or > 180)
        {
            throw new DomainException("PLAN_DURATION_INVALID");
        }

        Name = name.Trim();
        Kind = kind;
        Channel = channel;
        PaymentMode = paymentMode;
        PriceInMinorUnits = priceInMinorUnits;
        Currency = "CNY";
        DurationMinutes = durationMinutes;
    }

    private void EnsureActive()
    {
        if (!Active)
        {
            throw new DomainException("PLAN_INACTIVE");
        }
    }
}
