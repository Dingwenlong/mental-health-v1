using MentalHealth.Application.Abstractions.Clock;

namespace MentalHealth.Application.Catalog;

public sealed class CatalogQueryHandler(
    ICatalogRepository catalog,
    IClock clock)
{
    public async Task<IReadOnlyList<ServicePlanDto>> ListPlansAsync(
        CancellationToken cancellationToken)
    {
        var plans = await catalog.ListActivePlansAsync(cancellationToken);
        return plans.Select(ServicePlanDto.From).ToArray();
    }

    public async Task<IReadOnlyList<PractitionerDto>> ListPractitionersAsync(
        CancellationToken cancellationToken)
    {
        var practitioners = await catalog.ListActivePractitionersAsync(
            cancellationToken);
        var slots = await catalog.ListActiveSlotsAsync(
            clock.UtcNow,
            cancellationToken);
        var slotsByPractitioner = slots
            .GroupBy(slot => slot.PractitionerId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<AvailabilitySlotDto>)group
                    .OrderBy(slot => slot.StartAt)
                    .Select(AvailabilitySlotDto.From)
                    .ToArray());

        return practitioners
            .Select(practitioner => new PractitionerDto(
                practitioner.Id,
                practitioner.DisplayName,
                practitioner.Role.ToString(),
                practitioner.Active,
                slotsByPractitioner.GetValueOrDefault(
                    practitioner.Id,
                    [])))
            .ToArray();
    }
}
