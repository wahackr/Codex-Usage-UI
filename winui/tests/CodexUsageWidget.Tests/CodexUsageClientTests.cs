using CodexUsageWidget.Core;
using Xunit;

namespace CodexUsageWidget.Tests;

// Ports the protocol behavior pinned by the Node client: handshake,
// rate-limit read, per-request timeout, JSON-RPC errors, and fail-all
// when the app-server process dies.
public class CodexUsageClientTests : IDisposable
{
    private static string FakeServer =>
        Path.Combine(AppContext.BaseDirectory,
            OperatingSystem.IsWindows() ? "FakeAppServer.exe" : "FakeAppServer");

    private static CodexUsageClient CreateClient(string mode, TimeSpan? timeout = null)
    {
        // The child apphost must locate the same runtime the tests run on,
        // which matters when dotnet lives outside the default install path.
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        Environment.SetEnvironmentVariable("DOTNET_ROOT",
            Path.GetFullPath(Path.Combine(runtimeDirectory, "..", "..", "..")));
        Environment.SetEnvironmentVariable("FAKE_MODE", mode);
        return new CodexUsageClient(FakeServer, timeout);
    }

    public void Dispose() => Environment.SetEnvironmentVariable("FAKE_MODE", null);

    [Fact]
    public async Task HandshakeThenReadRateLimitsReturnsThePayload()
    {
        using var client = CreateClient("ok");
        var result = await client.ReadRateLimitsAsync();
        Assert.Equal("acct", result?["accountId"]?.ToString());
        Assert.Equal("plus", result?["rateLimits"]?["planType"]?.ToString());
        Assert.Equal(30, (int)result!["rateLimits"]!["primary"]!["usedPercent"]!);
    }

    [Fact]
    public async Task SilentServerTimesOutWithTheRequestName()
    {
        using var client = CreateClient("silent", TimeSpan.FromMilliseconds(500));
        var error = await Assert.ThrowsAsync<TimeoutException>(client.ReadRateLimitsAsync);
        Assert.Equal("Codex timed out while calling account/rateLimits/read", error.Message);
    }

    [Fact]
    public async Task JsonRpcErrorSurfacesAsTheServerErrorText()
    {
        using var client = CreateClient("error");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(client.ReadRateLimitsAsync);
        Assert.Equal("rate limits unavailable", error.Message);
    }

    [Fact]
    public async Task DyingServerFailsEveryPendingRequest()
    {
        using var client = CreateClient("exit");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(client.ReadRateLimitsAsync);
        Assert.Matches(@"Codex app-server exited \(\d+\)", error.Message);
    }
}
