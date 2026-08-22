using MentalHealth.Contracts.Common;
using MentalHealth.Domain.Shared;
using Microsoft.AspNetCore.Mvc;

namespace MentalHealth.Api.Controllers;

internal static class ConsultationProblemMapper
{
    public static ObjectResult From(DomainException exception)
    {
        var status = exception.Code switch
        {
            ApiProblemCodes.ForbiddenResource => StatusCodes.Status403Forbidden,
            ApiProblemCodes.SessionNotFound or
                ApiProblemCodes.OrderNotFound or
                ApiProblemCodes.PlanNotAvailable or
                ApiProblemCodes.PractitionerNotFound => StatusCodes.Status404NotFound,
            ApiProblemCodes.IdempotencyConflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity
        };
        var title = exception.Code switch
        {
            ApiProblemCodes.ForbiddenResource => "你没有权限查看或修改这次咨询",
            ApiProblemCodes.SessionNotFound => "没有找到这次咨询",
            ApiProblemCodes.OrderNotFound => "没有找到这笔订单",
            ApiProblemCodes.OrderNotConfirmed => "请先确认订单",
            ApiProblemCodes.PlanNotAvailable => "这项服务已经不可用",
            ApiProblemCodes.PractitionerRequired => "请选择咨询师",
            ApiProblemCodes.PractitionerNotAvailable => "这名咨询师当前不可用",
            ApiProblemCodes.PractitionerNotAllowed => "AI 咨询不能分配咨询师",
            ApiProblemCodes.ConsentRequired => "请先完成本次咨询需要的授权",
            ApiProblemCodes.InvalidSessionState => "这次咨询现在不能执行该动作",
            ApiProblemCodes.MessageTextInvalid => "消息内容不能为空或过长",
            ApiProblemCodes.ClientMessageIdInvalid => "消息请求标识无效",
            ApiProblemCodes.MessageCursorInvalid => "消息位置无效",
            ApiProblemCodes.IdempotencyKeyInvalid => "请求标识无效",
            ApiProblemCodes.IdempotencyConflict => "相同请求标识对应了不同内容",
            _ => "无法完成这次咨询操作"
        };
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title
        };
        problem.Extensions["code"] = exception.Code;
        return new ObjectResult(problem) { StatusCode = status };
    }

    public static ObjectResult Forbidden() => From(
        new DomainException(ApiProblemCodes.ForbiddenResource));
}
