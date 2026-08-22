using System.Security.Cryptography;
using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Application.Consultations.Media;

public sealed class CompleteUploadHandler(
    IMediaAssetRepository mediaAssets,
    MediaSessionAccessService access,
    IObjectStorage storage,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<CompleteUploadResult> HandleAsync(
        ConsultationActor actor,
        Guid mediaAssetId,
        string expectedSha256,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var asset = await mediaAssets.FindAsync(mediaAssetId, cancellationToken)
            ?? throw new DomainException("MEDIA_NOT_FOUND");
        await access.DemandExistingAsync(actor, asset.SessionId, cancellationToken);
        if (asset.IsCompletionReplay(expectedSha256, idempotencyKey))
        {
            await DeleteChunksAsync(asset, cancellationToken);
            return new CompleteUploadResult(asset, false);
        }

        asset.EnsureCanComplete(clock.UtcNow);

        var chunkKeys = Enumerable.Range(0, asset.ExpectedChunks)
            .Select(index => MediaStorageKeys.Chunk(asset.Id, index))
            .ToArray();
        var combined = await ComputeCombinedHashAsync(chunkKeys, cancellationToken);
        if (!HashMatches(combined.Sha256, expectedSha256))
        {
            throw new DomainException("MEDIA_HASH_MISMATCH");
        }

        var finalKey = MediaStorageKeys.Final(asset.SubjectId, asset.Id);
        await using var combinedStream = new SequentialObjectReadStream(
            storage,
            chunkKeys);
        var stored = await storage.PutAsync(
            new ObjectWriteRequest(finalKey, asset.ContentType),
            combinedStream,
            cancellationToken);
        if (stored.Length != combined.Length
            || !HashMatches(stored.Sha256, combined.Sha256))
        {
            await storage.DeleteAsync(finalKey, cancellationToken);
            throw new DomainException("MEDIA_STORAGE_HASH_MISMATCH");
        }

        asset.Complete(
            finalKey,
            combined.Sha256,
            combined.Length,
            idempotencyKey,
            clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await DeleteChunksAsync(asset, cancellationToken);
        return new CompleteUploadResult(asset, true);
    }

    private async Task<StoredObject> ComputeCombinedHashAsync(
        IReadOnlyList<string> chunkKeys,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long length = 0;
        foreach (var key in chunkKeys)
        {
            try
            {
                await using var chunk = await storage.OpenReadAsync(
                    key,
                    cancellationToken);
                int read;
                while ((read = await chunk.ReadAsync(
                    buffer,
                    cancellationToken)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    length = checked(length + read);
                }
            }
            catch (FileNotFoundException)
            {
                throw new DomainException("MEDIA_CHUNK_MISSING");
            }
        }

        return new StoredObject(
            string.Empty,
            Convert.ToHexStringLower(hash.GetHashAndReset()),
            length);
    }

    private async Task DeleteChunksAsync(
        MediaAsset asset,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < asset.ExpectedChunks; index += 1)
        {
            await storage.DeleteAsync(
                MediaStorageKeys.Chunk(asset.Id, index),
                cancellationToken);
        }
    }

    private static bool HashMatches(string actualSha256, string? expectedSha256)
    {
        try
        {
            var actual = Convert.FromHexString(actualSha256);
            var expected = Convert.FromHexString(expectedSha256?.Trim() ?? string.Empty);
            return actual.Length == expected.Length
                && CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private sealed class SequentialObjectReadStream(
        IObjectStorage storage,
        IReadOnlyList<string> objectKeys) : Stream
    {
        private Stream? current;
        private int index;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            while (index < objectKeys.Count)
            {
                current ??= await storage.OpenReadAsync(
                    objectKeys[index],
                    cancellationToken);
                var read = await current.ReadAsync(buffer, cancellationToken);
                if (read > 0)
                {
                    return read;
                }

                await current.DisposeAsync();
                current = null;
                index += 1;
            }

            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override async ValueTask DisposeAsync()
        {
            if (current is not null)
            {
                await current.DisposeAsync();
            }

            GC.SuppressFinalize(this);
        }
    }
}
