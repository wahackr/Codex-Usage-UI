using System.Globalization;
using System.Text.Json.Nodes;

namespace CodexUsageWidget.Core;

// Port of normalizeUsage() in src/codex-client.cjs: prefer the codex
// bucket, clamp percentages to 0..100, and convert reset seconds to ms.
public static class UsageNormalizer
{
    public static UsageSnapshot Normalize(JsonNode? result, long? nowMs = null)
    {
        var rate = result?["rateLimitsByLimitId"]?["codex"] ?? result?["rateLimits"];
        if (rate is null) throw new InvalidOperationException("Codex did not return a usage snapshot");

        return new UsageSnapshot
        {
            Source = "live",
            FetchedAtMs = nowMs ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            AccountId = result?["accountId"]?.ToString(),
            PlanType = rate["planType"]?.ToString() ?? "unknown",
            OrdinaryUsageAllowed = Bool(result?["ordinaryUsageAllowed"]),
            Primary = Window(rate["primary"]),
            Secondary = Window(rate["secondary"]),
            Credits = Credits(rate["credits"]),
            ResetCredits = (int)ToNumber(result?["rateLimitResetCredits"]?["availableCount"]),
            ReachedType = rate["rateLimitReachedType"]?.ToString(),
        };
    }

    private static UsageWindow? Window(JsonNode? value)
    {
        if (value is null) return null;
        var used = Math.Max(0, Math.Min(100, ToNumber(value["usedPercent"])));
        int? minutes = TryNumber(value["windowDurationMins"], out int minutesValue) ? minutesValue : null;
        long? resetsAt = TryNumber(value["resetsAt"], out long resetsValue) ? resetsValue * 1000 : null;
        return new UsageWindow(used, minutes, resetsAt);
    }

    private static CreditsInfo? Credits(JsonNode? node)
    {
        if (node is null) return null;
        return new CreditsInfo(Bool(node["hasCredits"]), Bool(node["unlimited"]), node["balance"]?.ToString());
    }

    private static bool? Bool(JsonNode? node)
    {
        return node is JsonValue json && json.TryGetValue(out bool value) ? value : null;
    }

    private static bool TryNumber<T>(JsonNode? node, out T value) where T : struct
    {
        value = default;
        return node is JsonValue json && json.TryGetValue(out value);
    }

    // Number(value.usedPercent) || 0 in the Node client: anything that is
    // not a finite number becomes 0.
    private static double ToNumber(JsonNode? node)
    {
        if (node is not JsonValue value) return 0;
        if (value.TryGetValue(out double number)) return double.IsFinite(number) ? number : 0;
        if (value.TryGetValue(out string? text)
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return double.IsFinite(parsed) ? parsed : 0;
        }
        return 0;
    }
}
