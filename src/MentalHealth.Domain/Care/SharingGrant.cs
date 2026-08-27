using MentalHealth.Domain.FollowUps;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.Care;

public sealed class SharingGrant
{
    private SharingGrant() { }
    public Guid Id { get; private set; }
    public Guid SubjectId { get; private set; }
    public Guid FollowUpId { get; private set; }
    public Guid DoctorId { get; private set; }
    public int AssignmentVersion { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public const string TextVersion = "daily-sharing-v1";
    public string ConsentVersion { get; private set; } = TextVersion;

    public static SharingGrant Create(Guid subjectId, Guid followUpId, Guid doctorId, int assignmentVersion, DateTimeOffset now)
    {
        if (subjectId == Guid.Empty || followUpId == Guid.Empty || doctorId == Guid.Empty)
            throw new DomainException("SHARING_INVALID");
        return new SharingGrant
        {
            Id = Guid.NewGuid(),
            SubjectId = subjectId,
            FollowUpId = followUpId,
            DoctorId = doctorId,
            AssignmentVersion = assignmentVersion,
            GrantedAt = now
        };
    }

    public bool Matches(FollowUpTask task) => RevokedAt is null && task.Id == FollowUpId
        && task.SubjectId == SubjectId && task.AssigneeId == DoctorId && task.AssignmentVersion == AssignmentVersion
        && task.Status is not (FollowUpStatus.Completed or FollowUpStatus.Cancelled);

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
