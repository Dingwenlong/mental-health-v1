using System.Security.Claims;
using MentalHealth.Application.Consents;
using MentalHealth.Contracts.Common;
using MentalHealth.Domain.Consents;
using MentalHealth.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentalHealth.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/consents")]
public sealed class ConsentsController(RecordConsentHandler handler) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Record(
        RecordConsentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var subjectId))
        {
            return ConsentProblem(
                StatusCodes.Status403Forbidden,
                ApiProblemCodes.ForbiddenResource,
                "无权操作他人的授权记录");
        }

        if (string.IsNullOrWhiteSpace(request.TextVersion)
            || request.TextVersion.Length > 64)
        {
            return ConsentProblem(
                StatusCodes.Status422UnprocessableEntity,
                ApiProblemCodes.InvalidConsentTextVersion,
                "授权文本版本无效");
        }

        if (string.Equals(
            request.Kind,
            "ModelTraining",
            StringComparison.OrdinalIgnoreCase))
        {
            return ConsentProblem(
                StatusCodes.Status422UnprocessableEntity,
                ApiProblemCodes.ConsentTypeDisabled,
                "v1 不开放模型训练授权");
        }

        if (!Enum.TryParse<ConsentKind>(
            request.Kind,
            ignoreCase: true,
            out var kind)
            || !Enum.IsDefined(kind))
        {
            return ConsentProblem(
                StatusCodes.Status422UnprocessableEntity,
                ApiProblemCodes.InvalidConsentKind,
                "不支持这种授权类型");
        }

        try
        {
            var result = await handler.RecordAsync(
                subjectId,
                actorUserId,
                kind,
                request.TextVersion,
                cancellationToken);
            var response = ConsentResponse.From(result.Consent);
            return result.Created
                ? Created($"/api/v1/consents/{result.Consent.Id}", response)
                : Ok(response);
        }
        catch (DomainException exception)
            when (exception.Code == ApiProblemCodes.ActiveConsentExists)
        {
            return ConsentProblem(
                StatusCodes.Status409Conflict,
                exception.Code,
                "请先撤回当前版本，再同意新版本");
        }
    }

    [HttpDelete("{consentId:guid}")]
    public async Task<IActionResult> Withdraw(
        Guid consentId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var subjectId))
        {
            return ConsentProblem(
                StatusCodes.Status403Forbidden,
                ApiProblemCodes.ForbiddenResource,
                "无权操作他人的授权记录");
        }

        var withdrawn = await handler.WithdrawAsync(
            subjectId,
            actorUserId,
            consentId,
            cancellationToken);
        return withdrawn ? NoContent() : NotFound();
    }

    private bool TryGetActor(out Guid actorUserId, out Guid subjectId)
    {
        actorUserId = Guid.Empty;
        subjectId = Guid.Empty;
        return Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out actorUserId)
            && Guid.TryParse(User.FindFirstValue("subject_id"), out subjectId);
    }

    private ObjectResult ConsentProblem(int status, string code, string title)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title
        };
        problem.Extensions["code"] = code;
        return new ObjectResult(problem) { StatusCode = status };
    }
}

public sealed record RecordConsentRequest(string Kind, string TextVersion);

public sealed record ConsentResponse(
    Guid Id,
    string Kind,
    string TextVersion,
    DateTimeOffset GrantedAt,
    DateTimeOffset? WithdrawnAt,
    bool Active)
{
    public static ConsentResponse From(ConsentRecord consent) => new(
        consent.Id,
        consent.Kind.ToString(),
        consent.TextVersion,
        consent.GrantedAt,
        consent.WithdrawnAt,
        consent.Active);
}
