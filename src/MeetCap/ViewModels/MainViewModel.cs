using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeetCap.Services;

namespace MeetCap.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IRecordingService _recording;

    public DashboardViewModel Dashboard { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty] private ObservableObject _currentViewModel;
    [ObservableProperty] private bool _isDashboardSelected = true;
    [ObservableProperty] private bool _isRecording;

    public MainViewModel(DashboardViewModel dashboard, SettingsViewModel settings, IRecordingService recording)
    {
        Dashboard = dashboard;
        Settings = settings;
        _recording = recording;
        _currentViewModel = dashboard;

        _recording.StateChanged += (_, _) =>
            System.Windows.Application.Current?.Dispatcher.Invoke(() => IsRecording = _recording.IsRecording);
        IsRecording = _recording.IsRecording;

        // When recording is blocked by licensing, jump the user to the License section in Settings.
        Dashboard.ActivationRequired += () =>
        {
            ShowSettings();
            System.Windows.MessageBox.Show(
                "Recording needs an active license. Enter your license key below to continue.",
                "MeetCap", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        };
    }

    [RelayCommand]
    private void ShowDashboard()
    {
        CurrentViewModel = Dashboard;
        IsDashboardSelected = true;
        Dashboard.RefreshRecordings();
    }

    [RelayCommand]
    private void ShowSettings()
    {
        CurrentViewModel = Settings;
        IsDashboardSelected = false;
    }
}
