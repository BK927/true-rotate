using Microsoft.UI.Xaml;

namespace TrueRotate;

/// <summary>
/// WinUI 3 Application. No main window is opened on launch — the tray icon
/// and hotkey sink keep the process alive.
/// </summary>
public partial class App : Application
{
    private TrayManager?  _tray;
    private HotkeyWindow? _hotkeyWindow;

    public App()
    {
        this.InitializeComponent();

        // This is a tray app with no persistent XAML window. Without this, WinUI's
        // default (OnLastWindowClose) shuts the dispatcher down — and the whole app —
        // the moment the Settings window is closed. Only our tray "Exit" should quit.
        this.DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;

        // Log-only: crashes still surface to Windows Error Reporting as before.
        this.UnhandledException += (_, e) => Log.Error("Unhandled UI exception", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Error("Unhandled exception", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => Log.Error("Unobserved task exception", e.Exception);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Log.Info($"Started TrueRotate {Diagnostics.AppVersion()} on {Environment.OSVersion.VersionString}");

        var store   = new OrientationStore();
        L.SetLanguage(store.Language);
        var reapply = new ReapplyController(store, ShowNotification);

        _hotkeyWindow = new HotkeyWindow(OnHotkey, reapply.OnDisplayChange);
        _tray = new TrayManager(store, _hotkeyWindow, OnOpenSettings, OnExitApp);
        _tray.RegisterHotkeys();
    }

    // ── Hotkey dispatch ───────────────────────────────────────────────────────

    private void OnHotkey(int id)
    {
        _tray?.HandleHotkey(id);
    }

    // ── Settings window ───────────────────────────────────────────────────────

    private SettingsWindow? _settingsWindow;

    private void OnOpenSettings()
    {
        try
        {
            if (_settingsWindow is not null)
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new SettingsWindow(_tray!.Store, _tray!.ReregisterHotkeys);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Activate();
        }
        catch (Exception ex)
        {
            _settingsWindow = null;
            Log.Error("Opening settings failed", ex);
            _tray?.ShowBalloon(L.Get("BalloonSettingsErrorTitle"), ex.Message);
        }
    }

    // ── Notifications ─────────────────────────────────────────────────────────

    private void ShowNotification(string title, string text)
    {
        _tray?.ShowBalloon(title, text);
    }

    // ── Exit ──────────────────────────────────────────────────────────────────

    private void OnExitApp()
    {
        Log.Info("Exit requested from tray");
        _tray?.Dispose();
        _hotkeyWindow?.Dispose();
        Exit();
    }
}
