using MentalHealth.Application.Abstractions.Providers;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace MentalHealth.Infrastructure.Media;

public sealed class VideoFeatureExtractor(
    IObjectStorage storage,
    FfprobeRunner ffprobe,
    OpenCvFacePresenceDetector faceDetector,
    IOptions<MediaFeatureOptions> configuredOptions) : IMediaFeatureExtractor
{
    public const string Version = "video-opencv-v1";
    private readonly MediaFeatureOptions _options = configuredOptions.Value;

    public string Modality => "video";

    public async Task<FeatureExtractionResult> ExtractAsync(
        FeatureExtractionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var media = await TemporaryMediaFile.CreateAsync(
            storage,
            request.ObjectKey,
            request.ContentType,
            _options,
            cancellationToken);
        try
        {
            var probe = await ffprobe.ProbeAsync(media.Path, cancellationToken);
            if (!probe.HasVideo)
            {
                throw new MediaParseException();
            }

            var sample = ExtractFrames(media.Path, probe, cancellationToken);
            var sourceRange = $"video:0-{probe.DurationSeconds:0.###}s";
            var quality = 1d - sample.FrameMissingRatio;
            return Success(
                new FeatureObservation(
                    "sampled_frames",
                    sample.SampledFrames,
                    quality,
                    sourceRange,
                    Version),
                new FeatureObservation(
                    "face_visible_ratio",
                    sample.FaceVisibleRatio,
                    quality,
                    sourceRange,
                    Version),
                new FeatureObservation(
                    "head_center_motion",
                    sample.HeadCenterMotion,
                    quality,
                    sourceRange,
                    Version),
                new FeatureObservation(
                    "frame_missing_ratio",
                    sample.FrameMissingRatio,
                    quality,
                    sourceRange,
                    Version));
        }
        catch (Exception exception) when (exception is MediaParseException
            or OpenCvSharpException)
        {
            return Failure();
        }
    }

    private VideoSample ExtractFrames(
        string inputPath,
        MediaProbeResult probe,
        CancellationToken cancellationToken)
    {
        using var capture = new VideoCapture(inputPath);
        if (!capture.IsOpened())
        {
            throw new MediaParseException();
        }

        var framesPerSecond = probe.FramesPerSecond is > 0d
            ? probe.FramesPerSecond.Value
            : capture.Fps;
        if (!double.IsFinite(framesPerSecond) || framesPerSecond <= 0d)
        {
            throw new MediaParseException();
        }

        var sampleEvery = Math.Max(1, (int)Math.Round(framesPerSecond));
        var expectedSamples = Math.Max(1, (int)Math.Ceiling(probe.DurationSeconds));
        var observations = new List<FaceObservation>(expectedSamples);
        using var frame = new Mat();
        var frameIndex = 0;
        while (capture.Read(frame))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (frame.Empty())
            {
                break;
            }

            if (frameIndex % sampleEvery == 0)
            {
                observations.Add(faceDetector.Detect(frame));
            }

            frameIndex++;
        }

        if (observations.Count == 0)
        {
            throw new MediaParseException();
        }

        var missingFrames = Math.Max(0, expectedSamples - observations.Count);
        var missingRatio = Math.Clamp(
            (double)missingFrames / expectedSamples,
            0d,
            1d);
        var visible = observations.Where(item => item.Visible).ToArray();
        var visibleRatio = (double)visible.Length / observations.Count;
        var motion = visible.Length < 2
            ? 0d
            : visible
                .Zip(visible.Skip(1), (first, second) => Math.Sqrt(
                    Math.Pow(second.CenterX - first.CenterX, 2d)
                    + Math.Pow(second.CenterY - first.CenterY, 2d)))
                .Average();
        return new VideoSample(
            observations.Count,
            visibleRatio,
            motion,
            missingRatio);
    }

    private FeatureExtractionResult Success(params FeatureObservation[] observations) =>
        new(
            Success: true,
            Modality,
            observations,
            FailureCode: null,
            Warnings: []);

    private FeatureExtractionResult Failure() => new(
        Success: false,
        Modality,
        Observations: [],
        FailureCode: "MEDIA_PARSE_FAILED",
        Warnings: []);

    private sealed record VideoSample(
        int SampledFrames,
        double FaceVisibleRatio,
        double HeadCenterMotion,
        double FrameMissingRatio);
}
