using CodexUsageWidget.Core;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using Windows.UI.ViewManagement;

namespace CodexUsageWidget;

// Mirrors src/main.cjs: a 390x600 content-sized widget. Mini mode, tray,
// and window behaviors are layered on in later steps.
public sealed partial class MainWindow : Window
{
    public const int FullWidth = 390;
    public const int FullHeight = 600;

    private readonly SettingsStore _settings = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexUsageWidget"));
    private readonly UISettings _uiSettings = new();
    private string _themeSource = "system";

    public MainWindow()
    {
        InitializeComponent();
        _themeSource = _settings.LoadThemeSource();
        ApplyTheme();
        _uiSettings.ColorValuesModeChanged += (_, _) => DispatcherQueue.TryEnqueue(ApplyTheme);
        AppWindow.ResizeClient(new SizeInt32(FullWidth, FullHeight));
    }

    // Auto / Light / Dark override, like nativeTheme.themeSource: Auto
    // follows the OS, and the choice persists across launches.
    public string SetThemeSource(string theme)
    {
        _themeSource = theme is "light" or "dark" ? theme : "system";
        _settings.SaveThemeSource(_themeSource);
        ApplyTheme();
        return _themeSource;
    }

    private void ApplyTheme()
    {
        Root.RequestedTheme = _themeSource switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }
}
