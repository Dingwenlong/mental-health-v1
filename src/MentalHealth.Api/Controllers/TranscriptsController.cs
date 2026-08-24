using MentalHealth.Api.Authorization;
using MentalHealth.Application.Analysis;
using MentalHealth.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentalHealth.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/consultations/{sessionId:guid}/transcript")]
public sealed class TranscriptsController(SaveManualTranscriptHandler save)
    : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateRevision(
        Guid sessionId,
        SaveTranscriptRequest request,
        CancellationToken cancellationToken)
    {
        var actor = User.ToConsultationActor();
        if (actor is null)
        {
            return ConsultationProblemMapper.Forbidden();
        }

        try
        {
            var transcript = await save.HandleAsync(
                actor,
                sessionId,
                request.Source,
                request.Text,
                cancellationToken);
            return Created(
                $"/api/v1/consultations/{sessionId}/transcript?revision={transcript.Revision}",
                new TranscriptRevisionResponse(
                    transcript.SessionId,
                    transcript.Revision,
                    transcript.Source.ToString(),
                    transcript.Text,
                    transcript.Sha256,
                    transcript.CreatedAt));
        }
        catch (DomainException exception)
        {
            return ConsultationProblemMapper.From(exception);
        }
    }
}

public sealed record SaveTranscriptRequest(string Source, string Text);

public sealed record TranscriptRevisionResponse(
    Guid SessionId,
    int Revision,
    string Source,
    string Text,
    string Sha256,
    DateTimeOffset CreatedAt);
