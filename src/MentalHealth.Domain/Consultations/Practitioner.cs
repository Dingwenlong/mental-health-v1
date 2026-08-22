using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.Consultations;

public sealed class Practitioner
{
    private Practitioner()
    {
    }

    private Practitioner(
        Guid id,
        string displayName,
        PractitionerRole role,
        DateTimeOffset now)
    {
        if (id == Guid.Empty || !Enum.IsDefined(role))
        {
            throw new DomainException("PRACTITIONER_VALUE_INVALID");
        }

        Id = id;
        DisplayName = NormalizeDisplayName(displayName);
        Role = role;
        Active = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public PractitionerRole Role { get; private set; }

    public bool Active { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Practitioner Create(
        string displayName,
        PractitionerRole role,
        DateTimeOffset now) => new(Guid.NewGuid(), displayName, role, now);

    public static Practitioner Create(
        Guid id,
        string displayName,
        PractitionerRole role,
        DateTimeOffset now) => new(id, displayName, role, now);

    public void Update(
        string displayName,
        PractitionerRole role,
        DateTimeOffset now)
    {
        EnsureActive();
        if (!Enum.IsDefined(role))
        {
            throw new DomainException("PRACTITIONER_VALUE_INVALID");
        }

        DisplayName = NormalizeDisplayName(displayName);
        Role = role;
        UpdatedAt = now;
    }

    public void Deactivate(DateTimeOffset now)
    {
        EnsureActive();
        Active = false;
        UpdatedAt = now;
    }

    private static string NormalizeDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 80)
        {
            throw new DomainException("PRACTITIONER_NAME_INVALID");
        }

        return displayName.Trim();
    }

    private void EnsureActive()
    {
        if (!Active)
        {
            throw new DomainException("PRACTITIONER_INACTIVE");
        }
    }
}
