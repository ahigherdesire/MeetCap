using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using MeetCap.Services;
using MeetCap.ViewModels;
using MeetCap.Views;
using Microsoft.Extensions.DependencyInjection;

namespace MeetCap;

public partial class App : Application
{
    private const string MutexName = "MeetCap.SingleInstance.{8E0F7A12-BFB3-4FE8-B9A5}";

    private Mutex? _singleInstanceMutex;
    private bool _ownsMutex;
    private ServiceProvider? _services;
    private TaskbarIcon? _tray;
    private MainWindow? _mainWindow;
    private bool _exiting;

    public static new App Current => (App)Application.Current;
    public IServiceProvider Services => _services!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single instance: if MeetCap is already running, just exit (the running copy stays).
        _singleInstanceMutex = new Mutex(initiallyOwned: true, MutexName, out bool isNew);
        _ownsMutex = isNew;
        if (!isNew)
        {
            Shutdown();
            return;
        }

        // Keep running in the background even when every window is closed.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _services = BuildServices();

        // Keep the auto-start registry entry in sync with the saved preference.
        var settings = _services.GetRequiredService<ISettingsService>();
        var autoStart = _services.GetRequiredService<IAutoStartService>();
        if (settings.Settings.StartOnBoot != autoStart.IsEnabled())
            autoStart.SetEnabled(settings.Settings.StartOnBoot);

        // Wire up the detection -> popup -> recording flow.
        _services.GetRequiredService<NotificationCoordinator>().Initialize();
        if (settings.Settings.DetectionEnabled)
            _services.GetRequiredService<IMeetingDetectionService>().Start();

        CreateTrayIcon();

        bool startMinimized = e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
        if (!startMinimized)
            ShowMainWindow();
    }

    private static ServiceProvider BuildServices()
    {
        var sc = new ServiceCollection();

        sc.AddSingleton<ISettingsService, SettingsService>();
        sc.AddSingleton<IAutoStartService, AutoStartService>();
        sc.AddSingleton<IRecordingService, RecordingService>();
        sc.AddSingleton<IRecordingLibraryService, RecordingLibraryService>();
        sc.AddSingleton<IMeetingDetectionService, MeetingDetectionService>();
        sc.AddSingleton<NotificationCoordinator>();

        sc.AddSingleton<DashboardViewModel>();
        sc.AddSingleton<SettingsViewModel>();
        sc.AddSingleton<MainViewModel>();
        sc.AddTransient<MainWindow>();

        return sc.BuildServiceProvider();
    }

    private void CreateTrayIcon()
    {
        _tray = new TaskbarIcon
        {
            ToolTipText = "MeetCap — meeting recorder",
            IconSource = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Assets/meetcap.ico", UriKind.Absolute)),
        };
        _tray.TrayMouseDoubleClick += (_, _) => ShowMainWindow();
        _tray.ContextMenu = BuildTrayMenu();
    }

    private System.Windows.Controls.ContextMenu BuildTrayMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();
        var recording = _services!.GetRequiredService<IRecordingService>();

        var open = new System.Windows.Controls.MenuItem { Header = "Open MeetCap" };
        open.Click += (_, _) => ShowMainWindow();

        var record = new System.Windows.Controls.MenuItem { Header = "Start / stop recording" };
        record.Click += (_, _) =>
        {
            if (recording.IsRecording) recording.Stop();
            else recording.Start();
        };

        var settings = new System.Windows.Controls.MenuItem { Header = "Settings" };
        settings.Click += (_, _) =>
        {
            ShowMainWindow();
            _services!.GetRequiredService<MainViewModel>().ShowSettingsCommand.Execute(null);
        };

        var quit = new System.Windows.Controls.MenuItem { Header = "Quit MeetCap" };
        quit.Click += (_, _) => ExitApplication();

        menu.Items.Add(open);
        menu.Items.Add(record);
        menu.Items.Add(settings);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(quit);
        return menu;
    }

    public void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            _mainWindow = _services!.GetRequiredService<MainWindow>();
            _mainWindow.CloseToTrayRequested += () => _mainWindow?.Hide();
        }

        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
    }

    public void ExitApplication()
    {
        if (_exiting) return;
        _exiting = true;

        try
        {
            var recording = _services?.GetService<IRecordingService>();
            if (recording?.IsRecording == true)
                recording.Stop();

            _services?.GetService<IMeetingDetectionService>()?.Stop();
        }
        catch { }

        _tray?.Dispose();
        _services?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        if (_ownsMutex)
        {
            try { _singleInstanceMutex?.ReleaseMutex(); } catch { }
        }
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
