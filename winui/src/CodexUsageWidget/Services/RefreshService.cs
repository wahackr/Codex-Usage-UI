using CodexUsageWidget.Core;

namespace CodexUsageWidget.Services;

// Mirrors refresh() in src/main.cjs: try the live app-server read, fall
// back to the newest local Codex snapshot, and only then report failure.
public sealed class RefreshService : IDisposable
{
    private readonly CodexUsageClient _client = new();

    public event Action<UsageResult>? Updated;

    public async Task<UsageResult> RefreshAsync()
    {
        UsageResult result;
        try
        {
            var usage = UsageNormalizer.Normalize(await _client.ReadRateLimitsAsync());
            result = new UsageResult { Ok = true, Usage = usage };
        }
        catch (Exception liveError)
        {
            try
            {
                result = new UsageResult { Ok = true, Usage = LocalSnapshotReader.Read() };
            }
            catch
            {
                result = new UsageResult { Ok = false, Error = liveError.Message };
            }
        }
        Updated?.Invoke(result);
        return result;
    }

    public void Dispose() => _client.Dispose();
}
