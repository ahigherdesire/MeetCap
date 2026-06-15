namespace MeetCap.Models;

/// <summary>User-selectable recording quality tiers.</summary>
public enum RecordingQuality
{
    Standard = 0,
    High,
    Ultra,
}

/// <summary>Concrete encoder settings derived from a <see cref="RecordingQuality"/> tier.</summary>
public sealed record QualityPreset(
    int Bitrate,          // bits per second for H.264
    int Framerate,        // frames per second
    int MaxHeight,        // 0 = native (no downscale)
    string Description)
{
    public static QualityPreset For(RecordingQuality quality) => quality switch
    {
        RecordingQuality.Standard => new QualityPreset(6_000_000, 30, 1080, "1080p · 30fps · smaller files"),
        RecordingQuality.High => new QualityPreset(12_000_000, 30, 0, "Native resolution · 30fps"),
        RecordingQuality.Ultra => new QualityPreset(24_000_000, 60, 0, "Native resolution · 60fps · best quality"),
        _ => For(RecordingQuality.High),
    };
}
