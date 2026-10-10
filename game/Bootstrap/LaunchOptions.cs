namespace StarPlayer.Client.Bootstrap;

/// <summary>
/// Diagnostic launch mode (ROADMAP Phase 0): <c>-- --diagnostics [--diagnostics-out=FILE] [--screenshot=FILE]</c>.
/// Runs the probe without clicking, writes the result, then exits 0 (pass) or 1 (fail).
/// Phase 1 adds <c>--player-diagnostics</c> (open the player card, run its self-check, dump the text, exit 0/1) and
/// <c>--cheat</c> (start the session in cheat mode; it still hides potential). Phase 2 adds <c>--game-diagnostics</c> (open the game screen,
/// step the scripted games, run its self-check, dump the text, exit 0/1).
/// </summary>
public sealed record LaunchOptions(bool RunDiagnostics, bool RunPlayerDiagnostics, bool RunGameDiagnostics, bool CheatMode, string? OutputPath, string? ScreenshotPath)
{
    public static LaunchOptions Parse(IEnumerable<string> args)
    {
        var list = args.ToList();
        return new LaunchOptions(list.Contains("--diagnostics"), list.Contains("--player-diagnostics"), list.Contains("--game-diagnostics"), list.Contains("--cheat"),
            ValueOf(list, "--diagnostics-out="), ValueOf(list, "--screenshot="));
    }

    private static string? ValueOf(List<string> args, string prefix) =>
        args.FirstOrDefault(arg => arg.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
}
