using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace MentalHealth.ContractTests.Providers;

public sealed class LocalObjectStorageContractTests : ObjectStorageContract, IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "mental-health-v1-storage-contract",
        Guid.NewGuid().ToString("N"));

    protected override IObjectStorage CreateStorage() => new LocalObjectStorage(
        Options.Create(new LocalObjectStorageOptions { RootPath = _rootPath }));

    [Fact]
    public async Task Cancelled_write_does_not_publish_or_leave_a_temporary_file()
    {
        var storage = CreateStorage();
        using var cancellation = new CancellationTokenSource();
        await using var content = new CancellingReadStream(
            new byte[100_000],
            cancellation);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.PutAsync(
            new ObjectWriteRequest("demo/cancelled.bin", "application/octet-stream"),
            content,
            cancellation.Token));

        Assert.Empty(Directory.EnumerateFiles(
            _rootPath,
            "*",
            SearchOption.AllDirectories));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private sealed class CancellingReadStream(
        byte[] buffer,
        CancellationTokenSource cancellation) : MemoryStream(buffer)
    {
        private bool _cancelled;

        public override ValueTask<int> ReadAsync(
            Memory<byte> destination,
            CancellationToken cancellationToken = default)
        {
            var read = base.ReadAsync(destination, CancellationToken.None);
            if (!_cancelled)
            {
                _cancelled = true;
                cancellation.Cancel();
            }

            return read;
        }
    }
}
