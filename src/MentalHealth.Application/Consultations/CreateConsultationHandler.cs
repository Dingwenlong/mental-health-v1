using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Application.Catalog;
using MentalHealth.Application.Consents;
using MentalHealth.Domain.Consents;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Application.Consultations;

public sealed record CreateConsultationResult(
    ConsultationSession Session,
    bool Created);

public sealed class CreateConsultationHandler(
    IConsultationRepository consultations,
    IOrderRepository orders,
    ICatalogRepository catalog,
    IConsentRepository consents,
    IUnitOfWork unitOfWork)
{
    public async Task<CreateConsultationResult> HandleAsync(
        ConsultationActor actor,
        Guid orderId,
        Guid? assignedPractitionerId,
        DateTimeOffset scheduledAt,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var subjectId = actor.RequireOwnedSubject();
        var normalizedKey = idempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedKey) || normalizedKey.Length > 100)
        {
            throw new DomainException("IDEMPOTENCY_KEY_INVALID");
        }

        var existing = await consultations.FindByCreationKeyAsync(
            subjectId,
            normalizedKey,
            cancellationToken);
        if (existing is not null)
        {
            EnsureSameRequest(existing, orderId, assignedPractitionerId);
            return new CreateConsultationResult(existing, false);
        }

        existing = await consultations.FindByOrderAsync(
            subjectId,
            orderId,
            cancellationToken);
        if (existing is not null)
        {
            EnsureSameRequest(existing, orderId, assignedPractitionerId);
            return new CreateConsultationResult(existing, false);
        }

        var order = await orders.FindAsync(subjectId, orderId, cancellationToken)
            ?? throw new DomainException("ORDER_NOT_FOUND");
        if (order.Status != DemoOrderStatus.Confirmed)
        {
            throw new DomainException("ORDER_NOT_CONFIRMED");
        }

        var plan = await catalog.FindPlanAsync(order.PlanId, cancellationToken)
            ?? throw new DomainException("PLAN_NOT_AVAILABLE");
        await ValidatePractitionerAsync(
            plan.Kind,
            assignedPractitionerId,
            cancellationToken);

        var grantedConsents = await LoadActiveConsentsAsync(
            subjectId,
            plan.Channel,
            cancellationToken);
        var session = ConsultationSession.CreateAuthorized(
            subjectId,
            order.Id,
            assignedPractitionerId,
            plan.Kind,
            plan.Channel,
            normalizedKey);
        session.RequestConsent();
        session.Schedule(grantedConsents, scheduledAt.ToUniversalTime());

        consultations.Add(session);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CreateConsultationResult(session, true);
    }

    private async Task ValidatePractitionerAsync(
        ConsultationKind kind,
        Guid? assignedPractitionerId,
        CancellationToken cancellationToken)
    {
        if (kind == ConsultationKind.AiVirtual)
        {
            if (assignedPractitionerId is not null)
            {
                throw new DomainException("PRACTITIONER_NOT_ALLOWED");
            }

            return;
        }

        if (assignedPractitionerId is not { } practitionerId)
        {
            throw new DomainException("PRACTITIONER_REQUIRED");
        }

        var practitioner = await catalog.FindPractitionerAsync(
            practitionerId,
            cancellationToken);
        if (practitioner is null
            || !practitioner.Active
            || practitioner.Role != PractitionerRole.Counselor)
        {
            throw new DomainException("PRACTITIONER_NOT_AVAILABLE");
        }
    }

    private async Task<IReadOnlySet<ConsentKind>> LoadActiveConsentsAsync(
        Guid subjectId,
        ConsultationChannel channel,
        CancellationToken cancellationToken)
    {
        var required = channel == ConsultationChannel.Video
            ? new[]
            {
                ConsentKind.Service,
                ConsentKind.Recording,
                ConsentKind.AiAnalysis
            }
            : new[] { ConsentKind.Service, ConsentKind.AiAnalysis };
        var granted = new HashSet<ConsentKind>();
        foreach (var kind in required)
        {
            if (await consents.FindActiveAsync(
                subjectId,
                kind,
                cancellationToken) is not null)
            {
                granted.Add(kind);
            }
        }

        return granted;
    }

    private static void EnsureSameRequest(
        ConsultationSession session,
        Guid orderId,
        Guid? assignedPractitionerId)
    {
        if (session.OrderId != orderId
            || session.AssignedPractitionerId != assignedPractitionerId)
        {
            throw new DomainException("IDEMPOTENCY_CONFLICT");
        }
    }
}
