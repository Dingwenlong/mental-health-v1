using System.ComponentModel.DataAnnotations;
using MentalHealth.Api.Authorization;
using MentalHealth.Application.Care;
using MentalHealth.Application.Consultations;
using MentalHealth.Domain.Care;
using MentalHealth.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentalHealth.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CareController(CareContinuityService care) : ControllerBase
{
    [HttpGet("account/me")]
    public IActionResult Me()
    {
        var actor = User.ToConsultationActor();
        return actor is null ? ConsultationProblemMapper.Forbidden() : Ok(new { actor.UserId, actor.SubjectId, actor.PractitionerId, actor.Roles });
    }
    [HttpGet("me/check-ins")]
    public Task<IActionResult> CheckIns([FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Run(actor => care.CheckInsAsync(actor, from, to, page, pageSize, ct), ct);
    [HttpPut("me/check-ins/{date}")]
    public Task<IActionResult> PutCheckIn(DateOnly date, CheckInRequest request, CancellationToken ct) =>
        Run(actor => care.PutCheckInAsync(actor, date, request.Mood, request.SleepHours, request.Note, request.Version, ct), ct);
    [HttpDelete("me/check-ins/{date}")]
    public Task<IActionResult> DeleteCheckIn(DateOnly date, CancellationToken ct) => Run(actor => care.DeleteCheckInAsync(actor, date, ct), ct, true);
    [HttpGet("me/trends")]
    public Task<IActionResult> Trends([FromQuery] int days = 7, CancellationToken ct = default) => Run(actor => care.TrendsAsync(actor, days, ct), ct);
    [HttpGet("exercises")]
    public IActionResult Exercises() => Ok(care.Exercises());
    [HttpGet("me/exercise-completions")]
    public Task<IActionResult> ExerciseCompletions([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Run(actor => care.CompletionsAsync(actor, page, pageSize, ct), ct);
    [HttpPost("me/exercise-completions")]
    public Task<IActionResult> CompleteExercise(ExerciseCompletionRequest request, CancellationToken ct) =>
        Run(actor => care.CompleteExerciseAsync(actor, request.Id, request.ExerciseId, ct), ct);
    [HttpGet("me/sharing-grants/candidates")]
    public Task<IActionResult> Candidates(CancellationToken ct) => Run(actor => care.CandidatesAsync(actor, ct), ct);
    [HttpGet("me/sharing-grants")]
    public Task<IActionResult> Grants([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Run(actor => care.GrantsAsync(actor, page, pageSize, ct), ct);
    [HttpPost("me/sharing-grants")]
    public Task<IActionResult> Grant(SharingRequest request, CancellationToken ct) =>
        Run(async actor => new { id = await care.GrantAsync(actor, request.FollowUpId, request.Acknowledged, ct) }, ct);
    [HttpDelete("me/sharing-grants/{id:guid}")]
    public Task<IActionResult> Revoke(Guid id, CancellationToken ct) => Run(actor => care.RevokeAsync(actor, id, ct), ct, true);
    [HttpGet("care-plans")]
    public Task<IActionResult> Plans([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Run(actor => care.PlansAsync(actor, page, pageSize, ct), ct);
    [HttpGet("care-plans/{id:guid}")]
    public Task<IActionResult> Plan(Guid id, CancellationToken ct) => Run(actor => care.PlanAsync(actor, id, ct), ct);
    [HttpPost("care-plans")]
    public Task<IActionResult> CreatePlan(CreateCarePlanRequest request, CancellationToken ct) =>
        Run(actor => care.CreatePlanAsync(actor, request.FollowUpId, request.Title, request.IdempotencyKey, request.Tasks, ct), ct);
    [HttpPut("care-plans/{id:guid}")]
    public Task<IActionResult> UpdatePlan(Guid id, UpdateCarePlanRequest request, CancellationToken ct) =>
        Run(actor => care.UpdateDraftAsync(actor, id, request.Title, request.Tasks, request.Version, ct), ct);
    [HttpPost("care-plans/{id:guid}/publish")]
    public Task<IActionResult> Publish(Guid id, CancellationToken ct) => Run(actor => care.ChangePlanAsync(actor, id, true, ct), ct);
    [HttpPost("care-plans/{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct) => Run(actor => care.ChangePlanAsync(actor, id, false, ct), ct);
    [HttpPost("care-plans/{id:guid}/tasks/{taskId:guid}/feedback")]
    public Task<IActionResult> Feedback(Guid id, Guid taskId, CareFeedbackRequest request, CancellationToken ct) =>
        Run(actor => care.FeedbackAsync(actor, id, taskId, request.Status, request.Feedback, request.Acknowledged, ct), ct);
    [HttpGet("clinical/subjects")]
    public Task<IActionResult> Subjects([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Run(actor => care.SubjectsAsync(actor, page, pageSize, ct), ct);
    [HttpGet("clinical/subjects/{id:guid}")]
    public Task<IActionResult> Subject(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Run(actor => care.SubjectAsync(actor, id, page, pageSize, ct), ct);
    [HttpGet("workspace/summary")]
    public Task<IActionResult> Summary(CancellationToken ct) => Run(actor => care.SummaryAsync(actor, ct), ct);
    [HttpGet("consultations")]
    public Task<IActionResult> Consultations([FromQuery] string? status, [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Run(actor => care.ConsultationsAsync(actor, false, status, from, to, page, pageSize, ct), ct);
    [HttpGet("results")]
    public Task<IActionResult> Results([FromQuery] string? status, [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Run(actor => care.ConsultationsAsync(actor, true, status, from, to, page, pageSize, ct), ct);

    private async Task<IActionResult> Run<T>(Func<ConsultationActor, Task<T>> action, CancellationToken ct, bool noContent = false)
    {
        var actor = User.ToConsultationActor();
        if (actor is null) return ConsultationProblemMapper.Forbidden();
        try
        {
            var result = await care.ExecuteAsync(() => action(actor), ct);
            return noContent ? NoContent() : Ok(result);
        }
        catch (DomainException exception)
        {
            var status = exception.Code switch
            {
                "FORBIDDEN_RESOURCE" or "DOCTOR_REVIEW_REQUIRED" => 403,
                "CARE_PLAN_NOT_FOUND" or "CARE_TASK_NOT_FOUND" => 404,
                "CARE_CONFLICT" or "CARE_PLAN_EXISTS" or "CARE_TASK_ALREADY_RECORDED" => 409,
                _ => 422
            };
            var problem = new ProblemDetails
            {
                Status = status,
                Title = status switch
                {
                    403 => "你没有权限查看或修改这项资料",
                    404 => "没有找到这项资料",
                    409 => "资料已变化，请刷新后重试",
                    _ => "请检查填写内容及当前状态"
                }
            };
            problem.Extensions["code"] = exception.Code;
            return new ObjectResult(problem) { StatusCode = status };
        }
    }
}

public sealed record CheckInRequest(int Mood, decimal SleepHours, [MaxLength(500)] string? Note, int? Version);
public sealed record ExerciseCompletionRequest(Guid Id, [Required] string ExerciseId);
public sealed record SharingRequest(Guid FollowUpId, bool Acknowledged);
public sealed record CreateCarePlanRequest(Guid FollowUpId, [Required, MaxLength(120)] string Title,
    [Required, MaxLength(100)] string IdempotencyKey, [Required] IReadOnlyList<CareTaskInput> Tasks);
public sealed record UpdateCarePlanRequest([Required, MaxLength(120)] string Title,
    [Required] IReadOnlyList<CareTaskInput> Tasks, int Version);
public sealed record CareFeedbackRequest([Required] string Status, [MaxLength(500)] string? Feedback, bool Acknowledged);
