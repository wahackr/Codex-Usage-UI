namespace CodexUsageWidget.Core;

// Resolves how to launch the Codex CLI. On Windows an npm-installed `codex`
// often lands as codex.cmd on PATH, which Process.Start refuses with
// UseShellExecute=false; prefer codex.exe and fall back to cmd.exe /c.
public static class CommandResolver
{
    public sealed record LaunchPlan(string FileName, IReadOnlyList<string> Arguments);

    public static LaunchPlan Resolve(string command, IReadOnlyList<string> arguments)
    {
        if (!OperatingSystem.IsWindows()) return new LaunchPlan(command, arguments);

        if (Path.IsPathRooted(command))
        {
            var extension = Path.GetExtension(command).ToLowerInvariant();
            return extension is ".cmd" or ".bat"
                ? new LaunchPlan("cmd.exe", Prepend(command, arguments))
                : new LaunchPlan(command, arguments);
        }

        var exe = FindOnPath(command + ".exe");
        if (exe is not null) return new LaunchPlan(exe, arguments);
        var batch = FindOnPath(command + ".cmd") ?? FindOnPath(command + ".bat");
        return batch is not null
            ? new LaunchPlan("cmd.exe", Prepend(batch, arguments))
            : new LaunchPlan(command, arguments);
    }

    private static IReadOnlyList<string> Prepend(string first, IReadOnlyList<string> rest)
    {
        var all = new List<string>(rest.Count + 2) { "/c", first };
        all.AddRange(rest);
        return all;
    }

    private static string? FindOnPath(string executable)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim(), executable);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
