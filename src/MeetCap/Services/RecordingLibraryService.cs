using System.IO;
using MeetCap.Models;

namespace MeetCap.Services;

public interface IRecordingLibraryService
{
    /// <summary>Most recent recordings in the current output folder, newest first.</summary>
    IReadOnlyList<RecordingItem> GetRecent(int max = 25);
    void OpenFolder();
    void RevealInExplorer(string filePath);
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
}
