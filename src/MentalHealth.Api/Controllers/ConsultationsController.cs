using MentalHealth.Api.Authorization;
using MentalHealth.Application.Consultations;
using MentalHealth.Application.Security;
using MentalHealth.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentalHealth.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/consultations")]
public sealed class ConsultationsController(
    CreateConsultationHandler create,
    StartConsultationHandler start,
    CompleteConsultationHandler complete) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = AppRoles.User)]
    public async Task<IActionResult> Create(
        CreateConsultationRequest request,
        CancellationToken cancellationToken)
    {
        var actor = User.ToConsultationActor();
        if (actor is null)
        {
            return ConsultationProblemMapper.Forbidden();
        }

        try
        {
            var result = await create.HandleAsync(
                actor,
                request.OrderId,
                request.AssignedPractitionerId,
                request.ScheduledAt,
                request.IdempotencyKey,
                cancellationToken);
            var response = ConsultationDto.From(result.Session);
            return result.Created
                ? Created($"/api/v1/consultations/{result.Session.Id}", response)
                : Ok(response);
        }
        catch (DomainException exception)
        {
            return ConsultationProblemMapper.From(exception);
        }
    }

    [HttpPost("{sessionId:guid}/start")]
    public async Task<IActionResult> Start(
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
            var session = await start.HandleAsync(
                actor,
                sessionId,
                cancellationToken);
            return Ok(ConsultationDto.From(session));
        }
        catch (DomainException exception)
        {
            return ConsultationProblemMapper.From(exception);
        }
    }

    [HttpPost("{sessionId:guid}/complete")]
    public async Task<IActionResult> Complete(
        Guid sessionId,
        CompleteConsultationRequest request,
        CancellationToken cancellationToken)
    {
        var actor = User.ToConsultationActor();
        if (actor is null)
        {
            return ConsultationProblemMapper.Forbidden();
        }

        try
        {
            var session = await complete.HandleAsync(
                actor,
                sessionId,
                request.IdempotencyKey,
                cancellationToken);
            return Ok(ConsultationDto.From(session));
        }
        catch (DomainException exception)
        {
            return ConsultationProblemMapper.From(exception);
        }
    }
}

public sealed record CreateConsultationRequest(
    Guid OrderId,
    Guid? AssignedPractitionerId,
    DateTimeOffset ScheduledAt,
    string IdempotencyKey);

public sealed record CompleteConsultationRequest(string IdempotencyKey);
