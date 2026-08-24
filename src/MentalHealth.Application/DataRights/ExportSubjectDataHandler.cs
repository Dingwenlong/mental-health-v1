using System.IO.Compression;
using System.Text.Json;
using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Application.Consultations;
using MentalHealth.Domain.Consents;
using MentalHealth.Domain.DataRights;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Application.DataRights;

public sealed record SubjectConsultationExport(
    Guid Id,
    string Kind,
    string Channel,
    string Status,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record SubjectMessageExport(
    Guid Id,
    Guid SessionId,
    string SenderKind,
    string Text,
    int Sequence,
    DateTimeOffset SentAt);

public sealed record SubjectTranscriptExport(
    Guid SessionId,
    int Revision,
    string Source,
    string Text,
    string Sha256,
    DateTimeOffset CreatedAt);

public sealed record SubjectConsentExport(
    Guid Id,
    ConsentKind Kind,
    string TextVersion,
    DateTimeOffset GrantedAt,
    DateTimeOffset? WithdrawnAt);

public sealed record SubjectAssessmentExport(
    Guid Id,
    Guid SessionId,
    decimal Score,
    string Level,
    decimal Confidence,
    bool IsCrisis,
    DateTimeOffset CreatedAt);

public sealed record SubjectFollowUpExport(
    Guid Id,
    Guid AssessmentId,
    string Status,
    DateTimeOffset? DueAt,
    DateTimeOffset? Deadline,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt);

public sealed record SubjectDataSnapshot(
    Guid SubjectId,
    IReadOnlyList<SubjectConsultationExport> Consultations,
    IReadOnlyList<SubjectMessageExport> Messages,
    IReadOnlyList<SubjectTranscriptExport> Transcripts,
    IReadOnlyList<SubjectConsentExport> Consents,
    IReadOnlyList<SubjectAssessmentExport> Assessments,
    IReadOnlyList<SubjectFollowUpExport> FollowUps);

public sealed record SubjectMediaReference(
    Guid AssetId,
    Guid SubjectId,
    string ContentType,
    string? ObjectKey,
    int ExpectedChunks,
    DateTimeOffset CapturedAt);

public sealed record SafeAuditRecord(
    DateTimeOffset OccurredAt,
    Guid ActorUserId,
    string Action,
    Guid ResourceId,
    string? Reason);

public interface IDataRightsRepository
{
    Task<SubjectDataSnapshot> ReadSubjectDataAsync(
        Guid subjectId,
        CancellationToken cancellationToken);

    Task<SubjectMediaReference?> FindOwnedMediaAsync(
        Guid subjectId,
        Guid assetId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SubjectMediaReference>> ListSubjectMediaAsync(
        Guid subjectId,
        CancellationToken cancellationToken);

    Task<DemoDataDeletion?> FindDeletionAsync(
        Guid subjectId,
        CancellationToken cancellationToken);

    void Add(DemoDataDeletion deletion);

    Task DeleteSubjectDataAsync(
        Guid subjectId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MentalHealth.Domain.Consultations.MediaAsset>>
        ListRetentionCandidatesAsync(
            DateTimeOffset capturedBefore,
            int maximumCount,
            CancellationToken cancellationToken);

    Task<IReadOnlyList<SafeAuditRecord>> ListAuditAsync(
        int maximumCount,
        CancellationToken cancellationToken);
}

public interface IMediaAccessTicketService
{
    string Create(Guid subjectId, Guid assetId, DateTimeOffset expiresAt);

    bool Validate(string ticket, Guid subjectId, Guid assetId);
}

public sealed record DataExportArchive(Stream Content, string FileName);

public sealed class ExportSubjectDataHandler(
    IDataRightsRepository dataRights,
    IObjectStorage storage,
    IClock clock)
{
    private static readonly JsonSerializerOptions JsonOptions = new(
        JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<DataExportArchive> HandleAsync(
        ConsultationActor actor,
        bool includeRawMedia,
        bool confirmRawMedia,
        CancellationToken cancellationToken)
    {
        var subjectId = actor.RequireOwnedSubject();
        if (includeRawMedia && !confirmRawMedia)
        {
            throw new DomainException("RAW_MEDIA_CONFIRMATION_REQUIRED");
        }

        var snapshot = await dataRights.ReadSubjectDataAsync(
            subjectId,
            cancellationToken);
        var output = new MemoryStream();
        try
        {
            using (var archive = new ZipArchive(
                       output,
                       ZipArchiveMode.Create,
                       leaveOpen: true))
            {
                await WriteJsonAsync(
                    archive,
                    "subject.json",
                    new
                    {
                        snapshot.SubjectId,
                        ExportedAt = clock.UtcNow
                    },
                    cancellationToken);
                await WriteJsonAsync(
                    archive,
                    "consultations.json",
                    snapshot.Consultations,
                    cancellationToken);
                await WriteJsonAsync(
                    archive,
                    "messages.json",
                    snapshot.Messages,
                    cancellationToken);
                await WriteJsonAsync(
                    archive,
                    "consents.json",
                    snapshot.Consents,
                    cancellationToken);
                await WriteJsonAsync(
                    archive,
                    "assessments.json",
                    snapshot.Assessments,
                    cancellationToken);
                await WriteJsonAsync(
                    archive,
                    "follow-ups.json",
                    snapshot.FollowUps,
                    cancellationToken);
                foreach (var transcript in snapshot.Transcripts)
                {
                    var entry = archive.CreateEntry(
                        $"transcripts/{transcript.SessionId:N}-{transcript.Revision:D4}.txt",
                        CompressionLevel.Optimal);
                    await using var target = entry.Open();
                    await using var writer = new StreamWriter(
                        target,
                        System.Text.Encoding.UTF8,
                        leaveOpen: false);
                    await writer.WriteAsync(
                        transcript.Text.AsMemory(),
                        cancellationToken);
                }

                if (includeRawMedia)
                {
                    var media = await dataRights.ListSubjectMediaAsync(
                        subjectId,
                        cancellationToken);
                    foreach (var item in media.Where(item =>
                                 !string.IsNullOrWhiteSpace(item.ObjectKey)))
                    {
                        await using var source = await storage.OpenReadAsync(
                            item.ObjectKey!,
                            cancellationToken);
                        var entry = archive.CreateEntry(
                            $"media/{item.AssetId:N}{ExtensionFor(item.ContentType)}",
                            CompressionLevel.NoCompression);
                        await using var target = entry.Open();
                        await source.CopyToAsync(target, cancellationToken);
                    }
                }
            }

            output.Position = 0;
            return new DataExportArchive(
                output,
                $"my-demo-data-{clock.UtcNow:yyyyMMddHHmmss}.zip");
        }
        catch
        {
            await output.DisposeAsync();
            throw;
        }
    }

    private static async Task WriteJsonAsync<T>(
        ZipArchive archive,
        string entryName,
        T value,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(
            stream,
            value,
            JsonOptions,
            cancellationToken);
    }

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "video/webm" => ".webm",
        "video/mp4" => ".mp4",
        "audio/wav" or "audio/x-wav" => ".wav",
        "audio/mpeg" => ".mp3",
        _ => ".media"
    };
}

public sealed record IssuedMediaTicket(string Ticket, DateTimeOffset ExpiresAt);

public sealed record ReadableMedia(Stream Content, string ContentType);

public sealed class MediaContentAccessHandler(
    IDataRightsRepository dataRights,
    IMediaAccessTicketService tickets,
    IObjectStorage storage,
    IClock clock)
{
    public async Task<IssuedMediaTicket> IssueAsync(
        ConsultationActor actor,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        var subjectId = actor.RequireOwnedSubject();
        var media = await dataRights.FindOwnedMediaAsync(
            subjectId,
            assetId,
            cancellationToken);
        if (media?.ObjectKey is null)
        {
            throw new DomainException("MEDIA_TICKET_INVALID");
        }

        var expiresAt = clock.UtcNow.AddMinutes(5);
        return new IssuedMediaTicket(
            tickets.Create(subjectId, assetId, expiresAt),
            expiresAt);
    }

    public async Task<ReadableMedia> OpenAsync(
        ConsultationActor actor,
        Guid assetId,
        string ticket,
        CancellationToken cancellationToken)
    {
        var subjectId = actor.RequireOwnedSubject();
        if (!tickets.Validate(ticket, subjectId, assetId))
        {
            throw new DomainException("MEDIA_TICKET_INVALID");
        }

        var media = await dataRights.FindOwnedMediaAsync(
            subjectId,
            assetId,
            cancellationToken);
        if (media?.ObjectKey is null)
        {
            throw new DomainException("MEDIA_TICKET_INVALID");
        }

        try
        {
            return new ReadableMedia(
                await storage.OpenReadAsync(media.ObjectKey, cancellationToken),
                media.ContentType);
        }
        catch (FileNotFoundException)
        {
            throw new DomainException("MEDIA_TICKET_INVALID");
        }
    }
}

public sealed class AuditQueryHandler(IDataRightsRepository dataRights)
{
    public Task<IReadOnlyList<SafeAuditRecord>> HandleAsync(
        int maximumCount,
        CancellationToken cancellationToken) =>
        dataRights.ListAuditAsync(
            Math.Clamp(maximumCount, 1, 200),
            cancellationToken);
}
