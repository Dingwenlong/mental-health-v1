using System.Security.Claims;
using MentalHealth.Api.Authorization;
using MentalHealth.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealth.IntegrationTests.Auth;

public sealed class AuthorizationMatrixTests
{
    private static readonly Guid SubjectId = Guid.NewGuid();
    private static readonly Guid CounselorId = Guid.NewGuid();
    private static readonly Guid DoctorId = Guid.NewGuid();

    private static readonly SessionAccessResource Resource = new(
        SubjectId,
        CounselorId,
        DoctorId,
        RequiresDoctorReview: false);

    private readonly IAuthorizationService _authorization;

    public AuthorizationMatrixTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMentalHealthAuthorization();
        _authorization = services.BuildServiceProvider()
            .GetRequiredService<IAuthorizationService>();
    }

    [Theory]
    [MemberData(nameof(ContentAccessCases))]
    public async Task Content_policies_enforce_resource_ownership(
        ClaimsPrincipal principal,
        string policy,
        bool expected)
    {
        var result = await _authorization.AuthorizeAsync(principal, Resource, policy);

        Assert.Equal(expected, result.Succeeded);
    }

    [Fact]
    public async Task Operations_admin_can_use_operations_policy_but_not_read_content()
    {
        var admin = Principal(AppRoles.OperationsAdmin);

        Assert.True((await _authorization.AuthorizeAsync(
            admin,
            resource: null,
            Policies.OperationsAdmin)).Succeeded);
        Assert.False((await _authorization.AuthorizeAsync(
            admin,
            Resource,
            Policies.UserOwnsSubject)).Succeeded);
        Assert.False((await _authorization.AuthorizeAsync(
            admin,
            Resource,
            Policies.AssignedPractitioner)).Succeeded);
        Assert.False((await _authorization.AuthorizeAsync(
            admin,
            Resource,
            Policies.RiskReviewer)).Succeeded);
    }

    [Fact]
    public async Task Doctor_can_read_a_session_waiting_for_doctor_review()
    {
        var pendingReview = Resource with
        {
            RiskReviewerId = Guid.NewGuid(),
            RequiresDoctorReview = true
        };

        var result = await _authorization.AuthorizeAsync(
            Principal(AppRoles.Doctor, practitionerId: Guid.NewGuid()),
            pendingReview,
            Policies.RiskReviewer);

        Assert.True(result.Succeeded);
    }

    public static TheoryData<ClaimsPrincipal, string, bool> ContentAccessCases => new()
    {
        { Principal(AppRoles.User, subjectId: SubjectId), Policies.UserOwnsSubject, true },
        { Principal(AppRoles.User, subjectId: Guid.NewGuid()), Policies.UserOwnsSubject, false },
        { Principal(AppRoles.Counselor, practitionerId: CounselorId), Policies.AssignedPractitioner, true },
        { Principal(AppRoles.Counselor, practitionerId: Guid.NewGuid()), Policies.AssignedPractitioner, false },
        { Principal(AppRoles.Doctor, practitionerId: DoctorId), Policies.RiskReviewer, true },
        { Principal(AppRoles.Doctor, practitionerId: Guid.NewGuid()), Policies.RiskReviewer, false }
    };

    private static ClaimsPrincipal Principal(
        string role,
        Guid? subjectId = null,
        Guid? practitionerId = null)
    {
        var claims = new List<Claim>
        {
            new("sub", Guid.NewGuid().ToString()),
            new("scope", "api"),
            new(ClaimTypes.Role, role)
        };
        if (subjectId is { } subject)
        {
            claims.Add(new Claim("subject_id", subject.ToString()));
        }

        if (practitionerId is { } practitioner)
        {
            claims.Add(new Claim("practitioner_id", practitioner.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }
}
