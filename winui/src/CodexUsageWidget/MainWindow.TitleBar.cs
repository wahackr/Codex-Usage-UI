using System.Runtime.InteropServices;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace CodexUsageWidget;

// The min/max/close caption is drawn by DWM, and WinUI 3's RequestedTheme
// never reaches it: an unpackaged app that forces Dark still gets the light
// caption strip. The documented switch is DWMWA_USE_IMMERSIVE_DARK_MODE
// (attribute 20; the pre-2004 builds used 19), so we flip it ourselves from
// the root element's resolved theme.
public sealed partial class MainWindow
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20 = 19;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    // Called after every ApplyTheme() (ActualTheme may have flipped) and after
    // SetBorderAndTitleBar(true, ...) rebuilds the caption on expand. Harmless
    // in mini mode, where the window is borderless and has no caption.
    private void ApplyTitleBarTheme()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (hwnd == IntPtr.Zero) return;
        var dark = Root.ActualTheme == ElementTheme.Dark ? 1 : 0;
        if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeBefore20, ref dark, sizeof(int));
        }
    }

    // Setting the attribute does not repaint a caption that already exists:
    // expanding from mini mode rebuilds the frame and DWM keeps painting the
    // stale light strip until something forces a non-client redraw. Toggling
    // the caption region through InputNonClientPointerSource is the documented
    // trigger, so re-register it (or clear it in mini mode) right after the
    // attribute lands. The rect mirrors UpdateDragRegion()'s expanded-mode
    // default: the whole client area minus the icon-button row, which WinUI
    // paints as the caption anyway — exact bounds do not matter for the
    // invalidation, and AppWindow (SDK 1.7) exposes no frame rect to measure.
    private void RepaintTitleBar()
    {
        var source = InputNonClientPointerSource.GetForWindowId(AppWindow.Id);
        if (_miniMode)
        {
            source.ClearRegionRects(NonClientRegionKind.Caption);
            return;
        }
        var client = AppWindow.ClientSize;
        source.SetRegionRects(NonClientRegionKind.Caption,
            new[] { new RectInt32(0, 0, Math.Max(1, client.Width), Math.Max(1, client.Height - 46)) });
    }
}
