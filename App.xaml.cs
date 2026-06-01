using System.Threading;
using System.Windows;
using FileFinder.Core;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;
using Loc = FileFinder.Localization.Localization;

namespace FileFinder;

public partial class App : Application
{
    private const string MutexName = "FileFinder.SingleInstance.v1";
    private const string ShowEventName = "FileFinder.ShowWindow.v1";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private WinForms.NotifyIcon? _tray;
    private bool _firstHide = true;

    /// <summary>Set when the user truly wants to quit (tray Exit / File → Exit).</summary>
    public static bool ReallyExit { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Hidden console modes exit immediately and never reach the tray/single-instance setup.
        if (e.Args.Length > 0 && e.Args[0] == "--selftest")
        {
            Shutdown(SelfTest.Run());
            return;
        }
        if (e.Args.Length > 0 && e.Args[0] == "--benchindex")
        {
            Shutdown(IndexBench.Run(e.Args.Length > 1 ? e.Args[1] : null));
            return;
        }

        // ---- single instance: a second launch just re-shows the first window ----
        _mutex = new Mutex(true, MutexName, out bool isNew);
        if (!isNew)
        {
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { /* primary gone */ }
            Shutdown();
            return;
        }
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var listener = new Thread(() =>
        {
            while (_showEvent != null && _showEvent.WaitOne())
                Dispatcher.BeginInvoke(new Action(ShowMainWindow));
        })
        { IsBackground = true, Name = "FileFinder.ShowListener" };
        listener.Start();

        // The app stays alive while hidden in the tray; only an explicit Exit quits.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        base.OnStartup(e);
        SetupTray();
    }

    private void SetupTray()
    {
        Drawing.Icon icon;
        try
        {
            var stream = GetResourceStream(new Uri("pack://application:,,,/FileFinder.ico"))!.Stream;
            icon = new Drawing.Icon(stream);
        }
        catch
        {
            icon = Drawing.SystemIcons.Application;
        }

        _tray = new WinForms.NotifyIcon { Icon = icon, Visible = true, Text = "FileFinder" };
        _tray.DoubleClick += (_, _) => ShowMainWindow();

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add(Loc.Instance["TrayOpen"], null, (_, _) => ShowMainWindow());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(Loc.Instance["TrayExit"], null, (_, _) => ExitApplication());
        _tray.ContextMenuStrip = menu;
    }

    private void ShowMainWindow()
    {
        var w = MainWindow;
        if (w == null) return;
        w.Show();
        w.ShowInTaskbar = true;
        if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
        w.Activate();
        w.Topmost = true;   // bounce to foreground
        w.Topmost = false;
    }

    /// <summary>Hide the window to the tray instead of closing.</summary>
    public void HideToTray()
    {
        MainWindow?.Hide();
        if (_firstHide)
        {
            _firstHide = false;
            try { _tray?.ShowBalloonTip(2500, "FileFinder", Loc.Instance["TrayRunning"], WinForms.ToolTipIcon.Info); }
            catch { /* balloons can be suppressed by the OS */ }
        }
    }

    /// <summary>Really quit: remove the tray icon and shut the app down.</summary>
    public void ExitApplication()
    {
        ReallyExit = true;
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _showEvent?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
