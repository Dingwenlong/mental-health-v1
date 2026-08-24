using MentalHealth.Application.Abstractions.Providers;

namespace MentalHealth.ContractTests.Providers;

public abstract class ObjectStorageContract
{
    protected abstract IObjectStorage CreateStorage();

    [Fact]
    public async Task Put_open_delete_round_trip_preserves_bytes()
    {
        var storage = CreateStorage();
        var expected = "synthetic"u8.ToArray();
        await using var input = new MemoryStream(expected);

        var saved = await storage.PutAsync(
            new ObjectWriteRequest("demo/session/file.txt", "text/plain"),
            input,
            CancellationToken.None);
        await using var output = await storage.OpenReadAsync(
            saved.ObjectKey,
            CancellationToken.None);
        await using var copy = new MemoryStream();
        await output.CopyToAsync(copy, CancellationToken.None);

        Assert.Equal(expected, copy.ToArray());

        await storage.DeleteAsync(saved.ObjectKey, CancellationToken.None);
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => storage.OpenReadAsync(saved.ObjectKey, CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../outside.txt")]
    [InlineData("demo/../../outside.txt")]
    [InlineData("/rooted.txt")]
    [InlineData("C:/rooted.txt")]
    [InlineData("demo\\outside.txt")]
    public async Task Put_rejects_invalid_object_keys(string objectKey)
    {
        var storage = CreateStorage();
        await using var content = new MemoryStream("synthetic"u8.ToArray());

        var exception = await Assert.ThrowsAsync<ProviderException>(
            () => storage.PutAsync(
                new ObjectWriteRequest(objectKey, "text/plain"),
                content,
                CancellationToken.None));

        Assert.Equal("INVALID_OBJECT_KEY", exception.Code);
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("/rooted.txt")]
    [InlineData("C:/rooted.txt")]
    [InlineData("demo\\outside.txt")]
    public async Task OpenRead_rejects_invalid_object_keys(string objectKey)
    {
        var storage = CreateStorage();

        var exception = await Assert.ThrowsAsync<ProviderException>(
            () => storage.OpenReadAsync(objectKey, CancellationToken.None));

        Assert.Equal("INVALID_OBJECT_KEY", exception.Code);
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("/rooted.txt")]
    [InlineData("C:/rooted.txt")]
    [InlineData("demo\\outside.txt")]
    public async Task Delete_rejects_invalid_object_keys(string objectKey)
    {
        var storage = CreateStorage();

        var exception = await Assert.ThrowsAsync<ProviderException>(
            () => storage.DeleteAsync(objectKey, CancellationToken.None));

        Assert.Equal("INVALID_OBJECT_KEY", exception.Code);
    }

    [Fact]
    public async Task Delete_missing_object_is_idempotent()
    {
        var storage = CreateStorage();

        await storage.DeleteAsync(
            $"missing/{Guid.NewGuid():N}/file.bin",
            CancellationToken.None);
    }

    [Fact]
    public async Task Put_rejects_blank_content_type()
    {
        var storage = CreateStorage();
        await using var content = new MemoryStream("synthetic"u8.ToArray());

        var exception = await Assert.ThrowsAsync<ProviderException>(
            () => storage.PutAsync(
                new ObjectWriteRequest("demo/file.txt", " "),
                content,
                CancellationToken.None));

        Assert.Equal("CONTENT_TYPE_REQUIRED", exception.Code);
    }

    [Fact]
    public async Task Put_accepts_empty_content_with_stable_digest()
    {
        var storage = CreateStorage();
        await using var content = new MemoryStream();

        var saved = await storage.PutAsync(
            new ObjectWriteRequest("demo/empty.txt", "text/plain"),
            content,
            CancellationToken.None);

        Assert.Equal(0, saved.Length);
        Assert.Equal(
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            saved.Sha256);
    }

    [Fact]
    public async Task Put_is_idempotent_for_same_key_and_bytes()
    {
        var storage = CreateStorage();
        var request = new ObjectWriteRequest("demo/idempotent.txt", "text/plain");
        await using var firstContent = new MemoryStream("same"u8.ToArray());
        await using var secondContent = new MemoryStream("same"u8.ToArray());

        var first = await storage.PutAsync(request, firstContent, CancellationToken.None);
        var second = await storage.PutAsync(request, secondContent, CancellationToken.None);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Put_rejects_changed_bytes_for_existing_key()
    {
        var storage = CreateStorage();
        var request = new ObjectWriteRequest("demo/immutable.txt", "text/plain");
        await using var firstContent = new MemoryStream("first"u8.ToArray());
        await using var changedContent = new MemoryStream("changed"u8.ToArray());
        await storage.PutAsync(request, firstContent, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ProviderException>(
            () => storage.PutAsync(request, changedContent, CancellationToken.None));

        Assert.Equal("OBJECT_KEY_CONFLICT", exception.Code);
    }

    [Fact]
    public async Task Put_honors_a_pre_cancelled_token()
    {
        var storage = CreateStorage();
        await using var content = new MemoryStream("synthetic"u8.ToArray());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => storage.PutAsync(
                new ObjectWriteRequest("demo/file.txt", "text/plain"),
                content,
                new CancellationToken(canceled: true)));
    }

    [Fact]
    public async Task OpenRead_honors_a_pre_cancelled_token()
    {
        var storage = CreateStorage();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => storage.OpenReadAsync(
                "demo/file.txt",
                new CancellationToken(canceled: true)));
    }

    [Fact]
    public async Task Delete_honors_a_pre_cancelled_token()
    {
        var storage = CreateStorage();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => storage.DeleteAsync(
                "demo/file.txt",
                new CancellationToken(canceled: true)));
    }
}
