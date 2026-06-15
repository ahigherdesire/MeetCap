using System.IO;

namespace MeetCap.Services;

/// <summary>
/// Tiny diagnostic logger for meeting detection. Writes timestamped lines to
/// %APPDATA%\MeetCap\detection.log so we can see exactly what the detector observed each tick.
/// Self-truncating so it can't grow without bound.
/// </summary>
public static class DetectionLog
{
    private static readonly object Gate = new();
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeetCap", "detection.log");
    private const long MaxBytes = 1_000_000;

    public static bool Enabled { get; set; } = true;

    public static void Write(string line)
    {
        if (!Enabled) return;
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                if (File.Exists(Path) && new FileInfo(Path).Length > MaxBytes)
                    File.WriteAllText(Path, string.Empty);
                File.AppendAllText(Path, $"{DateTime.Now:HH:mm:ss} {line}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never affect the app.
        }
    }

    public static string LogPath => Path;
}
