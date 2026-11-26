using System.Globalization;
using CodexUsageWidget.Core;
using CodexUsageWidget.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using Windows.UI.ViewManagement;

namespace CodexUsageWidget;

// Mirrors src/main.cjs + src/renderer/renderer.js: a 390x600 content-sized
// widget refreshed every 60 seconds. The window-mode, tray, and window-behaviour
// halves live in the companion partials.
public sealed partial class MainWindow : Window
{
    public const int FullWidth = 390;
    public const int FullHeight = 600;

    private const string UsageSettingsUrl = "https://chatgpt.com/#settings/Usage";

    private readonly SettingsStore _settings = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexUsageWidget"));
    private readonly UISettings _uiSettings = new();
    private readonly RefreshService _refresh = new();
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private string _themeSource = "system";
    private bool _refreshing;
    private double? _primaryRemaining;
    private double? _secondaryRemaining;

    public MainWindow()
    {
        InitializeComponent();
        _themeSource = _settings.LoadThemeSource();
        ApplyTheme();
        RenderTheme(_themeSource);
        _uiSettings.ColorValuesChanged += (_, _) => DispatcherQueue.TryEnqueue(ApplyTheme);
        AppWindow.ResizeClient(new SizeInt32(FullWidth, FullHeight));
        _miniMode = _settings.LoadMiniMode();
        ApplyMode();
        PrimaryTrack.SizeChanged += (_, _) => ApplyBarWidths();
        SecondaryTrack.SizeChanged += (_, _) => ApplyBarWidths();
        Strip.SizeChanged += (_, _) => { if (_miniMode) UpdateDragRegion(); };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
        _refresh.Updated += Render;
        _refresh.Updated += result => _tray?.Update(result);
        CreateTray();
        AttachCloseToTray();
        AttachWindowBehaviors();
        Root.Loaded += OnRootLoaded;
        Closed += (_, _) =>
        {
            _refreshTimer.Stop();
            _refresh.Dispose();
            _tray?.Dispose();
            _tray = null;
        };
    }

    private async void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        UpdateDragRegion();
        _refreshTimer.Start();
        await RefreshAsync();
        FitToContent();
    }

    // Auto / Light / Dark override, like nativeTheme.themeSource: Auto
    // follows the OS, and the choice persists across launches.
    public string SetThemeSource(string theme)
    {
        _themeSource = theme is "light" or "dark" ? theme : "system";
        _settings.SaveThemeSource(_themeSource);
        ApplyTheme();
        RenderTheme(_themeSource);
        _tray?.RefreshState();
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
        var dark = Root.RequestedTheme == ElementTheme.Dark
            || (_themeSource == "system" && IsSystemDark());
        StripLogo.Source = dark ? "Assets/openai-white.svg" : "Assets/openai.svg";
    }

    private bool IsSystemDark()
    {
        var color = _uiSettings.UIElementColor(UIElementType.WindowText);
        // WindowText is light in dark mode and dark in light mode; use luminance.
        var luminance = 0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B;
        return luminance > 128;
    }

    private async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try { await _refresh.RefreshAsync(); }
        finally { _refreshing = false; }
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => _ = RefreshAsync();

    private void OnThemeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string theme) return;
        RenderTheme(SetThemeSource(theme));
    }

    private void OnOpenUsageClick(object sender, RoutedEventArgs e) => OpenUsageSettings();

    private void RenderTheme(string active)
    {
        foreach (var button in new[] { ThemeAutoButton, ThemeLightButton, ThemeDarkButton })
        {
            button.Style = NamedStyle(button.Tag as string == active ? "ThemePillActiveStyle" : "ThemePillStyle");
        }
    }

    private void Render(UsageResult result)
    {
        if (!result.Ok)
        {
            StatusDot.Style = NamedStyle("DotErrorStyle");
            StripDot.Style = NamedStyle("DotErrorStyle");
            StatusText.Text = string.IsNullOrWhiteSpace(result.Error) ? "Unable to read Codex usage" : result.Error!;
            UpdatedText.Text = "Install Codex CLI and sign in with your ChatGPT account.";
            StripReset.Text = "No data";
            FitToContent();
            return;
        }

        var usage = result.Usage!;
        var live = usage.Source == "live";
        var dotStyle = NamedStyle(live ? "DotLiveStyle" : "DotWarnStyle");
        StatusDot.Style = dotStyle;
        StripDot.Style = dotStyle;
        StatusText.Text = live
            ? usage.OrdinaryUsageAllowed == false ? "Included usage paused" : "Live account usage"
            : "Offline snapshot · may be stale";

        _primaryRemaining = RenderWindow(usage.Primary, PrimaryRemaining, PrimaryLeft, PrimaryTrack, PrimaryBar, PrimaryReset, StripPrimary);
        _secondaryRemaining = RenderWindow(usage.Secondary, SecondaryRemaining, SecondaryLeft, SecondaryTrack, SecondaryBar, SecondaryReset, StripSecondary);
        ApplyBarWidths();

        PlanValue.Text = Capitalize(usage.PlanType);
        CreditsValue.Text = usage.Credits?.Unlimited == true
            ? "Unlimited"
            : usage.Credits?.HasCredits == true
                ? (string.IsNullOrWhiteSpace(usage.Credits.Balance) ? "Available" : usage.Credits.Balance!)
                : "None";
        ResetCreditsValue.Text = usage.ResetCredits.ToString(CultureInfo.CurrentCulture);
        UpdatedText.Text = $"Updated {FormatClock(usage.FetchedAtMs)} · {(live ? "live from OpenAI" : "local Codex cache")}";
        StripReset.Text = ShortReset(new[] { usage.Primary?.ResetsAtMs, usage.Secondary?.ResetsAtMs }
            .Where(value => value is not null && value > 0)
            .DefaultIfEmpty()
            .Min());
        FitToContent();
    }

    // Ports renderWindow(): the big number, the pill, the bar width and
    // colour, the reset line, and the matching mini-mode strip chip.
    private double? RenderWindow(
        UsageWindow? window,
        TextBlock remainingText,
        TextBlock pill,
        FrameworkElement track,
        Border bar,
        TextBlock reset,
        TextBlock chip)
    {
        if (window is null)
        {
            remainingText.Text = "—";
            pill.Text = "—";
            bar.Width = 0;
            bar.Style = NamedStyle("BarNormalStyle");
            chip.Text = "—";
            chip.Style = NamedStyle("ChipInkStyle");
            return null;
        }

        var remaining = Math.Max(0, Math.Min(100, 100 - window.UsedPercent));
        var text = $"{remaining.ToString("0.###", CultureInfo.CurrentCulture)}% left";
        remainingText.Text = text;
        pill.Text = text;
        bar.Width = BarWidth(track, remaining);
        bar.Style = NamedStyle(remaining <= 10 ? "BarDangerStyle" : remaining <= 30 ? "BarWarnStyle" : "BarNormalStyle");
        reset.Text = RelativeReset(window.ResetsAtMs);
        chip.Text = $"{remaining.ToString("0.###", CultureInfo.CurrentCulture)}%";
        chip.Style = NamedStyle(remaining <= 10 ? "ChipDangerStyle" : remaining <= 30 ? "ChipWarnStyle" : "ChipInkStyle");
        return remaining;
    }

    private void ApplyBarWidths()
    {
        PrimaryBar.Width = BarWidth(PrimaryTrack, _primaryRemaining);
        SecondaryBar.Width = BarWidth(SecondaryTrack, _secondaryRemaining);
    }

    private static double BarWidth(FrameworkElement track, double? remaining) =>
        remaining is null ? 0 : Math.Clamp(remaining.Value / 100, 0, 1) * track.ActualWidth;

    // Ports relativeReset(): "Resets in 2d 4h · Mon 3:45 PM".
    private static string RelativeReset(long? timestamp)
    {
        if (timestamp is null or <= 0) return "Reset time unavailable";
        var diff = timestamp.Value - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (diff <= 0) return "Reset due now";
        var minutes = (long)Math.Ceiling(diff / 60_000.0);
        var days = minutes / 1440;
        var hours = minutes % 1440 / 60;
        var relative = days > 0 ? $"{days}d {hours}h" : hours > 0 ? $"{hours}h {minutes % 60}m" : $"{minutes}m";
        var local = DateTimeOffset.FromUnixTimeMilliseconds(timestamp.Value).LocalDateTime;
        return $"Resets in {relative} · {local.ToString("ddd h:mm", CultureInfo.CurrentCulture)}";
    }

    // Ports shortReset(): the compact countdown shown in mini mode.
    private static string ShortReset(long? timestamp)
    {
        if (timestamp is null or <= 0) return "—";
        var diff = timestamp.Value - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (diff <= 0) return "reset now";
        var minutes = (long)Math.Ceiling(diff / 60_000.0);
        var days = minutes / 1440;
        var hours = minutes % 1440 / 60;
        return days > 0 ? $"{days}d {hours}h" : hours > 0 ? $"{hours}h {minutes % 60}m" : $"{minutes}m";
    }

    // Intl's hour/minute/second pattern maps to the culture's long time
    // pattern, which omits the AM/PM designator in some locales.
    private static string FormatClock(long timestampMs)
    {
        var local = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).LocalDateTime;
        var info = DateTimeFormatInfo.CurrentInfo;
        var pattern = info.LongTimePattern;
        if (info.ShortTimePattern.Contains('t') && !pattern.Contains('t')) pattern += " tt";
        return local.ToString(pattern, CultureInfo.CurrentCulture);
    }

    // CSS text-transform: capitalize on the plan name.
    private static string Capitalize(string value) => string.Concat(
        value.Split(' ').Select(word => word.Length == 0
            ? word
            : char.ToUpper(word[0], CultureInfo.CurrentCulture) + word[1..]));

    private static Style NamedStyle(string key) => (Style)Application.Current.Resources[key];
}
