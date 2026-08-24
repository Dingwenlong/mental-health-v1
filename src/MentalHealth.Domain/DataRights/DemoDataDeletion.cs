using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.DataRights;

public enum DemoDataDeletionStatus
{
    DeletionPending,
    Deleted
}

public sealed class DemoDataDeletion
{
    private DemoDataDeletion()
    {
    }

    private DemoDataDeletion(
        Guid subjectId,
        Guid requestedByUserId,
        DateTimeOffset requestedAt)
    {
        if (subjectId == Guid.Empty || requestedByUserId == Guid.Empty)
        {
            throw new DomainException("DEMO_DELETION_REFERENCE_INVALID");
        }

        SubjectId = subjectId;
        RequestedByUserId = requestedByUserId;
        RequestedAt = requestedAt.ToUniversalTime();
        LastAttemptAt = RequestedAt;
        Status = DemoDataDeletionStatus.DeletionPending;
    }

    public Guid SubjectId { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public DemoDataDeletionStatus Status { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset LastAttemptAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public static DemoDataDeletion Request(
        Guid subjectId,
        Guid requestedByUserId,
        DateTimeOffset requestedAt) =>
        new(subjectId, requestedByUserId, requestedAt);

    public void Retry(Guid requestedByUserId, DateTimeOffset requestedAt)
    {
        if (requestedByUserId == Guid.Empty)
        {
            throw new DomainException("DEMO_DELETION_REFERENCE_INVALID");
        }

        RequestedByUserId = requestedByUserId;
        LastAttemptAt = requestedAt.ToUniversalTime();
        DeletedAt = null;
        Status = DemoDataDeletionStatus.DeletionPending;
    }

    public void MarkDeleted(DateTimeOffset deletedAt)
    {
        if (Status != DemoDataDeletionStatus.DeletionPending)
        {
            throw new DomainException("DEMO_DELETION_STATE_INVALID");
        }

        DeletedAt = deletedAt.ToUniversalTime();
        Status = DemoDataDeletionStatus.Deleted;
    }
}
