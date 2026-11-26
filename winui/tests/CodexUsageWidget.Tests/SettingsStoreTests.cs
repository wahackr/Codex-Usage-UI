using CodexUsageWidget.Core;
using Xunit;

namespace CodexUsageWidget.Tests;

public class SettingsStoreTests
{
    private static SettingsStore CreateStore(out string directory)
    {
        directory = Path.Combine(Path.GetTempPath(), $"codex-widget-{Guid.NewGuid():N}");
        return new SettingsStore(directory);
    }

    [Fact]
    public void MiniModeRoundTripsInWidgetModeJson()
    {
        var store = CreateStore(out var directory);
        try
        {
            Assert.False(store.LoadMiniMode());
            store.SaveMiniMode(true);
            Assert.True(store.LoadMiniMode());
            Assert.Equal("mini", File.ReadAllText(Path.Combine(directory, "widget-mode.json")));
            store.SaveMiniMode(false);
            Assert.False(store.LoadMiniMode());
        }
        finally { global::System.IO.Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void ThemeRoundTripsAndUnknownValuesFallBackToSystem()
    {
        var store = CreateStore(out var directory);
        try
        {
            Assert.Equal("system", store.LoadThemeSource());
            store.SaveThemeSource("dark");
            Assert.Equal("dark", store.LoadThemeSource());
            File.WriteAllText(Path.Combine(directory, "widget-theme.json"), """{"theme":"neon"}""");
            Assert.Equal("system", store.LoadThemeSource());
        }
        finally { global::System.IO.Directory.Delete(directory, recursive: true); }
    }
}
