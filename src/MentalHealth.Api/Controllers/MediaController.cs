using MentalHealth.Api.Authorization;
using MentalHealth.Application.Consultations.Media;
using MentalHealth.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentalHealth.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/uploads")]
public sealed class MediaController(
    CreateUploadHandler create,
    WriteChunkHandler writeChunk,
    CompleteUploadHandler complete) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        CreateUploadRequest request,
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
                request.SessionId,
                request.ContentType,
                request.ExpectedChunks,
                request.IdempotencyKey,
                cancellationToken);
            var response = MediaUploadDto.From(result.Asset);
            return result.Created
                ? Created($"/api/v1/uploads/{result.Asset.Id}", response)
                : Ok(response);
        }
        catch (DomainException exception)
        {
            return ConsultationProblemMapper.From(exception);
        }
    }

    [HttpPut("{mediaAssetId:guid}/chunks/{index:int}")]
    [Consumes("application/octet-stream")]
    [RequestSizeLimit(WriteChunkHandler.MaximumChunkBytes + 1)]
    public async Task<IActionResult> PutChunk(
        Guid mediaAssetId,
        int index,
        CancellationToken cancellationToken)
    {
        var actor = User.ToConsultationActor();
        if (actor is null)
        {
            return ConsultationProblemMapper.Forbidden();
        }

        try
        {
            var result = await writeChunk.HandleAsync(
                actor,
                mediaAssetId,
                index,
                Request.Body,
                cancellationToken);
            return result.Created
                ? Created(
                    $"/api/v1/uploads/{mediaAssetId}/chunks/{index}",
                    result)
                : Ok(result);
        }
        catch (DomainException exception)
        {
            return ConsultationProblemMapper.From(exception);
        }
    }

    [HttpPost("{mediaAssetId:guid}/complete")]
    public async Task<IActionResult> Complete(
        Guid mediaAssetId,
        CompleteUploadRequest request,
        CancellationToken cancellationToken)
    {
        var actor = User.ToConsultationActor();
        if (actor is null)
        {
            return ConsultationProblemMapper.Forbidden();
        }

        try
        {
            var result = await complete.HandleAsync(
                actor,
                mediaAssetId,
                request.ExpectedSha256,
                request.IdempotencyKey,
                cancellationToken);
            return Ok(MediaUploadDto.From(result.Asset));
        }
        catch (DomainException exception)
        {
            return ConsultationProblemMapper.From(exception);
        }
    }
}

public sealed record CreateUploadRequest(
    Guid SessionId,
    string ContentType,
    int ExpectedChunks,
    string IdempotencyKey);

public sealed record CompleteUploadRequest(
    string ExpectedSha256,
    string IdempotencyKey);
