using System.Text.Json.Nodes;

namespace CodexUsageWidget.Core;

// Port of loadMode/saveMode/loadTheme/saveTheme from src/main.cjs,
// keeping the same file names and formats as the Electron userData
// directory so behavior (and debugging) stays familiar.
public sealed class SettingsStore
{
    private static readonly string[] ThemeSources = ["system", "light", "dark"];

    public SettingsStore(string directory) => Directory = directory;

    public string Directory { get; }

    public bool LoadMiniMode()
    {
        try { return File.ReadAllText(Path.Combine(Directory, "widget-mode.json")).Trim() == "mini"; }
        catch { return false; } // first run or unreadable: stay in full mode
    }

    public void SaveMiniMode(bool mini)
    {
        try
        {
            global::System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(Path.Combine(Directory, "widget-mode.json"), mini ? "mini" : "full");
        }
        catch { /* settings are best-effort */ }
    }

    public string LoadThemeSource()
    {
        try
        {
            var saved = JsonNode.Parse(File.ReadAllText(Path.Combine(Directory, "widget-theme.json")));
            var theme = saved?["theme"]?.ToString();
            return theme is not null && ThemeSources.Contains(theme) ? theme : "system";
        }
        catch { return "system"; }
    }

    public void SaveThemeSource(string theme)
    {
        try
        {
            global::System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(Path.Combine(Directory, "widget-theme.json"),
                new JsonObject { ["theme"] = theme }.ToJsonString());
        }
        catch { /* settings are best-effort */ }
    }
}
