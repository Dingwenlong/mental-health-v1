using System.Security.Claims;
using MentalHealth.Application.Catalog;
using MentalHealth.Contracts.Common;
using MentalHealth.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentalHealth.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/orders")]
public sealed class OrdersController(OrderHandler handler) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var actorUserId, out var subjectId))
        {
            return OrderProblem(
                StatusCodes.Status403Forbidden,
                ApiProblemCodes.ForbiddenResource,
                "无权创建这笔订单");
        }

        try
        {
            var result = await handler.CreateAsync(
                actorUserId,
                subjectId,
                request.PlanId,
                request.IdempotencyKey,
                cancellationToken);
            var response = DemoOrderDto.From(result.Order);
            return result.Created
                ? Created($"/api/v1/orders/{result.Order.Id}", response)
                : Ok(response);
        }
        catch (DomainException exception)
        {
            return DomainProblem(exception);
        }
    }

    [HttpPost("{orderId:guid}/confirm")]
    public async Task<IActionResult> Confirm(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var actorUserId, out var subjectId))
        {
            return OrderProblem(
                StatusCodes.Status403Forbidden,
                ApiProblemCodes.ForbiddenResource,
                "无权确认这笔订单");
        }

        try
        {
            var order = await handler.ConfirmAsync(
                actorUserId,
                subjectId,
                orderId,
                cancellationToken);
            return order is null
                ? OrderProblem(
                    StatusCodes.Status404NotFound,
                    ApiProblemCodes.OrderNotFound,
                    "没有找到这笔订单")
                : Ok(DemoOrderDto.From(order));
        }
        catch (DomainException exception)
        {
            return DomainProblem(exception);
        }
    }

    private bool TryActor(out Guid actorUserId, out Guid subjectId)
    {
        actorUserId = Guid.Empty;
        subjectId = Guid.Empty;
        return Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out actorUserId)
            && Guid.TryParse(User.FindFirstValue("subject_id"), out subjectId);
    }

    private ObjectResult DomainProblem(DomainException exception)
    {
        var status = exception.Code == ApiProblemCodes.PlanNotAvailable
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status422UnprocessableEntity;
        var title = exception.Code switch
        {
            ApiProblemCodes.PlanNotAvailable => "这个套餐当前不可用",
            ApiProblemCodes.IdempotencyKeyInvalid => "订单请求标识无效",
            "DEMO_PAYMENT_DECLINED" => "模拟支付未确认",
            _ => "无法完成这次订单操作"
        };
        return OrderProblem(status, exception.Code, title);
    }

    private ObjectResult OrderProblem(int status, string code, string title)
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

public sealed record CreateOrderRequest(Guid PlanId, string IdempotencyKey);
