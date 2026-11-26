using Microsoft.Win32;

namespace CodexUsageWidget.Core;

// Windows stand-in for app.setLoginItemSettings({ openAtLogin }) in
// src/main.cjs. An unpackaged app has no startup task to enable, so the
// classic HKCU Run value is what actually starts it at sign-in.
public sealed class StartupTask
{
    private const string DefaultRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CodexUsageWidget";

    // Both paths are injectable so tests never touch the real Run key.
    public StartupTask(string? executablePath = null, string? runKeyPath = null)
    {
        ExecutablePath = executablePath ?? Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0];
        RunKeyPath = runKeyPath ?? DefaultRunKeyPath;
    }

    public string ExecutablePath { get; }

    public string RunKeyPath { get; }

    public bool IsEnabled()
    {
        if (!OperatingSystem.IsWindows()) return false; // the Core layer stays loadable anywhere
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(ValueName) is not null;
        }
        catch (Exception)
        {
            return false; // a locked or missing key reads as "off"
        }
    }

    // Returns the state the registry actually ended up in, like
    // app.getLoginItemSettings().openAtLogin after a set.
    public bool SetEnabled(bool enabled)
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is not null)
            {
                if (enabled) key.SetValue(ValueName, Quote(ExecutablePath));
                else key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception)
        {
            // login startup is best-effort, like the settings files
        }
        return IsEnabled();
    }

    // The Run value is started by the shell, so a path with spaces needs quotes.
    public static string Quote(string path) =>
        path.Contains(' ') && !path.StartsWith('"') ? $"\"{path}\"" : path;
}
