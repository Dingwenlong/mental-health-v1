using MentalHealth.Api.Authorization;
using MentalHealth.Application.Analysis;
using MentalHealth.Application.Security;
using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.Shared;
using MentalHealth.Api.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentalHealth.Api.Controllers;

[ApiController]
[Authorize]
[Authorize(Roles = AppRoles.Doctor)]
[Route("api/v1/risk-cases")]
public sealed class RiskCasesController(
    RiskCaseQueryHandler query,
    ReviewRiskCaseHandler review,
    NotificationPublisher notifications) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] RiskLevel? level,
        [FromQuery] ObservationCaseStatus? status,
        [FromQuery] bool assignedToMe,
        CancellationToken cancellationToken)
    {
        var actor = User.ToConsultationActor();
        if (actor is null)
        {
            return ConsultationProblemMapper.Forbidden();
        }

        try
        {
            var cases = await query.ListAsync(
                actor,
                level,
                status,
                assignedToMe,
                cancellationToken);
            return Ok(cases.Select(RiskCaseResponse.From).ToArray());
        }
        catch (DomainException exception)
        {
            return ConsultationProblemMapper.From(exception);
        }
    }

    [HttpGet("{caseId:guid}")]
    public async Task<IActionResult> Get(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var actor = User.ToConsultationActor();
        if (actor is null)
        {
            return ConsultationProblemMapper.Forbidden();
        }

        try
        {
            return Ok(RiskCaseResponse.From(await query.GetAsync(
                actor,
                caseId,
                cancellationToken)));
        }
        catch (DomainException exception)
        {
            return ConsultationProblemMapper.From(exception);
        }
    }

    [HttpPost("{caseId:guid}/reviews")]
    public async Task<IActionResult> Review(
        Guid caseId,
        ReviewRiskCaseRequest request,
        CancellationToken cancellationToken)
    {
        var actor = User.ToConsultationActor();
        if (actor is null)
        {
            return ConsultationProblemMapper.Forbidden();
        }

        try
        {
            var saved = await review.HandleAsync(
                actor,
                caseId,
                ParseLevel(request.ReviewedLevel),
                request.Reason ?? string.Empty,
                cancellationToken);
            await notifications.RiskCaseChangedAsync(
                caseId,
                saved.ReviewedLevel.ToString(),
                "Open",
                cancellationToken);
            return Created(
                $"/api/v1/risk-cases/{caseId}",
                ClinicalReviewResponse.From(saved));
        }
        catch (DomainException exception)
        {
            return ConsultationProblemMapper.From(exception);
        }
    }

    private static RiskLevel ParseLevel(string? value) =>
        Enum.TryParse<RiskLevel>(value, ignoreCase: true, out var level)
        && Enum.IsDefined(level)
            ? level
            : throw new DomainException("REVIEW_LEVEL_INVALID");
}

public sealed record ReviewRiskCaseRequest(
    string? ReviewedLevel,
    string? Reason);

public sealed record ClinicalReviewResponse(
    Guid Id,
    Guid AssessmentId,
    Guid ReviewerId,
    string ReviewedLevel,
    string Reason,
    DateTimeOffset ReviewedAt)
{
    public static ClinicalReviewResponse From(ClinicalReview review) => new(
        review.Id,
        review.AssessmentId,
        review.ReviewerId,
        review.ReviewedLevel.ToString(),
        review.Reason,
        review.ReviewedAt);
}

public sealed record RiskCaseResponse(
    Guid Id,
    Guid AssessmentId,
    Guid SessionId,
    Guid SubjectId,
    string ConsultationKind,
    string OriginalLevel,
    string CurrentLevel,
    string Status,
    Guid? FollowUpTaskId,
    DateTimeOffset CreatedAt,
    RiskAssessmentResponse Assessment,
    IReadOnlyList<ClinicalReviewResponse> Reviews,
    FollowUpResponse? FollowUp)
{
    public static RiskCaseResponse From(RiskCaseDetails details) => new(
        details.ObservationCase.Id,
        details.ObservationCase.AssessmentId,
        details.ObservationCase.SessionId,
        details.ObservationCase.SubjectId,
        details.ObservationCase.ConsultationKind.ToString(),
        details.ObservationCase.OriginalLevel.ToString(),
        details.ObservationCase.CurrentLevel.ToString(),
        details.ObservationCase.Status.ToString(),
        details.ObservationCase.FollowUpTaskId,
        details.ObservationCase.CreatedAt,
        RiskAssessmentResponse.From(details.Assessment),
        details.Reviews.Select(ClinicalReviewResponse.From).ToArray(),
        details.FollowUp is null ? null : FollowUpResponse.From(details.FollowUp));
}
