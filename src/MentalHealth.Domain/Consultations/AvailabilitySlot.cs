using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.Consultations;

public sealed class AvailabilitySlot
{
    private AvailabilitySlot()
    {
    }

    private AvailabilitySlot(
        Guid practitionerId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        DateTimeOffset createdAt)
    {
        if (practitionerId == Guid.Empty)
        {
            throw new DomainException("PRACTITIONER_REQUIRED");
        }

        var normalizedStartAt = startAt.ToUniversalTime();
        var normalizedEndAt = endAt.ToUniversalTime();
        var duration = normalizedEndAt - normalizedStartAt;
        if (duration < TimeSpan.FromMinutes(10)
            || duration > TimeSpan.FromHours(8))
        {
            throw new DomainException("AVAILABILITY_SLOT_RANGE_INVALID");
        }

        Id = Guid.NewGuid();
        PractitionerId = practitionerId;
        StartAt = normalizedStartAt;
        EndAt = normalizedEndAt;
        Active = true;
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }

    public Guid PractitionerId { get; private set; }

    public DateTimeOffset StartAt { get; private set; }

    public DateTimeOffset EndAt { get; private set; }

    public bool Active { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static AvailabilitySlot Create(
        Guid practitionerId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        DateTimeOffset createdAt) =>
        new(practitionerId, startAt, endAt, createdAt);

    public bool Overlaps(DateTimeOffset startAt, DateTimeOffset endAt) =>
        Active && StartAt < endAt && startAt < EndAt;

    public void Deactivate()
    {
        if (!Active)
        {
            throw new DomainException("AVAILABILITY_SLOT_INACTIVE");
        }

        Active = false;
    }
}
