using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace CodexUsageWidget;

// Mirrors src/main.cjs: a 390x600 content-sized widget. Mini mode, tray,
// and settings handling are layered on in later steps.
public sealed partial class MainWindow : Window
{
    public const int FullWidth = 390;
    public const int FullHeight = 600;

    public MainWindow()
    {
        InitializeComponent();
        AppWindow.ResizeClient(new SizeInt32(FullWidth, FullHeight));
    }
}
