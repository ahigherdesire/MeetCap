using System.Diagnostics;
using System.Text.RegularExpressions;
using MeetCap.Interop;
using MeetCap.Models;

namespace MeetCap.Services;

/// <summary>
/// Polls (every few seconds) for safe meeting signals — running processes, visible window /
/// browser-tab titles, and microphone/webcam usage — and raises start/end events.
///
/// Design notes:
///  * Browser-hosted meetings (Meet / Zoom Web / Teams Web) additionally require the mic or
///    webcam to be active, which distinguishes "in a call" from "just has the tab open".
///  * The Zoom desktop client exposes a dedicated "Zoom Meeting" window, which is a definitive
///    signal on its own.
///  * Meeting-end is debounced over several misses so a brief title flicker doesn't end a call.
/// </summary>
public sealed partial class MeetingDetectionService : IMeetingDetectionService, IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    private const int EndGraceTicks = 3; // ~9s of "no meeting" before we declare it ended.

    private static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "arc",
    };

    private readonly ISettingsService _settings;
    private readonly object _gate = new();
    private Timer? _timer;
    private int _missCount;

    public bool IsRunning { get; private set; }
    public bool IsMeetingActive { get; private set; }
    public MeetingPlatform CurrentPlatform { get; private set; } = MeetingPlatform.Unknown;

    public event EventHandler<MeetingEventArgs>? MeetingStarted;
    public event EventHandler<MeetingEventArgs>? MeetingEnded;

    public MeetingDetectionService(ISettingsService settings) => _settings = settings;

    public void Start()
    {
        if (IsRunning) return;
        IsRunning = true;
        _timer = new Timer(_ => SafeTick(), null, TimeSpan.FromSeconds(1), PollInterval);
    }

    public void Stop()
    {
        IsRunning = false;
        _timer?.Dispose();
        _timer = null;
    }

    private void SafeTick()
    {
        try { Tick(); }
        catch { /* Detection must never crash the app. */ }
    }

    private void Tick()
    {
        if (!_settings.Settings.DetectionEnabled)
            return;

        var (platform, detail) = Detect();

        lock (_gate)
        {
            if (platform != MeetingPlatform.Unknown)
            {
                _missCount = 0;
                if (!IsMeetingActive)
                {
                    IsMeetingActive = true;
                    CurrentPlatform = platform;
                    MeetingStarted?.Invoke(this, new MeetingEventArgs { Platform = platform, Detail = detail });
                }
            }
            else if (IsMeetingActive)
            {
                if (++_missCount >= EndGraceTicks)
                {
                    var ended = CurrentPlatform;
                    IsMeetingActive = false;
                    CurrentPlatform = MeetingPlatform.Unknown;
                    _missCount = 0;
                    MeetingEnded?.Invoke(this, new MeetingEventArgs { Platform = ended });
                }
            }
        }
    }

    /// <summary>Returns the strongest matched platform for the current system state.</summary>
    private (MeetingPlatform Platform, string? Detail) Detect()
    {
        var windows = WindowEnumerator.GetVisibleWindows();
        var processes = RunningProcessNames();
        bool mediaActive = MediaUsageMonitor.IsAnyInUse();

        // 1) Zoom desktop client — its dedicated meeting window is definitive.
        if (processes.Contains("Zoom") || processes.Contains("Zoom.exe"))
        {
            var w = windows.FirstOrDefault(w =>
                IsZoomLike(w.ProcessName) && ZoomMeetingTitle().IsMatch(w.Title));
            if (!w.Equals(default(WindowInfo)) && !string.IsNullOrEmpty(w.Title))
                return (MeetingPlatform.ZoomDesktop, w.Title);
        }

        // 2) Browser-hosted meetings — title attributes the platform; mic/cam confirms a live call.
        foreach (var w in windows)
        {
            if (!BrowserProcesses.Contains(w.ProcessName))
                continue;

            if (GoogleMeetTitle().IsMatch(w.Title) && mediaActive)
                return (MeetingPlatform.GoogleMeet, w.Title);

            if (TeamsWebTitle().IsMatch(w.Title) && mediaActive)
                return (MeetingPlatform.TeamsWeb, w.Title);

            if (ZoomWebTitle().IsMatch(w.Title) && mediaActive)
                return (MeetingPlatform.ZoomWeb, w.Title);
        }

        // 3) Microsoft Teams desktop — running client plus an active mic/cam session.
        if (mediaActive && (processes.Contains("ms-teams") || processes.Contains("msteams") || processes.Contains("Teams")))
            return (MeetingPlatform.TeamsDesktop, "Microsoft Teams call");

        return (MeetingPlatform.Unknown, null);
    }

    private static bool IsZoomLike(string proc) =>
        proc.StartsWith("Zoom", StringComparison.OrdinalIgnoreCase);

    private static HashSet<string> RunningProcessNames()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                try { set.Add(p.ProcessName); } catch { }
                finally { p.Dispose(); }
            }
        }
        catch { }
        return set;
    }

    // "Zoom Meeting" is the title of the active Zoom desktop meeting window.
    [GeneratedRegex(@"\bZoom Meeting\b", RegexOptions.IgnoreCase)]
    private static partial Regex ZoomMeetingTitle();

    // Google Meet sets the title to the meeting name or a code like abc-defg-hij.
    [GeneratedRegex(@"(Google Meet|\bMeet\b.*\b\w{3,4}-\w{3,4}-\w{3,4}\b|\b\w{3}-\w{4}-\w{3}\b)", RegexOptions.IgnoreCase)]
    private static partial Regex GoogleMeetTitle();

    [GeneratedRegex(@"Microsoft Teams", RegexOptions.IgnoreCase)]
    private static partial Regex TeamsWebTitle();

    [GeneratedRegex(@"(Zoom Meeting|zoom\.us|Meeting\s*-\s*Zoom)", RegexOptions.IgnoreCase)]
    private static partial Regex ZoomWebTitle();

    public void Dispose() => Stop();
}
