using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.SimHarness.Tests.Evidence;

/// <summary>
/// B1-T04a / N13: the per-event observer changes nothing — game-result-v1, run-result-v1/v2, event / RNG hashes, the PZL recorder's observation, the counters and the RNG chain are bit-identical with and without it —
/// and what it records links H0 → H1 → H2 to the commit record of every event.
/// </summary>
public class ObserverSeamTests
{
    private static string Sha(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    public static TheoryData<string, string, ulong> Sample => new()
    {
        { HarnessData.AutomatedProfile, "fc2-dry-cal-va", 900_101UL },
        { HarnessData.NonAutomatedProfile, "fc2-dry-cal-vn", 900_151UL },
    };

    private static RunConfig Config(string rules, string key, int budget = 3000) =>
        H.Ok(RunConfig.Create("t04a", "dry", CalibrationCohorts.VirtualLeague(), H.Ok(HarnessData.LoadRules(rules)), H.Tuning, H.Ok(HarnessData.NeutralPark()), "advancement-policy-v2", budget,
            competitionKey: key));

    [Theory]
    [MemberData(nameof(Sample))]
    public void Observing_every_event_leaves_every_result_hash_counter_and_pzl_observation_bit_identical(string rules, string key, ulong firstSeed)
    {
        var config = Config(rules, key);
        var specs = Enumerable.Range(1, 8).Select(i => config.Spec(i, firstSeed + (ulong)(i - 1))).ToList();
        var plain = SimRunner.RunSpecs(config, specs);
        var observed = SimRunner.RunSpecs(config, specs, play: s => GameExecutor.Play(config, s, default, null, new PzlEventObserver()));
        Assert.Equal(plain.RunResultHash, observed.RunResultHash);
        Assert.Equal(plain.ConfigHash, observed.ConfigHash);
        foreach (var (a, b) in plain.Games.Zip(observed.Games))
        {
            Assert.Equal((a.ResultHash, a.EventHash, a.RngHash, a.Status, a.CommittedEvents), (b.ResultHash, b.EventHash, b.RngHash, b.Status, b.CommittedEvents));
            Assert.Equal(a.Counters.Canonical(), b.Counters.Canonical());
            Assert.Equal(a.Pzl, b.Pzl);
            Assert.True(a.Regulation!.SameAs(b.Regulation));
        }
    }

    [Theory]
    [MemberData(nameof(Sample))]
    public void The_observer_links_h0_h1_h2_to_every_commit_and_the_final_rng_is_the_game_rng(string rules, string key, ulong seed)
    {
        var config = Config(rules, key);
        var spec = config.Spec(1, seed);
        var obs = new PzlEventObserver();
        var commits = new List<StarPlayer.Simulation.Baseball.CommitRecord>();
        var game = GameExecutor.Play(config, spec, default, commits.Add, obs);
        Assert.True(game.Completed);
        Assert.Equal(game.CommittedEvents, obs.Events.Count);
        Assert.Equal(obs.StartRng, obs.Events[0].RngBefore);
        int advancements = 0, pitches = 0;
        for (var i = 0; i < obs.Events.Count; i++)
        {
            var e = obs.Events[i];
            Assert.Equal(i + 1, e.Sequence);
            Assert.Same(commits[i].Event, e.Event);
            Assert.Equal(commits[i].RngStateHashBefore, Sha(e.RngBefore));
            Assert.Equal(commits[i].RngStateHashAfter, Sha(e.RngAfter));
            if (i > 0)
                Assert.Equal(obs.Events[i - 1].RngAfter, e.RngBefore);
            if (e.Pitch is { } p)
            {
                pitches++;
                if (p.RunnerCalls == 0)
                    Assert.Equal(p.H1, e.RngAfter);                      // no advancement: H1 = H2
                else
                {
                    advancements++;
                    Assert.NotEqual(p.H1, e.RngAfter);
                    Assert.True(p.RunnerRawDraws >= p.RunnerCalls);
                }

                Assert.NotEqual(e.RngBefore, p.H1);                      // a pitch always draws (target zone, location, …)
                Assert.Equal(p.StreamUsage.Select(u => u.Stream).Order(StringComparer.Ordinal), p.StreamUsage.Select(u => u.Stream));
                Assert.All(p.StreamUsage, u => Assert.True(u.RawDraws >= u.Calls));
                Assert.Equal(p.StreamUsage.Sum(u => u.Calls), p.Steps.Count);
            }
            else
                Assert.Equal(e.RngBefore, e.RngAfter);                   // an event that draws nothing
        }

        Assert.True(pitches > 0);
        Assert.Equal(game.RngHash, Sha(obs.FinalRng!));
        _ = advancements;
    }

    [Fact]
    public void A_game_that_never_starts_has_no_observation_and_one_stopped_before_its_first_commit_keeps_the_start_position()
    {
        // case A: the pins of the run are broken, so the game never starts
        var config = Config(HarnessData.AutomatedProfile, "fc2-dry-cal-va");
        var broken = config with { EventBudget = config.EventBudget, Execution = config.Execution with { TuningHash = new string('0', 64) } };
        var none = new PzlEventObserver();
        var a = GameExecutor.Play(broken, broken.Spec(1, 900_101UL), default, null, none);
        Assert.Equal("config", a.Failure!.Stage);
        Assert.Null(none.StartRng);
        Assert.Null(none.FinalRng);
        Assert.Empty(none.Events);

        // case C: a budget of one event stops after the first commit; the observer keeps exactly that commit and the final RNG is its H2
        var small = Config(HarnessData.AutomatedProfile, "fc2-dry-cal-va", budget: 1);
        var obs = new PzlEventObserver();
        var c = GameExecutor.Play(small, small.Spec(1, 900_101UL), default, null, obs);
        Assert.Equal("budget", c.Failure!.Stage);
        Assert.Single(obs.Events);
        Assert.Equal(c.RngHash, Sha(obs.FinalRng!));
        Assert.Equal(c.Failure.RngHash, c.RngHash);
    }

    [Fact]
    public void The_evidence_sources_draw_no_random_number_compute_no_probability_and_apply_no_reducer_step()
    {
        string[] forbidden =
        [
            "NextBounded", "GameRandom", "RngStream", "Pcg32", "PitchBranchResolver", "StreamKeyDerivation", "ProbabilityScale", "Distribution.", "InPlayTemplate",
            "GameReducer.Apply", "GameReducer.Finalize", "System.Random", "new Random", "Guid.NewGuid", "Xunit",
        ];
        var dir = Path.Combine(H.Root, "tools", "StarPlayer.SimHarness", "Evidence");
        var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var code = Regex.Replace(File.ReadAllText(file), @"//.*?$|/\*.*?\*/", "", RegexOptions.Multiline | RegexOptions.Singleline);
            // whole identifiers: the draft's recorded count NextBoundedCalls is a copied value, not a draw
            foreach (var word in forbidden)
                Assert.False(Regex.IsMatch(code, $@"(?<![A-Za-z0-9_]){Regex.Escape(word)}(?![A-Za-z0-9_])"), $"{Path.GetFileName(file)} mentions {word}");
            foreach (var word in new[] { "DateTime", "TimeProvider", "Stopwatch", "MachineName" })
                Assert.False(code.Contains(word, StringComparison.Ordinal), $"{Path.GetFileName(file)} reads the clock or machine ({word})");
        }
    }
}
