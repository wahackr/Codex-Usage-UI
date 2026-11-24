using System.Text.Json;

// Stands in for the `codex app-server` stdio mode in CodexUsageClient tests.
// The FAKE_MODE environment variable selects behavior:
//   ok     - answers initialize and rateLimits/read (default)
//   silent - answers initialize but never answers rateLimits/read
//   error  - returns a JSON-RPC error for rateLimits/read
//   exit   - dies with code 3 as soon as it receives any request
var mode = Environment.GetEnvironmentVariable("FAKE_MODE") ?? "ok";

string? line;
while ((line = Console.ReadLine()) is not null)
{
    JsonElement message;
    try { message = JsonDocument.Parse(line).RootElement; }
    catch { continue; }

    var method = message.TryGetProperty("method", out var methodValue) ? methodValue.GetString() : null;
    var hasId = message.TryGetProperty("id", out var id);

    if (mode == "exit" && hasId) Environment.Exit(3);

    if (method == "initialize" && hasId)
    {
        Respond(id, new { });
    }
    else if (method == "account/rateLimits/read" && hasId)
    {
        if (mode == "silent") continue;
        if (mode == "error")
        {
            Send($"{{\"id\":{id.GetRawText()},\"error\":{{\"message\":{JsonSerializer.Serialize("rate limits unavailable")}}}}}");
            continue;
        }
        Respond(id, new
        {
            accountId = "acct",
            rateLimits = new
            {
                planType = "plus",
                primary = new { usedPercent = 30, windowDurationMins = 300, resetsAt = 2000 },
                secondary = new { usedPercent = 9, windowDurationMins = 10080, resetsAt = 3000 },
                credits = new { hasCredits = false, unlimited = false, balance = "0" },
            },
            rateLimitResetCredits = new { availableCount = 1 },
        });
    }
}

return 0;

void Respond(JsonElement id, object result) =>
    Send($"{{\"id\":{id.GetRawText()},\"result\":{JsonSerializer.Serialize(result)}}}");

void Send(string json)
{
    Console.Out.WriteLine(json);
    Console.Out.Flush();
}
