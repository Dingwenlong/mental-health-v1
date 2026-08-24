using MentalHealth.Api.Authorization;
using MentalHealth.Application.DataRights;
using MentalHealth.Application.Security;
using MentalHealth.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentalHealth.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/data-rights")]
public sealed class DataRightsController(
    ExportSubjectDataHandler export,
    DeleteDemoSubjectHandler delete,
    AuditQueryHandler audit) : ControllerBase
{
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] bool includeRawMedia = false,
        [FromQuery] bool confirmRawMedia = false,
        CancellationToken cancellationToken = default)
    {
        var actor = User.ToConsultationActor();
        if (actor is null)
        {
            return ConsultationProblemMapper.Forbidden();
        }

        try
        {
            var result = await export.HandleAsync(
                actor,
                includeRawMedia,
                confirmRawMedia,
                cancellationToken);
            return File(result.Content, "application/zip", result.FileName);
        }
        catch (DomainException exception)
        {
            return ConsultationProblemMapper.From(exception);
        }
    }

    [HttpDelete("demo-data")]
    public async Task<IActionResult> DeleteDemoData(
        CancellationToken cancellationToken)
    {
        var actor = User.ToConsultationActor();
        if (actor is null)
        {
            return ConsultationProblemMapper.Forbidden();
        }

        try
        {
            await delete.HandleAsync(actor, cancellationToken);
            return NoContent();
        }
        catch (DomainException exception)
        {
            return ConsultationProblemMapper.From(exception);
        }
    }

    [HttpGet("audit")]
    [Authorize(Roles = AppRoles.OperationsAdmin)]
    public async Task<IActionResult> ListAudit(
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var records = await audit.HandleAsync(limit, cancellationToken);
        return Ok(records);
    }
}
