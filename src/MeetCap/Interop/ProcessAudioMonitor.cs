using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace MeetCap.Interop;

/// <summary>
/// Reports which processes currently have an ACTIVE audio session (playback or recording),
/// using the Windows Core Audio session API.
///
/// Why this beats the global mic/cam signal:
///  * It is <b>per-process</b>, so "in a call" can be attributed to a specific app (Teams /
///    Zoom / the browser) instead of "something on this PC is using the mic."
///  * It catches <b>muted</b> participants: you still hear the meeting, so the app keeps an
///    active render (playback) session open even when your microphone is muted.
/// </summary>
public static class ProcessAudioMonitor
{
    /// <summary>Lower-cased process names that have at least one active audio session.</summary>
    public static HashSet<string> GetProcessNamesWithActiveAudio()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        MMDeviceEnumerator? enumerator = null;
        try
        {
            enumerator = new MMDeviceEnumerator();
            // Render = audio you hear (active during a call even when muted);
            // Capture = your microphone.
            Collect(enumerator, DataFlow.Render, names);
            Collect(enumerator, DataFlow.Capture, names);
        }
        catch
        {
            // No audio devices, RDP session, or locked-down policy — fail soft (no detection).
        }
        finally
        {
            enumerator?.Dispose();
        }
        return names;
    }

    private static void Collect(MMDeviceEnumerator enumerator, DataFlow flow, HashSet<string> names)
    {
        foreach (var device in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            try
            {
                var sessions = device.AudioSessionManager.Sessions;
                if (sessions is null)
                    continue;

                for (int i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    if (session.IsSystemSoundsSession)
                        continue;
                    if (session.State != AudioSessionState.AudioSessionStateActive)
                        continue;

                    var name = SafeProcessName((int)session.GetProcessID);
                    if (!string.IsNullOrEmpty(name))
                        names.Add(name);
                }
            }
            catch
            {
                // Skip a device we can't read.
            }
            finally
            {
                device.Dispose();
            }
        }
    }

    private static string SafeProcessName(int pid)
    {
        if (pid <= 0)
            return string.Empty;
        try
        {
            using var p = Process.GetProcessById(pid);
            return p.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }
}
