using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MeetCap.Interop;

/// <summary>A visible top-level window: its title and owning process name.</summary>
public readonly record struct WindowInfo(string Title, string ProcessName);

/// <summary>
/// Enumerates visible top-level windows via Win32. This is a read-only, injection-free
/// way to read browser tab titles (Chrome/Edge expose the active tab title in the window
/// title) and detect app meeting windows such as Zoom's "Zoom Meeting" window.
/// </summary>
public static class WindowEnumerator
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public static IReadOnlyList<WindowInfo> GetVisibleWindows()
    {
        var results = new List<WindowInfo>();
        // Cache pid -> process name to avoid repeated lookups within one enumeration.
        var nameByPid = new Dictionary<uint, string>();

        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            int len = GetWindowTextLength(hWnd);
            if (len <= 0)
                return true;

            var sb = new StringBuilder(len + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            var title = sb.ToString();
            if (string.IsNullOrWhiteSpace(title))
                return true;

            GetWindowThreadProcessId(hWnd, out uint pid);
            if (!nameByPid.TryGetValue(pid, out var procName))
            {
                procName = SafeProcessName(pid);
                nameByPid[pid] = procName;
            }

            results.Add(new WindowInfo(title, procName));
            return true;
        }, IntPtr.Zero);

        return results;
    }

    private static string SafeProcessName(uint pid)
    {
        try
        {
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }
}
