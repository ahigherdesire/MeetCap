using System.IO;

namespace MeetCap.Models;

/// <summary>A recording file shown in the "Recent recordings" list.</summary>
public sealed class RecordingItem
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public DateTime CreatedAt { get; init; }
    public long SizeBytes { get; init; }

    public string SizeDisplay => SizeBytes switch
    {
        >= 1L << 30 => $"{SizeBytes / (double)(1L << 30):0.0} GB",
        >= 1L << 20 => $"{SizeBytes / (double)(1L << 20):0.0} MB",
        >= 1L << 10 => $"{SizeBytes / (double)(1L << 10):0.0} KB",
        _ => $"{SizeBytes} B",
    };

    public string CreatedDisplay => CreatedAt.ToString("MMM d, yyyy · HH:mm");

    public static RecordingItem FromFile(string path)
    {
        var fi = new FileInfo(path);
        return new RecordingItem
        {
            FilePath = fi.FullName,
            FileName = fi.Name,
            CreatedAt = fi.CreationTime,
            SizeBytes = fi.Exists ? fi.Length : 0,
        };
    }
}
