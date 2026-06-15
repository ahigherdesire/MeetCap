using MeetCap.Models;

namespace MeetCap.Services;

public interface IRecordingService
{
    bool IsRecording { get; }
    string? CurrentFilePath { get; }
    DateTime StartedAtUtc { get; }

    event EventHandler? StateChanged;
    /// <summary>Raised with a user-facing error message when recording fails.</summary>
    event EventHandler<string>? Failed;
    /// <summary>Raised with the saved file path when a recording completes successfully.</summary>
    event EventHandler<string>? Completed;

    /// <summary>Start recording. <paramref name="platform"/> only affects the file name.</summary>
    void Start(MeetingPlatform platform = MeetingPlatform.Unknown);
    void Stop();
}
