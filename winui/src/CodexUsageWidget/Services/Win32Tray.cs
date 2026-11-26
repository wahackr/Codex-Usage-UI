using System.Runtime.InteropServices;

namespace CodexUsageWidget.Services;

// The shell notification icon and its context menu, spoken straight to Win32.
// The Windows App SDK 1.7 managed projections carry no tray type, and pulling
// WinForms into a WinUI 3 project drags the WPF targets along with it, so the
// classic Shell_NotifyIcon + TrackPopupMenu route is the one that stays put.
internal static partial class Win32Tray
{
    public const uint NimAdd = 0;
    public const uint NimModify = 1;
    public const uint NimDelete = 2;

    public const uint NifMessage = 0x1;
    public const uint NifIcon = 0x2;
    public const uint NifTip = 0x4;

    public const uint WmLButtonUp = 0x0202;
    public const uint WmLButtonDblClk = 0x0203;
    public const uint WmRButtonUp = 0x0205;
    public const uint WmContextMenu = 0x007B;

    public const uint MfString = 0x0;
    public const uint MfSeparator = 0x800;
    public const uint MfChecked = 0x8;
    public const uint MfGrayed = 0x1;

    // Return the clicked id instead of posting a message, and let a click
    // outside the menu close it.
    public const uint TpmReturnCmd = 0x100;
    public const uint TpmRightButton = 0x2;

    public const uint ImageIcon = 1;
    public const uint LrLoadFromFile = 0x10;
    public const int SmCxSmIcon = 49;

    public const int GwlpWndProc = -4;

    public delegate IntPtr WndProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uId;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        // Vista-era tail: cbSize covering these fields is what lets the shell
        // accept a 127-character tooltip instead of the classic 63.
        public Guid guidItem;
        public IntPtr hIconVista;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint Shell_NotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr LoadImage(IntPtr hInstance, string name, uint imageType, int width, int height, uint flags);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool AppendMenu(IntPtr menu, uint flags, uint id, string? text);

    [DllImport("user32.dll")]
    public static extern bool CheckMenuItem(IntPtr menu, uint id, uint flags);

    [DllImport("user32.dll")]
    public static extern bool EnableMenuItem(IntPtr menu, uint id, uint flags);

    [DllImport("user32.dll")]
    public static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr hWnd, IntPtr taskbarParams);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr newLong);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CallWindowProc(IntPtr previousProc, IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);
}
