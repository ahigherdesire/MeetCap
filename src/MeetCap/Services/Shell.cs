using System.Diagnostics;

namespace MeetCap.Services;

/// <summary>Thin wrapper around launching shell processes (open files/folders/URLs).</summary>
public static class Shell
{
    /// <summary>Open a file, folder, or URL with the default handler.</summary>
    public static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch
        {
            // Ignore: nothing useful to do if the shell refuses to open the target.
        }
    }

    /// <summary>Run an executable with arguments (no shell execute).</summary>
    public static void Run(string fileName, string arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = false });
        }
        catch
        {
            // Ignore launch failures.
        }
    }
}
