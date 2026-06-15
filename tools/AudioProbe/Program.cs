// Standalone proof that per-process audio-session detection works on this machine.
// It plays a quiet tone (creating an audio session owned by THIS process), then runs the
// exact Core Audio enumeration MeetCap uses and checks that this process is attributed correctly.

using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

string me = Process.GetCurrentProcess().ProcessName;
Console.WriteLine($"This process: {me} (pid {Environment.ProcessId})");

Console.WriteLine("Active-audio processes BEFORE playing:");
Print(GetProcessNamesWithActiveAudio());

// Start a quiet 440 Hz tone on the default output device -> opens a render session for this process.
using var output = new WaveOutEvent();
var tone = new SignalGenerator(44100, 2) { Type = SignalGeneratorType.Sin, Frequency = 440, Gain = 0.03 };
output.Init(tone.ToWaveProvider());
output.Play();
Thread.Sleep(900);

var during = GetProcessNamesWithActiveAudio();
Console.WriteLine("Active-audio processes WHILE playing:");
Print(during);

output.Stop();

bool detected = during.Contains(me);
Console.WriteLine();
Console.WriteLine(detected
    ? $"RESULT: PASS — detector correctly attributed the active audio session to \"{me}\"."
    : $"RESULT: FAIL — \"{me}\" was not found among active-audio processes.");
Environment.Exit(detected ? 0 : 1);

static void Print(HashSet<string> names) =>
    Console.WriteLine("  " + (names.Count > 0 ? string.Join(", ", names) : "(none)"));

// --- Same logic as MeetCap.Interop.ProcessAudioMonitor ---
static HashSet<string> GetProcessNamesWithActiveAudio()
{
    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    MMDeviceEnumerator? enumerator = null;
    try
    {
        enumerator = new MMDeviceEnumerator();
        Collect(enumerator, DataFlow.Render, names);
        Collect(enumerator, DataFlow.Capture, names);
    }
    catch { }
    finally { enumerator?.Dispose(); }
    return names;
}

static void Collect(MMDeviceEnumerator enumerator, DataFlow flow, HashSet<string> names)
{
    foreach (var device in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
    {
        try
        {
            var sessions = device.AudioSessionManager.Sessions;
            if (sessions is null) continue;
            for (int i = 0; i < sessions.Count; i++)
            {
                var s = sessions[i];
                if (s.IsSystemSoundsSession) continue;
                if (s.State != AudioSessionState.AudioSessionStateActive) continue;
                int pid = (int)s.GetProcessID;
                if (pid <= 0) continue;
                try { using var p = Process.GetProcessById(pid); names.Add(p.ProcessName); } catch { }
            }
        }
        catch { }
        finally { device.Dispose(); }
    }
}
