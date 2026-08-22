using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.ContractTests.Fakes;

namespace MentalHealth.ContractTests.Providers;

public abstract class TranscriptionProviderContract
{
    protected abstract ITranscriptionProvider CreateProvider();

    [Fact]
    public async Task Get_preserves_supplied_transcript_text()
    {
        var provider = CreateProvider();
        var request = new TranscriptionRequest(
            Guid.NewGuid(),
            "demo/session/audio.wav",
            "这是人工校对的测试稿。");

        var transcript = await provider.GetAsync(request, CancellationToken.None);

        Assert.NotNull(transcript);
        Assert.Equal("这是人工校对的测试稿。", transcript.Text);
        Assert.True(transcript.IsManual);
    }

    [Fact]
    public async Task Get_rejects_blank_object_key()
    {
        var provider = CreateProvider();
        var request = new TranscriptionRequest(Guid.NewGuid(), " ", "测试稿");

        var exception = await Assert.ThrowsAsync<ProviderException>(
            () => provider.GetAsync(request, CancellationToken.None));

        Assert.Equal("TRANSCRIPT_REQUIRED", exception.Code);
    }

    [Fact]
    public async Task Get_honors_a_pre_cancelled_token()
    {
        var provider = CreateProvider();
        var request = new TranscriptionRequest(Guid.NewGuid(), "demo/audio.wav", "测试稿");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.GetAsync(request, new CancellationToken(canceled: true)));
    }
}

public sealed class FakeTranscriptionProviderContractTests : TranscriptionProviderContract
{
    protected override ITranscriptionProvider CreateProvider() => new FakeTranscriptionProvider();
}
