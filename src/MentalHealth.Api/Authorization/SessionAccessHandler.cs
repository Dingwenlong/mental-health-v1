using System.Security.Claims;
using MentalHealth.Application.Security;
using Microsoft.AspNetCore.Authorization;

namespace MentalHealth.Api.Authorization;

public enum SessionAccessKind
{
    SubjectOwner,
    AssignedPractitioner,
    RiskReviewer
}

public sealed class SessionAccessRequirement(SessionAccessKind kind)
    : IAuthorizationRequirement
{
    public SessionAccessKind Kind { get; } = kind;
}

public sealed class SessionAccessHandler
    : AuthorizationHandler<SessionAccessRequirement, SessionAccessResource>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SessionAccessRequirement requirement,
        SessionAccessResource resource)
    {
        var allowed = requirement.Kind switch
        {
            SessionAccessKind.SubjectOwner =>
                context.User.IsInRole(AppRoles.User)
                && ClaimMatches(context.User, "subject_id", resource.SubjectId),
            SessionAccessKind.AssignedPractitioner =>
                context.User.IsInRole(AppRoles.Counselor)
                && ClaimMatches(
                    context.User,
                    "practitioner_id",
                    resource.AssignedPractitionerId),
            SessionAccessKind.RiskReviewer =>
                context.User.IsInRole(AppRoles.Doctor)
                && (resource.RequiresDoctorReview
                    || ClaimMatches(
                        context.User,
                        "practitioner_id",
                        resource.RiskReviewerId)),
            _ => false
        };

        if (allowed)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool ClaimMatches(
        ClaimsPrincipal principal,
        string claimType,
        Guid expected)
    {
        return Guid.TryParse(principal.FindFirstValue(claimType), out var actual)
            && actual == expected;
    }
}
