namespace CodexUsageWidget.Core;

// Ports usageTint() and usageToolTip() from src/main.cjs. The tray icon's
// background colour reports how much of the 5-hour window is left; the
// weekly window only feeds the tooltip. Bands are half-open, so exactly
// 20% left is already orange and exactly 60% left is green.
public static class UsageFormatter
{
    public static string Tint(UsageSnapshot? usage)
    {
        var used = usage?.Primary?.UsedPercent;
        if (used is null || !double.IsFinite(used.Value)) return "neutral";
        var remaining = 100 - Math.Max(0, Math.Min(100, used.Value));
        if (remaining < 20) return "red";
        if (remaining < 40) return "orange";
        if (remaining < 60) return "yellow";
        return "green";
    }

    public static string ToolTip(UsageResult? result) => string.Join('\n', ToolTipLines(result));

    // The tooltip without its title line; the tray menu shows these as
    // disabled items on desktops where hover tooltips are unreliable.
    public static IReadOnlyList<string> SummaryLines(UsageResult? result) =>
        ToolTipLines(result).Skip(1).ToList();

    private static IReadOnlyList<string> ToolTipLines(UsageResult? result)
    {
        var lines = new List<string> { "Codex Usage Widget" };
        if (result?.Ok != true || result.Usage is null)
        {
            lines.Add("Usage unavailable");
            return lines;
        }
        if (result.Usage.Source == "local-cache") lines.Add("Offline snapshot — may be stale");
        AddWindow(lines, "5-hour", result.Usage.Primary);
        AddWindow(lines, "Weekly", result.Usage.Secondary);
        return lines;
    }

    private static void AddWindow(List<string> lines, string label, UsageWindow? window)
    {
        if (window is null)
        {
            lines.Add($"{label}: unavailable");
            return;
        }
        var clamped = Math.Max(0, Math.Min(100, window.UsedPercent));
        // Math.round in JS rounds .5 away from zero on this positive range.
        var remaining = (int)(100 - Math.Round(clamped, MidpointRounding.AwayFromZero));
        lines.Add($"{label}: {remaining}% left");
    }
}
