using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;
using static StarPlayer.Infrastructure.Tests.Pc;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-E, the version regression matrix: whole games played through the REAL pipeline (prepare, plan, advancement resolution, stage, publish) under legacy v1, model v2, model v3 with automated
/// rules, and model v3 with non_automated rules (neutral and non-neutral Framing), including the Phase 3-D park / fielding / advancement-v2 slots. A game whose catcher is neutral must be the model-v2
/// game event for event (receipts are compared), so nothing of Phase 3-D moved; a non-neutral catcher may change calls and then only calls.
/// </summary>
public class FramingIntegrationTests
{
    private static readonly ulong[] Seeds = [3, 11, 29];

    private static void Clean(Gx.GameRun run)
    {
        Assert.Empty(run.Violations);
        Assert.Equal(GameStatus.Final, run.Last.State.Status);
        Assert.True(run.Events > 100);
    }

    // ---------------------------------------------------------------- 1. legacy v1 and 2. model v2 never see Framing

    [Fact]
    public void Legacy_v1_and_model_v2_games_never_connect_the_framing_slot()
    {
        foreach (var seed in Seeds)
        {
            var legacy = Gx.Play(null, Gx.Tuning1, null, seed);
            Clean(legacy);
            Assert.DoesNotContain("catcher.framing", legacy.SlotsSeen.Keys);

            var v1Set = Gx.Set();
            var v2 = Gx.Play(Gx.Exec(inputsId: v1Set.Id, tuning: Gx.Tuning2), Gx.Tuning2, v1Set, seed);
            Clean(v2);
            Assert.DoesNotContain("catcher.framing", v2.SlotsSeen.Keys);
            Assert.Equal("prob-model-v2", v2.Last.State.Final!.Execution!.ProbabilityModelVersion);
        }
    }

    // ---------------------------------------------------------------- 3. model v3 + automated

    [Fact]
    public void Model_v3_with_automated_rules_is_the_model_v2_game_whatever_the_catchers_ratings_and_asks_for_no_framing()
    {
        foreach (var seed in Seeds)
        {
            var v1Set = Gx.Set();
            var v2 = Gx.Play(Gx.Exec(inputsId: v1Set.Id, tuning: Gx.Tuning2), Gx.Tuning2, v1Set, seed, rules: Automated);

            // a content-version-2 set whose players have wildly different catcher ratings, and one in which nobody has catcher ratings, and a content-version-1 set
            foreach (var set in new[] { Fr.SetV2(id => BasePlayers.ToList().IndexOf(id) * 13 % 101), Fr.SetV2(_ => null), Fr.SetV1(_ => 0) })
            {
                var v3 = Gx.Play(Fr.Exec(set), Fr.T3, set, seed, rules: Automated);
                Clean(v3);
                Assert.DoesNotContain("catcher.framing", v3.SlotsSeen.Keys);
                Assert.Equal(v2.Receipts, v3.Receipts);                       // event for event, fingerprint for fingerprint
                Assert.Equal("prob-model-v3", v3.Last.State.Final!.Execution!.ProbabilityModelVersion);
            }
        }
    }

    // ---------------------------------------------------------------- 4-6. model v3 + non_automated

    [Fact]
    public void Model_v3_with_non_automated_rules_and_a_neutral_catcher_is_the_model_v2_game_event_for_event_while_still_connecting_the_slot()
    {
        foreach (var seed in Seeds)
        {
            var v1Set = Gx.Set();
            var v2 = Gx.Play(Gx.Exec(inputsId: v1Set.Id, tuning: Gx.Tuning2), Gx.Tuning2, v1Set, seed);
            var neutral = Fr.SetV2(_ => 50);
            var v3 = Gx.Play(Fr.Exec(neutral), Fr.T3, neutral, seed);

            Clean(v3);
            Assert.True(v3.SlotsSeen.GetValueOrDefault("catcher.framing") > 20);      // the slot really ran on called pitches ...
            Assert.Equal(v2.Receipts, v3.Receipts);                                   // ... and a neutral rating changed nothing
            Assert.Equal(v2.Pitches, v3.Pitches);
        }
    }

    [Fact]
    public void A_non_neutral_catcher_changes_only_calls_and_the_game_still_obeys_every_invariant_and_is_deterministic()
    {
        var differing = 0;
        foreach (var seed in Seeds)
        {
            var great = Fr.SetV2(_ => 100);
            var poor = Fr.SetV2(_ => 0);
            var a1 = Gx.Play(Fr.Exec(great), Fr.T3, great, seed);
            var a2 = Gx.Play(Fr.Exec(great), Fr.T3, great, seed);
            var p1 = Gx.Play(Fr.Exec(poor), Fr.T3, poor, seed);
            Clean(a1);
            Clean(p1);
            Assert.Equal(a1.Receipts, a2.Receipts);                                    // same seed, same inputs: the same game
            if (!a1.Receipts.SequenceEqual(p1.Receipts))
                differing++;
        }

        Assert.True(differing > 0, "a great and a poor framer should not play the identical game");
    }

    // ---------------------------------------------------------------- 7. with Phase 3-D park, fielding and advancement v2

    [Fact]
    public void Model_v3_with_park_fielding_and_advancement_v2_keeps_every_phase_3d_slot_and_equals_model_v2_for_a_neutral_catcher()
    {
        var park = Gx.Park(1120, 930, 1210);
        foreach (var seed in Seeds)
        {
            var v1Set = Fr.VariedSetV1();
            var v2 = Gx.Play(Gx.Exec(park, AdvancementPolicy.VersionV2, v1Set.Id, Gx.Tuning2), Gx.Tuning2, v1Set, seed);
            var neutral = Fr.VariedSetV2(_ => 50);
            var v3 = Gx.Play(Fr.Exec(neutral, policy: AdvancementPolicy.VersionV2, park: park), Fr.T3, neutral, seed);

            Clean(v3);
            foreach (var slot in new[] { "park.home_runs", "park.hits", "park.extra_base_hits", "defense.fielding", "catcher.framing" })
                Assert.True(v3.SlotsSeen.GetValueOrDefault(slot) > 0, slot);
            Assert.Equal(v2.Receipts, v3.Receipts);
            Assert.Equal((v2.InPlay, v2.Pending, v2.Resolved), (v3.InPlay, v3.Pending, v3.Resolved));
            Assert.Equal(v2.SlotsSeen.Where(kv => kv.Key != "catcher.framing").OrderBy(kv => kv.Key), v3.SlotsSeen.Where(kv => kv.Key != "catcher.framing").OrderBy(kv => kv.Key));
            Assert.Equal(park, v3.Last.State.Final!.Execution!.Park);                  // the retained venue context is the one the game was started with
        }
    }

    [Fact]
    public void A_real_pipeline_game_fails_at_the_first_pitch_when_the_catcher_input_is_missing_instead_of_playing_with_a_default()
    {
        var set = Fr.SetV2(id => BasePlayers.ToList().IndexOf(id) < 0 ? 50 : null);              // nobody has catcher ratings
        var execution = Fr.Exec(set);
        var run = Gx.Play(execution, Fr.T3, set, 5, maxEvents: 50);
        Assert.Contains(run.Violations, v => v.Contains("prepare: PitchPrepare.catcher_ratings_missing", StringComparison.Ordinal));
        Assert.True(run.Pitches <= 1);
    }
}
