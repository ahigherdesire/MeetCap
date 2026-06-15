using System.ComponentModel;
using MeetCap.ViewModels;
using Wpf.Ui.Controls;

namespace MeetCap.Views;

public partial class MainWindow : FluentWindow
{
    /// <summary>Raised when the user closes the window; the app hides to tray instead of exiting.</summary>
    public event Action? CloseToTrayRequested;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Don't exit — keep MeetCap running in the background. Hide to the tray instead.
        e.Cancel = true;
        CloseToTrayRequested?.Invoke();
        base.OnClosing(e);
    }
}
