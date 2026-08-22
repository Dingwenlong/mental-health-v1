using System.Security.Cryptography;
using MentalHealth.Application.Abstractions.Providers;

namespace MentalHealth.ContractTests.Fakes;

internal sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly Dictionary<string, StoredEntry> _objects = new(StringComparer.Ordinal);

    public async Task<StoredObject> PutAsync(
        ObjectWriteRequest request,
        Stream content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateObjectKey(request.ObjectKey);
        if (string.IsNullOrWhiteSpace(request.ContentType))
        {
            throw new ProviderException("CONTENT_TYPE_REQUIRED");
        }

        await using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        var bytes = copy.ToArray();

        if (_objects.TryGetValue(request.ObjectKey, out var existing))
        {
            if (!bytes.AsSpan().SequenceEqual(existing.Bytes))
            {
                throw new ProviderException("OBJECT_KEY_CONFLICT");
            }

            return existing.Metadata;
        }

        var metadata = new StoredObject(
            request.ObjectKey,
            Convert.ToHexStringLower(SHA256.HashData(bytes)),
            bytes.LongLength);
        _objects.Add(request.ObjectKey, new StoredEntry(bytes, metadata));
        return metadata;
    }

    public Task<Stream> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateObjectKey(objectKey);
        if (!_objects.TryGetValue(objectKey, out var entry))
        {
            throw new FileNotFoundException("Object was not found.", objectKey);
        }

        return Task.FromResult<Stream>(new MemoryStream(entry.Bytes, writable: false));
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateObjectKey(objectKey);
        _objects.Remove(objectKey);
        return Task.CompletedTask;
    }

    private static void ValidateObjectKey(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey)
            || objectKey.StartsWith("/", StringComparison.Ordinal)
            || objectKey.Contains('\\')
            || objectKey.Contains(':')
            || objectKey.Split('/').Any(segment =>
                string.IsNullOrWhiteSpace(segment) || segment is "." or ".."))
        {
            throw new ProviderException("INVALID_OBJECT_KEY");
        }
    }

    private sealed record StoredEntry(byte[] Bytes, StoredObject Metadata);
}
