using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;
using Policy = StarPlayer.Simulation.Baseball.AdvancementPolicy;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-D v2, Part D: the six approved combinations of park, fielding, runner speed and fielder arm played as COMPLETE games through the real pipeline (preparation, planning, advancement,
/// staging, publication, finalization). No production runner exists; the harness only chooses an approach for each plate appearance. Allowed version combinations are explicit:
/// legacy = no execution + v1 tuning + no player inputs; active = an execution (model v2, tuning v2, a policy v1 or v2, a park snapshot) + v2 tuning, with a pinned player input set
/// or none (policy v2 needs one). Nothing is converted to the latest version automatically.
/// </summary>
public class ParkFieldingAdvancementIntegrationTests
{
    private static readonly ulong[] Seeds = [11, 12, 13];

    /// <summary>Deterministic, non-uniform ratings: the same player always gets the same three values (no hash codes, which differ between runs).</summary>
    private static (int Fielding, int Speed, int Arm) Varied(PlayerId id)
    {
        var sum = id.Value.Sum(c => (int)c);
        return (10 + sum * 7 % 81, 10 + sum * 13 % 81, 10 + sum * 29 % 81);
    }

    private sealed record Combo(string Name, GameExecution? Execution, TuningProfile Tuning, PlayerInputSet? Inputs);

    private static Combo Legacy() => new("1 legacy v1, no park, neutral defence and running", null, Gx.Tuning1, null);

    private static Combo Active(string name, int[] park, bool varied, string policy)
    {
        var set = varied || policy == Policy.VersionV2 ? Gx.Set(varied ? id => Varied(id) : (Func<PlayerId, (int Fielding, int Speed, int Arm)?>?)null) : null;
        var execution = Gx.Exec(Gx.Park(park[0], park[1], park[2]), policy, set?.Id);
        return new Combo(name, execution, Gx.Tuning2, set);
    }

    /// <summary>A game whose every player runs at 100 and throws at 100 (and fields at 50): extreme enough that speed and arm change a game within a few seeds.</summary>
    private static Combo FastAndStrong(string policy)
    {
        var set = Gx.Set(_ => (50, 100, 100));
        return new Combo("fast and strong", Gx.Exec(Gx.Park(Hitter[0], Hitter[1], Hitter[2]), policy, set.Id), Gx.Tuning2, set);
    }

    private static readonly int[] Neutral = [1000, 1000, 1000];
    private static readonly int[] Hitter = [1100, 1020, 1050];

    private static IEnumerable<Combo> AllCombos() =>
    [
        Legacy(),
        Active("2 model v2, neutral park, neutral abilities", Neutral, false, Policy.Version),
        Active("3 model v2, non-neutral park", Hitter, false, Policy.Version),
        Active("4 non-neutral park + fielding", Hitter, true, Policy.Version),
        Active("5 fielding + speed + arm (policy v2), neutral park", Neutral, true, Policy.VersionV2),
        Active("6 non-neutral park + fielding + speed + arm (policy v2)", Hitter, true, Policy.VersionV2),
    ];

    private static Gx.GameRun Run(Combo c, ulong seed) => Gx.Play(c.Execution, c.Tuning, c.Inputs, seed);

    [Fact]
    public void Every_combination_plays_complete_legal_games_with_every_invariant_intact()
    {
        foreach (var combo in AllCombos())
            foreach (var seed in Seeds)
            {
                var run = Run(combo, seed);

                Assert.True(run.Violations.Count == 0, $"{combo.Name} seed {seed}: {string.Join(" | ", run.Violations.Take(3))}");
                Assert.Equal(GameStatus.Final, run.Last.State.Status);
                Assert.NotNull(run.Last.State.Final);
                Assert.Same(combo.Execution, run.Last.State.Final!.Execution);        // the finalized game carries exactly the start contract (null for legacy)
                Assert.Same(combo.Execution, run.Last.State.Execution);
                Assert.True(run.Pitches > 100 && run.InPlay > 20, $"{combo.Name}: a trivial game ({run.Pitches} pitches)");
                Assert.True(run.Pending > 0 && run.Resolved == run.Pending, $"{combo.Name}: no advancement decision was exercised");
                Assert.True(AuthoritativeGame.IsComplete(run.Last.State));
            }
    }

    [Fact]
    public void The_same_seed_and_the_same_inputs_replay_to_the_identical_game_in_every_combination()
    {
        foreach (var combo in AllCombos())
        {
            var first = Run(combo, 21);
            var second = Run(combo, 21);

            Assert.Equal(first.Receipts, second.Receipts);
            Assert.Equal(first.Last.Rng.StateHash, second.Last.Rng.StateHash);
            Assert.Equal(first.Last.State.AwayRuns, second.Last.State.AwayRuns);
            Assert.Equal(first.Last.State.HomeRuns, second.Last.State.HomeRuns);
        }
    }

    [Fact]
    public void A_neutral_v2_game_replays_the_legacy_game_event_for_event_and_to_the_same_final_rng_position()
    {
        // model v2 with a neutral park, policy v1 and no player inputs, and model v2 with policy v2 and every rating 50: both are the legacy game on the same seed
        var neutralPolicyV2 = new Combo("neutral policy v2", Gx.Exec(Gx.Park(), Policy.VersionV2, Gx.Set().Id), Gx.Tuning2, Gx.Set());
        foreach (var seed in Seeds)
        {
            var legacy = Run(Legacy(), seed);
            foreach (var other in new[] { Active("neutral v2", Neutral, false, Policy.Version), neutralPolicyV2 })
            {
                var run = Run(other, seed);
                Assert.Empty(run.Violations);
                Assert.Equal(legacy.Receipts, run.Receipts);                              // every event fingerprint identical
                Assert.Equal(legacy.Last.Rng.StateHash, run.Last.Rng.StateHash);          // not one draw more or fewer
                Assert.Equal((legacy.Last.State.AwayRuns, legacy.Last.State.HomeRuns), (run.Last.State.AwayRuns, run.Last.State.HomeRuns));
            }
        }
    }

    [Fact]
    public void Each_activated_effect_really_changes_games_and_only_the_activated_slots_are_applied()
    {
        ulong[] seeds = [31, 32, 33, 34, 35, 36];
        var neutral = seeds.Select(s => Run(Active("2", Neutral, false, Policy.Version), s)).ToArray();
        var park = seeds.Select(s => Run(Active("3", Hitter, false, Policy.Version), s)).ToArray();
        var parkFielding = seeds.Select(s => Run(Active("4", Hitter, true, Policy.Version), s)).ToArray();
        var runners = seeds.Select(s => Run(Active("5", Neutral, true, Policy.VersionV2), s)).ToArray();
        var all = seeds.Select(s => Run(Active("6", Hitter, true, Policy.VersionV2), s)).ToArray();

        static bool SomeSeedDiffers(Gx.GameRun[] a, Gx.GameRun[] b) => a.Zip(b, (x, y) => !x.Receipts.SequenceEqual(y.Receipts)).Any(d => d);
        Assert.True(SomeSeedDiffers(neutral, park), "the park changed no game");
        Assert.True(SomeSeedDiffers(park, parkFielding), "fielding changed no game");
        Assert.True(SomeSeedDiffers(neutral, runners), "fielding / speed / arm changed no game");
        // speed and arm only matter at the few extra-base decisions of a game: with every player at 100 / 100 the two policies must part within a few seeds
        ulong[] more = Enumerable.Range(41, 12).Select(i => (ulong)i).ToArray();
        Assert.True(SomeSeedDiffers(more.Select(s => Run(FastAndStrong(Policy.Version), s)).ToArray(), more.Select(s => Run(FastAndStrong(Policy.VersionV2), s)).ToArray()),
            "speed and arm changed no game");

        // the slots of the defense_result stage that were applied (the nine ability / strategy slots of earlier stages are the same in every game)
        var slots = (Gx.GameRun[] runs) => runs.SelectMany(r => r.SlotsSeen.Keys).Where(k => k.StartsWith("park.", StringComparison.Ordinal) || k.StartsWith("defense.", StringComparison.Ordinal)).ToHashSet();
        // an active-model game always supplies its (possibly neutral, factor 1,000,000) park slots; the fielding slot appears only when a player input set was frozen
        Assert.Equal(["park.extra_base_hits", "park.hits", "park.home_runs"], slots(neutral).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(slots(neutral).Order(StringComparer.Ordinal), slots(park).Order(StringComparer.Ordinal));
        Assert.Contains("defense.fielding", slots(parkFielding));
        Assert.Contains("defense.fielding", slots(runners));
        Assert.Equal(slots(parkFielding).Order(StringComparer.Ordinal), slots(all).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(slots(all), k => k is "runner.speed" or "defense.arm");                      // those two belong to the advancement stage, which the draft never contains
        // every in-play pitch lists each applied slot exactly once (a second one is a harness violation) and the three park slots always come together
        foreach (var run in all)
        {
            Assert.Empty(run.Violations);
            Assert.Equal(run.SlotsSeen["park.hits"], run.SlotsSeen["park.home_runs"]);
            Assert.Equal(run.SlotsSeen["park.hits"], run.SlotsSeen["park.extra_base_hits"]);
            Assert.Equal(run.SlotsSeen["park.hits"], run.SlotsSeen["defense.fielding"]);
        }
    }

    [Fact]
    public void The_stage_order_streams_and_unchanged_tuning_hashes_hold_in_every_combination()
    {
        Assert.Equal(TuningProfileTests.GoldenHash, Gx.Tuning1.ContentHash);
        Assert.Equal(TuningV2Tests.GoldenHashV2, Gx.Tuning2.ContentHash);
        Assert.Equal(ProbabilityModel.V1.Stages.Select(s => s.Id), ProbabilityModel.V2.Stages.Select(s => s.Id));
        foreach (var combo in AllCombos())
        {
            var d = Gx.InSequence(combo.Execution);
            var g = Gx.Aggregate(d, 5);
            var draft = Gx.Prepare(g, combo.Tuning, inputs: combo.Inputs).Draft;
            var order = ProbabilityModel.V2.Stages.Select(s => s.Id).ToList();

            Assert.Equal(draft.Steps.Select(s => order.IndexOf(s.Stage)).Order().ToArray(), draft.Steps.Select(s => order.IndexOf(s.Stage)).ToArray());      // the stages run in the model's order
            Assert.All(draft.StreamUsage, u => Assert.Contains(u.Stream.Canonical, new[] { "game/target@1", "game/location@1", "game/pitch@1", "game/contact@1", "game/field@1" }));
        }
    }

    [Fact]
    public void An_unlisted_combination_is_refused_and_never_converted_to_the_latest_version()
    {
        var d = Gx.InSequence(Gx.Exec());
        var g = Gx.Aggregate(d, 5);
        var legacyState = Gx.InSequence(null);

        Assert.Equal("PitchBranch.execution_mismatch", Pc.Code(Gx.TryPrepare(g.State, g.Rng, Gx.Tuning1)));                              // a v2 game with the v1 tuning
        Assert.Equal("PitchBranch.park_snapshot_required", Pc.Code(Gx.TryPrepare(legacyState.State, Gx.Aggregate(legacyState, 5).Rng, Gx.Tuning2)));   // a legacy game with the v2 tuning
        Assert.Equal("Game.execution_model_unsupported", Pc.Code(GameReducer.Start(new GameStart("g", PitchBranchFixture.NonAutomated, BasePlayersTeams().Away, BasePlayersTeams().Home,
            Gx.Exec(tuning: Gx.Tuning1) with { ProbabilityModelVersion = "prob-model-v1" }))));
    }

    private static (TeamSetup Away, TeamSetup Home) BasePlayersTeams()
    {
        var state = Gx.InSequence(null).State;
        return (new TeamSetup(state.Away.Lineup, state.Away.Pitcher, state.Away.Bench), new TeamSetup(state.Home.Lineup, state.Home.Pitcher, state.Home.Bench));
    }
}
