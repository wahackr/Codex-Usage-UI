using CodexUsageWidget.Core;
using Xunit;

namespace CodexUsageWidget.Tests;

// Port of the local session snapshot test in test/codex-client.test.cjs.
public class LocalSnapshotReaderTests
{
    [Fact]
    public void ReadsALocalSessionSnapshotAsAnOfflineFallback()
    {
        var home = Path.Combine(Path.GetTempPath(), $"codex-usage-{Guid.NewGuid():N}");
        try
        {
            var sessions = Path.Combine(home, "sessions", "2026", "10", "02");
            Directory.CreateDirectory(sessions);
            File.WriteAllText(Path.Combine(sessions, "rollout.jsonl"), """
            {"type":"event_msg","payload":{"type":"token_count","rate_limits":{"plan_type":"plus","primary":{"used_percent":44,"window_minutes":300,"resets_at":20},"secondary":{"used_percent":12,"window_minutes":10080,"resets_at":30},"credits":{"has_credits":false,"unlimited":false,"balance":"0"}}}}

            """);
            var usage = LocalSnapshotReader.Read(home);
            Assert.Equal("local-cache", usage.Source);
            Assert.Equal(44, usage.Primary!.UsedPercent);
            Assert.Equal("plus", usage.PlanType);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void MissingSessionLogsThrow()
    {
        var home = Path.Combine(Path.GetTempPath(), $"codex-usage-{Guid.NewGuid():N}");
        var error = Assert.Throws<InvalidOperationException>(() => LocalSnapshotReader.Read(home));
        Assert.Equal("No local Codex usage snapshot found", error.Message);
    }
}
