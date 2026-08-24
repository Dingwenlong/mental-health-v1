using MentalHealth.Api.Authorization;
using MentalHealth.Application.DataRights;
using MentalHealth.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentalHealth.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/media")]
public sealed class MediaContentController(MediaContentAccessHandler access)
    : ControllerBase
{
    [HttpPost("{assetId:guid}/ticket")]
    public async Task<IActionResult> IssueTicket(
        Guid assetId,
        CancellationToken cancellationToken)
    {
        var actor = User.ToConsultationActor();
        if (actor is null)
        {
            return ConsultationProblemMapper.Forbidden();
        }

        try
        {
            return Ok(await access.IssueAsync(
                actor,
                assetId,
                cancellationToken));
        }
        catch (DomainException exception)
        {
            return ConsultationProblemMapper.From(exception);
        }
    }

    [HttpGet("{assetId:guid}/content")]
    public async Task<IActionResult> Read(
        Guid assetId,
        [FromQuery] string ticket,
        CancellationToken cancellationToken)
    {
        var actor = User.ToConsultationActor();
        if (actor is null)
        {
            return ConsultationProblemMapper.Forbidden();
        }

        try
        {
            var media = await access.OpenAsync(
                actor,
                assetId,
                ticket,
                cancellationToken);
            return File(media.Content, media.ContentType);
        }
        catch (DomainException exception)
        {
            return ConsultationProblemMapper.From(exception);
        }
    }
}
