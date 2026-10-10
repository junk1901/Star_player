namespace StarPlayer.Client.Bootstrap;

/// <summary>
/// Diagnostic launch mode (ROADMAP Phase 0): <c>-- --diagnostics [--diagnostics-out=FILE] [--screenshot=FILE]</c>.
/// Runs the probe without clicking, writes the result, then exits 0 (pass) or 1 (fail).
/// </summary>
public sealed record LaunchOptions(bool RunDiagnostics, string? OutputPath, string? ScreenshotPath)
{
    public static LaunchOptions Parse(IEnumerable<string> args)
    {
        var list = args.ToList();
        return new LaunchOptions(list.Contains("--diagnostics"), ValueOf(list, "--diagnostics-out="), ValueOf(list, "--screenshot="));
    }

    private static string? ValueOf(List<string> args, string prefix) =>
        args.FirstOrDefault(arg => arg.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
}
