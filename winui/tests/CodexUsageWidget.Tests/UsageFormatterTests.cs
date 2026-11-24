using CodexUsageWidget.Core;
using Xunit;

namespace CodexUsageWidget.Tests;

// Ports the tray behavior pinned by test/main.test.cjs: tint bands are
// half-open (20% left is orange, 40% yellow, 60% green), the weekly
// window never tints, and tooltip text matches the Electron tray exactly.
public class UsageFormatterTests
{
    private static UsageSnapshot Usage(double? primaryUsed, double? secondaryUsed = null,
        string source = "live") => new()
    {
        Source = source,
        Primary = primaryUsed is null ? null : new UsageWindow(primaryUsed.Value, 300, null),
        Secondary = secondaryUsed is null ? null : new UsageWindow(secondaryUsed.Value, 10080, null),
    };

    [Theory]
    [InlineData(30, "green")]
    [InlineData(85, "red")]
    [InlineData(65, "orange")]
    [InlineData(45, "yellow")]
    [InlineData(20, "green")]
    // Band edges belong to the lighter band.
    [InlineData(100, "red")]
    [InlineData(80, "orange")]
    [InlineData(60, "yellow")]
    [InlineData(40, "green")]
    public void TintFollowsTheRemainingFiveHourAllowance(double primaryUsed, string expected)
        => Assert.Equal(expected, UsageFormatter.Tint(Usage(primaryUsed, secondaryUsed: 100)));

    [Fact]
    public void MissingUsageKeepsTheNeutralTint()
    {
        Assert.Equal("neutral", UsageFormatter.Tint(null));
        Assert.Equal("neutral", UsageFormatter.Tint(Usage(primaryUsed: null, secondaryUsed: 99)));
    }

    [Fact]
    public void OfflineSnapshotsTintLikeLiveOnes()
        => Assert.Equal("red", UsageFormatter.Tint(Usage(91, source: "local-cache")));

    [Fact]
    public void ToolTipReportsUnavailableUntilUsageArrives()
    {
        Assert.Equal("Codex Usage Widget\nUsage unavailable", UsageFormatter.ToolTip(null));
        Assert.Equal("Codex Usage Widget\nUsage unavailable",
            UsageFormatter.ToolTip(new UsageResult { Ok = false, Error = "boom" }));
    }

    [Fact]
    public void ToolTipShowsRemainingPercentForEveryWindow()
    {
        var result = new UsageResult { Ok = true, Usage = Usage(30, 9) };
        Assert.Equal("Codex Usage Widget\n5-hour: 70% left\nWeekly: 91% left",
            UsageFormatter.ToolTip(result));
    }

    [Fact]
    public void OfflineSnapshotsAreLabeledStaleAndMissingWindowsUnavailable()
    {
        var result = new UsageResult { Ok = true, Usage = Usage(44, source: "local-cache") };
        Assert.Equal("Codex Usage Widget\nOffline snapshot — may be stale\n5-hour: 56% left\nWeekly: unavailable",
            UsageFormatter.ToolTip(result));
    }

    [Fact]
    public void MissingLiveWindowsAreIndividuallyUnavailable()
    {
        var result = new UsageResult { Ok = true, Usage = Usage(primaryUsed: null, secondaryUsed: 15) };
        Assert.Equal("Codex Usage Widget\n5-hour: unavailable\nWeekly: 85% left",
            UsageFormatter.ToolTip(result));
    }

    [Fact]
    public void SummaryLinesDropTheToolTipTitle()
    {
        var result = new UsageResult { Ok = true, Usage = Usage(30, 9) };
        Assert.Equal(new[] { "5-hour: 70% left", "Weekly: 91% left" },
            UsageFormatter.SummaryLines(result));
    }
}
