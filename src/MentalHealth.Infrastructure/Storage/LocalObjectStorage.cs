using System.Buffers;
using System.Security.Cryptography;
using MentalHealth.Application.Abstractions.Providers;
using Microsoft.Extensions.Options;

namespace MentalHealth.Infrastructure.Storage;

public sealed class LocalObjectStorage : IObjectStorage
{
    private const int BufferSize = 81920;
    private readonly string _rootPath;
    private readonly string _rootPrefix;
    private readonly StringComparison _pathComparison;

    public LocalObjectStorage(IOptions<LocalObjectStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Value.RootPath))
        {
            throw new ArgumentException("Local object storage root is required.", nameof(options));
        }

        _rootPath = Path.GetFullPath(options.Value.RootPath);
        _rootPrefix = _rootPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        _pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<StoredObject> PutAsync(
        ObjectWriteRequest request,
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();
        if (!content.CanRead)
        {
            throw new ProviderException("CONTENT_STREAM_UNREADABLE");
        }

        if (string.IsNullOrWhiteSpace(request.ContentType))
        {
            throw new ProviderException("CONTENT_TYPE_REQUIRED");
        }

        var targetPath = ResolveObjectPath(request.ObjectKey);
        var parentPath = Path.GetDirectoryName(targetPath)
            ?? throw new ProviderException("INVALID_OBJECT_KEY");
        Directory.CreateDirectory(parentPath);
        var temporaryPath = Path.Combine(
            parentPath,
            $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            var storedObject = await WriteTemporaryFileAsync(
                request.ObjectKey,
                temporaryPath,
                content,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(targetPath))
            {
                await EnsureExistingObjectMatchesAsync(
                    targetPath,
                    storedObject,
                    cancellationToken);
                return storedObject;
            }

            try
            {
                File.Move(temporaryPath, targetPath);
            }
            catch (IOException) when (File.Exists(targetPath))
            {
                await EnsureExistingObjectMatchesAsync(
                    targetPath,
                    storedObject,
                    cancellationToken);
            }

            return storedObject;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public Task<Stream> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var targetPath = ResolveObjectPath(objectKey);
        if (!File.Exists(targetPath))
        {
            throw new FileNotFoundException("Object was not found.", objectKey);
        }

        Stream stream = new FileStream(
            targetPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read | FileShare.Delete,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var targetPath = ResolveObjectPath(objectKey);
        File.Delete(targetPath);
        return Task.CompletedTask;
    }

    private async Task<StoredObject> WriteTemporaryFileAsync(
        string objectKey,
        string temporaryPath,
        Stream content,
        CancellationToken cancellationToken)
    {
        await using var output = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        long length = 0;

        try
        {
            int bytesRead;
            while ((bytesRead = await content.ReadAsync(
                buffer.AsMemory(0, BufferSize),
                cancellationToken)) > 0)
            {
                await output.WriteAsync(
                    buffer.AsMemory(0, bytesRead),
                    cancellationToken);
                hash.AppendData(buffer, 0, bytesRead);
                length += bytesRead;
            }

            await output.FlushAsync(cancellationToken);
            return new StoredObject(
                objectKey,
                Convert.ToHexStringLower(hash.GetHashAndReset()),
                length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static async Task EnsureExistingObjectMatchesAsync(
        string targetPath,
        StoredObject expected,
        CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(targetPath);
        if (fileInfo.Length != expected.Length)
        {
            throw new ProviderException("OBJECT_KEY_CONFLICT");
        }

        await using var existing = new FileStream(
            targetPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var actualHash = await SHA256.HashDataAsync(existing, cancellationToken);
        var expectedHash = Convert.FromHexString(expected.Sha256);
        if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
        {
            throw new ProviderException("OBJECT_KEY_CONFLICT");
        }
    }

    private string ResolveObjectPath(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey)
            || Path.IsPathRooted(objectKey)
            || objectKey.Contains('\\')
            || objectKey.Contains(':')
            || objectKey.Split('/').Any(segment =>
                string.IsNullOrWhiteSpace(segment) || segment is "." or ".."))
        {
            throw new ProviderException("INVALID_OBJECT_KEY");
        }

        var relativePath = objectKey.Replace('/', Path.DirectorySeparatorChar);
        var targetPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        if (!targetPath.StartsWith(_rootPrefix, _pathComparison))
        {
            throw new ProviderException("INVALID_OBJECT_KEY");
        }

        return targetPath;
    }
}
