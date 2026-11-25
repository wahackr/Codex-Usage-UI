using CodexUsageWidget.Core;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.Graphics;

namespace CodexUsageWidget;

// The window-side settings the footer exposes: grow-to-fit content, always on
// top, and launch-at-login — the ipcMain handlers at the bottom of main.cjs.
public sealed partial class MainWindow
{
    private readonly StartupTask _startup = new();
    private bool _fitting;

    private void AttachWindowBehaviors()
    {
        // Mirrors the renderer's ResizeObserver on <main>: the content drives
        // the window height, and the window never shrinks below what it has.
        FullView.SizeChanged += (_, _) => FitToContent();
        AlwaysOnTopToggle.IsChecked = IsAlwaysOnTop();
        OpenAtLoginToggle.IsChecked = _startup.IsEnabled();
    }

    // Ports ipcMain.handle('window:fitContent'): measure the natural content
    // height, clamp it to the desktop work area, and only ever grow.
    private void FitToContent()
    {
        if (_miniMode || _fitting) return;
        _fitting = true;
        try
        {
            FullView.UpdateLayout();
            FullView.Measure(new Size(FullWidth, double.PositiveInfinity));
            var natural = FullView.DesiredSize.Height;
            if (!double.IsFinite(natural) || natural <= 0) return;

            var client = AppWindow.ClientSize;
            var frameHeight = Math.Max(0, AppWindow.Size.Height - client.Height);
            var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
            var available = Math.Max(0, workArea.Height - frameHeight);
            var fitted = (int)Math.Min(Math.Ceiling(natural), available);
            if (fitted > client.Height) AppWindow.ResizeClient(new SizeInt32(client.Width, fitted));
        }
        catch
        {
            // A display query can fail mid-unplug; the next refresh retries.
        }
        finally
        {
            _fitting = false;
        }
    }

    private void OnAlwaysOnTopClick(object sender, RoutedEventArgs e) => SetAlwaysOnTop(AlwaysOnTopToggle.IsChecked == true);

    // The checkbox follows whatever the registry ended up holding.
    private void OnOpenAtLoginClick(object sender, RoutedEventArgs e) =>
        OpenAtLoginToggle.IsChecked = _startup.SetEnabled(OpenAtLoginToggle.IsChecked == true);
}
