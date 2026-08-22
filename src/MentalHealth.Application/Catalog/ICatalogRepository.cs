using MentalHealth.Domain.Consultations;

namespace MentalHealth.Application.Catalog;

public interface ICatalogRepository
{
    Task<IReadOnlyList<ServicePlan>> ListActivePlansAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Practitioner>> ListActivePractitionersAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AvailabilitySlot>> ListActiveSlotsAsync(
        DateTimeOffset endingAfter,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AvailabilitySlot>> ListActiveSlotsAsync(
        Guid practitionerId,
        CancellationToken cancellationToken);

    Task<ServicePlan?> FindPlanAsync(
        Guid planId,
        CancellationToken cancellationToken);

    Task<Practitioner?> FindPractitionerAsync(
        Guid practitionerId,
        CancellationToken cancellationToken);

    Task<AvailabilitySlot?> FindSlotAsync(
        Guid practitionerId,
        Guid slotId,
        CancellationToken cancellationToken);

    Task<bool> HasSlotOverlapAsync(
        Guid practitionerId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        CancellationToken cancellationToken);

    Task<bool> IsPractitionerLinkedToAccountAsync(
        Guid practitionerId,
        CancellationToken cancellationToken);

    void Add(ServicePlan plan);

    void Add(Practitioner practitioner);

    void Add(AvailabilitySlot slot);
}
