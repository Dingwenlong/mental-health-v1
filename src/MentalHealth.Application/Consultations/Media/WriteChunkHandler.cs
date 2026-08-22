using System.Security.Cryptography;
using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Application.Consultations.Media;

public sealed class WriteChunkHandler(
    IMediaAssetRepository mediaAssets,
    MediaSessionAccessService access,
    IObjectStorage storage,
    IClock clock)
{
    public const long MaximumChunkBytes = 4 * 1024 * 1024;

    public async Task<ChunkWriteResult> HandleAsync(
        ConsultationActor actor,
        Guid mediaAssetId,
        int index,
        Stream content,
        CancellationToken cancellationToken)
    {
        var asset = await mediaAssets.FindAsync(mediaAssetId, cancellationToken)
            ?? throw new DomainException("MEDIA_NOT_FOUND");
        await access.DemandExistingAsync(actor, asset.SessionId, cancellationToken);
        asset.EnsureChunkCanBeWritten(index, clock.UtcNow);
        var bytes = await ReadChunkAsync(content, cancellationToken);
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var objectKey = MediaStorageKeys.Chunk(asset.Id, index);
        var existing = await ReadExistingAsync(objectKey, cancellationToken);
        if (existing is not null)
        {
            EnsureSameChunk(existing, sha256, bytes.LongLength);
            return new ChunkWriteResult(
                index,
                false,
                existing.Sha256,
                existing.Length);
        }

        try
        {
            await using var input = new MemoryStream(bytes, writable: false);
            var stored = await storage.PutAsync(
                new ObjectWriteRequest(objectKey, "application/octet-stream"),
                input,
                cancellationToken);
            return new ChunkWriteResult(index, true, stored.Sha256, stored.Length);
        }
        catch (ProviderException exception)
            when (exception.Code == "OBJECT_KEY_CONFLICT")
        {
            existing = await ReadExistingAsync(objectKey, cancellationToken);
            if (existing is null)
            {
                throw;
            }

            EnsureSameChunk(existing, sha256, bytes.LongLength);
            return new ChunkWriteResult(
                index,
                false,
                existing.Sha256,
                existing.Length);
        }
    }

    private async Task<StoredObject?> ReadExistingAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var existing = await storage.OpenReadAsync(
                objectKey,
                cancellationToken);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long length = 0;
            int read;
            while ((read = await existing.ReadAsync(
                buffer,
                cancellationToken)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                length = checked(length + read);
            }

            return new StoredObject(
                objectKey,
                Convert.ToHexStringLower(hash.GetHashAndReset()),
                length);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static void EnsureSameChunk(
        StoredObject existing,
        string sha256,
        long length)
    {
        if (existing.Length != length
            || !string.Equals(
                existing.Sha256,
                sha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("MEDIA_CHUNK_CONFLICT");
        }
    }

    private static async Task<byte[]> ReadChunkAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        await using var buffer = new MemoryStream();
        var block = new byte[81920];
        while (true)
        {
            var read = await content.ReadAsync(block, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > MaximumChunkBytes)
            {
                throw new DomainException("MEDIA_CHUNK_TOO_LARGE");
            }

            await buffer.WriteAsync(block.AsMemory(0, read), cancellationToken);
        }

        if (buffer.Length == 0)
        {
            throw new DomainException("MEDIA_CHUNK_EMPTY");
        }

        return buffer.ToArray();
    }
}
