using System.Security.Claims;
using MentalHealth.Api.Authorization;
using MentalHealth.Application.Analysis;
using MentalHealth.Contracts.Common;
using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentalHealth.Api.Controllers;

[ApiController]
[Authorize(Policy = Policies.OperationsAdmin)]
[Route("api/v1/admin/risk-rules")]
public sealed class AdminRiskRulesController(CreateRiskRuleSetHandler handler)
    : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        RiskRuleSetRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var actorUserId))
        {
            return ProblemFor(ApiProblemCodes.ForbiddenResource);
        }

        try
        {
            var ruleSet = await handler.CreateAsync(
                actorUserId,
                request.ToInput(),
                cancellationToken);
            return Created(
                $"/api/v1/admin/risk-rules/{ruleSet.Version}",
                RiskRuleSetResponse.From(ruleSet));
        }
        catch (DomainException exception)
        {
            return ProblemFor(exception.Code);
        }
    }

    [HttpPost("{version}/activate")]
    public async Task<IActionResult> Activate(
        string version,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var actorUserId))
        {
            return ProblemFor(ApiProblemCodes.ForbiddenResource);
        }

        try
        {
            var ruleSet = await handler.ActivateAsync(
                actorUserId,
                version,
                cancellationToken);
            return Ok(RiskRuleSetResponse.From(ruleSet));
        }
        catch (DomainException exception)
        {
            return ProblemFor(exception.Code);
        }
    }

    private bool TryActor(out Guid actorUserId) => Guid.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier),
        out actorUserId);

    private ObjectResult ProblemFor(string code)
    {
        var status = code switch
        {
            ApiProblemCodes.ForbiddenResource => StatusCodes.Status403Forbidden,
            ApiProblemCodes.RiskRuleVersionNotFound => StatusCodes.Status404NotFound,
            ApiProblemCodes.RiskRuleVersionExists => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity
        };
        var title = code switch
        {
            ApiProblemCodes.ForbiddenResource => "无权修改关注指数规则",
            ApiProblemCodes.RiskRuleVersionNotFound => "没有找到这个规则版本",
            ApiProblemCodes.RiskRuleVersionExists => "这个规则版本已经存在",
            ApiProblemCodes.RiskRuleWeightsInvalid => "五项权重必须大于零且合计为 1",
            ApiProblemCodes.RiskRuleThresholdsInvalid => "三个等级分界必须从小到大且低于 100",
            ApiProblemCodes.CrisisRulesRequired => "危机规则不能关闭",
            _ => "关注指数规则内容无效"
        };
        var problem = new ProblemDetails { Status = status, Title = title };
        problem.Extensions["code"] = code;
        return new ObjectResult(problem) { StatusCode = status };
    }
}

public sealed record RiskRuleSetRequest(
    string? Version,
    decimal ScaleWeight,
    decimal TextWeight,
    decimal AudioWeight,
    decimal VideoWeight,
    decimal TrendWeight,
    IReadOnlyList<decimal>? Thresholds,
    bool CrisisRulesEnabled)
{
    public RiskRuleSetInput ToInput() => new(
        Version ?? string.Empty,
        new Dictionary<Modality, decimal>
        {
            [Modality.Scale] = ScaleWeight,
            [Modality.Text] = TextWeight,
            [Modality.Audio] = AudioWeight,
            [Modality.Video] = VideoWeight,
            [Modality.Trend] = TrendWeight
        },
        Thresholds ?? [],
        CrisisRulesEnabled);
}

public sealed record RiskRuleSetResponse(
    Guid Id,
    string Version,
    IReadOnlyDictionary<string, decimal> Weights,
    IReadOnlyList<decimal> Thresholds,
    bool CrisisRulesEnabled,
    bool Active,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ActivatedAt)
{
    public static RiskRuleSetResponse From(RiskRuleSet ruleSet) => new(
        ruleSet.Id,
        ruleSet.Version,
        ruleSet.Weights.ToDictionary(
            item => item.Key.ToString(),
            item => item.Value,
            StringComparer.Ordinal),
        ruleSet.Thresholds,
        ruleSet.CrisisRulesEnabled,
        ruleSet.Active,
        ruleSet.CreatedAt,
        ruleSet.ActivatedAt);
}
