namespace MeetCap.Models;

/// <summary>Online meeting platforms MeetCap can detect.</summary>
public enum MeetingPlatform
{
    Unknown = 0,
    ZoomDesktop,
    TeamsDesktop,
    GoogleMeet,
    ZoomWeb,
    TeamsWeb,
}

public static class MeetingPlatformExtensions
{
    /// <summary>Human-friendly name used in popups, file names, and settings.</summary>
    public static string DisplayName(this MeetingPlatform platform) => platform switch
    {
        MeetingPlatform.ZoomDesktop => "Zoom",
        MeetingPlatform.TeamsDesktop => "Microsoft Teams",
        MeetingPlatform.GoogleMeet => "Google Meet",
        MeetingPlatform.ZoomWeb => "Zoom (Web)",
        MeetingPlatform.TeamsWeb => "Teams (Web)",
        _ => "Meeting",
    };

    /// <summary>File-name-safe token, e.g. "Google-Meet".</summary>
    public static string FileToken(this MeetingPlatform platform) => platform switch
    {
        MeetingPlatform.ZoomDesktop => "Zoom",
        MeetingPlatform.TeamsDesktop => "Microsoft-Teams",
        MeetingPlatform.GoogleMeet => "Google-Meet",
        MeetingPlatform.ZoomWeb => "Zoom-Web",
        MeetingPlatform.TeamsWeb => "Teams-Web",
        _ => "Meeting",
    };
}

/// <summary>What MeetCap should do when this platform is detected.</summary>
public enum PlatformAction
{
    /// <summary>Show the "do you want to record?" popup (default).</summary>
    Ask = 0,
    /// <summary>Start recording automatically, no popup.</summary>
    AutoRecord,
    /// <summary>Never prompt or record for this platform.</summary>
    Ignore,
}
