using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Application.Audit;
using MentalHealth.Domain.Audit;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Application.Catalog;

public sealed record ServicePlanInput(
    string Name,
    ConsultationKind Kind,
    ConsultationChannel Channel,
    PlanPaymentMode PaymentMode,
    long PriceInMinorUnits,
    string Currency,
    int DurationMinutes);

public sealed record PractitionerInput(
    string DisplayName,
    PractitionerRole Role);

public sealed class AdminCatalogHandler(
    ICatalogRepository catalog,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<ServicePlan> CreatePlanAsync(
        Guid actorUserId,
        ServicePlanInput input,
        CancellationToken cancellationToken)
    {
        var plan = ServicePlan.Create(
            input.Name,
            input.Kind,
            input.Channel,
            input.PaymentMode,
            input.PriceInMinorUnits,
            input.Currency,
            input.DurationMinutes,
            clock.UtcNow);
        catalog.Add(plan);
        AddAudit(actorUserId, "ServicePlanCreated", plan.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return plan;
    }

    public async Task<ServicePlan?> UpdatePlanAsync(
        Guid actorUserId,
        Guid planId,
        ServicePlanInput input,
        CancellationToken cancellationToken)
    {
        var plan = await catalog.FindPlanAsync(planId, cancellationToken);
        if (plan is null)
        {
            return null;
        }

        plan.Update(
            input.Name,
            input.Kind,
            input.Channel,
            input.PaymentMode,
            input.PriceInMinorUnits,
            input.Currency,
            input.DurationMinutes,
            clock.UtcNow);
        AddAudit(actorUserId, "ServicePlanUpdated", plan.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return plan;
    }

    public async Task<bool> DeactivatePlanAsync(
        Guid actorUserId,
        Guid planId,
        CancellationToken cancellationToken)
    {
        var plan = await catalog.FindPlanAsync(planId, cancellationToken);
        if (plan is null)
        {
            return false;
        }

        plan.Deactivate(clock.UtcNow);
        AddAudit(actorUserId, "ServicePlanDeactivated", plan.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<Practitioner> CreatePractitionerAsync(
        Guid actorUserId,
        PractitionerInput input,
        CancellationToken cancellationToken)
    {
        var practitioner = Practitioner.Create(
            input.DisplayName,
            input.Role,
            clock.UtcNow);
        catalog.Add(practitioner);
        AddAudit(actorUserId, "PractitionerCreated", practitioner.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return practitioner;
    }

    public async Task<Practitioner?> UpdatePractitionerAsync(
        Guid actorUserId,
        Guid practitionerId,
        PractitionerInput input,
        CancellationToken cancellationToken)
    {
        var practitioner = await catalog.FindPractitionerAsync(
            practitionerId,
            cancellationToken);
        if (practitioner is null)
        {
            return null;
        }

        if (practitioner.Role != input.Role
            && await catalog.IsPractitionerLinkedToAccountAsync(
                practitionerId,
                cancellationToken))
        {
            throw new DomainException("PRACTITIONER_ROLE_LOCKED");
        }

        practitioner.Update(input.DisplayName, input.Role, clock.UtcNow);
        AddAudit(actorUserId, "PractitionerUpdated", practitioner.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return practitioner;
    }

    public async Task<bool> DeactivatePractitionerAsync(
        Guid actorUserId,
        Guid practitionerId,
        CancellationToken cancellationToken)
    {
        var practitioner = await catalog.FindPractitionerAsync(
            practitionerId,
            cancellationToken);
        if (practitioner is null)
        {
            return false;
        }

        practitioner.Deactivate(clock.UtcNow);
        var slots = await catalog.ListActiveSlotsAsync(
            practitionerId,
            cancellationToken);
        foreach (var slot in slots)
        {
            slot.Deactivate();
        }

        AddAudit(actorUserId, "PractitionerDeactivated", practitioner.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AvailabilitySlot> CreateSlotAsync(
        Guid actorUserId,
        Guid practitionerId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        CancellationToken cancellationToken)
    {
        var practitioner = await catalog.FindPractitionerAsync(
            practitionerId,
            cancellationToken);
        if (practitioner is null || !practitioner.Active)
        {
            throw new DomainException("PRACTITIONER_NOT_FOUND");
        }

        var slot = AvailabilitySlot.Create(
            practitionerId,
            startAt,
            endAt,
            clock.UtcNow);
        if (await catalog.HasSlotOverlapAsync(
            practitionerId,
            startAt,
            endAt,
            cancellationToken))
        {
            throw new DomainException("AVAILABILITY_SLOT_CONFLICT");
        }

        catalog.Add(slot);
        AddAudit(actorUserId, "AvailabilitySlotCreated", slot.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return slot;
    }

    public async Task<bool> DeactivateSlotAsync(
        Guid actorUserId,
        Guid practitionerId,
        Guid slotId,
        CancellationToken cancellationToken)
    {
        var slot = await catalog.FindSlotAsync(
            practitionerId,
            slotId,
            cancellationToken);
        if (slot is null)
        {
            return false;
        }

        slot.Deactivate();
        AddAudit(actorUserId, "AvailabilitySlotDeactivated", slot.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private void AddAudit(Guid actorUserId, string action, Guid resourceId)
    {
        auditTrail.Add(AuditEvent.Create(
            actorUserId,
            action,
            "Catalog",
            resourceId,
            clock.UtcNow));
    }
}
