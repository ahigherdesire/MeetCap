namespace MeetCap.Models;

/// <summary>Raised when a meeting starts or ends.</summary>
public sealed class MeetingEventArgs : EventArgs
{
    public required MeetingPlatform Platform { get; init; }
    /// <summary>The window title that triggered detection (for display/debug).</summary>
    public string? Detail { get; init; }
}
