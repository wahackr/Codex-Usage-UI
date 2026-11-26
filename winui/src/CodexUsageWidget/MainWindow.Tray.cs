using CodexUsageWidget.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.System;

namespace CodexUsageWidget;

// Tray icon, close-to-tray, always-on-top, and quit — the window-side half of
// createTray()/trayMenu() in src/main.cjs.
public sealed partial class MainWindow
{
    private TrayService? _tray;
    private bool _quitting;

    private void CreateTray()
    {
        _tray = new TrayService(WinRT.Interop.WindowNative.GetWindowHandle(this), new TrayActions(
            GetAlwaysOnTop: IsAlwaysOnTop,
            // Wrapped in lambdas: the setters return the resulting state, and a
            // method group with a non-void body will not bind to Action<bool>.
            SetAlwaysOnTop: value => SetAlwaysOnTop(value),
            GetLightTheme: () => Root.ActualTheme != ElementTheme.Dark,
            SetLightTheme: light => SetThemeSource(light ? "light" : "dark"),
            GetMiniMode: () => _miniMode,
            SetMiniMode: value => SetMiniMode(value),
            OpenUsage: OpenUsageSettings,
            ShowWindow: ShowFromTray,
            Quit: QuitApp));
    }

    // The close button hides the window instead of ending the app; Quit is the
    // only way out while the tray is alive, matching app.isQuitting in Electron.
    private void AttachCloseToTray()
    {
        AppWindow.Closing += (_, args) =>
        {
            // Without a live tray there is no way back, so closing quits for real.
            if (_quitting || _tray?.IsAlive != true) return;
            args.Cancel = true;
            AppWindow.Hide();
        };
    }

    public void ShowFromTray()
    {
        AppWindow.Show();
        Activate();
    }

    private void QuitApp()
    {
        _quitting = true;
        _tray?.Dispose();
        _tray = null;
        Close();
    }

    public bool IsAlwaysOnTop() => AppWindow.Presenter is OverlappedPresenter presenter && presenter.IsAlwaysOnTop;

    public bool SetAlwaysOnTop(bool value)
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter) presenter.IsAlwaysOnTop = value;
        // The tray menu and the footer switch show the same state, as in
        // updateTraySettings() + the settings broadcast in main.cjs.
        AlwaysOnTopToggle.IsOn = IsAlwaysOnTop();
        _tray?.RefreshState();
        return IsAlwaysOnTop();
    }

    public void OpenUsageSettings() => _ = Launcher.LaunchUriAsync(new Uri(UsageSettingsUrl));
}
