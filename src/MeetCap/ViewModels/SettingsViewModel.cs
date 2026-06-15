using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeetCap.Models;
using MeetCap.Services;

namespace MeetCap.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IAutoStartService _autoStart;
    private bool _loading;

    [ObservableProperty] private string _outputFolder = string.Empty;
    [ObservableProperty] private RecordingQuality _quality;
    [ObservableProperty] private bool _recordSystemAudio;
    [ObservableProperty] private bool _recordMicrophone;
    [ObservableProperty] private bool _startOnBoot;
    [ObservableProperty] private bool _detectionEnabled;
    [ObservableProperty] private bool _consentAcknowledged;
    [ObservableProperty] private string _appVersion = "1.0.0";

    public IReadOnlyList<RecordingQuality> Qualities { get; } = new[]
    {
        RecordingQuality.Standard, RecordingQuality.High, RecordingQuality.Ultra,
    };

    public ObservableCollection<PlatformActionItem> Platforms { get; } = new();

    public string QualityDescription => QualityPreset.For(Quality).Description;

    public SettingsViewModel(ISettingsService settingsService, IAutoStartService autoStart)
    {
        _settingsService = settingsService;
        _autoStart = autoStart;
        Load();
    }

    private void Load()
    {
        _loading = true;
        var s = _settingsService.Settings;
        OutputFolder = s.OutputFolder;
        Quality = s.Quality;
        RecordSystemAudio = s.RecordSystemAudio;
        RecordMicrophone = s.RecordMicrophone;
        DetectionEnabled = s.DetectionEnabled;
        ConsentAcknowledged = s.ConsentAcknowledged;
        // Reflect the real OS state for auto-start, not just the stored flag.
        StartOnBoot = _autoStart.IsEnabled();

        Platforms.Clear();
        foreach (MeetingPlatform p in new[]
        {
            MeetingPlatform.ZoomDesktop, MeetingPlatform.TeamsDesktop,
            MeetingPlatform.GoogleMeet, MeetingPlatform.ZoomWeb, MeetingPlatform.TeamsWeb,
        })
        {
            Platforms.Add(new PlatformActionItem(p, s.ActionFor(p), OnPlatformActionChanged));
        }

        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        if (v is not null) AppVersion = $"{v.Major}.{v.Minor}.{v.Build}";
        _loading = false;
    }

    private void OnPlatformActionChanged(MeetingPlatform platform, PlatformAction action)
    {
        _settingsService.Settings.PlatformActions[platform] = action;
        Persist();
    }

    private void Persist()
    {
        if (_loading) return;
        _settingsService.Save();
    }

    partial void OnQualityChanged(RecordingQuality value)
    {
        _settingsService.Settings.Quality = value;
        OnPropertyChanged(nameof(QualityDescription));
        Persist();
    }

    partial void OnRecordSystemAudioChanged(bool value)
    {
        _settingsService.Settings.RecordSystemAudio = value;
        Persist();
    }

    partial void OnRecordMicrophoneChanged(bool value)
    {
        _settingsService.Settings.RecordMicrophone = value;
        Persist();
    }

    partial void OnDetectionEnabledChanged(bool value)
    {
        _settingsService.Settings.DetectionEnabled = value;
        Persist();
    }

    partial void OnConsentAcknowledgedChanged(bool value)
    {
        _settingsService.Settings.ConsentAcknowledged = value;
        Persist();
    }

    partial void OnStartOnBootChanged(bool value)
    {
        if (_loading) return;
        _autoStart.SetEnabled(value);
        _settingsService.Settings.StartOnBoot = value;
        Persist();
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose where MeetCap saves your recordings",
            InitialDirectory = OutputFolder,
        };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            OutputFolder = dialog.FolderName;
            _settingsService.Settings.OutputFolder = dialog.FolderName;
            Persist();
        }
    }
}
