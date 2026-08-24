using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using MentalHealth.Application.Abstractions.Providers;
using Microsoft.Extensions.Options;

namespace MentalHealth.Infrastructure.Media;

public sealed class MediaFeatureOptions
{
    public const string SectionName = "MediaFeatures";

    public string FfprobePath { get; set; } = "ffprobe";

    public string FfmpegPath { get; set; } = "ffmpeg";

    public string CascadePath { get; set; } = Path.Combine(
        AppContext.BaseDirectory,
        "config",
        "opencv",
        "haarcascade_frontalface_default.xml");

    public string TemporaryRootPath { get; set; } = Path.Combine(
        Path.GetTempPath(),
        "mental-health-v1",
        "media-analysis");

    public static MediaFeatureOptions ForTests(string cascadePath) => new()
    {
        CascadePath = Path.GetFullPath(cascadePath),
        TemporaryRootPath = Path.Combine(
            Path.GetTempPath(),
            "mental-health-v1-tests",
            "media-analysis")
    };
}

public sealed record MediaProbeResult(
    double DurationSeconds,
    bool HasAudio,
    bool HasVideo,
    double? FramesPerSecond);

public sealed class FfprobeRunner(IOptions<MediaFeatureOptions> configuredOptions)
{
    private readonly MediaFeatureOptions _options = configuredOptions.Value;

    public async Task<MediaProbeResult> ProbeAsync(
        string inputPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(_options.FfprobePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-v");
        start.ArgumentList.Add("error");
        start.ArgumentList.Add("-show_format");
        start.ArgumentList.Add("-show_streams");
        start.ArgumentList.Add("-of");
        start.ArgumentList.Add("json");
        start.ArgumentList.Add(inputPath);

        using var process = StartProcess(start);
        using var registration = cancellationToken.Register(
            static state => TryKill((Process)state!),
            process);
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = await standardOutput;
        _ = await standardError;
        if (process.ExitCode != 0)
        {
            throw new MediaParseException();
        }

        try
        {
            using var json = JsonDocument.Parse(output);
            var root = json.RootElement;
            var streams = root.TryGetProperty("streams", out var streamList)
                ? streamList.EnumerateArray().ToArray()
                : [];
            var duration = ReadDuration(root, streams);
            if (!double.IsFinite(duration) || duration <= 0d)
            {
                throw new MediaParseException();
            }

            var video = streams.FirstOrDefault(stream =>
                ReadString(stream, "codec_type") == "video");
            return new MediaProbeResult(
                duration,
                streams.Any(stream => ReadString(stream, "codec_type") == "audio"),
                video.ValueKind != JsonValueKind.Undefined,
                ReadFrameRate(video));
        }
        catch (MediaParseException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException
            or InvalidOperationException
            or FormatException)
        {
            throw new MediaParseException(exception);
        }
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

    private static double ReadDuration(
        JsonElement root,
        IReadOnlyCollection<JsonElement> streams)
    {
        if (root.TryGetProperty("format", out var format)
            && TryReadDouble(format, "duration", out var formatDuration))
        {
            return formatDuration;
        }

        foreach (var stream in streams)
        {
            if (TryReadDouble(stream, "duration", out var streamDuration))
            {
                return streamDuration;
            }
        }

        throw new MediaParseException();
    }

    private static double? ReadFrameRate(JsonElement video)
    {
        var value = ReadString(video, "avg_frame_rate");
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split('/', 2);
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var top)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var bottom)
            && bottom > 0d)
        {
            return top / bottom;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var direct)
            ? direct
            : null;
    }

    private static bool TryReadDouble(
        JsonElement parent,
        string name,
        out double value)
    {
        var text = ReadString(parent, name);
        return double.TryParse(
            text,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static string? ReadString(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(name, out var property)
            ? property.GetString()
            : null;

    internal static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}

internal sealed class MediaParseException : Exception
{
    public MediaParseException()
    {
    }

    public MediaParseException(Exception innerException)
        : base("Media could not be parsed.", innerException)
    {
    }
}

internal sealed class TemporaryMediaFile : IAsyncDisposable
{
    private readonly string _directory;

    private TemporaryMediaFile(string directory, string path)
    {
        _directory = directory;
        Path = path;
    }

    public string Path { get; }

    public static async Task<TemporaryMediaFile> CreateAsync(
        IObjectStorage storage,
        string objectKey,
        string contentType,
        MediaFeatureOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            throw new ProviderException("INVALID_OBJECT_KEY");
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ProviderException("MEDIA_CONTENT_TYPE_INVALID");
        }

        var root = System.IO.Path.GetFullPath(options.TemporaryRootPath);
        Directory.CreateDirectory(root);
        var directory = System.IO.Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var extension = contentType.ToLowerInvariant() switch
        {
            "audio/wav" or "audio/x-wav" => ".wav",
            "video/mp4" => ".mp4",
            _ when contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) =>
                ".audio",
            _ when contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) =>
                ".video",
            _ => ".media"
        };
        var path = System.IO.Path.Combine(directory, "input" + extension);

        try
        {
            await using var input = await storage.OpenReadAsync(objectKey, cancellationToken);
            await using var output = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await input.CopyToAsync(output, cancellationToken);
            await output.FlushAsync(cancellationToken);
            return new TemporaryMediaFile(directory, path);
        }
        catch
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: false);
            }

            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        if (File.Exists(Path))
        {
            File.Delete(Path);
        }

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: false);
        }

        return ValueTask.CompletedTask;
    }
}
