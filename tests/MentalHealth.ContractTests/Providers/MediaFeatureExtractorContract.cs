using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.ContractTests.Fakes;

namespace MentalHealth.ContractTests.Providers;

public abstract class MediaFeatureExtractorContract
{
    protected abstract IMediaFeatureExtractor CreateExtractor();

    [Fact]
    public async Task Extract_returns_finite_features_with_quality_in_range()
    {
        var extractor = CreateExtractor();
        var request = new FeatureExtractionRequest(
            Guid.NewGuid(),
            "demo/session/audio.wav",
            "audio/wav",
            "测试稿");

        var result = await extractor.ExtractAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(extractor.Modality, result.Modality);
        Assert.False(string.IsNullOrWhiteSpace(result.Modality));
        Assert.Null(result.FailureCode);
        Assert.NotEmpty(result.Observations);
        Assert.All(result.Observations, feature =>
        {
            Assert.False(string.IsNullOrWhiteSpace(feature.Code));
            Assert.True(double.IsFinite(feature.Value));
            Assert.InRange(feature.Quality, 0d, 1d);
            Assert.False(string.IsNullOrWhiteSpace(feature.SourceRange));
            Assert.False(string.IsNullOrWhiteSpace(feature.ExtractorVersion));
        });
    }

    [Fact]
    public async Task Extract_rejects_blank_object_key()
    {
        var extractor = CreateExtractor();
        var request = new FeatureExtractionRequest(Guid.NewGuid(), " ", "audio/wav", null);

        var exception = await Assert.ThrowsAsync<ProviderException>(
            () => extractor.ExtractAsync(request, CancellationToken.None));

        Assert.Equal("INVALID_OBJECT_KEY", exception.Code);
    }

    [Fact]
    public async Task Extract_honors_a_pre_cancelled_token()
    {
        var extractor = CreateExtractor();
        var request = new FeatureExtractionRequest(
            Guid.NewGuid(),
            "demo/audio.wav",
            "audio/wav",
            null);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => extractor.ExtractAsync(
                request,
                new CancellationToken(canceled: true)));
    }
}

public sealed class FakeMediaFeatureExtractorContractTests : MediaFeatureExtractorContract
{
    protected override IMediaFeatureExtractor CreateExtractor() => new FakeMediaFeatureExtractor();
}
