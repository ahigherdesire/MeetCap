using Microsoft.Win32;

namespace MeetCap.Interop;

/// <summary>
/// Reads the Windows Capability Access Manager consent store to tell whether the
/// microphone or webcam is currently in use by some app. When an app is actively
/// using the device, its <c>LastUsedTimeStop</c> value is 0. This is a clean,
/// privacy-respecting "someone is in a call" signal that requires no hooks.
/// </summary>
public static class MediaUsageMonitor
{
    private const string MicPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";
    private const string CamPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam";

    public static bool IsMicrophoneInUse() => IsCapabilityInUse(MicPath);

    public static bool IsWebcamInUse() => IsCapabilityInUse(CamPath);

    /// <summary>True if either the microphone or the webcam is currently active.</summary>
    public static bool IsAnyInUse() => IsMicrophoneInUse() || IsWebcamInUse();

    private static bool IsCapabilityInUse(string basePath)
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(basePath);
            if (root is null)
                return false;

            // Direct per-app keys (packaged apps) ...
            if (AnyChildActive(root))
                return true;

            // ... and NonPackaged\<app> keys (classic desktop apps like Zoom/Teams/Chrome).
            using var nonPackaged = root.OpenSubKey("NonPackaged");
            if (nonPackaged is not null && AnyChildActive(nonPackaged))
                return true;
        }
        catch
        {
            // Registry access can fail under locked-down policies; treat as "not in use".
        }

        return false;
    }

    private static bool AnyChildActive(RegistryKey parent)
    {
        foreach (var name in parent.GetSubKeyNames())
        {
            if (string.Equals(name, "NonPackaged", StringComparison.OrdinalIgnoreCase))
                continue;

            using var child = parent.OpenSubKey(name);
            if (child is null)
                continue;

            var stop = child.GetValue("LastUsedTimeStop");
            // A value of 0 means the device session is still open (in use right now).
            if (stop is long l && l == 0)
                return true;
        }

        return false;
    }
}
