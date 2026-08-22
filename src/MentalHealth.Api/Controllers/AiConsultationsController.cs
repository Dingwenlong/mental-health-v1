using MentalHealth.Api.Authorization;
using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Application.Consultations;
using MentalHealth.Application.Consultations.Ai;
using MentalHealth.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentalHealth.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/consultations/{sessionId:guid}/ai-turns")]
public sealed class AiConsultationsController(
    SendAiTurnHandler handler,
    ILogger<AiConsultationsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Send(
        Guid sessionId,
        SendAiTurnRequest request,
        CancellationToken cancellationToken)
    {
        var actor = User.ToConsultationActor();
        if (actor is null)
        {
            return ConsultationProblemMapper.Forbidden();
        }

        try
        {
            var result = await handler.HandleAsync(
                actor,
                sessionId,
                request.Text,
                request.ClientMessageId,
                cancellationToken);
            if (!result.NotificationAccepted)
            {
                logger.LogWarning(
                    "A local notification could not be recorded for AI reply {ReplyId}.",
                    result.Reply.Id);
            }

            logger.LogInformation(
                "Stored AI reply {ReplyId} for session {SessionId} with user length {UserLength} and reply length {ReplyLength}",
                result.Reply.Id,
                sessionId,
                result.UserMessage.Text.Length,
                result.Reply.Text.Length);
            var response = AiTurnDto.From(result);
            return result.Created
                ? Created(
                    $"/api/v1/consultations/{sessionId}/messages/{result.Reply.Id}",
                    response)
                : Ok(response);
        }
        catch (DomainException exception)
        {
            return ConsultationProblemMapper.From(exception);
        }
        catch (ProviderException exception)
        {
            return ConsultationProblemMapper.From(exception);
        }
    }
}

public sealed record SendAiTurnRequest(string Text, string ClientMessageId);

public sealed record AiTurnDto(
    ChatMessageDto UserMessage,
    ChatMessageDto Reply,
    string RuleId,
    bool IsCrisis)
{
    public static AiTurnDto From(AiTurnResult result) => new(
        ChatMessageDto.From(result.UserMessage),
        ChatMessageDto.From(result.Reply),
        result.RuleId,
        result.IsCrisis);
}
