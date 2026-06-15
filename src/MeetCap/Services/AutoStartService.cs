using System.Diagnostics;
using Microsoft.Win32;

namespace MeetCap.Services;

public interface IAutoStartService
{
    bool IsEnabled();
    void SetEnabled(bool enabled);
}

/// <summary>
/// Manages "start on sign-in" via the per-user HKCU Run key. A per-user logon launch is
/// the correct design for MeetCap: capture must run in the interactive desktop session,
/// so a Windows Service (Session 0) would not work. The Run key is trivially toggleable.
/// </summary>
public sealed class AutoStartService : IAutoStartService
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MeetCap";

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(ValueName) is not null;
        }
        catch
        {
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (key is null)
                return;

            if (enabled)
            {
                // Launch minimized to tray on boot so it doesn't steal focus.
                var exe = GetExecutablePath();
                key.SetValue(ValueName, $"\"{exe}\" --minimized");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Group policy may block HKCU\Run; toggle is best-effort.
        }
    }

    private static string GetExecutablePath()
    {
        // Prefer the host .exe (Process.MainModule) over the managed dll path.
        var main = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrEmpty(main))
            return main;
        return Environment.ProcessPath ?? AppContext.BaseDirectory;
    }
}
