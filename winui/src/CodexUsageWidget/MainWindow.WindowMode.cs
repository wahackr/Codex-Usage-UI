using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace CodexUsageWidget;

// Mini mode: the same window shrunk to a 390x46 strip, like the Electron
// window recreated frameless with skipTaskbar. WinUI can resize and re-frame
// a live window, so no window is rebuilt here.
public sealed partial class MainWindow
{
    public const int MiniWidth = 390;
    public const int MiniHeight = 46;

    // The expand button plus its gap; kept out of the draggable caption
    // region so the button stays clickable.
    private const int StripButtonZone = 40;

    private bool _miniMode;

    public bool MiniMode
    {
        get => _miniMode;
        set => SetMiniMode(value);
    }

    public bool SetMiniMode(bool value)
    {
        _miniMode = value;
        _settings.SaveMiniMode(_miniMode);
        ApplyMode();
        return _miniMode;
    }

    private void ApplyMode()
    {
        if (AppWindow.Presenter is not OverlappedPresenter presenter) return;
        presenter.SetBorderAndTitleBar(!_miniMode, !_miniMode);
        presenter.IsResizable = !_miniMode;
        presenter.IsMaximizable = !_miniMode;
        presenter.IsShownInSwitchers = !_miniMode;
        FullView.Visibility = _miniMode ? Visibility.Collapsed : Visibility.Visible;
        Strip.Visibility = _miniMode ? Visibility.Visible : Visibility.Collapsed;
        MiniButton.ToolTip = _miniMode ? "Exit mini mode" : "Mini mode";
        AppWindow.ResizeClient(new SizeInt32(_miniMode ? MiniWidth : FullWidth, _miniMode ? MiniHeight : FullHeight));
        UpdateDragRegion();
    }

    // A borderless window has no caption to grab, so the strip itself is
    // registered as the caption region (minus the expand button).
    private void UpdateDragRegion()
    {
        var source = InputNonClientPointerSource.GetForWindowId(AppWindow.Id);
        if (!_miniMode)
        {
            source.ClearRegionRects(NonClientPointerKind.Caption);
            return;
        }
        var width = (int)Math.Max(0, Strip.ActualWidth - StripButtonZone);
        source.SetRegionRects(NonClientPointerKind.Caption, new[] { new RectInt32(0, 0, width, MiniHeight) });
    }

    private void OnMiniClick(object sender, RoutedEventArgs e) => MiniMode = !_miniMode;

    private void OnExpandClick(object sender, RoutedEventArgs e) => MiniMode = false;
}
