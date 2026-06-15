using System.Runtime.InteropServices;

namespace MeetCap.Interop;

/// <summary>Primary-display pixel dimensions via Win32 (process is per-monitor DPI aware).</summary>
public static class NativeScreen
{
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    public static int PrimaryWidth => GetSystemMetrics(SM_CXSCREEN);
    public static int PrimaryHeight => GetSystemMetrics(SM_CYSCREEN);
}
