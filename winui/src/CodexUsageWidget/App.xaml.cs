using Microsoft.UI.Dispatching;
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
        UnhandledException += (_, args) => LogFailure(args.Exception, args.Message);
        InitializeComponent();
    }

    private static void LogFailure(Exception exception, string message)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexUsageWidget");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "startup-error.log"), $"{DateTimeOffset.Now:O}\n{message}\n{exception}\n\n");
        }
        catch { /* Logging must not replace the original failure. */ }
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
        // Application has no DispatcherQueue of its own; OnLaunched runs on the
        // UI thread, so the thread's queue is the right one.
        AppInstance.GetCurrent().Activated += (_, _) =>
            DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() => _window?.ShowFromTray());
        _window = new MainWindow();
        _window.Activate();
    }
}
