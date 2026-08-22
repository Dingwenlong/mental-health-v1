using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Application.Audit;
using MentalHealth.Domain.Audit;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Application.Catalog;

public sealed record CreateOrderResult(DemoOrder Order, bool Created);

public sealed class OrderHandler(
    ICatalogRepository catalog,
    IOrderRepository orders,
    IPaymentGateway paymentGateway,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<CreateOrderResult> CreateAsync(
        Guid actorUserId,
        Guid subjectId,
        Guid planId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)
            || idempotencyKey.Trim().Length > 100)
        {
            throw new DomainException("IDEMPOTENCY_KEY_INVALID");
        }

        var normalizedKey = idempotencyKey.Trim();
        var existing = await orders.FindByIdempotencyKeyAsync(
            subjectId,
            normalizedKey,
            cancellationToken);
        if (existing is not null)
        {
            return new CreateOrderResult(existing, false);
        }

        var plan = await catalog.FindPlanAsync(planId, cancellationToken);
        if (plan is null || !plan.Active)
        {
            throw new DomainException("PLAN_NOT_AVAILABLE");
        }

        var order = DemoOrder.Create(
            subjectId,
            plan,
            normalizedKey,
            clock.UtcNow);
        orders.Add(order);
        AddAudit(actorUserId, "OrderCreated", order.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CreateOrderResult(order, true);
    }

    public async Task<DemoOrder?> ConfirmAsync(
        Guid actorUserId,
        Guid subjectId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var order = await orders.FindAsync(subjectId, orderId, cancellationToken);
        if (order is null || order.Status == DemoOrderStatus.Confirmed)
        {
            return order;
        }

        var confirmation = await paymentGateway.ConfirmAsync(
            new PaymentRequest(
                order.Id,
                order.AmountInMinorUnits,
                order.Currency,
                $"demo-payment:{order.Id:N}"),
            cancellationToken);
        if (confirmation.Status != PaymentStatus.Confirmed)
        {
            throw new DomainException("DEMO_PAYMENT_DECLINED");
        }

        order.Confirm(confirmation.ProviderReference, confirmation.ConfirmedAt);
        AddAudit(actorUserId, "DemoPaymentConfirmed", order.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return order;
    }

    private void AddAudit(Guid actorUserId, string action, Guid resourceId)
    {
        auditTrail.Add(AuditEvent.Create(
            actorUserId,
            action,
            nameof(DemoOrder),
            resourceId,
            clock.UtcNow));
    }
}
