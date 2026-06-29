using System.Windows;
using MeetCap.Models;
using MeetCap.Views;

namespace MeetCap.Services;

/// <summary>
/// Orchestrates the user-facing flow: meeting detected -> ask/auto-record -> show the
/// recording indicator -> meeting ended -> ask to stop. All UI work is marshaled to the
/// dispatcher; the user always confirms (or has pre-confirmed) before any recording starts.
/// </summary>
public sealed class NotificationCoordinator
{
    private readonly IMeetingDetectionService _detection;
    private readonly IRecordingService _recording;
    private readonly ISettingsService _settings;
    private readonly ILicenseService _license;

    private MeetingPromptWindow? _activePrompt;
    private RecordingIndicatorWindow? _indicator;
    private bool _recordingFromMeeting;
    private MeetingPlatform _recordingPlatform = MeetingPlatform.Unknown;

    public NotificationCoordinator(
        IMeetingDetectionService detection,
        IRecordingService recording,
        ISettingsService settings,
        ILicenseService license)
    {
        _detection = detection;
        _recording = recording;
        _settings = settings;
        _license = license;
    }

    public void Initialize()
    {
        _detection.MeetingStarted += (_, e) => Dispatch(() => OnMeetingStarted(e));
        _detection.MeetingEnded += (_, e) => Dispatch(() => OnMeetingEnded(e));
        _recording.StateChanged += (_, _) => Dispatch(SyncIndicator);
        _recording.Failed += (_, msg) => Dispatch(() => ShowError(msg));
    }

    private static void Dispatch(Action action) =>
        Application.Current?.Dispatcher.Invoke(action);

    private async void OnMeetingStarted(MeetingEventArgs e)
    {
        if (_recording.IsRecording)
            return;

        var action = _settings.Settings.ActionFor(e.Platform);
        if (action == PlatformAction.Ignore)
            return;

        if (action == PlatformAction.AutoRecord)
        {
            StartForMeeting(e.Platform);
            return;
        }

        // Ask.
        if (_activePrompt is not null)
            return;

        var prompt = new MeetingPromptWindow(
            title: "Meeting detected",
            message: $"You appear to be in a {e.Platform.DisplayName()} meeting. Do you want to record it?",
            buttons: new[]
            {
                new PromptButton("Start recording", "start", IsPrimary: true),
                new PromptButton($"Always record {e.Platform.DisplayName()}", "always"),
                new PromptButton("Ignore", "ignore"),
            },
            defaultResult: "ignore");

        _activePrompt = prompt;
        var result = await prompt.ShowAndWaitAsync();
        _activePrompt = null;

        switch (result)
        {
            case "always":
                _settings.Settings.PlatformActions[e.Platform] = PlatformAction.AutoRecord;
                _settings.Save();
                StartForMeeting(e.Platform);
                break;
            case "start":
                StartForMeeting(e.Platform);
                break;
        }
    }

    private void StartForMeeting(MeetingPlatform platform)
    {
        if (_recording.IsRecording)
            return;
        if (!_license.IsRecordingAllowed)
        {
            MessageBox.Show(
                "MeetCap detected a meeting, but recording needs an active license.\n\n" +
                "Open MeetCap → Settings → License to activate.",
                "MeetCap", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _recordingFromMeeting = true;
        _recordingPlatform = platform;
        _recording.Start(platform);
    }

    private async void OnMeetingEnded(MeetingEventArgs e)
    {
        if (!_recording.IsRecording || !_recordingFromMeeting)
            return;
        if (_activePrompt is not null)
            return;

        var prompt = new MeetingPromptWindow(
            title: "Meeting ended?",
            message: $"Your {_recordingPlatform.DisplayName()} meeting appears to have ended. Stop recording and save?",
            buttons: new[]
            {
                new PromptButton("Stop and save", "stop", IsPrimary: true),
                new PromptButton("Continue recording", "continue"),
            },
            // If the user is away, keep recording rather than risk cutting a live call short.
            defaultResult: "continue",
            autoDismissAfter: TimeSpan.FromSeconds(60));

        _activePrompt = prompt;
        var result = await prompt.ShowAndWaitAsync();
        _activePrompt = null;

        if (result == "stop")
            _recording.Stop();
    }

    private void SyncIndicator()
    {
        if (_recording.IsRecording)
        {
            if (_indicator is null)
            {
                _indicator = new RecordingIndicatorWindow(_recording.StartedAtUtc);
                _indicator.StopRequested += (_, _) => _recording.Stop();
                _indicator.Show();
            }
        }
        else
        {
            _recordingFromMeeting = false;
            _recordingPlatform = MeetingPlatform.Unknown;
            if (_indicator is not null)
            {
                _indicator.Close();
                _indicator = null;
            }
        }
    }

    private void ShowError(string message)
    {
        MessageBox.Show(
            $"MeetCap couldn't complete the recording:\n\n{message}",
            "MeetCap", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
