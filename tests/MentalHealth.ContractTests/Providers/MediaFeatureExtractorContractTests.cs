using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Infrastructure.Media;
using MentalHealth.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace MentalHealth.ContractTests.Providers;

public sealed class MediaFeatureExtractorContractTests : IAsyncLifetime
{
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(),
        "mental-health-v1-tests",
        "media-features",
        Guid.NewGuid().ToString("N"));
    private LocalObjectStorage? _storage;

    [Fact]
    public async Task Silence_and_tone_with_pauses_return_bounded_audio_features()
    {
        var extractor = CreateAudioExtractor();
        await StoreFixtureAsync("audio/silence.wav", "audio/silence.wav", "audio/wav");
        await StoreFixtureAsync(
            "audio/tone-with-pauses.wav",
            "audio/tone-with-pauses.wav",
            "audio/wav");

        var silence = await extractor.ExtractAsync(
            Request("audio/silence.wav", "audio/wav"),
            CancellationToken.None);
        var tone = await extractor.ExtractAsync(
            Request("audio/tone-with-pauses.wav", "audio/wav"),
            CancellationToken.None);

        Assert.True(silence.Success);
        Assert.Equal(0d, Feature(silence, "speech_ratio").Value, precision: 3);
        Assert.Equal(1d, Feature(silence, "pause_ratio").Value, precision: 3);
        Assert.True(tone.Success);
        Assert.InRange(Feature(tone, "speech_ratio").Value, 0.30d, 0.50d);
        Assert.InRange(Feature(tone, "pause_ratio").Value, 0.50d, 0.70d);
        Assert.All(tone.Observations, AssertBoundedQuality);
    }

    [Fact]
    public async Task Blank_video_has_no_face_and_synthetic_video_has_a_visible_face()
    {
        var extractor = CreateVideoExtractor();
        await StoreFixtureAsync("video/blank.mp4", "video/blank.mp4", "video/mp4");
        await StoreFixtureAsync(
            "video/synthetic-face.mp4",
            "video/synthetic-face.mp4",
            "video/mp4");

        var blank = await extractor.ExtractAsync(
            Request("video/blank.mp4", "video/mp4"),
            CancellationToken.None);
        var synthetic = await extractor.ExtractAsync(
            Request("video/synthetic-face.mp4", "video/mp4"),
            CancellationToken.None);

        Assert.True(blank.Success);
        Assert.Equal(0d, Feature(blank, "face_visible_ratio").Value);
        Assert.True(synthetic.Success);
        Assert.InRange(Feature(synthetic, "face_visible_ratio").Value, 0.80d, 1d);
        Assert.True(Feature(synthetic, "sampled_frames").Value >= 5d);
        Assert.All(synthetic.Observations, AssertBoundedQuality);
    }

    [Theory]
    [InlineData("audio/wav", "audio")]
    [InlineData("video/mp4", "video")]
    public async Task Corrupt_media_returns_explicit_failure_without_fabricated_features(
        string contentType,
        string modality)
    {
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("not media"));
        await Storage.PutAsync(
            new ObjectWriteRequest("corrupt/item.bin", contentType),
            content,
            CancellationToken.None);
        var extractor = modality == "audio"
            ? (IMediaFeatureExtractor)CreateAudioExtractor()
            : CreateVideoExtractor();

        var result = await extractor.ExtractAsync(
            Request("corrupt/item.bin", contentType),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("MEDIA_PARSE_FAILED", result.FailureCode);
        Assert.Empty(result.Observations);
        Assert.Empty(Directory.Exists(TemporaryMediaRoot)
            ? Directory.EnumerateDirectories(TemporaryMediaRoot)
            : []);
    }

    [Fact]
    public async Task Extractor_honors_a_pre_cancelled_token()
    {
        var extractor = CreateAudioExtractor();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => extractor.ExtractAsync(
            Request("audio/silence.wav", "audio/wav"),
            cancellation.Token));
    }

    [Fact]
    public async Task Synthetic_face_provenance_matches_the_fixture()
    {
        var imagePath = FixturePath("video/synthetic-face.png");
        var provenancePath = FixturePath("video/synthetic-face.provenance.json");
        await using var image = File.OpenRead(imagePath);
        var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(image));
        using var provenance = JsonDocument.Parse(await File.ReadAllTextAsync(provenancePath));

        Assert.True(provenance.RootElement.GetProperty("synthetic").GetBoolean());
        Assert.Equal(
            actualHash,
            provenance.RootElement.GetProperty("sha256").GetString());
    }

    public Task InitializeAsync()
    {
        _storage = new LocalObjectStorage(Options.Create(new LocalObjectStorageOptions
        {
            RootPath = _storageRoot
        }));
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        var allowedRoot = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "mental-health-v1-tests",
            "media-features"));
        var target = Path.GetFullPath(_storageRoot);
        if (Directory.Exists(target)
            && target.StartsWith(
                allowedRoot + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal))
        {
            Directory.Delete(target, recursive: true);
        }

        return Task.CompletedTask;
    }

    private LocalObjectStorage Storage => _storage
        ?? throw new InvalidOperationException("Storage is not initialized.");

    private AudioFeatureExtractor CreateAudioExtractor()
    {
        var options = Options.Create(MediaFeatureOptions.ForTests(
            cascadePath: RepositoryPath("config/opencv/haarcascade_frontalface_default.xml")));
        return new AudioFeatureExtractor(Storage, new FfprobeRunner(options), options);
    }

    private VideoFeatureExtractor CreateVideoExtractor()
    {
        var options = Options.Create(MediaFeatureOptions.ForTests(
            cascadePath: RepositoryPath("config/opencv/haarcascade_frontalface_default.xml")));
        return new VideoFeatureExtractor(
            Storage,
            new FfprobeRunner(options),
            new OpenCvFacePresenceDetector(options),
            options);
    }

    private async Task StoreFixtureAsync(
        string objectKey,
        string relativeFixturePath,
        string contentType)
    {
        await using var content = File.OpenRead(FixturePath(relativeFixturePath));
        await Storage.PutAsync(
            new ObjectWriteRequest(objectKey, contentType),
            content,
            CancellationToken.None);
    }

    private static FeatureExtractionRequest Request(string objectKey, string contentType) =>
        new(Guid.NewGuid(), objectKey, contentType, TranscriptText: null);

    private static FeatureObservation Feature(
        FeatureExtractionResult result,
        string code) => result.Observations.Single(item => item.Code == code);

    private static void AssertBoundedQuality(FeatureObservation observation)
    {
        Assert.True(double.IsFinite(observation.Value));
        Assert.InRange(observation.Quality, 0d, 1d);
        Assert.False(string.IsNullOrWhiteSpace(observation.SourceRange));
        Assert.False(string.IsNullOrWhiteSpace(observation.ExtractorVersion));
    }

    private static string FixturePath(string relativePath)
    {
        return RepositoryPath(Path.Combine(
            "tests",
            "fixtures",
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string TemporaryMediaRoot => Path.Combine(
        Path.GetTempPath(),
        "mental-health-v1-tests",
        "media-analysis");

    private static string RepositoryPath(string relativePath)
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            ".."));
        return Path.GetFullPath(Path.Combine(repositoryRoot, relativePath));
    }
}
