using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace CodexUsageWidget;

public partial class App : Application
{
    // Unpackaged apps have no AUMID, so the single-instance key is a literal.
    private const string SingleInstanceKey = "codex-usage-widget";

    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // A second launch redirects its activation to the running instance and
        // exits; the running instance answers by surfacing its window.
        var instance = AppInstance.FindOrRegisterForKey(SingleInstanceKey);
        if (!instance.IsCurrent)
        {
            await instance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
            return;
        }
        AppInstance.GetCurrent().Activated += (_, _) => DispatcherQueue.TryEnqueue(() => _window?.ShowFromTray());
        _window = new MainWindow();
        _window.Activate();
    }
}
