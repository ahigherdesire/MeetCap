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
    private const int StartConfirmTicks = 2; // require the same platform twice (~3s) before starting.
    private const int EndGraceTicks = 3;     // ~9s of "no meeting" before we declare it ended.

    private static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "arc",
    };

    private readonly ISettingsService _settings;
    private readonly UiAutomationDetector _uia;
    private readonly object _gate = new();
    private Timer? _timer;
    private int _missCount;
    private MeetingPlatform _candidate = MeetingPlatform.Unknown;
    private int _candidateHits;

    public bool IsRunning { get; private set; }
    public bool IsMeetingActive { get; private set; }
    public MeetingPlatform CurrentPlatform { get; private set; } = MeetingPlatform.Unknown;

    public event EventHandler<MeetingEventArgs>? MeetingStarted;
    public event EventHandler<MeetingEventArgs>? MeetingEnded;

    public MeetingDetectionService(ISettingsService settings, UiAutomationDetector uia)
    {
        _settings = settings;
        _uia = uia;
    }

    public void Start()
    {
        if (IsRunning) return;
        IsRunning = true;
        DetectionLog.Write("=== Detection service started ===");
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
        catch (Exception ex) { DetectionLog.Write($"!!! TICK ERROR: {ex.GetType().Name}: {ex.Message}"); }
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

                // Require the same platform on consecutive polls before starting, so a
                // transient sound (e.g. a Teams notification "ding") doesn't trigger a prompt.
                _candidateHits = platform == _candidate ? _candidateHits + 1 : 1;
                _candidate = platform;

                if (!IsMeetingActive && _candidateHits >= StartConfirmTicks)
                {
                    IsMeetingActive = true;
                    CurrentPlatform = platform;
                    DetectionLog.Write($">>> MEETING STARTED: {platform} ({detail}) — firing popup");
                    MeetingStarted?.Invoke(this, new MeetingEventArgs { Platform = platform, Detail = detail });
                }
            }
            else
            {
                _candidate = MeetingPlatform.Unknown;
                _candidateHits = 0;

                if (IsMeetingActive && ++_missCount >= EndGraceTicks)
                {
                    var ended = CurrentPlatform;
                    IsMeetingActive = false;
                    CurrentPlatform = MeetingPlatform.Unknown;
                    _missCount = 0;
                    DetectionLog.Write($"<<< MEETING ENDED: {ended}");
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

        // Primary "in a call" signal: which processes have an active MICROPHONE (capture) session.
        // This is the key precision signal — playing a YouTube video opens only a playback (render)
        // session, whereas joining a call opens a microphone capture session. Using the mic signal
        // avoids false positives like "a Meet tab is open while music plays in another tab".
        var audio = ProcessAudioMonitor.GetActivity();
        bool HasMic(params string[] names) => audio.HasMic(names);

        // Webcam is a strong, independent corroborator (camera on => almost certainly a call).
        bool webcam = MediaUsageMonitor.IsWebcamInUse();

        bool zoomRunning = processes.Contains("Zoom") || processes.Contains("Zoom.exe");
        bool teamsRunning = processes.Contains("ms-teams") || processes.Contains("msteams") || processes.Contains("Teams");

        // UI Automation confirmation for the desktop clients: looks for the in-call control surface
        // (a "Leave"/"Hang up" button). It runs on a background thread (never blocks detection) and
        // only targets an app that currently uses the mic — i.e. a likely call — so it never churns
        // walking an idle Teams/Zoom accessibility tree (which is very slow for the WebView2 client).
        var uiaTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (zoomRunning && HasMic("Zoom")) uiaTargets.Add("Zoom");
        if (teamsRunning && HasMic("ms-teams", "msteams", "Teams"))
        {
            uiaTargets.Add("ms-teams"); uiaTargets.Add("msteams"); uiaTargets.Add("Teams");
        }
        var inCallUia = _uia.GetInCallProcessNames(uiaTargets);
        bool UiaInCall(params string[] names) => names.Any(inCallUia.Contains);

        (MeetingPlatform Platform, string? Detail) result = (MeetingPlatform.Unknown, null);

        // 1) Zoom desktop — dedicated "Zoom Meeting" window is definitive; UIA / mic / cam confirm.
        if (zoomRunning)
        {
            var w = windows.FirstOrDefault(x => IsZoomLike(x.ProcessName) && ZoomMeetingTitle().IsMatch(x.Title));
            if (!string.IsNullOrEmpty(w.Title))
                result = (MeetingPlatform.ZoomDesktop, w.Title);
            else if (UiaInCall("Zoom"))
                result = (MeetingPlatform.ZoomDesktop, "Zoom call (UI Automation)");
            else if (HasMic("Zoom") || webcam)
                result = (MeetingPlatform.ZoomDesktop, "Zoom mic/camera active");
        }

        // WhatsApp is intentionally NOT a supported platform. If a browser is showing WhatsApp
        // (Web), any active mic on that browser most likely belongs to a WhatsApp call — so we skip
        // browser-meeting detection for it, to avoid misattributing it to a background Meet tab.
        var whatsAppBrowsers = windows
            .Where(w => BrowserProcesses.Contains(w.ProcessName) && WhatsAppTitle().IsMatch(w.Title))
            .Select(w => w.ProcessName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 2) Browser-hosted meetings — the tab title attributes the platform; the browser must have
        //    an active MICROPHONE session (or webcam) to confirm a live call. A merely-open Meet tab
        //    with music playing elsewhere has no mic session, so it won't false-trigger.
        if (result.Platform == MeetingPlatform.Unknown)
        {
            foreach (var w in windows)
            {
                if (!BrowserProcesses.Contains(w.ProcessName))
                    continue;
                if (whatsAppBrowsers.Contains(w.ProcessName))
                    continue; // mic belongs to a WhatsApp call, not a meeting

                bool live = HasMic(w.ProcessName) || webcam;
                if (!live)
                    continue;

                if (GoogleMeetTitle().IsMatch(w.Title)) { result = (MeetingPlatform.GoogleMeet, w.Title); break; }
                if (TeamsWebTitle().IsMatch(w.Title)) { result = (MeetingPlatform.TeamsWeb, w.Title); break; }
                if (ZoomWebTitle().IsMatch(w.Title)) { result = (MeetingPlatform.ZoomWeb, w.Title); break; }
            }
        }

        // 3) Microsoft Teams desktop — the client is almost always running, so a definitive UIA hit
        //    wins; otherwise an active Teams microphone session (caught even when muted) or the webcam.
        if (result.Platform == MeetingPlatform.Unknown && teamsRunning)
        {
            if (UiaInCall("ms-teams", "msteams", "Teams"))
                result = (MeetingPlatform.TeamsDesktop, "Teams call (UI Automation)");
            else if (HasMic("ms-teams", "msteams", "Teams") || webcam)
                result = (MeetingPlatform.TeamsDesktop, "Microsoft Teams call");
        }

        // Diagnostic snapshot of everything the detector saw this tick.
        var browserTabs = windows
            .Where(w => BrowserProcesses.Contains(w.ProcessName))
            .Select(w => $"{w.ProcessName}:{w.Title}")
            .ToList();
        DetectionLog.Write(
            $"detect={result.Platform} | mic=[{string.Join(",", audio.Capture)}] | playback=[{string.Join(",", audio.Render)}] | " +
            $"webcam={webcam} | teamsRunning={teamsRunning} zoomRunning={zoomRunning} | " +
            $"uiaInCall=[{string.Join(",", inCallUia)}] | browserWindows=[{string.Join(" || ", browserTabs)}]");

        return result;
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

    // Google Meet titles look like "Meet – abc-defg-hij" (a 3-4-3 letter code). Require the word
    // "Meet" together with a Meet-style code so a random hyphenated title can't false-match.
    [GeneratedRegex(@"(Google Meet|\bMeet\b.*\b[a-z]{3}-[a-z]{4}-[a-z]{3}\b)", RegexOptions.IgnoreCase)]
    private static partial Regex GoogleMeetTitle();

    [GeneratedRegex(@"Microsoft Teams", RegexOptions.IgnoreCase)]
    private static partial Regex TeamsWebTitle();

    [GeneratedRegex(@"(Zoom Meeting|zoom\.us|Meeting\s*-\s*Zoom)", RegexOptions.IgnoreCase)]
    private static partial Regex ZoomWebTitle();

    // WhatsApp is excluded from detection (not a supported platform).
    [GeneratedRegex(@"\bWhatsApp\b", RegexOptions.IgnoreCase)]
    private static partial Regex WhatsAppTitle();

    public void Dispose() => Stop();
}
