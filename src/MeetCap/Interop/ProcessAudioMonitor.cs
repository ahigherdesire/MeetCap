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
/// <summary>
/// Active audio sessions split by direction. <see cref="Capture"/> (microphone) is the strong
/// "actually in a call" signal — playing a YouTube video only opens a <see cref="Render"/>
/// (playback) session, while joining a call opens a microphone capture session.
/// </summary>
public readonly record struct AudioActivity(HashSet<string> Render, HashSet<string> Capture)
{
    public bool HasMic(params string[] names) => names.Any(Capture.Contains);
    public bool HasAny(string name) => Render.Contains(name) || Capture.Contains(name);
    public IEnumerable<string> All => Render.Concat(Capture).Distinct(StringComparer.OrdinalIgnoreCase);
}

public static class ProcessAudioMonitor
{
    /// <summary>Active audio sessions, separated into playback (render) and microphone (capture).</summary>
    public static AudioActivity GetActivity()
    {
        var render = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var capture = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        MMDeviceEnumerator? enumerator = null;
        try
        {
            enumerator = new MMDeviceEnumerator();
            Collect(enumerator, DataFlow.Render, render);
            Collect(enumerator, DataFlow.Capture, capture);
        }
        catch
        {
            // No audio devices, RDP session, or locked-down policy — fail soft (no detection).
        }
        finally
        {
            enumerator?.Dispose();
        }
        return new AudioActivity(render, capture);
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
