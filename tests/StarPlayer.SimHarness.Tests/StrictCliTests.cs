using System.Diagnostics;
using StarPlayer.SimHarness;

namespace StarPlayer.SimHarness.Tests;

/// <summary>
/// F-C2-A Part A: the command line is parsed against one per-command option whitelist (<see cref="Cli"/>). A misspelt, repeated, valueless, malformed or extra argument is bad input (exit 2) and is
/// never replaced by a default. The counterexample that started this: <c>run --seedz 1</c> used to play the default seeds 3, 11, 29 and exit 0.
/// </summary>
public class StrictCliTests
{
    private static string TempDir() => Path.Combine(Path.GetTempPath(), "simharness-tests", "strict-" + Guid.NewGuid().ToString("N"));

    private sealed record Child(int ExitCode, string Output, string Error);

    private static Child Spawn(params string[] arguments)
    {
        var host = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "") == "dotnet" ? Environment.ProcessPath! : "dotnet";
        var start = new ProcessStartInfo(host) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        foreach (var a in arguments)
            start.ArgumentList.Add(a);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(120_000), "the harness process did not finish in 120 s");
        return new Child(process.ExitCode, output.Result, error.Result);
    }

    private static string CodeOf(params string[] args)
    {
        var parsed = Cli.Parse(args);
        Assert.False(parsed.IsSuccess, $"expected a parse error for: {string.Join(' ', args)}");
        return parsed.Error!.Value.Code;
    }

    [Fact]
    public void The_misspelt_seeds_option_is_refused_by_a_real_process_and_no_game_is_played()
    {
        var dir = TempDir();
        try
        {
            var child = Spawn("run", "--seedz", "1", "--run-id", "seedz", "--out", dir);

            Assert.Equal(2, child.ExitCode);
            Assert.Contains("Cli.option_unknown", child.Error, StringComparison.Ordinal);
            Assert.Contains("--seedz", child.Error, StringComparison.Ordinal);                  // the offending option is named
            Assert.DoesNotContain("games:", child.Output, StringComparison.Ordinal);            // nothing ran
            Assert.False(Directory.Exists(dir));                                                // nothing was written
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Theory]
    [InlineData("Cli.command_missing")]
    [InlineData("Cli.command_unknown", "bogus")]
    [InlineData("Cli.command_unknown", "--seeds", "3")]
    [InlineData("Cli.command_unknown", "RUN")]
    [InlineData("Cli.option_unknown", "run", "--seedz", "1")]
    [InlineData("Cli.option_unknown", "run", "--Seeds", "1")]
    [InlineData("Cli.option_unknown", "run", "--seeds=3")]
    [InlineData("Cli.option_unknown", "run", "--manifest", "m.json")]                            // a replay option is not a run option
    [InlineData("Cli.option_unknown", "replay", "--manifest", "m.json", "--seeds", "3")]
    [InlineData("Cli.option_duplicate", "run", "--seeds", "3", "--seeds", "11")]
    [InlineData("Cli.option_duplicate", "run", "--rules", "automated", "--rules", "automated")]  // even with the same value
    [InlineData("Cli.option_duplicate", "replay", "--manifest", "a.json", "--manifest", "b.json")]
    [InlineData("Cli.value_missing", "run", "--rules")]
    [InlineData("Cli.value_missing", "run", "--seeds", "--run-id", "x")]
    [InlineData("Cli.value_missing", "replay", "--manifest")]
    [InlineData("Cli.value_invalid", "run", "--seeds", "abc")]
    [InlineData("Cli.value_invalid", "run", "--seeds", "3,,11")]
    [InlineData("Cli.value_invalid", "run", "--seeds", "3,")]
    [InlineData("Cli.value_invalid", "run", "--seeds", "-3")]
    [InlineData("Cli.value_invalid", "run", "--seeds", "+3")]
    [InlineData("Cli.value_invalid", "run", "--seeds", "3, 11")]
    [InlineData("Cli.value_invalid", "run", "--seeds", "1.5")]
    [InlineData("Cli.value_invalid", "run", "--seeds", "18446744073709551616")]                 // ulong.MaxValue + 1
    [InlineData("Cli.value_invalid", "run", "--seeds", "")]
    [InlineData("Cli.value_invalid", "run", "--rules", "bogus")]                                // used to fall back to non-automated
    [InlineData("Cli.value_invalid", "run", "--rules", "Automated")]
    [InlineData("Cli.value_invalid", "run", "--advancement", "zzz")]                            // used to fall back to v2
    [InlineData("Cli.value_invalid", "run", "--event-budget", "many")]
    [InlineData("Cli.value_invalid", "run", "--event-budget", "-1")]
    [InlineData("Cli.value_invalid", "run", "--event-budget", "99999999999")]
    [InlineData("Cli.value_invalid", "run", "--run-id", "")]
    [InlineData("Cli.value_invalid", "run", "--out", "")]
    [InlineData("Cli.argument_unexpected", "run", "3")]
    [InlineData("Cli.argument_unexpected", "run", "--seeds", "3", "extra")]
    [InlineData("Cli.argument_unexpected", "replay", "m.json")]
    [InlineData("Cli.option_required", "replay")]
    [InlineData("Cli.option_required", "official", "--run-id", "x")]
    [InlineData("Cli.option_forbidden", "official", "--seed-manifest", "s.json", "--run-id", "x", "--tuning", "t.json")]   // the test seam is never official (FC-D10)
    [InlineData("Cli.option_forbidden", "official", "--seed-manifest", "s.json", "--run-id", "x", "--seeds", "3")]         // official seeds come only from the pinned manifest
    [InlineData("Cli.option_forbidden", "official", "--seed-manifest", "s.json", "--run-id", "x", "--rules", "automated")]
    public void Bad_command_lines_are_refused_with_a_typed_code(string code, params string[] args) => Assert.Equal(code, CodeOf(args));

    [Fact]
    public void Every_refusal_exits_two_through_Main_and_names_the_option()
    {
        Assert.Equal(2, Program.Main(["run", "--seedz", "1"]));
        Assert.Equal(2, Program.Main(["run", "--seeds", "3", "--seeds", "11"]));
        Assert.Equal(2, Program.Main(["run", "--rules"]));
        Assert.Equal(2, Program.Main(["run", "--seeds", "x"]));
        Assert.Equal(2, Program.Main(["run", "--seeds", "3", "extra"]));
        Assert.Equal(2, Program.Main(["run", "--rules", "bogus", "--seeds", "3"]));
        Assert.Equal(2, Program.Main(["official", "--seed-manifest", "s.json", "--run-id", "x", "--tuning", "t.json"]));
        Assert.Equal(2, Program.Main(["nope"]));

        var parsed = Cli.Parse(["run", "--seeds", "3", "--seeds", "11"]);
        Assert.Contains("--seeds", parsed.Error!.Value.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Real_processes_refuse_each_class_of_bad_input_with_exit_two()
    {
        Assert.Equal(2, Spawn("bogus").ExitCode);                                              // unknown command
        Assert.Equal(2, Spawn("run", "--seeds", "3", "--seeds", "11").ExitCode);               // duplicate
        Assert.Equal(2, Spawn("run", "--rules").ExitCode);                                     // missing value
        Assert.Equal(2, Spawn("run", "--seeds", "3,x").ExitCode);                              // bad seed
        Assert.Equal(2, Spawn("run", "--seeds", "3", "leftover").ExitCode);                    // positional
        Assert.Equal(2, Spawn("official", "--seed-manifest", "s.json", "--run-id", "x", "--tuning", "t.json").ExitCode);  // forbidden combination
    }

    [Fact]
    public void Valid_command_lines_parse_to_exactly_the_given_options()
    {
        var run = H.Ok(Cli.Parse(["run", "--seeds", "3,11,29", "--rules", "automated", "--advancement", "v1", "--event-budget", "3000", "--run-id", "a.b-c_d", "--out", "x y", "--tuning", "t.json"]));
        Assert.Equal("run", run.Name);
        Assert.Equal(7, run.Options.Count);
        Assert.Equal("x y", run.Options["out"]);
        Assert.Equal(new ulong[] { 3, 11, 29 }, H.Ok(Cli.ParseSeeds(run.Options["seeds"])));
        Assert.Equal(new ulong[] { 0, ulong.MaxValue, 3, 3 }, H.Ok(Cli.ParseSeeds("0,18446744073709551615,3,3")));   // repeated seeds are legal: each gets its own GameId

        var bare = H.Ok(Cli.Parse(["run"]));
        Assert.Empty(bare.Options);                                                             // the documented defaults are applied by the command, not invented by the parser

        Assert.Equal("m.json", H.Ok(Cli.Parse(["replay", "--manifest", "m.json"])).Options["manifest"]);
        Assert.Equal(2, H.Ok(Cli.Parse(["official", "--seed-manifest", "s.json", "--run-id", "x"])).Options.Count);
    }

    [Fact]
    public void Each_command_has_one_authoritative_whitelist()
    {
        // F-C2-B adds the non-official stop-aware dry run (ADR-009 SR-D4); the F-C2 official integration adds the 48-cell run and its replay
        Assert.Equal(new[] { "dry", "official", "official-dom", "replay", "replay-dom", "run" }, Cli.Commands.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(new[] { "out", "run-id" }, Cli.Commands["official-dom"].Options.Keys.Order(StringComparer.Ordinal).ToArray());   // no seed file, pin or size from the command line
        Assert.Equal(new[] { "run-id" }, Cli.Commands["official-dom"].Required.ToArray());
        Assert.Equal(new[] { "result" }, Cli.Commands["replay-dom"].Options.Keys.ToArray());
        Assert.Equal(new[] { "advancement", "event-budget", "out", "rules", "run-id", "seeds", "tuning" }, Cli.Commands["run"].Options.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(new[] { "manifest", "seed-manifest", "seed-sha256" }, Cli.Commands["replay"].Options.Keys.Order(StringComparer.Ordinal).ToArray());   // + the dry replay pin (SR-D8.6)
        Assert.Equal(new[] { "out", "run-id", "seed-manifest", "seed-sha256" }, Cli.Commands["dry"].Options.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(new[] { "seed-manifest", "seed-sha256" }, Cli.Commands["dry"].Required.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(new[] { "out", "run-id", "seed-manifest" }, Cli.Commands["official"].Options.Keys.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void The_default_smoke_run_keeps_its_pre_FC2A_hashes()
    {
        var dir = TempDir();
        try
        {
            Assert.Equal(0, Program.Main(["run", "--run-id", "default-golden", "--out", dir]));
            var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "manifest.json"))).RootElement;
            // produced by the harness at 97045829 (before F-C2-A) with `run` and no options: seeds 3, 11, 29
            Assert.Equal("b9bbac722cddfefad9b0e039565ec5f409efef10ee71d640705f876535d1195a", manifest.GetProperty("run").GetProperty("config_hash").GetString());
            Assert.Equal("28e0536c644d6a38b4eefb4f673423229b35725ef87f7fa51a39e476cd552ec6", manifest.GetProperty("results").GetProperty("run_result_hash").GetString());
            Assert.Equal(new ulong[] { 3, 11, 29 }, manifest.GetProperty("games").EnumerateArray().Select(g => g.GetProperty("seed").GetUInt64()).ToArray());
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }
}
