using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace MeetCap.Services;

/// <summary>
/// Uses UI Automation (the Windows accessibility API — read-only, injection-free) to confirm a
/// desktop app is genuinely in a call by finding the in-call control surface, e.g. a "Leave" /
/// "Hang up" button in the app's accessibility tree.
///
/// Role and limits:
///  * This is a <b>positive booster only</b> — it can add a detection (notably the muted-and-
///    deafened, camera-off case that emits no audio) but never blocks the audio/title backbone.
///  * It matches localized control names, so the <see cref="LeaveHints"/> list is the tuning point
///    for other languages / future app versions.
///  * Scans are <b>throttled and fully sandboxed</b> so they can't stall or crash detection, and a
///    result is cached between scans.
/// </summary>
public sealed class UiAutomationDetector : IDisposable
{
    // Names of the "leave/end call" control across common variants. Matching is case-insensitive
    // and exact against the control's Name. Extend this for additional languages.
    private static readonly string[] LeaveHints =
    {
        "Leave", "Leave call", "Leave meeting", "Hang up", "Hangup",
        "End call", "End meeting", "Leave (Ctrl+Shift+H)",
    };

    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(5);

    private readonly object _gate = new();
    private UIA3Automation? _automation;
    private Thread? _worker;
    private volatile bool _stop;
    private HashSet<string> _targets = new(StringComparer.OrdinalIgnoreCase);
    private volatile HashSet<string> _result = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the subset of <paramref name="candidateProcessNames"/> that currently own a window
    /// with an in-call control. This is <b>non-blocking</b>: it returns the most recent background
    /// scan result instantly and updates the targets the background worker scans. A UIA scan can be
    /// slow (it walks app accessibility trees), so it must never run on the detection tick — doing so
    /// would stall the whole detector. The fast audio/title path is unaffected if UIA lags.
    /// </summary>
    public HashSet<string> GetInCallProcessNames(ISet<string> candidateProcessNames)
    {
        lock (_gate)
            _targets = new HashSet<string>(candidateProcessNames, StringComparer.OrdinalIgnoreCase);
        EnsureWorker();
        return _result;
    }

    private void EnsureWorker()
    {
        if (_worker is not null) return;
        lock (_gate)
        {
            if (_worker is not null) return;
            _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "MeetCap-UIA" };
            _worker.SetApartmentState(ApartmentState.MTA);
            _worker.Start();
        }
    }

    private void WorkerLoop()
    {
        while (!_stop)
        {
            HashSet<string> targets;
            lock (_gate)
                targets = new HashSet<string>(_targets, StringComparer.OrdinalIgnoreCase);

            if (targets.Count > 0)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    _result = Scan(targets);
                    DetectionLog.Write($"[uia] scan {string.Join("/", targets)} -> [{string.Join(",", _result)}] in {sw.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    DisposeAutomation();
                    _result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    DetectionLog.Write($"[uia] scan ERROR after {sw.ElapsedMilliseconds}ms: {ex.GetType().Name}");
                }
            }
            else
            {
                _result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            Thread.Sleep(ScanInterval);
        }
    }

    private HashSet<string> Scan(ISet<string> processNames)
    {
        var inCall = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            _automation ??= new UIA3Automation();
            var desktop = _automation.GetDesktop();
            var windows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));

            foreach (var window in windows)
            {
                var owner = OwnerProcessName(window, processNames);
                if (owner is null || inCall.Contains(owner))
                    continue;
                if (HasLeaveControl(window))
                    inCall.Add(owner);
            }
        }
        catch
        {
            // UIA can throw transiently (apps closing, COM hiccups). Reset and fail soft.
            DisposeAutomation();
        }

        return inCall;
    }

    private static string? OwnerProcessName(AutomationElement window, ISet<string> processNames)
    {
        try
        {
            int pid = window.Properties.ProcessId.ValueOrDefault;
            if (pid <= 0)
                return null;
            using var p = Process.GetProcessById(pid);
            return processNames.Contains(p.ProcessName) ? p.ProcessName : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool HasLeaveControl(AutomationElement window)
    {
        try
        {
            // One native FindFirst with an OR of candidate names — short-circuits on first hit.
            var match = window.FindFirstDescendant(cf =>
            {
                ConditionBase names = cf.ByName(LeaveHints[0]);
                for (int i = 1; i < LeaveHints.Length; i++)
                    names = names.Or(cf.ByName(LeaveHints[i]));
                return cf.ByControlType(ControlType.Button).And(names);
            });
            return match is not null;
        }
        catch
        {
            return false;
        }
    }

    private void DisposeAutomation()
    {
        try { _automation?.Dispose(); } catch { }
        _automation = null;
    }

    public void Dispose()
    {
        _stop = true;
        lock (_gate)
            DisposeAutomation();
    }
}
