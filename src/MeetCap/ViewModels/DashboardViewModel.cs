using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeetCap.Models;
using MeetCap.Services;

namespace MeetCap.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IRecordingService _recording;
    private readonly IRecordingLibraryService _library;
    private readonly IMeetingDetectionService _detection;
    private readonly ISettingsService _settings;
    private readonly DispatcherTimer _elapsedTimer;

    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private string _statusTitle = "Ready";
    [ObservableProperty] private string _statusDetail = "MeetCap is watching for meetings.";
    [ObservableProperty] private string _elapsed = "00:00:00";
    [ObservableProperty] private string _toggleLabel = "Start recording";
    [ObservableProperty] private string _toggleGlyph = ""; // record
    [ObservableProperty] private string _qualityText = "High";
    [ObservableProperty] private string _folderText = "";

    public ObservableCollection<RecordingItem> RecentRecordings { get; } = new();

    public bool HasRecordings => RecentRecordings.Count > 0;
    public string RecordingsCountText =>
        RecentRecordings.Count == 0 ? "No recordings yet"
        : RecentRecordings.Count == 1 ? "1 recording"
        : $"{RecentRecordings.Count} recordings";

    public DashboardViewModel(
        IRecordingService recording,
        IRecordingLibraryService library,
        IMeetingDetectionService detection,
        ISettingsService settings)
    {
        _recording = recording;
        _library = library;
        _detection = detection;
        _settings = settings;

        _recording.StateChanged += (_, _) => OnDispatcher(RefreshState);
        _detection.MeetingStarted += (_, _) => OnDispatcher(RefreshState);
        _detection.MeetingEnded += (_, _) => OnDispatcher(RefreshState);
        _recording.Completed += (_, _) => OnDispatcher(RefreshRecordings);
        _settings.Changed += (_, _) => OnDispatcher(RefreshInfo);

        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _elapsedTimer.Tick += (_, _) => UpdateElapsed();

        RefreshInfo();
        RefreshState();
        RefreshRecordings();
    }

    private void RefreshInfo()
    {
        QualityText = _settings.Settings.Quality.ToString();
        FolderText = _settings.Settings.OutputFolder;
    }

    private static void OnDispatcher(Action action) =>
        System.Windows.Application.Current?.Dispatcher.Invoke(action);

    private void RefreshState()
    {
        IsRecording = _recording.IsRecording;
        ToggleLabel = IsRecording ? "Stop recording" : "Start recording";

        if (IsRecording)
        {
            StatusTitle = "Recording";
            StatusDetail = _detection.IsMeetingActive
                ? $"Capturing your {_detection.CurrentPlatform.DisplayName()} meeting."
                : "Manual recording in progress.";
            if (!_elapsedTimer.IsEnabled) _elapsedTimer.Start();
            UpdateElapsed();
        }
        else
        {
            _elapsedTimer.Stop();
            Elapsed = "00:00:00";
            if (_detection.IsMeetingActive)
            {
                StatusTitle = "Meeting detected";
                StatusDetail = $"You're in a {_detection.CurrentPlatform.DisplayName()} meeting (not recording).";
            }
            else
            {
                StatusTitle = "Ready";
                StatusDetail = "MeetCap is watching for meetings.";
            }
        }
    }

    private void UpdateElapsed()
    {
        if (!_recording.IsRecording) return;
        var span = DateTime.UtcNow - _recording.StartedAtUtc;
        Elapsed = span.ToString(@"hh\:mm\:ss");
    }

    public void RefreshRecordings()
    {
        RecentRecordings.Clear();
        foreach (var item in _library.GetRecent())
            RecentRecordings.Add(item);
        OnPropertyChanged(nameof(HasRecordings));
        OnPropertyChanged(nameof(RecordingsCountText));
    }

    [RelayCommand]
    private void ToggleRecording()
    {
        if (_recording.IsRecording)
            _recording.Stop();
        else
            _recording.Start();
    }

    [RelayCommand]
    private void OpenFolder() => _library.OpenFolder();

    [RelayCommand]
    private void OpenRecording(RecordingItem? item)
    {
        if (item is not null) Shell.Open(item.FilePath);
    }

    [RelayCommand]
    private void RevealRecording(RecordingItem? item)
    {
        if (item is not null) _library.RevealInExplorer(item.FilePath);
    }

    [RelayCommand]
    private void DeleteRecording(RecordingItem? item)
    {
        if (item is null) return;

        var answer = System.Windows.MessageBox.Show(
            $"Move this recording to the Recycle Bin?\n\n{item.FileName}",
            "Delete recording", System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (answer != System.Windows.MessageBoxResult.Yes)
            return;

        if (_library.Delete(item.FilePath))
            RefreshRecordings();
        else
            System.Windows.MessageBox.Show(
                "Couldn't delete the file — it may be open in another app.",
                "MeetCap", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
    }

    [RelayCommand]
    private void Refresh() => RefreshRecordings();
}
