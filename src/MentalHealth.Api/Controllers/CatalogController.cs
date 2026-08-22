using MentalHealth.Application.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentalHealth.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/catalog")]
public sealed class CatalogController(CatalogQueryHandler queries) : ControllerBase
{
    [HttpGet("plans")]
    public async Task<IReadOnlyList<ServicePlanDto>> ListPlans(
        CancellationToken cancellationToken) =>
        await queries.ListPlansAsync(cancellationToken);

    [HttpGet("practitioners")]
    public async Task<IReadOnlyList<PractitionerDto>> ListPractitioners(
        CancellationToken cancellationToken) =>
        await queries.ListPractitionersAsync(cancellationToken);
}
