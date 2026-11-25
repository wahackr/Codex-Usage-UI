using System.Runtime.InteropServices;
using CodexUsageWidget.Core;

namespace CodexUsageWidget.Services;

// The shell tray icon: its colour reports how much of the 5-hour window is
// left, and its menu mirrors the Electron Tray context menu in src/main.cjs
// (checkbox items, disabled usage lines, Quit).
internal sealed class TrayService : IDisposable
{
    private const uint CallbackMessage = 0x8001; // WM_APP + 1
    private const uint IconId = 1;

    private const uint CommandAlwaysOnTop = 1;
    private const uint CommandLightTheme = 2;
    private const uint CommandCompact = 3;
    private const uint CommandOpenUsage = 4;
    private const uint CommandQuit = 5;

    private const uint MfChecked = 0x8;
    private const uint MfUnchecked = 0x0;

    private static readonly string[] Tints = ["neutral", "red", "orange", "yellow", "green"];

    private readonly IntPtr _hwnd;
    private readonly TrayActions _actions;
    private readonly Dictionary<string, IntPtr> _icons;
    private readonly Win32Tray.WndProc _wndProc; // kept alive for the subclassed window
    private IntPtr _originalProc;
    private IntPtr _currentIcon;
    private UsageResult? _latest;
    private bool _added;
    private bool _disposed;

    public TrayService(IntPtr hwnd, TrayActions actions)
    {
        _hwnd = hwnd;
        _actions = actions;
        _icons = LoadIcons();
        _currentIcon = _icons.TryGetValue("neutral", out var neutral) ? neutral : IntPtr.Zero;
        _wndProc = HandleMessage;
        _originalProc = Win32Tray.SetWindowLongPtr(
            _hwnd, Win32Tray.GwlpWndProc, Marshal.GetFunctionPointerForDelegate(_wndProc));
        var data = BuildIconData();
        _added = Win32Tray.Shell_NotifyIcon(Win32Tray.NimAdd, ref data) != 0;
    }

    // Mirrors refresh()'s tray updates: swap the tinted icon and rewrite the
    // tooltip. The menu is built when it opens, so it is always current.
    public void Update(UsageResult? result)
    {
        _latest = result;
        var tint = UsageFormatter.Tint(result?.Ok == true ? result.Usage : null);
        if (_icons.TryGetValue(tint, out var icon) && icon != IntPtr.Zero) _currentIcon = icon;
        if (!_added) return;
        var data = BuildIconData();
        Win32Tray.Shell_NotifyIcon(Win32Tray.NimModify, ref data);
    }

    // True once the shell has accepted the icon; close-to-tray is skipped when
    // the icon never made it to the notification area.
    public bool IsAlive => _added;

    // The Electron tray menu is rebuilt after every setting change; the native
    // menu is rebuilt on open instead, so there is nothing to do here.
    public void RefreshState()
    {
    }

    private IntPtr HandleMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message != CallbackMessage) return Win32Tray.CallWindowProc(_originalProc, hWnd, message, wParam, lParam);
        switch ((uint)(lParam.ToInt64() & 0xffff))
        {
            case Win32Tray.WmLButtonUp:
            case Win32Tray.WmLButtonDblClk:
                _actions.ShowWindow();
                break;
            case Win32Tray.WmRButtonUp:
            case Win32Tray.WmContextMenu:
                ShowMenu();
                break;
        }
        return IntPtr.Zero;
    }

    private void ShowMenu()
    {
        var menu = Win32Tray.CreatePopupMenu();
        if (menu == IntPtr.Zero) return;
        try
        {
            AddCheckable(menu, CommandAlwaysOnTop, "Always on top", _actions.GetAlwaysOnTop());
            AddCheckable(menu, CommandLightTheme, "Light theme", _actions.GetLightTheme());
            AddCheckable(menu, CommandCompact, "Compact mode", _actions.GetMiniMode());
            Win32Tray.AppendMenu(menu, Win32Tray.MfString, CommandOpenUsage, "Open Codex Settings");
            Win32Tray.AppendMenu(menu, Win32Tray.MfSeparator, 0, null);
            foreach (var line in UsageFormatter.SummaryLines(_latest))
            {
                Win32Tray.AppendMenu(menu, Win32Tray.MfGrayed, 0, line);
            }
            Win32Tray.AppendMenu(menu, Win32Tray.MfSeparator, 0, null);
            Win32Tray.AppendMenu(menu, Win32Tray.MfString, CommandQuit, "Quit");

            // The menu only dismisses on an outside click if the window is
            // already foreground when the menu is shown.
            Win32Tray.SetForegroundWindow(_hwnd);
            Win32Tray.GetCursorPos(out var cursor);
            var command = Win32Tray.TrackPopupMenuEx(
                menu, Win32Tray.TpmRightButton | Win32Tray.TpmReturnCmd, cursor.X, cursor.Y, _hwnd, IntPtr.Zero);
            Dispatch(command);
        }
        finally
        {
            Win32Tray.DestroyMenu(menu);
        }
    }

    private static void AddCheckable(IntPtr menu, uint command, string text, bool isChecked)
    {
        Win32Tray.AppendMenu(menu, Win32Tray.MfString, command, text);
        Win32Tray.CheckMenuItem(menu, command, isChecked ? MfChecked : MfUnchecked);
    }

    private void Dispatch(uint command)
    {
        switch (command)
        {
            case CommandAlwaysOnTop: _actions.SetAlwaysOnTop(!_actions.GetAlwaysOnTop()); break;
            case CommandLightTheme: _actions.SetLightTheme(!_actions.GetLightTheme()); break;
            case CommandCompact: _actions.SetMiniMode(!_actions.GetMiniMode()); break;
            case CommandOpenUsage: _actions.OpenUsage(); break;
            case CommandQuit: _actions.Quit(); break;
        }
    }

    private Win32Tray.NotifyIconData BuildIconData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<Win32Tray.NotifyIconData>(),
        hWnd = _hwnd,
        uId = IconId,
        uFlags = Win32Tray.NifMessage | Win32Tray.NifIcon | Win32Tray.NifTip,
        uCallbackMessage = CallbackMessage,
        hIcon = _currentIcon,
        hIconVista = _currentIcon,
        szTip = UsageFormatter.ToolTip(_latest),
    };

    private static Dictionary<string, IntPtr> LoadIcons()
    {
        var icons = new Dictionary<string, IntPtr>();
        var dir = Path.Combine(AppContext.BaseDirectory, "Assets");
        var size = Win32Tray.GetSystemMetrics(Win32Tray.SmCxSmIcon);
        foreach (var tint in Tints)
        {
            var file = Path.Combine(dir, $"tray-{tint}.ico");
            if (!File.Exists(file)) continue;
            var handle = Win32Tray.LoadImage(IntPtr.Zero, file, Win32Tray.ImageIcon, size, size, Win32Tray.LrLoadFromFile);
            if (handle != IntPtr.Zero) icons[tint] = handle;
        }
        return icons;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_added)
        {
            var data = new Win32Tray.NotifyIconData
            {
                cbSize = (uint)Marshal.SizeOf<Win32Tray.NotifyIconData>(),
                hWnd = _hwnd,
                uId = IconId,
            };
            Win32Tray.Shell_NotifyIcon(Win32Tray.NimDelete, ref data);
            _added = false;
        }
        if (_originalProc != IntPtr.Zero)
        {
            Win32Tray.SetWindowLongPtr(_hwnd, Win32Tray.GwlpWndProc, _originalProc);
            _originalProc = IntPtr.Zero;
        }
        foreach (var icon in _icons.Values) Win32Tray.DestroyIcon(icon);
        _icons.Clear();
    }
}

// The tray menu's hooks into the window, matching the set* helpers in main.cjs.
internal sealed record TrayActions(
    Func<bool> GetAlwaysOnTop,
    Action<bool> SetAlwaysOnTop,
    Func<bool> GetLightTheme,
    Action<bool> SetLightTheme,
    Func<bool> GetMiniMode,
    Action<bool> SetMiniMode,
    Action OpenUsage,
    Action ShowWindow,
    Action Quit);
