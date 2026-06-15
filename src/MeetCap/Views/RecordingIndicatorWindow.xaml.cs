using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace MeetCap.Views;

/// <summary>Always-on-top pill that makes it unmistakable when recording is active.</summary>
public partial class RecordingIndicatorWindow : Window
{
    private readonly DispatcherTimer _timer;
    private readonly DateTime _startUtc;

    /// <summary>Raised when the user clicks Stop on the indicator.</summary>
    public event EventHandler? StopRequested;

    public RecordingIndicatorWindow(DateTime startUtc)
    {
        InitializeComponent();
        _startUtc = startUtc;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateElapsed();
        Loaded += (_, _) => { PositionTopCenter(); UpdateElapsed(); _timer.Start(); };
    }

    private void PositionTopCenter()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - ActualWidth) / 2;
        Top = area.Top + 16;
    }

    private void UpdateElapsed()
    {
        var span = DateTime.UtcNow - _startUtc;
        ElapsedText.Text = span.ToString(@"hh\:mm\:ss");
    }

    private void OnStopClick(object sender, RoutedEventArgs e) => StopRequested?.Invoke(this, EventArgs.Empty);

    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }
}
