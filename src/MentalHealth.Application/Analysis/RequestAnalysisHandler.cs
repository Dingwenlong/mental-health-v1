using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Application.Consultations;
using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Application.Analysis;

public interface IManualTranscriptReader
{
    Task<ManualTranscript?> FindAsync(
        Guid sessionId,
        int? revision,
        CancellationToken cancellationToken);
}

public interface IAnalysisRepository : IManualTranscriptReader
{
    Task<AnalysisJob> GetOrCreateJobAsync(
        Guid sessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<int> GetLatestTranscriptRevisionAsync(
        Guid sessionId,
        CancellationToken cancellationToken);

    void Add(ManualTranscript transcript);
}

public sealed class RequestAnalysisHandler(
    IAnalysisRepository analyses,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<AnalysisJob> HandleAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return await analyses.GetOrCreateJobAsync(
            sessionId,
            clock.UtcNow,
            cancellationToken);
    }

    public async Task UseTranscriptAsync(
        Guid sessionId,
        int revision,
        CancellationToken cancellationToken)
    {
        var job = await HandleAsync(sessionId, cancellationToken);
        job.UseTranscript(revision, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkNeedsManualAsync(
        Guid sessionId,
        string failureCode,
        CancellationToken cancellationToken)
    {
        var job = await HandleAsync(sessionId, cancellationToken);
        job.MarkNeedsManual(failureCode, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordFailureAsync(
        Guid sessionId,
        string failureCode,
        bool terminal,
        CancellationToken cancellationToken)
    {
        var job = await HandleAsync(sessionId, cancellationToken);
        job.RecordFailure(failureCode, terminal, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SaveManualTranscriptHandler(
    SessionAccessService access,
    IAnalysisRepository analyses,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<ManualTranscript> HandleAsync(
        ConsultationActor actor,
        Guid sessionId,
        string source,
        string text,
        CancellationToken cancellationToken)
    {
        var sessionAccess = await access.DemandAsync(actor, sessionId, cancellationToken);
        if (sessionAccess.Session.Status != ConsultationStatus.Completed)
        {
            throw new DomainException("TRANSCRIPT_SESSION_NOT_COMPLETED");
        }

        if (!Enum.TryParse<TranscriptSource>(source?.Trim(), ignoreCase: false, out var parsedSource))
        {
            throw new DomainException("TRANSCRIPT_SOURCE_INVALID");
        }

        var latestRevision = await analyses.GetLatestTranscriptRevisionAsync(
            sessionId,
            cancellationToken);
        var expectedSource = latestRevision == 0
            ? TranscriptSource.ManualUpload
            : TranscriptSource.ManualCorrection;
        if (parsedSource != expectedSource)
        {
            throw new DomainException("TRANSCRIPT_SOURCE_INVALID");
        }

        var transcript = ManualTranscript.Create(
            sessionId,
            checked(latestRevision + 1),
            parsedSource,
            text,
            clock.UtcNow);
        analyses.Add(transcript);

        var job = await analyses.GetOrCreateJobAsync(
            sessionId,
            clock.UtcNow,
            cancellationToken);
        job.UseTranscript(transcript.Revision, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return transcript;
    }
}
