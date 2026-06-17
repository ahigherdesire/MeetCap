using System.IO;
using MeetCap.Models;

namespace MeetCap.Services;

public interface IRecordingLibraryService
{
    /// <summary>Most recent recordings in the current output folder, newest first.</summary>
    IReadOnlyList<RecordingItem> GetRecent(int max = 25);
    void OpenFolder();
    void RevealInExplorer(string filePath);
    /// <summary>Send a recording to the Recycle Bin (recoverable). Returns true on success.</summary>
    bool Delete(string filePath);
}

public sealed class RecordingLibraryService : IRecordingLibraryService
{
    private readonly ISettingsService _settings;

    public RecordingLibraryService(ISettingsService settings) => _settings = settings;

    public IReadOnlyList<RecordingItem> GetRecent(int max = 25)
    {
        var folder = _settings.Settings.OutputFolder;
        try
        {
            if (!Directory.Exists(folder))
                return Array.Empty<RecordingItem>();

            return new DirectoryInfo(folder)
                .EnumerateFiles("*.mp4", SearchOption.TopDirectoryOnly)
                .OrderByDescending(f => f.CreationTimeUtc)
                .Take(max)
                .Select(f => RecordingItem.FromFile(f.FullName))
                .ToList();
        }
        catch
        {
            return Array.Empty<RecordingItem>();
        }
    }

    public void OpenFolder()
    {
        var folder = _settings.Settings.OutputFolder;
        Directory.CreateDirectory(folder);
        Shell.Open(folder);
    }

    public void RevealInExplorer(string filePath)
    {
        if (File.Exists(filePath))
            Shell.Run("explorer.exe", $"/select,\"{filePath}\"");
        else
            OpenFolder();
    }

    public bool Delete(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                // Recycle Bin instead of permanent delete, so an accidental delete is recoverable.
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                    filePath,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
