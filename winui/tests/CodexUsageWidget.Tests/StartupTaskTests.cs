using CodexUsageWidget.Core;
using Microsoft.Win32;
using Xunit;

namespace CodexUsageWidget.Tests;

public class StartupTaskTests
{
    [Theory]
    [InlineData(@"C:\Program Files\Codex\widget.exe", @"""C:\Program Files\Codex\widget.exe""")]
    [InlineData(@"C:\Codex\widget.exe", @"C:\Codex\widget.exe")]
    [InlineData(@"""C:\Program Files\Codex\widget.exe""", @"""C:\Program Files\Codex\widget.exe""")]
    public void OnlyPathsWithSpacesGetQuoted(string path, string expected) =>
        Assert.Equal(expected, StartupTask.Quote(path));

    [Fact]
    public void MissingKeyAndRegistryLessHostsBothReadAsOff()
    {
        var task = new StartupTask("/tmp/codex-usage-widget.exe", @"Software\CodexUsageWidgetTests\Missing");
        Assert.Equal("/tmp/codex-usage-widget.exe", task.ExecutablePath);
        Assert.False(task.IsEnabled()); // missing key on Windows, no registry elsewhere
    }

    [Fact]
    public void StartupValueRoundTripsUnderATestKey()
    {
        if (!OperatingSystem.IsWindows()) return; // no registry on this host
        var root = @"Software\CodexUsageWidgetTests";
        var task = new StartupTask(@"C:\Program Files\Codex\widget.exe", $@"{root}\{Guid.NewGuid():N}");
        try
        {
            Assert.False(task.IsEnabled());
            Assert.True(task.SetEnabled(true));
            using (var key = Registry.CurrentUser.OpenSubKey(task.RunKeyPath, false))
                Assert.Equal(@"""C:\Program Files\Codex\widget.exe""", key?.GetValue("CodexUsageWidget"));
            Assert.False(task.SetEnabled(false));
            Assert.False(task.IsEnabled());
        }
        finally
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(root, throwOnMissingSubKey: false); }
            catch (Exception) { /* a leftover test key is harmless */ }
        }
    }
}
