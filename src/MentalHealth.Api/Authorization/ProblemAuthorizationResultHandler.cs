using MentalHealth.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace MentalHealth.Api.Authorization;

public sealed class ProblemAuthorizationResultHandler
    : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _fallback = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(
                new
                {
                    type = "about:blank",
                    title = "无权访问这项资料",
                    status = StatusCodes.Status403Forbidden,
                    code = ApiProblemCodes.ForbiddenResource
                },
                cancellationToken: context.RequestAborted);
            return;
        }

        await _fallback.HandleAsync(next, context, policy, authorizeResult);
    }
}
