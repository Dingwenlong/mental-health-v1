using System.Security.Cryptography;
using System.Text;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.Analysis;

public enum AnalysisJobStatus
{
    Pending,
    Ready,
    Processing,
    NeedsManual,
    Completed
}

public enum TranscriptSource
{
    ManualUpload,
    ManualCorrection
}

public sealed class AnalysisJob
{
    private AnalysisJob()
    {
    }

    private AnalysisJob(Guid sessionId, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        SessionId = sessionId;
        Status = AnalysisJobStatus.Pending;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid SessionId { get; private set; }

    public AnalysisJobStatus Status { get; private set; }

    public int? TranscriptRevision { get; private set; }

    public int Attempts { get; private set; }

    public string? FailureCode { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public Guid? AssessmentId { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public static AnalysisJob Request(Guid sessionId, DateTimeOffset now)
    {
        if (sessionId == Guid.Empty)
        {
            throw new DomainException("SESSION_REFERENCE_INVALID");
        }

        return new AnalysisJob(sessionId, now);
    }

    public void UseTranscript(int revision, DateTimeOffset now)
    {
        if (revision < 1)
        {
            throw new DomainException("TRANSCRIPT_REVISION_INVALID");
        }

        TranscriptRevision = revision;
        Status = AnalysisJobStatus.Ready;
        Attempts = 0;
        FailureCode = null;
        AssessmentId = null;
        CompletedAt = null;
        UpdatedAt = now;
    }

    public void MarkNeedsManual(string failureCode, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        Status = AnalysisJobStatus.NeedsManual;
        FailureCode = failureCode.Trim();
        UpdatedAt = now;
    }

    public void RecordFailure(string failureCode, bool terminal, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        Attempts = checked(Attempts + 1);
        FailureCode = failureCode.Trim();
        Status = terminal ? AnalysisJobStatus.NeedsManual : AnalysisJobStatus.Pending;
        UpdatedAt = now;
    }

    public void Complete(Guid assessmentId, DateTimeOffset now)
    {
        if (assessmentId == Guid.Empty || Status != AnalysisJobStatus.Ready)
        {
            throw new DomainException("ANALYSIS_JOB_NOT_READY");
        }

        AssessmentId = assessmentId;
        Status = AnalysisJobStatus.Completed;
        CompletedAt = now;
        UpdatedAt = now;
    }
}

public sealed class ManualTranscript
{
    private ManualTranscript()
    {
    }

    private ManualTranscript(
        Guid sessionId,
        int revision,
        TranscriptSource source,
        string text,
        string sha256,
        DateTimeOffset createdAt)
    {
        SessionId = sessionId;
        Revision = revision;
        Source = source;
        Text = text;
        Sha256 = sha256;
        CreatedAt = createdAt;
    }

    public Guid SessionId { get; private set; }

    public int Revision { get; private set; }

    public TranscriptSource Source { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public string Sha256 { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static ManualTranscript Create(
        Guid sessionId,
        int revision,
        TranscriptSource source,
        string text,
        DateTimeOffset createdAt)
    {
        if (sessionId == Guid.Empty || revision < 1)
        {
            throw new DomainException("TRANSCRIPT_REFERENCE_INVALID");
        }

        var normalizedText = text?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedText) || normalizedText.Length > 200_000)
        {
            throw new DomainException("TRANSCRIPT_TEXT_INVALID");
        }

        var sha256 = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(normalizedText)));
        return new ManualTranscript(
            sessionId,
            revision,
            source,
            normalizedText,
            sha256,
            createdAt);
    }
}
