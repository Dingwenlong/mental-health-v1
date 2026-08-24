using MentalHealth.Api.Authorization;
using MentalHealth.Application.Consultations;
using MentalHealth.Application.FollowUps;
using MentalHealth.Domain.FollowUps;
using MentalHealth.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentalHealth.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/follow-ups")]
public sealed class FollowUpsController(
    FollowUpQueryHandler query,
    RescheduleFollowUpHandler commands) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var actor = User.ToConsultationActor();
        if (actor is null)
        {
            return ConsultationProblemMapper.Forbidden();
        }

        try
        {
            var tasks = await query.HandleAsync(actor, cancellationToken);
            return Ok(tasks.Select(FollowUpResponse.From).ToArray());
        }
        catch (DomainException exception)
        {
            return ConsultationProblemMapper.From(exception);
        }
    }

    [HttpPost("{taskId:guid}/reschedule")]
    public Task<IActionResult> Reschedule(
        Guid taskId,
        FollowUpSlotRequest request,
        CancellationToken cancellationToken) =>
        RunSlotCommandAsync(
            taskId,
            request,
            commands.RescheduleAsync,
            cancellationToken);

    [HttpPost("{taskId:guid}/reassign")]
    public Task<IActionResult> Reassign(
        Guid taskId,
        FollowUpSlotRequest request,
        CancellationToken cancellationToken) =>
        RunSlotCommandAsync(
            taskId,
            request,
            commands.ReassignAsync,
            cancellationToken);

    [HttpPost("{taskId:guid}/cancel")]
    public Task<IActionResult> Cancel(
        Guid taskId,
        FollowUpReasonRequest request,
        CancellationToken cancellationToken) =>
        RunReasonCommandAsync(
            taskId,
            request,
            commands.CancelAsync,
            cancellationToken);

    [HttpPost("{taskId:guid}/complete")]
    public Task<IActionResult> Complete(
        Guid taskId,
        FollowUpReasonRequest request,
        CancellationToken cancellationToken) =>
        RunReasonCommandAsync(
            taskId,
            request,
            commands.CompleteAsync,
            cancellationToken);

    private async Task<IActionResult> RunSlotCommandAsync(
        Guid taskId,
        FollowUpSlotRequest request,
        Func<
            ConsultationActor,
            Guid,
            Guid,
            string,
            CancellationToken,
            Task<FollowUpTask>> command,
        CancellationToken cancellationToken)
    {
        var actor = User.ToConsultationActor();
        if (actor is null)
        {
            return ConsultationProblemMapper.Forbidden();
        }

        try
        {
            return Ok(FollowUpResponse.From(await command(
                actor,
                taskId,
                request.AvailabilitySlotId,
                request.Reason ?? string.Empty,
                cancellationToken)));
        }
        catch (DomainException exception)
        {
            return ConsultationProblemMapper.From(exception);
        }
    }

    private async Task<IActionResult> RunReasonCommandAsync(
        Guid taskId,
        FollowUpReasonRequest request,
        Func<
            ConsultationActor,
            Guid,
            string,
            CancellationToken,
            Task<FollowUpTask>> command,
        CancellationToken cancellationToken)
    {
        var actor = User.ToConsultationActor();
        if (actor is null)
        {
            return ConsultationProblemMapper.Forbidden();
        }

        try
        {
            return Ok(FollowUpResponse.From(await command(
                actor,
                taskId,
                request.Reason ?? string.Empty,
                cancellationToken)));
        }
        catch (DomainException exception)
        {
            return ConsultationProblemMapper.From(exception);
        }
    }
}

public sealed record FollowUpSlotRequest(Guid AvailabilitySlotId, string? Reason);

public sealed record FollowUpReasonRequest(string? Reason);

public sealed record FollowUpResponse(
    Guid Id,
    Guid AssessmentId,
    string Status,
    Guid? AssigneeId,
    Guid? AvailabilitySlotId,
    DateTimeOffset? DueAt,
    DateTimeOffset? Deadline,
    string? ConflictCode,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt)
{
    public static FollowUpResponse From(FollowUpTask task) => new(
        task.Id,
        task.AssessmentId,
        task.Status.ToString(),
        task.AssigneeId,
        task.AvailabilitySlotId,
        task.DueAt,
        task.Deadline,
        task.ConflictCode,
        task.CompletedAt,
        task.CancelledAt);
}
