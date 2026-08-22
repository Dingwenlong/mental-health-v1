using MentalHealth.Api.Authorization;
using MentalHealth.Application.Consultations;
using MentalHealth.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentalHealth.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/consultations/{sessionId:guid}/messages")]
public sealed class MessagesController(
    SendMessageHandler messages,
    ILogger<MessagesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        Guid sessionId,
        CancellationToken cancellationToken,
        [FromQuery] int afterSequence = 0)
    {
        var actor = User.ToConsultationActor();
        if (actor is null)
        {
            return ConsultationProblemMapper.Forbidden();
        }

        try
        {
            var result = await messages.ListAsync(
                actor,
                sessionId,
                afterSequence,
                cancellationToken);
            return Ok(result.Select(ChatMessageDto.From));
        }
        catch (DomainException exception)
        {
            return ConsultationProblemMapper.From(exception);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Send(
        Guid sessionId,
        SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var actor = User.ToConsultationActor();
        if (actor is null)
        {
            return ConsultationProblemMapper.Forbidden();
        }

        try
        {
            var result = await messages.HandleAsync(
                actor,
                sessionId,
                request.Text,
                request.ClientMessageId,
                cancellationToken);
            logger.LogInformation(
                "Stored message {MessageId} for session {SessionId} with sequence {Sequence} and Length {Length}",
                result.Message.Id,
                result.Message.SessionId,
                result.Message.Sequence,
                result.Message.Text.Length);
            var response = ChatMessageDto.From(result.Message);
            return result.Created
                ? Created(
                    $"/api/v1/consultations/{sessionId}/messages/{result.Message.Id}",
                    response)
                : Ok(response);
        }
        catch (DomainException exception)
        {
            return ConsultationProblemMapper.From(exception);
        }
    }
}

public sealed record SendMessageRequest(string Text, string ClientMessageId);
