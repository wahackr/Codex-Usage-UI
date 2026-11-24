using System.Text;
using System.Text.Json.Nodes;

namespace CodexUsageWidget.Core;

// Port of readLocalSnapshot() in src/codex-client.cjs: the newest
// token_count rate_limits event in the local Codex session logs, used as
// an offline fallback and labeled as potentially stale.
public static class LocalSnapshotReader
{
    private const int TailBytes = 512 * 1024;

    public static UsageSnapshot Read(string? codexHome = null)
    {
        codexHome ??= Environment.GetEnvironmentVariable("CODEX_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        var newest = FindNewestJsonl(Path.Combine(codexHome, "sessions"));
        if (newest is null) throw new InvalidOperationException("No local Codex usage snapshot found");

        var (path, mtimeMs) = newest.Value;
        var lines = ReadTailLines(path);
        for (var index = lines.Length - 1; index >= 0; index--)
        {
            JsonNode? line;
            try { line = JsonNode.Parse(lines[index]); }
            catch { continue; }
            if (line?["type"]?.ToString() != "event_msg") continue;
            var payload = line["payload"];
            if (payload?["type"]?.ToString() != "token_count") continue;
            var raw = payload["rate_limits"];
            if (raw is null) continue;

            var usage = UsageNormalizer.Normalize(new JsonObject
            {
                ["rateLimits"] = new JsonObject
                {
                    ["planType"] = raw["plan_type"]?.DeepClone(),
                    ["primary"] = AdaptWindow(raw["primary"]),
                    ["secondary"] = AdaptWindow(raw["secondary"]),
                    ["credits"] = raw["credits"] is { } credits ? new JsonObject
                    {
                        ["hasCredits"] = credits["has_credits"]?.DeepClone(),
                        ["unlimited"] = credits["unlimited"]?.DeepClone(),
                        ["balance"] = credits["balance"]?.DeepClone(),
                    } : null,
                    ["rateLimitReachedType"] = raw["rate_limit_reached_type"]?.DeepClone(),
                },
            }, mtimeMs);
            usage.Source = "local-cache";
            return usage;
        }
        throw new InvalidOperationException("No local Codex usage snapshot found");
    }

    private static JsonObject? AdaptWindow(JsonNode? value)
    {
        if (value is null) return null;
        return new JsonObject
        {
            ["usedPercent"] = value["used_percent"]?.DeepClone(),
            ["windowDurationMins"] = value["window_minutes"]?.DeepClone(),
            ["resetsAt"] = value["resets_at"]?.DeepClone(),
        };
    }

    private static (string Path, long MtimeMs)? FindNewestJsonl(string root)
    {
        if (!Directory.Exists(root)) return null;
        (string Path, long MtimeMs)? newest = null;
        foreach (var file in Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories))
        {
            var mtimeMs = new DateTimeOffset(File.GetLastWriteTimeUtc(file)).ToUnixTimeMilliseconds();
            if (newest is null || mtimeMs > newest.Value.MtimeMs) newest = (file, mtimeMs);
        }
        return newest;
    }

    // Only the tail can contain the newest event; a partial first line
    // simply fails to parse and is skipped, like the Node client.
    private static string[] ReadTailLines(string path)
    {
        var length = new FileInfo(path).Length;
        var bytes = (int)Math.Min(length, TailBytes);
        using var stream = File.OpenRead(path);
        stream.Seek(length - bytes, SeekOrigin.Begin);
        var buffer = new byte[bytes];
        stream.ReadExactly(buffer);
        return Encoding.UTF8.GetString(buffer).Split('\n');
    }
}
