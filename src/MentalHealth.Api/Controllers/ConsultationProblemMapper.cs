using MentalHealth.Contracts.Common;
using MentalHealth.Application.Abstractions.Providers;
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
                ApiProblemCodes.PractitionerNotFound or
                ApiProblemCodes.MediaNotFound or
                ApiProblemCodes.ResultNotFound => StatusCodes.Status404NotFound,
            ApiProblemCodes.IdempotencyConflict or
                ApiProblemCodes.MediaChunkConflict => StatusCodes.Status409Conflict,
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
            ApiProblemCodes.TranscriptSourceInvalid => "转写来源不符合当前版本",
            ApiProblemCodes.TranscriptTextInvalid => "转写内容不能为空或过长",
            ApiProblemCodes.TranscriptSessionNotCompleted => "咨询结束后才能提交转写",
            ApiProblemCodes.MessageTextInvalid => "消息内容不能为空或过长",
            ApiProblemCodes.ClientMessageIdInvalid => "消息请求标识无效",
            ApiProblemCodes.MessageCursorInvalid => "消息位置无效",
            ApiProblemCodes.MediaNotFound => "没有找到这次媒体上传",
            ApiProblemCodes.ResultNotFound => "这次咨询还没有关注指数结果",
            ApiProblemCodes.MediaChunkConflict => "这个分块已存在但内容不同",
            ApiProblemCodes.MediaChunkMissing => "还有分块没有上传",
            ApiProblemCodes.MediaHashMismatch => "媒体摘要不一致",
            ApiProblemCodes.InvalidChunkIndex => "分块编号无效",
            ApiProblemCodes.MediaChunkCountInvalid => "分块数量无效",
            ApiProblemCodes.MediaChunkTooLarge => "这个分块超过大小限制",
            ApiProblemCodes.MediaChunkEmpty => "不能上传空分块",
            ApiProblemCodes.MediaContentTypeInvalid => "媒体类型无效",
            ApiProblemCodes.MediaUploadExpired => "这次媒体上传已经过期",
            ApiProblemCodes.InvalidMediaState => "这次媒体上传现在不能执行该动作",
            ApiProblemCodes.VideoSessionRequired => "只有视频咨询可以上传媒体",
            ApiProblemCodes.AiChatSessionRequired => "只有 AI 文字咨询可以发送这条消息",
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

    public static ObjectResult From(ProviderException exception)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "暂时无法生成回复，请稍后重试"
        };
        problem.Extensions["code"] = exception.Code;
        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status503ServiceUnavailable
        };
    }
}
