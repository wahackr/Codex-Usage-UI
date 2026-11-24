namespace CodexUsageWidget.Core;

// Shape of the normalized snapshot produced by UsageNormalizer.Normalize,
// matching the object the Electron renderer consumes.
public sealed record UsageWindow(double UsedPercent, int? WindowMinutes, long? ResetsAtMs);

public sealed record CreditsInfo(bool? HasCredits, bool? Unlimited, string? Balance);

public sealed class UsageSnapshot
{
    public string Source { get; set; } = "live";
    public long FetchedAtMs { get; set; }
    public string? AccountId { get; init; }
    public string PlanType { get; init; } = "unknown";
    public bool? OrdinaryUsageAllowed { get; init; }
    public UsageWindow? Primary { get; init; }
    public UsageWindow? Secondary { get; init; }
    public CreditsInfo? Credits { get; init; }
    public int ResetCredits { get; init; }
    public string? ReachedType { get; init; }
}

// Mirrors the { ok, usage, error } envelope sent over IPC in main.cjs.
public sealed class UsageResult
{
    public required bool Ok { get; init; }
    public UsageSnapshot? Usage { get; init; }
    public string? Error { get; init; }
}
