using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.Consultations;

public enum MediaAssetStatus
{
    Uploading,
    Completed,
    Expired
}

public sealed class MediaAsset
{
    public const int MaximumExpectedChunks = 4096;

    private MediaAsset()
    {
    }

    private MediaAsset(
        Guid sessionId,
        Guid subjectId,
        Guid createdByUserId,
        string contentType,
        int expectedChunks,
        string creationIdempotencyKey,
        DateTimeOffset capturedAt)
    {
        Id = Guid.NewGuid();
        SessionId = sessionId;
        SubjectId = subjectId;
        CreatedByUserId = createdByUserId;
        ContentType = contentType;
        ExpectedChunks = expectedChunks;
        CreationIdempotencyKey = creationIdempotencyKey;
        CapturedAt = capturedAt;
        UploadExpiresAt = capturedAt.AddHours(24);
        IsDemo = true;
        Status = MediaAssetStatus.Uploading;
    }

    public Guid Id { get; private set; }

    public Guid SessionId { get; private set; }

    public Guid SubjectId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public string ContentType { get; private set; } = string.Empty;

    public int ExpectedChunks { get; private set; }

    public string CreationIdempotencyKey { get; private set; } = string.Empty;

    public MediaAssetStatus Status { get; private set; }

    public string? ObjectKey { get; private set; }

    public string? Sha256 { get; private set; }

    public long? Length { get; private set; }

    public DateTimeOffset CapturedAt { get; private set; }

    public DateTimeOffset UploadExpiresAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? ChunksDeletedAt { get; private set; }

    public string? CompletionIdempotencyKey { get; private set; }

    public bool IsDemo { get; private set; }

    public static MediaAsset Create(
        Guid sessionId,
        Guid subjectId,
        Guid createdByUserId,
        string contentType,
        int expectedChunks,
        string creationIdempotencyKey,
        DateTimeOffset capturedAt)
    {
        if (sessionId == Guid.Empty
            || subjectId == Guid.Empty
            || createdByUserId == Guid.Empty)
        {
            throw new DomainException("MEDIA_REFERENCE_INVALID");
        }

        var normalizedContentType = NormalizeContentType(contentType);
        if (expectedChunks is < 1 or > MaximumExpectedChunks)
        {
            throw new DomainException("MEDIA_CHUNK_COUNT_INVALID");
        }

        return new MediaAsset(
            sessionId,
            subjectId,
            createdByUserId,
            normalizedContentType,
            expectedChunks,
            NormalizeIdempotencyKey(creationIdempotencyKey),
            capturedAt.ToUniversalTime());
    }

    public void EnsureChunkCanBeWritten(int index, DateTimeOffset now)
    {
        EnsureActiveUpload(now);
        if (index < 0 || index >= ExpectedChunks)
        {
            throw new DomainException("INVALID_CHUNK_INDEX");
        }
    }

    public void EnsureCanComplete(DateTimeOffset now) => EnsureActiveUpload(now);

    public bool ExpireIfDue(DateTimeOffset now)
    {
        if (Status == MediaAssetStatus.Expired)
        {
            return false;
        }

        if (Status != MediaAssetStatus.Uploading || now < UploadExpiresAt)
        {
            return false;
        }

        Status = MediaAssetStatus.Expired;
        return true;
    }

    public void MarkExpiredChunksDeleted(DateTimeOffset deletedAt)
    {
        if (Status != MediaAssetStatus.Expired)
        {
            throw new DomainException("INVALID_MEDIA_STATE");
        }

        ChunksDeletedAt ??= deletedAt.ToUniversalTime();
    }

    private void EnsureActiveUpload(DateTimeOffset now)
    {
        if (Status == MediaAssetStatus.Expired || now >= UploadExpiresAt)
        {
            throw new DomainException("MEDIA_UPLOAD_EXPIRED");
        }

        if (Status != MediaAssetStatus.Uploading)
        {
            throw new DomainException("INVALID_MEDIA_STATE");
        }
    }

    public bool IsCompletionReplay(
        string expectedSha256,
        string idempotencyKey)
    {
        var normalizedKey = NormalizeIdempotencyKey(idempotencyKey);
        if (Status != MediaAssetStatus.Completed)
        {
            return false;
        }

        if (string.Equals(
                CompletionIdempotencyKey,
                normalizedKey,
                StringComparison.Ordinal)
            && string.Equals(
                Sha256,
                expectedSha256?.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        throw new DomainException("IDEMPOTENCY_CONFLICT");
    }

    public void Complete(
        string objectKey,
        string sha256,
        long length,
        string idempotencyKey,
        DateTimeOffset completedAt)
    {
        if (Status != MediaAssetStatus.Uploading)
        {
            throw new DomainException("INVALID_MEDIA_STATE");
        }

        if (string.IsNullOrWhiteSpace(objectKey)
            || objectKey.Length > 500
            || string.IsNullOrWhiteSpace(sha256)
            || sha256.Length != 64
            || length <= 0)
        {
            throw new DomainException("MEDIA_COMPLETION_INVALID");
        }

        ObjectKey = objectKey;
        Sha256 = sha256.ToLowerInvariant();
        Length = length;
        CompletionIdempotencyKey = NormalizeIdempotencyKey(idempotencyKey);
        CompletedAt = completedAt.ToUniversalTime();
        Status = MediaAssetStatus.Completed;
    }

    public static string NormalizeContentType(string contentType)
    {
        var normalized = contentType?.Split(';', 2)[0].Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.Length > 100
            || !(normalized.StartsWith("audio/", StringComparison.Ordinal)
                || normalized.StartsWith("video/", StringComparison.Ordinal)))
        {
            throw new DomainException("MEDIA_CONTENT_TYPE_INVALID");
        }

        return normalized;
    }

    public static string NormalizeIdempotencyKey(string idempotencyKey)
    {
        var normalized = idempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 100)
        {
            throw new DomainException("IDEMPOTENCY_KEY_INVALID");
        }

        return normalized;
    }
}
