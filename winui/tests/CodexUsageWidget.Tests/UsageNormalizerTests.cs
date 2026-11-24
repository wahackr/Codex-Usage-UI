using System.Text.Json.Nodes;
using CodexUsageWidget.Core;
using Xunit;

namespace CodexUsageWidget.Tests;

// Direct ports of the four tests in test/codex-client.test.cjs.
public class UsageNormalizerTests
{
    [Fact]
    public void NormalizesTheLiveCodexQuotaSnapshot()
    {
        var result = UsageNormalizer.Normalize(JsonNode.Parse("""
        {
          "ordinaryUsageAllowed": true,
          "accountId": "acct",
          "rateLimits": {
            "planType": "plus",
            "primary": { "usedPercent": 30, "windowDurationMins": 300, "resetsAt": 2000 },
            "secondary": { "usedPercent": 9, "windowDurationMins": 10080, "resetsAt": 3000 },
            "credits": { "hasCredits": false, "unlimited": false, "balance": "0" }
          },
          "rateLimitResetCredits": { "availableCount": 1 }
        }
        """), 1000);
        Assert.Equal("plus", result.PlanType);
        Assert.Equal(30, result.Primary!.UsedPercent);
        Assert.Equal(2_000_000, result.Primary.ResetsAtMs);
        Assert.Equal(10080, result.Secondary!.WindowMinutes);
        Assert.Equal(1, result.ResetCredits);
        Assert.Equal(1000, result.FetchedAtMs);
    }

    [Fact]
    public void PrefersTheCodexBucketAndClampsPercentages()
    {
        var result = UsageNormalizer.Normalize(JsonNode.Parse("""
        {
          "rateLimits": { "primary": { "usedPercent": 1 } },
          "rateLimitsByLimitId": { "codex": { "primary": { "usedPercent": 140 } } }
        }
        """));
        Assert.Equal(100, result.Primary!.UsedPercent);
    }

    [Fact]
    public void RejectsAnEmptyResponse()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => UsageNormalizer.Normalize(JsonNode.Parse("{}")));
        Assert.Contains("usage snapshot", error.Message);
    }

    [Fact]
    public void MissingWindowsAreIndividuallyNull()
    {
        var result = UsageNormalizer.Normalize(JsonNode.Parse(
            """{ "rateLimits": { "secondary": { "usedPercent": 15 } } }"""));
        Assert.Null(result.Primary);
        Assert.Equal(15, result.Secondary!.UsedPercent);
    }
}
