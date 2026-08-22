using System.Security.Claims;
using MentalHealth.Api.Authorization;
using MentalHealth.Application.Catalog;
using MentalHealth.Contracts.Common;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentalHealth.Api.Controllers;

[ApiController]
[Authorize(Policy = Policies.OperationsAdmin)]
[Route("api/v1/admin/catalog")]
public sealed class AdminCatalogController(AdminCatalogHandler handler)
    : ControllerBase
{
    [HttpPost("plans")]
    public async Task<IActionResult> CreatePlan(
        ServicePlanRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var actorUserId))
        {
            return CatalogProblem(
                StatusCodes.Status403Forbidden,
                ApiProblemCodes.ForbiddenResource,
                "无权修改服务目录");
        }

        if (!TryPlanInput(request, out var input))
        {
            return InvalidCatalogValue();
        }

        try
        {
            var plan = await handler.CreatePlanAsync(
                actorUserId,
                input,
                cancellationToken);
            return Created(
                $"/api/v1/admin/catalog/plans/{plan.Id}",
                ServicePlanDto.From(plan));
        }
        catch (DomainException exception)
        {
            return DomainProblem(exception);
        }
    }

    [HttpPut("plans/{planId:guid}")]
    public async Task<IActionResult> UpdatePlan(
        Guid planId,
        ServicePlanRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var actorUserId))
        {
            return CatalogProblem(
                StatusCodes.Status403Forbidden,
                ApiProblemCodes.ForbiddenResource,
                "无权修改服务目录");
        }

        if (!TryPlanInput(request, out var input))
        {
            return InvalidCatalogValue();
        }

        try
        {
            var plan = await handler.UpdatePlanAsync(
                actorUserId,
                planId,
                input,
                cancellationToken);
            return plan is null ? NotFound() : Ok(ServicePlanDto.From(plan));
        }
        catch (DomainException exception)
        {
            return DomainProblem(exception);
        }
    }

    [HttpDelete("plans/{planId:guid}")]
    public async Task<IActionResult> DeactivatePlan(
        Guid planId,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var actorUserId))
        {
            return CatalogProblem(
                StatusCodes.Status403Forbidden,
                ApiProblemCodes.ForbiddenResource,
                "无权修改服务目录");
        }

        try
        {
            return await handler.DeactivatePlanAsync(
                actorUserId,
                planId,
                cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (DomainException exception)
        {
            return DomainProblem(exception);
        }
    }

    [HttpPost("practitioners")]
    public async Task<IActionResult> CreatePractitioner(
        PractitionerRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var actorUserId))
        {
            return CatalogProblem(
                StatusCodes.Status403Forbidden,
                ApiProblemCodes.ForbiddenResource,
                "无权修改人员目录");
        }

        if (!Enum.TryParse<PractitionerRole>(request.Role, true, out var role)
            || !Enum.IsDefined(role))
        {
            return InvalidCatalogValue();
        }

        try
        {
            var practitioner = await handler.CreatePractitionerAsync(
                actorUserId,
                new PractitionerInput(request.DisplayName, role),
                cancellationToken);
            return Created(
                $"/api/v1/admin/catalog/practitioners/{practitioner.Id}",
                ToDto(practitioner));
        }
        catch (DomainException exception)
        {
            return DomainProblem(exception);
        }
    }

    [HttpPut("practitioners/{practitionerId:guid}")]
    public async Task<IActionResult> UpdatePractitioner(
        Guid practitionerId,
        PractitionerRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var actorUserId))
        {
            return CatalogProblem(
                StatusCodes.Status403Forbidden,
                ApiProblemCodes.ForbiddenResource,
                "无权修改人员目录");
        }

        if (!Enum.TryParse<PractitionerRole>(request.Role, true, out var role)
            || !Enum.IsDefined(role))
        {
            return InvalidCatalogValue();
        }

        try
        {
            var practitioner = await handler.UpdatePractitionerAsync(
                actorUserId,
                practitionerId,
                new PractitionerInput(request.DisplayName, role),
                cancellationToken);
            return practitioner is null ? NotFound() : Ok(ToDto(practitioner));
        }
        catch (DomainException exception)
        {
            return DomainProblem(exception);
        }
    }

    [HttpDelete("practitioners/{practitionerId:guid}")]
    public async Task<IActionResult> DeactivatePractitioner(
        Guid practitionerId,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var actorUserId))
        {
            return CatalogProblem(
                StatusCodes.Status403Forbidden,
                ApiProblemCodes.ForbiddenResource,
                "无权修改人员目录");
        }

        try
        {
            return await handler.DeactivatePractitionerAsync(
                actorUserId,
                practitionerId,
                cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (DomainException exception)
        {
            return DomainProblem(exception);
        }
    }

    [HttpPost("practitioners/{practitionerId:guid}/slots")]
    public async Task<IActionResult> CreateSlot(
        Guid practitionerId,
        AvailabilitySlotRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var actorUserId))
        {
            return CatalogProblem(
                StatusCodes.Status403Forbidden,
                ApiProblemCodes.ForbiddenResource,
                "无权修改人员时段");
        }

        try
        {
            var slot = await handler.CreateSlotAsync(
                actorUserId,
                practitionerId,
                request.StartAt,
                request.EndAt,
                cancellationToken);
            return Created(
                $"/api/v1/admin/catalog/practitioners/{practitionerId}/slots/{slot.Id}",
                AvailabilitySlotDto.From(slot));
        }
        catch (DomainException exception)
        {
            return DomainProblem(exception);
        }
    }

    [HttpDelete("practitioners/{practitionerId:guid}/slots/{slotId:guid}")]
    public async Task<IActionResult> DeactivateSlot(
        Guid practitionerId,
        Guid slotId,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var actorUserId))
        {
            return CatalogProblem(
                StatusCodes.Status403Forbidden,
                ApiProblemCodes.ForbiddenResource,
                "无权修改人员时段");
        }

        return await handler.DeactivateSlotAsync(
            actorUserId,
            practitionerId,
            slotId,
            cancellationToken)
            ? NoContent()
            : NotFound();
    }

    private static PractitionerDto ToDto(Practitioner practitioner) => new(
        practitioner.Id,
        practitioner.DisplayName,
        practitioner.Role.ToString(),
        practitioner.Active,
        []);

    private static bool TryPlanInput(
        ServicePlanRequest request,
        out ServicePlanInput input)
    {
        input = null!;
        if (!TryConsultationKind(request.Kind, out var kind)
            || !Enum.TryParse<ConsultationChannel>(
                request.Channel,
                true,
                out var channel)
            || !Enum.IsDefined(channel)
            || !Enum.TryParse<PlanPaymentMode>(
                request.PaymentMode,
                true,
                out var paymentMode)
            || !Enum.IsDefined(paymentMode))
        {
            return false;
        }

        input = new ServicePlanInput(
            request.Name,
            kind,
            channel,
            paymentMode,
            request.PriceInMinorUnits,
            request.Currency,
            request.DurationMinutes);
        return true;
    }

    private static bool TryConsultationKind(
        string value,
        out ConsultationKind kind)
    {
        if (string.Equals(value, "Human", StringComparison.OrdinalIgnoreCase))
        {
            kind = ConsultationKind.Human;
            return true;
        }

        if (string.Equals(value, "Ai", StringComparison.OrdinalIgnoreCase))
        {
            kind = ConsultationKind.AiVirtual;
            return true;
        }

        kind = default;
        return false;
    }

    private bool TryActor(out Guid actorUserId) =>
        Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out actorUserId);

    private ObjectResult DomainProblem(DomainException exception)
    {
        var status = exception.Code switch
        {
            ApiProblemCodes.AvailabilitySlotConflict =>
                StatusCodes.Status409Conflict,
            ApiProblemCodes.PractitionerNotFound =>
                StatusCodes.Status404NotFound,
            _ => StatusCodes.Status422UnprocessableEntity
        };
        var title = exception.Code switch
        {
            ApiProblemCodes.PlanCombinationUnsupported =>
                "v1 不支持 AI 视频咨询",
            ApiProblemCodes.AvailabilitySlotConflict =>
                "这个时段与现有时段重叠",
            ApiProblemCodes.PractitionerRoleLocked =>
                "已绑定登录账户，不能单独修改人员角色",
            ApiProblemCodes.PractitionerNotFound =>
                "没有找到这名人员",
            "PLAN_PRICE_INVALID" => "套餐价格设置不正确",
            "PLAN_DURATION_INVALID" => "套餐时长设置不正确",
            "AVAILABILITY_SLOT_RANGE_INVALID" => "时段起止时间不正确",
            _ => "请检查目录配置"
        };
        return CatalogProblem(status, exception.Code, title);
    }

    private ObjectResult InvalidCatalogValue() => CatalogProblem(
        StatusCodes.Status422UnprocessableEntity,
        ApiProblemCodes.CatalogValueInvalid,
        "目录配置值无效");

    private ObjectResult CatalogProblem(int status, string code, string title)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title
        };
        problem.Extensions["code"] = code;
        return new ObjectResult(problem) { StatusCode = status };
    }
}

public sealed record ServicePlanRequest(
    string Name,
    string Kind,
    string Channel,
    string PaymentMode,
    long PriceInMinorUnits,
    string Currency,
    int DurationMinutes);

public sealed record PractitionerRequest(string DisplayName, string Role);

public sealed record AvailabilitySlotRequest(
    DateTimeOffset StartAt,
    DateTimeOffset EndAt);
