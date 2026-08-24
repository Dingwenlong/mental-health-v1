using MentalHealth.Application.Abstractions.Providers;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace MentalHealth.Infrastructure.Media;

public sealed record FaceObservation(bool Visible, double CenterX, double CenterY)
{
    public static FaceObservation None { get; } = new(false, 0d, 0d);
}

public sealed class OpenCvFacePresenceDetector : IDisposable
{
    private readonly CascadeClassifier _cascade;
    private readonly object _sync = new();

    public OpenCvFacePresenceDetector(IOptions<MediaFeatureOptions> configuredOptions)
    {
        var cascadePath = Path.GetFullPath(configuredOptions.Value.CascadePath);
        if (!File.Exists(cascadePath))
        {
            throw new ProviderException("FACE_DETECTOR_MODEL_MISSING");
        }

        try
        {
            _cascade = new CascadeClassifier(cascadePath);
            if (_cascade.Empty())
            {
                _cascade.Dispose();
                throw new ProviderException("FACE_DETECTOR_MODEL_INVALID");
            }
        }
        catch (ProviderException)
        {
            throw;
        }
        catch (OpenCvSharpException exception)
        {
            throw new ProviderException(
                "FACE_DETECTOR_MODEL_INVALID",
                innerException: exception);
        }
    }

    public FaceObservation Detect(Mat frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Empty())
        {
            return FaceObservation.None;
        }

        using var gray = new Mat();
        if (frame.Channels() == 1)
        {
            frame.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        }

        Cv2.EqualizeHist(gray, gray);
        Rect[] faces;
        lock (_sync)
        {
            faces = _cascade.DetectMultiScale(
                gray,
                scaleFactor: 1.1,
                minNeighbors: 4,
                flags: HaarDetectionTypes.ScaleImage,
                minSize: new Size(48, 48));
        }

        var face = faces
            .OrderByDescending(item => item.Width * item.Height)
            .FirstOrDefault();
        return face.Width <= 0 || face.Height <= 0
            ? FaceObservation.None
            : new FaceObservation(
                Visible: true,
                CenterX: Math.Clamp(
                    (face.X + face.Width / 2d) / frame.Width,
                    0d,
                    1d),
                CenterY: Math.Clamp(
                    (face.Y + face.Height / 2d) / frame.Height,
                    0d,
                    1d));
    }

    public void Dispose() => _cascade.Dispose();
}
