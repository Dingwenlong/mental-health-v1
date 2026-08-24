using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.FollowUps;

public sealed class FollowUpScheduler
{
    public FollowUpProposal Propose(
        FollowUpScheduleRequest request,
        IReadOnlyCollection<FollowUpCandidate> candidates,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidates);
        var deadline = GetDeadline(request, now);
        if (deadline is null)
        {
            return FollowUpProposal.NotRequired();
        }

        if (request.IsCrisis || request.Level == RiskLevel.Crisis)
        {
            return FollowUpProposal.Conflict(
                deadline.Value,
                "NO_QUALIFIED_SLOT_BEFORE_SLA");
        }

        var qualified = candidates
            .Where(candidate => candidate.AvailabilitySlotId != Guid.Empty
                && candidate.PractitionerId != Guid.Empty
                && candidate.Role == PractitionerRole.Doctor
                && candidate.Active
                && candidate.IncompleteTaskCount >= 0
                && candidate.StartAt >= now
                && candidate.StartAt <= deadline
                && candidate.EndAt > candidate.StartAt)
            .OrderBy(candidate =>
                candidate.PractitionerId == request.OriginalPractitionerId ? 0 : 1)
            .ThenBy(candidate => candidate.StartAt)
            .ThenBy(candidate => candidate.IncompleteTaskCount)
            .ThenBy(candidate => candidate.PractitionerId)
            .ToArray();
        return qualified.Length == 0
            ? FollowUpProposal.Conflict(
                deadline.Value,
                "NO_QUALIFIED_SLOT_BEFORE_SLA")
            : FollowUpProposal.Scheduled(qualified[0], deadline.Value);
    }

    public DateTimeOffset? GetDeadline(
        FollowUpScheduleRequest request,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Level))
        {
            throw new DomainException("FOLLOW_UP_LEVEL_INVALID");
        }

        if (request.IsCrisis || request.Level == RiskLevel.Crisis)
        {
            return now;
        }

        return request.Level switch
        {
            RiskLevel.L1 => now.AddDays(7),
            RiskLevel.L2 => now.AddHours(72),
            RiskLevel.L3 => now.AddHours(24),
            _ => null
        };
    }
}
