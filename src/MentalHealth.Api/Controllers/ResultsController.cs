using MentalHealth.Api.Authorization;
using MentalHealth.Application.Analysis;
using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentalHealth.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/results")]
public sealed class ResultsController(RiskReportQueryHandler query) : ControllerBase
{
    [HttpGet("{sessionId:guid}")]
    public async Task<IActionResult> Get(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var actor = User.ToConsultationActor();
        if (actor is null)
        {
            return ConsultationProblemMapper.Forbidden();
        }

        try
        {
            var assessment = await query.HandleAsync(
                actor,
                sessionId,
                cancellationToken);
            return Ok(RiskAssessmentResponse.From(assessment));
        }
        catch (DomainException exception)
        {
            return ConsultationProblemMapper.From(exception);
        }
    }
}

public sealed record RiskEvidenceResponse(
    string Code,
    string Modality,
    decimal Contribution,
    string SourceRange,
    decimal Quality);

public sealed record RiskAssessmentResponse(
    Guid Id,
    Guid SessionId,
    int? TranscriptRevision,
    string RuleSetVersion,
    decimal Score,
    decimal AvailableWeight,
    decimal Confidence,
    string Level,
    bool IsCrisis,
    string? CrisisRuleId,
    IReadOnlyList<string> Missing,
    IReadOnlyList<RiskEvidenceResponse> Evidence,
    DateTimeOffset CreatedAt,
    string Notice)
{
    public static RiskAssessmentResponse From(RiskAssessment assessment) => new(
        assessment.Id,
        assessment.SessionId,
        assessment.TranscriptRevision,
        assessment.RuleSetVersion,
        assessment.Score,
        assessment.AvailableWeight,
        assessment.Confidence,
        assessment.Level.ToString(),
        assessment.IsCrisis,
        assessment.CrisisRuleId,
        assessment.Missing.Select(item => item.ToString()).ToArray(),
        assessment.Evidence.Select(item => new RiskEvidenceResponse(
            item.Code,
            item.Modality,
            item.Contribution,
            item.SourceRange,
            item.Quality)).ToArray(),
        assessment.CreatedAt,
        "这是比赛演示，不是诊断");
}
