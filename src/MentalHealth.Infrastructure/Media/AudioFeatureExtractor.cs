using System.Diagnostics;
using MentalHealth.Application.Abstractions.Providers;
using Microsoft.Extensions.Options;

namespace MentalHealth.Infrastructure.Media;

public sealed class AudioFeatureExtractor(
    IObjectStorage storage,
    FfprobeRunner ffprobe,
    IOptions<MediaFeatureOptions> configuredOptions) : IMediaFeatureExtractor
{
    public const string Version = "audio-ffmpeg-v1";
    private const int SampleRate = 16_000;
    private const int WindowSamples = 320;
    private const double SpeechEnergyThreshold = 0.01d;
    private readonly MediaFeatureOptions _options = configuredOptions.Value;

    public string Modality => "audio";

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
            if (!probe.HasAudio)
            {
                throw new MediaParseException();
            }

            var statistics = await DecodeStatisticsAsync(media.Path, cancellationToken);
            var sourceRange = $"audio:0-{probe.DurationSeconds:0.###}s";
            return Success(
                new FeatureObservation(
                    "duration_seconds",
                    probe.DurationSeconds,
                    1d,
                    sourceRange,
                    Version),
                new FeatureObservation(
                    "speech_ratio",
                    statistics.SpeechRatio,
                    1d,
                    sourceRange,
                    Version),
                new FeatureObservation(
                    "pause_ratio",
                    statistics.PauseRatio,
                    1d,
                    sourceRange,
                    Version),
                new FeatureObservation(
                    "mean_energy",
                    statistics.MeanEnergy,
                    1d,
                    sourceRange,
                    Version),
                new FeatureObservation(
                    "energy_variation",
                    statistics.EnergyVariation,
                    1d,
                    sourceRange,
                    Version));
        }
        catch (MediaParseException)
        {
            return Failure();
        }
    }

    private async Task<AudioStatistics> DecodeStatisticsAsync(
        string inputPath,
        CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(_options.FfmpegPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-v");
        start.ArgumentList.Add("error");
        start.ArgumentList.Add("-i");
        start.ArgumentList.Add(inputPath);
        start.ArgumentList.Add("-vn");
        start.ArgumentList.Add("-ac");
        start.ArgumentList.Add("1");
        start.ArgumentList.Add("-ar");
        start.ArgumentList.Add(SampleRate.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("-f");
        start.ArgumentList.Add("f32le");
        start.ArgumentList.Add("pipe:1");

        using var process = StartProcess(start);
        using var registration = cancellationToken.Register(
            static state => FfprobeRunner.TryKill((Process)state!),
            process);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var accumulator = new AudioStatisticsAccumulator();
        var buffer = new byte[81920];
        var remainder = new byte[4];
        var remainderCount = 0;
        int bytesRead;
        while ((bytesRead = await process.StandardOutput.BaseStream.ReadAsync(
            buffer,
            cancellationToken)) > 0)
        {
            var offset = 0;
            if (remainderCount > 0)
            {
                var needed = 4 - remainderCount;
                var copied = Math.Min(needed, bytesRead);
                buffer.AsSpan(0, copied).CopyTo(remainder.AsSpan(remainderCount));
                remainderCount += copied;
                offset += copied;
                if (remainderCount == 4)
                {
                    accumulator.Add(BitConverter.ToSingle(remainder));
                    remainderCount = 0;
                }
            }

            while (offset + 4 <= bytesRead)
            {
                accumulator.Add(BitConverter.ToSingle(buffer, offset));
                offset += 4;
            }

            if (offset < bytesRead)
            {
                remainderCount = bytesRead - offset;
                buffer.AsSpan(offset, remainderCount).CopyTo(remainder);
            }
        }

        await process.WaitForExitAsync(cancellationToken);
        _ = await errorTask;
        if (process.ExitCode != 0 || remainderCount != 0)
        {
            throw new MediaParseException();
        }

        return accumulator.Complete();
    }

    private static Process StartProcess(ProcessStartInfo start)
    {
        try
        {
            return Process.Start(start)
                ?? throw new ProviderException("MEDIA_TOOL_UNAVAILABLE");
        }
        catch (ProviderException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
            throw new ProviderException(
                "MEDIA_TOOL_UNAVAILABLE",
                innerException: exception);
        }
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

    private sealed class AudioStatisticsAccumulator
    {
        private readonly List<double> _windowEnergy = [];
        private double _sumSquares;
        private int _windowSamples;

        public void Add(float sample)
        {
            if (!float.IsFinite(sample))
            {
                throw new MediaParseException();
            }

            _sumSquares += sample * sample;
            _windowSamples++;
            if (_windowSamples == WindowSamples)
            {
                CompleteWindow();
            }
        }

        public AudioStatistics Complete()
        {
            if (_windowSamples > 0)
            {
                CompleteWindow();
            }

            if (_windowEnergy.Count == 0)
            {
                throw new MediaParseException();
            }

            var mean = _windowEnergy.Average();
            var speechWindows = _windowEnergy.Count(value => value >= SpeechEnergyThreshold);
            var speechRatio = (double)speechWindows / _windowEnergy.Count;
            var variance = _windowEnergy.Average(value => Math.Pow(value - mean, 2d));
            return new AudioStatistics(
                speechRatio,
                1d - speechRatio,
                mean,
                Math.Sqrt(variance));
        }

        private void CompleteWindow()
        {
            _windowEnergy.Add(Math.Sqrt(_sumSquares / _windowSamples));
            _sumSquares = 0d;
            _windowSamples = 0;
        }
    }

    private sealed record AudioStatistics(
        double SpeechRatio,
        double PauseRatio,
        double MeanEnergy,
        double EnergyVariation);
}
