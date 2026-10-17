using System.Reflection;
using System.Text.RegularExpressions;
using StarPlayer.Simulation.Baseball;
using StarPlayer.SimHarness;

namespace StarPlayer.SimHarness.Tests;

/// <summary>F-A + F-B together on small deterministic runs, and the structural facts that keep the harness a plain consumer of the production engine.</summary>
public class EndToEndAndBoundaryTests
{
    // ---------------------------------------------------------------- end to end (small)

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_small_run_completes_replays_identically_and_agrees_with_the_box_score_in_both_call_modes(bool automated)
    {
        var seeds = new ulong[] { 3, 11, 29 };                                    // the three regression seeds of the earlier phases: a small example, NOT a seed list for the later validation
        var config = H.Config(rules: H.Rules(automated));
        var first = SimRunner.Run(config, seeds);
        var again = SimRunner.Run(H.Config("again", rules: H.Rules(automated)), seeds);

        Assert.True(first.AllCompleted);
        Assert.All(first.Games, g =>
        {
            Assert.True(g.CrossCheck!.Passed);
            Assert.Equal(g.CommittedEvents, g.Counters.Events);
            Assert.Equal(0, g.Counters.UnlocatedPitches);
            Assert.Equal(g.Counters.Pitches, g.Counters.CalledBalls + g.Counters.CalledStrikes + g.Counters.Swings + g.Counters.HitByPitchPitches);
        });
        Assert.Equal(first.RunResultHash, again.RunResultHash);
        Assert.Equal(first.Games.Select(g => g.EventHash), again.Games.Select(g => g.EventHash));
        Assert.Equal(first.Games.Select(g => g.RngHash), again.Games.Select(g => g.RngHash));
        Assert.Equal(automated ? "Automated" : "NonAutomated", config.Rules.CalledPitchMode.ToString());
        // every metric of the small run is a measurement, none is a verdict
        Assert.All(first.Metrics.Value.Metrics, m => Assert.Equal(MetricStatus.NotEvaluated, m.Status));
    }

    [Fact]
    public void A_range_of_small_seeds_all_end_legally_and_none_is_dropped_or_replaced()
    {
        var seeds = Enumerable.Range(1, 8).Select(i => (ulong)i).ToArray();      // a plain 1..8, not chosen for any result
        var run = SimRunner.Run(H.Config(), seeds);

        Assert.Equal(seeds.Length, run.Games.Count);
        Assert.Equal(seeds, run.Games.Select(g => g.Game.Seed).ToArray());
        Assert.True(run.AllCompleted, string.Join("; ", run.Games.Where(g => g.Failure is not null).Select(g => $"seed {g.Game.Seed}: {g.Failure!.Code} {g.Failure.Message}")));
        Assert.All(run.Games, g => Assert.NotEqual(GameEndKind.None, g.EndKind));
        Assert.All(run.Games, g => Assert.True(g.CrossCheck!.Passed));
    }

    [Fact]
    public void The_neutral_cohort_pitch_rates_are_measured_on_the_actual_location_and_land_in_plausible_ranges_without_any_claim()
    {
        var run = SimRunner.Run(H.Config(), Enumerable.Range(1, 6).Select(i => (ulong)i).ToArray());
        var c = run.Aggregate;

        // sanity only (not the declared targets, not a verdict): the three pitch rates are fractions of their own denominators
        Assert.InRange(c.ZonePitches, 1, c.LocatedPitches - 1);
        Assert.InRange(c.Swings, 1, c.Pitches - c.HitByPitchPitches - 1);
        Assert.InRange(c.SwingingStrikes, 1, c.Swings - 1);
        Assert.Equal(c.Pitches, c.LocatedPitches);
    }

    // ---------------------------------------------------------------- structure

    private static string Tools => Path.Combine(H.Root, "tools", "StarPlayer.SimHarness");

    private static string StripComments(string code) => Regex.Replace(Regex.Replace(code, @"/\*.*?\*/", "", RegexOptions.Singleline), @"//[^\r\n]*", "");

    [Fact]
    public void The_harness_references_only_domain_and_simulation_and_nothing_with_a_back_door()
    {
        var referenced = typeof(HarnessData).Assembly.GetReferencedAssemblies().Select(a => a.Name!).Where(n => n.StartsWith("StarPlayer.", StringComparison.Ordinal)).Order().ToArray();
        Assert.Equal(["StarPlayer.Domain", "StarPlayer.Simulation"], referenced);

        // production assemblies expose their internals to nobody new: the harness is not on any list
        foreach (var assembly in new[] { typeof(GameStarter).Assembly, typeof(StarPlayer.Domain.Common.PlayerId).Assembly })
            Assert.DoesNotContain(assembly.GetCustomAttributes<System.Runtime.CompilerServices.InternalsVisibleToAttribute>(), a => a.AssemblyName.Contains("SimHarness", StringComparison.Ordinal));
        Assert.Equal(["StarPlayer.SimHarness.Tests"], typeof(HarnessData).Assembly.GetCustomAttributes<System.Runtime.CompilerServices.InternalsVisibleToAttribute>().Select(a => a.AssemblyName).ToArray());

        var references = System.Xml.Linq.XDocument.Load(Path.Combine(Tools, "StarPlayer.SimHarness.csproj")).Descendants("ProjectReference").Select(e => e.Attribute("Include")!.Value.Split('\\', '/')[^1]).Order().ToArray();
        Assert.Equal(["StarPlayer.Domain.csproj", "StarPlayer.Simulation.csproj"], references);                           // no Infrastructure, Application, Client or test project
    }

    [Fact]
    public void The_harness_sources_compute_no_probability_outcome_or_random_number_and_read_no_test_helper()
    {
        string[] forbidden =
        [
            "NextBounded", "GameRandom", "RngStream", "Pcg32", "PitchBranchResolver", "StreamKeyDerivation", "ProbabilityScale", "Distribution.", "InPlayTemplate",      // no draw, no probability
            "GameReducer.Apply", "GameReducer.Finalize",                                                                                                               // no direct reducer step: only the commit gate applies events
            "System.Random", "new Random", "Guid.NewGuid", "RandomNumberGenerator", "ExecutionFixture", "PitchBranchFixture", "Xunit",
        ];
        var files = Directory.GetFiles(Tools, "*.cs").Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)).ToArray();
        Assert.True(files.Length >= 9);
        foreach (var file in files)
        {
            var code = StripComments(File.ReadAllText(file));
            foreach (var word in forbidden)
                Assert.False(code.Contains(word, StringComparison.Ordinal), $"{Path.GetFileName(file)} mentions {word}");
        }
    }

    [Fact]
    public void Nothing_that_feeds_a_result_hash_reads_the_clock_or_the_machine()
    {
        foreach (var name in new[] { "GameExecutor.cs", "SimRunner.cs", "MetricCounters.cs", "MetricsEvaluator.cs", "RunModel.cs", "Cohorts.cs", "BoxScoreCrossCheck.cs", "HarnessData.cs" })
        {
            var code = StripComments(File.ReadAllText(Path.Combine(Tools, name)));
            foreach (var word in new[] { "DateTime", "TimeProvider", "Environment.", "MachineName", "Thread", "Task.Run", "Parallel", "GetHashCode", "HashCode.Combine", "Process" })
                Assert.False(code.Contains(word, StringComparison.Ordinal), $"{name} mentions {word}");
        }

        // the one clock reading in the executor is the elapsed time of a game, which is reported and never hashed
        var executor = StripComments(File.ReadAllText(Path.Combine(Tools, "GameExecutor.cs")));
        Assert.Single(Regex.Matches(executor, @"Stopwatch\.StartNew"));
        var first = H.Play(H.Config(), 3);
        var second = H.Play(H.Config(), 3);
        Assert.Equal(first.ResultHash, second.ResultHash);
    }

    [Fact]
    public void Every_commit_the_executor_confirms_goes_through_the_cursor_publish_and_nothing_else_makes_a_state()
    {
        var code = StripComments(File.ReadAllText(Path.Combine(Tools, "GameExecutor.cs")));

        Assert.Contains("cursor.Publish(", code, StringComparison.Ordinal);
        Assert.Contains("PitchCommitGate.Stage(", code, StringComparison.Ordinal);
        Assert.Contains("CommitGate.Stage(", code, StringComparison.Ordinal);
        Assert.Contains("GameStarter.Begin(", code, StringComparison.Ordinal);
        // no state or aggregate is built or edited by hand: no `with {` on anything and no constructor of the authority types
        Assert.DoesNotMatch(new Regex(@"\bwith\s*\{"), code);
        Assert.DoesNotContain("new AuthoritativeGame", code, StringComparison.Ordinal);
        Assert.DoesNotContain("new GameCursor", code, StringComparison.Ordinal);
        Assert.DoesNotContain("new StagedCommit", code, StringComparison.Ordinal);
        Assert.DoesNotContain("new CommitRecord", code, StringComparison.Ordinal);
    }

    [Fact]
    public void A_game_reads_only_the_authoritative_state_a_policy_cannot_change_it()
    {
        // the policy receives the immutable GameState; a policy that "edits" it only makes a copy that nobody reads
        var config = H.Config("meddling", policy: new MeddlingPolicy());
        var baseline = H.Play(H.Config(), 11);
        var meddled = H.Play(config, 11);

        Assert.Equal(GameRunStatus.Completed, meddled.Status);
        Assert.Equal(baseline.EventHash, meddled.EventHash);
        Assert.Equal(baseline.RngHash, meddled.RngHash);
    }

    private sealed class MeddlingPolicy : IGamePolicy
    {
        public string Id => "normal-balanced-v1";                                 // same id and choices as the default: any difference could only come from tampering

        public PaDecision AtPlateAppearance(StarPlayer.Domain.Baseball.Game.GameState state)
        {
            _ = state with { AwayRuns = 99, HomeRuns = -1, Outs = 2 };
            _ = state.Bases with { First = null };
            return PaDecision.Choose(StarPlayer.Domain.Baseball.Game.ApproachKind.Normal);
        }

        public PitchIntent IntentFor(StarPlayer.Domain.Baseball.Game.GameState state) => PitchIntent.Balanced;
    }

    [Fact]
    public void The_harness_is_a_tool_not_a_runtime_dependency_of_the_game()
    {
        var sln = File.ReadAllText(Path.Combine(H.Root, "StarPlayer.sln"));
        Assert.Contains("StarPlayer.SimHarness", sln, StringComparison.Ordinal);
        foreach (var project in new[] { "src/StarPlayer.Domain/StarPlayer.Domain.csproj", "src/StarPlayer.Simulation/StarPlayer.Simulation.csproj", "src/StarPlayer.Application/StarPlayer.Application.csproj",
                     "src/StarPlayer.Infrastructure/StarPlayer.Infrastructure.csproj", "game/StarPlayer.Client.csproj" })
            Assert.DoesNotContain("SimHarness", File.ReadAllText(Path.Combine(H.Root, project)), StringComparison.Ordinal);
    }
}
