using System.IO;

namespace MeetCap.Models;

/// <summary>Persisted user settings, serialized to %APPDATA%\MeetCap\settings.json.</summary>
public sealed class AppSettings
{
    /// <summary>Folder where recordings are saved. Defaults to Videos\MeetCap.</summary>
    public string OutputFolder { get; set; } = DefaultOutputFolder();

    public RecordingQuality Quality { get; set; } = RecordingQuality.High;

    /// <summary>Capture system (loopback) audio.</summary>
    public bool RecordSystemAudio { get; set; } = true;

    /// <summary>Capture microphone audio.</summary>
    public bool RecordMicrophone { get; set; } = true;

    /// <summary>Launch MeetCap automatically when the user signs in.</summary>
    public bool StartOnBoot { get; set; } = true;

    /// <summary>Master switch for meeting detection / popups.</summary>
    public bool DetectionEnabled { get; set; } = true;

    /// <summary>User has acknowledged the recording-consent notice.</summary>
    public bool ConsentAcknowledged { get; set; }

    /// <summary>Per-platform behavior (Ask / AutoRecord / Ignore).</summary>
    public Dictionary<MeetingPlatform, PlatformAction> PlatformActions { get; set; } = new()
    {
        [MeetingPlatform.ZoomDesktop] = PlatformAction.Ask,
        [MeetingPlatform.TeamsDesktop] = PlatformAction.Ask,
        [MeetingPlatform.GoogleMeet] = PlatformAction.Ask,
        [MeetingPlatform.ZoomWeb] = PlatformAction.Ask,
        [MeetingPlatform.TeamsWeb] = PlatformAction.Ask,
    };

    public PlatformAction ActionFor(MeetingPlatform platform) =>
        PlatformActions.TryGetValue(platform, out var a) ? a : PlatformAction.Ask;

    public static string DefaultOutputFolder()
    {
        var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        if (string.IsNullOrEmpty(videos))
            videos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Videos");
        return Path.Combine(videos, "MeetCap");
    }
}
