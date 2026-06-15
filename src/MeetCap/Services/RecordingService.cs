using System.IO;
using MeetCap.Interop;
using MeetCap.Models;
using ScreenRecorderLib;

namespace MeetCap.Services;

/// <summary>
/// Wraps ScreenRecorderLib (Windows.Graphics.Capture + Media Foundation) to record the
/// primary display plus system + microphone audio into a single, non-corrupt MP4.
/// Hardware H.264 encoding (NVENC / QuickSync / AMF) keeps CPU low and files stable.
/// </summary>
public sealed class RecordingService : IRecordingService
{
    private readonly ISettingsService _settings;
    private Recorder? _recorder;

    public bool IsRecording { get; private set; }
    public string? CurrentFilePath { get; private set; }
    public DateTime StartedAtUtc { get; private set; }

    public event EventHandler? StateChanged;
    public event EventHandler<string>? Failed;
    public event EventHandler<string>? Completed;

    public RecordingService(ISettingsService settings) => _settings = settings;

    public void Start(MeetingPlatform platform = MeetingPlatform.Unknown)
    {
        if (IsRecording)
            return;

        var s = _settings.Settings;
        try
        {
            Directory.CreateDirectory(s.OutputFolder);
            var path = BuildOutputPath(s.OutputFolder, platform);
            var options = BuildOptions(s);

            _recorder = Recorder.CreateRecorder(options);
            _recorder.OnRecordingComplete += OnComplete;
            _recorder.OnRecordingFailed += OnFailed;

            CurrentFilePath = path;
            StartedAtUtc = DateTime.UtcNow;
            IsRecording = true;

            _recorder.Record(path);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            CleanupRecorder();
            IsRecording = false;
            CurrentFilePath = null;
            StateChanged?.Invoke(this, EventArgs.Empty);
            Failed?.Invoke(this, ex.Message);
        }
    }

    public void Stop()
    {
        if (!IsRecording || _recorder is null)
            return;

        try
        {
            // Recorder.Stop is async; finalization arrives via OnRecordingComplete.
            _recorder.Stop();
        }
        catch (Exception ex)
        {
            Failed?.Invoke(this, ex.Message);
        }
    }

    private void OnComplete(object? sender, RecordingCompleteEventArgs e)
    {
        var path = e.FilePath ?? CurrentFilePath;
        IsRecording = false;
        CleanupRecorder();
        StateChanged?.Invoke(this, EventArgs.Empty);
        if (!string.IsNullOrEmpty(path))
            Completed?.Invoke(this, path);
        CurrentFilePath = null;
    }

    private void OnFailed(object? sender, RecordingFailedEventArgs e)
    {
        IsRecording = false;
        CleanupRecorder();
        StateChanged?.Invoke(this, EventArgs.Empty);
        Failed?.Invoke(this, e.Error ?? "Recording failed.");
        CurrentFilePath = null;
    }

    private void CleanupRecorder()
    {
        if (_recorder is null)
            return;
        _recorder.OnRecordingComplete -= OnComplete;
        _recorder.OnRecordingFailed -= OnFailed;
        try { _recorder.Dispose(); } catch { }
        _recorder = null;
    }

    private static RecorderOptions BuildOptions(AppSettings s)
    {
        var preset = QualityPreset.For(s.Quality);

        var encoder = new H264VideoEncoder
        {
            BitrateMode = H264BitrateControlMode.Quality,
            EncoderProfile = H264Profile.High,
        };

        var video = new VideoEncoderOptions
        {
            Encoder = encoder,
            Bitrate = preset.Bitrate,
            Framerate = preset.Framerate,
            Quality = 70,
            IsHardwareEncodingEnabled = true, // GPU encode (NVENC/QSV/AMF), falls back to SW.
            IsMp4FastStartEnabled = true,      // Move the moov atom to the front for instant playback.
            IsFragmentedMp4Enabled = true,     // Fragmented MP4 survives a crash without corruption.
            IsThrottlingDisabled = false,
        };

        var output = new OutputOptions
        {
            RecorderMode = RecorderMode.Video,
        };

        var capped = CappedOutputSize(preset.MaxHeight);
        if (capped is not null)
            output.OutputFrameSize = capped;

        var audio = new AudioOptions
        {
            IsAudioEnabled = s.RecordSystemAudio || s.RecordMicrophone,
            IsOutputDeviceEnabled = s.RecordSystemAudio,  // system / loopback audio
            IsInputDeviceEnabled = s.RecordMicrophone,    // microphone
            Bitrate = AudioBitrate.bitrate_128kbps,
            Channels = AudioChannels.Stereo,
        };

        return new RecorderOptions
        {
            SourceOptions = SourceOptions.MainMonitor,
            VideoEncoderOptions = video,
            AudioOptions = audio,
            OutputOptions = output,
        };
    }

    /// <summary>If the primary display is taller than <paramref name="maxHeight"/>, return a
    /// downscaled (aspect-preserving) frame size; otherwise null (keep native resolution).</summary>
    private static ScreenSize? CappedOutputSize(int maxHeight)
    {
        if (maxHeight <= 0)
            return null;

        try
        {
            int w = NativeScreen.PrimaryWidth, h = NativeScreen.PrimaryHeight;
            if (h <= maxHeight || h == 0)
                return null;

            double scale = maxHeight / (double)h;
            // Encoders prefer even dimensions.
            int targetW = (int)Math.Round(w * scale / 2) * 2;
            int targetH = (int)Math.Round(maxHeight / 2.0) * 2;
            return new ScreenSize(targetW, targetH);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildOutputPath(string folder, MeetingPlatform platform)
    {
        var token = platform == MeetingPlatform.Unknown ? "Manual" : platform.FileToken();
        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
        var baseName = $"{stamp}_{token}_Recording";
        var path = Path.Combine(folder, baseName + ".mp4");

        int n = 2;
        while (File.Exists(path))
            path = Path.Combine(folder, $"{baseName}-{n++}.mp4");

        return path;
    }
}
