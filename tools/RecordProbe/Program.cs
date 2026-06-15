// Isolates whether ScreenRecorderLib can record on this machine at all.
// Tries hardware encoding first, then software, and reports file size / errors for each.
using ScreenRecorderLib;

string dir = Path.Combine(Path.GetTempPath(), "MeetCapRecProbe");
Directory.CreateDirectory(dir);

RunTest("hardware", hardware: true);
RunTest("software", hardware: false);

static void RunTest(string label, bool hardware)
{
    string path = Path.Combine(Path.GetTempPath(), "MeetCapRecProbe", $"probe_{label}.mp4");
    if (File.Exists(path)) File.Delete(path);

    Console.WriteLine($"\n=== Test: {label} encoding ===");
    string? error = null;
    bool completed = false;
    var done = new ManualResetEventSlim(false);

    var options = new RecorderOptions
    {
        SourceOptions = SourceOptions.MainMonitor,
        AudioOptions = new AudioOptions { IsAudioEnabled = true, IsOutputDeviceEnabled = true, IsInputDeviceEnabled = false },
        VideoEncoderOptions = new VideoEncoderOptions
        {
            Encoder = new H264VideoEncoder { BitrateMode = H264BitrateControlMode.Quality, EncoderProfile = H264Profile.High },
            Bitrate = 8_000_000,
            Framerate = 30,
            IsHardwareEncodingEnabled = hardware,
            IsMp4FastStartEnabled = true,
            IsFragmentedMp4Enabled = true,
        },
        OutputOptions = new OutputOptions { RecorderMode = RecorderMode.Video },
    };

    var rec = Recorder.CreateRecorder(options);
    rec.OnRecordingFailed += (_, e) => { error = e.Error; done.Set(); };
    rec.OnRecordingComplete += (_, _) => { completed = true; done.Set(); };
    rec.OnStatusChanged += (_, e) => Console.WriteLine($"  status: {e.Status}");

    Console.WriteLine($"  recording 4s -> {path}");
    rec.Record(path);
    Thread.Sleep(4000);
    rec.Stop();
    done.Wait(10000);

    var size = File.Exists(path) ? new FileInfo(path).Length : -1;
    Console.WriteLine($"  completed={completed} error={(error ?? "none")} fileBytes={size}");
    Console.WriteLine(size > 10000 && error is null ? "  RESULT: PASS" : "  RESULT: FAIL");
    rec.Dispose();
}
