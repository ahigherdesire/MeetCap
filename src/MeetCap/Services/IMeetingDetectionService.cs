using MeetCap.Models;

namespace MeetCap.Services;

public interface IMeetingDetectionService
{
    bool IsRunning { get; }
    bool IsMeetingActive { get; }
    MeetingPlatform CurrentPlatform { get; }

    /// <summary>Raised (off the UI thread) when a meeting is first detected.</summary>
    event EventHandler<MeetingEventArgs>? MeetingStarted;

    /// <summary>Raised (off the UI thread) when an active meeting appears to have ended.</summary>
    event EventHandler<MeetingEventArgs>? MeetingEnded;

    void Start();
    void Stop();
}
