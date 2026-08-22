using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using MentalHealth.Application.Security;

namespace MentalHealth.Api.Authorization;

public static class Policies
{
    public const string MfaSetup = "MfaSetup";
    public const string UserOwnsSubject = "UserOwnsSubject";
    public const string AssignedPractitioner = "AssignedPractitioner";
    public const string RiskReviewer = "RiskReviewer";
    public const string OperationsAdmin = "OperationsAdmin";
}

public static class AuthorizationRegistration
{
    public static IServiceCollection AddMentalHealthAuthorization(
        this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, SessionAccessHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler,
            ProblemAuthorizationResultHandler>();
        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim("scope", "api")
                .Build())
            .AddPolicy(Policies.MfaSetup, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("scope", "mfa_setup"))
            .AddPolicy(Policies.UserOwnsSubject, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("scope", "api")
                .AddRequirements(new SessionAccessRequirement(
                    SessionAccessKind.SubjectOwner)))
            .AddPolicy(Policies.AssignedPractitioner, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("scope", "api")
                .AddRequirements(new SessionAccessRequirement(
                    SessionAccessKind.AssignedPractitioner)))
            .AddPolicy(Policies.RiskReviewer, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("scope", "api")
                .AddRequirements(new SessionAccessRequirement(
                    SessionAccessKind.RiskReviewer)))
            .AddPolicy(Policies.OperationsAdmin, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("scope", "api")
                .RequireRole(AppRoles.OperationsAdmin));

        return services;
    }
}
