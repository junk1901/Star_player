using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;
using static StarPlayer.Infrastructure.Tests.Pc;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-E (Game Calling v4, GC-C): whole games through the REAL pipeline (prepare, plan, advancement, staging with the commit evidence and its replay, publication by the cursor) on every model and mode, with
/// Game Calling and Control at their corners, and with Park, Fielding, Advancement v2, Framing and Game Calling all active at once. A neutral Game Calling rating plays the v3 (and, with a neutral catcher, the
/// v2) game event for event, fingerprint for fingerprint; only the version labels may differ. No production runner exists: the loop only chooses an approach for every new plate appearance.
/// </summary>
public class GameCallingIntegrationTests
{
    private static readonly ulong[] Seeds = [3, 11, 29];

    private static Gx.GameRun PlayV4(string mode, int control, int calling, int framing, ulong seed, string policy = AdvancementPolicy.Version, ParkFactorSnapshot? park = null, bool varied = false)
    {
        var set = Gc.SetV3(id => varied ? Gc.Varied(id, control, (framing, calling)) : new Gc.Spec(control, (framing, calling)));
        return Gx.Play(Gc.Exec(set, policy, park), Gc.T4, set, seed, rules: RulesFor(mode), sheets: s => Gc.SheetsOf(s, control));
    }

    private static void Clean(Gx.GameRun run)
    {
        Assert.Empty(run.Violations);
        Assert.Equal(GameStatus.Final, run.Last.State.Status);
        Assert.True(run.Events > 100);
    }

    private static Gx.GameRun PlayV2(ulong seed, string mode, ParkFactorSnapshot? park = null, string policy = AdvancementPolicy.Version, bool varied = false)
    {
        var set = varied ? Fr.VariedSetV1() : Gx.Set();
        return Gx.Play(Gx.Exec(park, policy, set.Id, Gx.Tuning2), Gx.Tuning2, set, seed, rules: RulesFor(mode));
    }

    // ---------------------------------------------------------------- scenarios 1-4: the earlier models never see Game Calling

    [Fact]
    public void Scenario_1_legacy_v1_and_2_model_v2_never_connect_game_calling_or_framing()
    {
        foreach (var seed in Seeds)
        {
            var legacy = Gx.Play(null, Gx.Tuning1, null, seed);
            Clean(legacy);
            var v2 = PlayV2(seed, "non_automated", Gx.Park(1100, 1000, 1200), AdvancementPolicy.VersionV2, varied: true);
            Clean(v2);
            foreach (var run in new[] { legacy, v2 })
                Assert.All(new[] { "catcher.game_calling", "catcher.framing", "battery.chemistry" }, slot => Assert.DoesNotContain(slot, run.SlotsSeen.Keys));
        }
    }

    [Fact]
    public void Scenarios_3_and_4_model_v3_never_connects_game_calling_and_still_equals_model_v2_for_a_neutral_catcher()
    {
        foreach (var seed in Seeds)
        {
            var v2 = PlayV2(seed, "non_automated");
            var neutral = Fr.SetV2(_ => 50);
            var v3 = Gx.Play(Fr.Exec(neutral), Fr.T3, neutral, seed);
            Clean(v3);
            Assert.DoesNotContain("catcher.game_calling", v3.SlotsSeen.Keys);
            Assert.True(v3.SlotsSeen.GetValueOrDefault("catcher.framing") > 20);
            Assert.Equal(v2.Receipts, v3.Receipts);

            var v2a = PlayV2(seed, "automated");
            var v3a = Gx.Play(Fr.Exec(neutral), Fr.T3, neutral, seed, rules: Automated);
            Clean(v3a);
            Assert.DoesNotContain("catcher.game_calling", v3a.SlotsSeen.Keys);
            Assert.DoesNotContain("catcher.framing", v3a.SlotsSeen.Keys);
            Assert.Equal(v2a.Receipts, v3a.Receipts);
        }
    }

    // ---------------------------------------------------------------- scenarios 5-7: model v4 + automated

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void Scenarios_5_to_7_model_v4_automated_with_game_calling_0_50_100_plays_clean_games_and_asks_for_no_framing(int calling)
    {
        foreach (var seed in Seeds)
        {
            var run = PlayV4("automated", control: 50, calling, framing: 50, seed);
            Clean(run);
            Assert.True(run.SlotsSeen.GetValueOrDefault("catcher.game_calling") > 100);          // listed on every pitch (applied only on Borderline aims)
            Assert.True(run.SlotsSeen.GetValueOrDefault("pitcher.control") > 100);
            Assert.DoesNotContain("catcher.framing", run.SlotsSeen.Keys);                         // Automated: Framing is neither read nor listed
            Assert.DoesNotContain("battery.chemistry", run.SlotsSeen.Keys);
            Assert.Equal("prob-model-v4", run.Last.State.Final!.Execution!.ProbabilityModelVersion);
            Assert.Equal(run.Receipts, PlayV4("automated", 50, calling, 50, seed).Receipts);      // the same seed and inputs: the same game
        }
    }

    [Fact]
    public void Scenario_6_a_neutral_game_calling_rating_plays_the_v3_and_v2_automated_game_event_for_event()
    {
        foreach (var seed in Seeds)
        {
            var v4 = PlayV4("automated", 50, 50, 50, seed);
            var v2 = PlayV2(seed, "automated");
            Clean(v4);
            Assert.Equal(v2.Receipts, v4.Receipts);                                               // the semantic game: identical events
            Assert.Equal(v2.Pitches, v4.Pitches);
            Assert.NotEqual(v2.Last.State.Final!.Execution!.ProbabilityModelVersion, v4.Last.State.Final!.Execution!.ProbabilityModelVersion);   // only the metadata differs
            Assert.NotEqual(v2.Last.State.Final.Execution!.TuningHash, v4.Last.State.Final.Execution!.TuningHash);
        }
    }

    [Fact]
    public void Game_calling_0_and_100_play_different_games_on_some_seed_and_never_a_broken_one()
    {
        var differing = 0;
        foreach (var seed in Seeds)
        {
            var low = PlayV4("automated", 50, 0, 50, seed);
            var high = PlayV4("automated", 50, 100, 50, seed);
            Clean(low);
            Clean(high);
            if (!low.Receipts.SequenceEqual(high.Receipts))
                differing++;
        }

        Assert.True(differing > 0, "a poor and a great game caller should not play the identical game");
    }

    // ---------------------------------------------------------------- scenarios 8-10: model v4 + non_automated

    [Theory]
    [InlineData(0, 50)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    public void Scenarios_8_to_10_model_v4_non_automated_runs_framing_and_game_calling_together(int framing, int calling)
    {
        foreach (var seed in Seeds)
        {
            var run = PlayV4("non_automated", 50, calling, framing, seed);
            Clean(run);
            Assert.True(run.SlotsSeen.GetValueOrDefault("catcher.framing") > 20);                 // on the called pitches
            Assert.True(run.SlotsSeen.GetValueOrDefault("catcher.game_calling") > 100);           // on every pitch
            Assert.DoesNotContain("battery.chemistry", run.SlotsSeen.Keys);
            Assert.Equal(run.Receipts, PlayV4("non_automated", 50, calling, framing, seed).Receipts);
        }
    }

    [Fact]
    public void Scenario_9_framing_50_and_game_calling_50_plays_the_v3_and_v2_non_automated_game_event_for_event()
    {
        foreach (var seed in Seeds)
        {
            var v4 = PlayV4("non_automated", 50, 50, 50, seed);
            var neutral = Fr.SetV2(_ => 50);
            var v3 = Gx.Play(Fr.Exec(neutral), Fr.T3, neutral, seed);
            var v2 = PlayV2(seed, "non_automated");
            Clean(v4);
            Assert.Equal(v3.Receipts, v4.Receipts);
            Assert.Equal(v2.Receipts, v4.Receipts);
            Assert.Equal((v3.InPlay, v3.Pending, v3.Resolved), (v4.InPlay, v4.Pending, v4.Resolved));
            Assert.Equal(v3.SlotsSeen.OrderBy(kv => kv.Key), v4.SlotsSeen.Where(kv => kv.Key != "catcher.game_calling").OrderBy(kv => kv.Key));   // v4 lists exactly one more slot
        }
    }

    [Fact]
    public void Scenario_8_and_10_framing_and_game_calling_list_the_same_slots_whatever_the_rating_and_each_rating_alone_can_change_the_game()
    {
        foreach (var seed in Seeds)
        {
            var poorFraming = PlayV4("non_automated", 50, 50, 0, seed);
            var neutral = PlayV4("non_automated", 50, 50, 50, seed);
            var poorCalling = PlayV4("non_automated", 50, 0, 50, seed);
            Clean(poorFraming);
            Clean(poorCalling);
            // the same set of slots is listed whatever the ratings (the counts differ because a different rating legitimately plays a different game): only numbers move
            Assert.Equal(neutral.SlotsSeen.Keys.Order(), poorFraming.SlotsSeen.Keys.Order());
            Assert.Equal(neutral.SlotsSeen.Keys.Order(), poorCalling.SlotsSeen.Keys.Order());
            Assert.False(poorFraming.Receipts.SequenceEqual(neutral.Receipts) && poorCalling.Receipts.SequenceEqual(neutral.Receipts));
        }
    }

    // ---------------------------------------------------------------- scenarios 11-14: Control x Game Calling corners

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(100, 100)]
    public void Scenarios_11_to_14_the_control_and_game_calling_corners_play_clean_deterministic_games_in_both_modes(int control, int calling)
    {
        foreach (var mode in new[] { "automated", "non_automated" })
            foreach (var seed in Seeds)
            {
                var run = PlayV4(mode, control, calling, 50, seed);
                Clean(run);
                Assert.Equal(run.Receipts, PlayV4(mode, control, calling, 50, seed).Receipts);
                Assert.True(run.SlotsSeen.GetValueOrDefault("pitcher.control") > 100);
            }
    }

    [Fact]
    public void The_four_corners_do_not_all_play_the_same_game()
    {
        var games = new[] { (0, 0), (0, 100), (100, 0), (100, 100) }.Select(c => PlayV4("non_automated", c.Item1, c.Item2, 50, 3).Receipts).ToList();
        Assert.True(games.Select(g => string.Join(",", g)).Distinct().Count() >= 3);
    }

    // ---------------------------------------------------------------- scenario 15: everything active together

    [Fact]
    public void Scenario_15_park_fielding_advancement_v2_framing_and_game_calling_are_all_active_and_a_neutral_v4_equals_the_v3_game()
    {
        var park = Gx.Park(1120, 930, 1210);
        foreach (var seed in Seeds)
        {
            var all = PlayV4("non_automated", control: 65, calling: 75, framing: 80, seed, AdvancementPolicy.VersionV2, park, varied: true);
            Clean(all);
            foreach (var slot in new[] { "park.home_runs", "park.hits", "park.extra_base_hits", "defense.fielding", "catcher.framing", "catcher.game_calling", "pitcher.control" })
                Assert.True(all.SlotsSeen.GetValueOrDefault(slot) > 0, slot);
            Assert.True(all.Resolved > 0 && all.Pending == all.Resolved);                         // the extra-base decisions really ran with Speed and Arm
            Assert.Equal(park, all.Last.State.Final!.Execution!.Park);                            // the retained venue context
            Assert.Equal(AdvancementPolicy.VersionV2, all.Last.State.Final.Execution!.AdvancementPolicyVersion);

            // neutral v4 (Control 50, Game Calling 50, Framing 50) on the same varied fielders / runners / arms and park = the v3 game = the v2 game
            var v4 = PlayV4("non_automated", 50, 50, 50, seed, AdvancementPolicy.VersionV2, park, varied: true);
            var neutral = Fr.VariedSetV2(_ => 50);
            var v3 = Gx.Play(Fr.Exec(neutral, policy: AdvancementPolicy.VersionV2, park: park), Fr.T3, neutral, seed);
            var v1Set = Fr.VariedSetV1();
            var v2 = Gx.Play(Gx.Exec(park, AdvancementPolicy.VersionV2, v1Set.Id, Gx.Tuning2), Gx.Tuning2, v1Set, seed);
            Clean(v4);
            Assert.Equal(v3.Receipts, v4.Receipts);
            Assert.Equal(v2.Receipts, v4.Receipts);
            Assert.Equal((v3.InPlay, v3.Pending, v3.Resolved, v3.Void), (v4.InPlay, v4.Pending, v4.Resolved, v4.Void));
        }
    }

    [Fact]
    public void The_finalized_game_keeps_the_v4_execution_and_the_pinned_input_set_that_was_used()
    {
        var set = Gc.SetV3(_ => new Gc.Spec(50, (50, 50)));
        var exec = Gc.Exec(set);
        var run = Gx.Play(exec, Gc.T4, set, 7, rules: NonAutomated, sheets: s => Gc.SheetsOf(s));
        Clean(run);
        Assert.Same(exec, run.Last.State.Final!.Execution);
        Assert.Equal(set.Id, run.Last.State.Final.Execution!.PlayerInputsId);
        Assert.Equal((Gc.T4.Model.Version.Value, Gc.T4.Tuning.Value, Gc.T4.ContentHash), (exec.ProbabilityModelVersion, exec.TuningVersion, exec.TuningHash));
    }

    // ---------------------------------------------------------------- unsupported combinations are typed errors, not silent runs

    [Fact]
    public void An_unsupported_input_combination_stops_the_game_at_the_first_pitch_with_a_typed_error_instead_of_playing_with_a_default()
    {
        var set = Gc.SetV3(_ => new Gc.Spec(50, (50, 50)));
        var exec = Gc.Exec(set);

        // no set at all, a content-version-2 set, a content-version-1 set and a v3 set that is not the pinned one
        foreach (var (inputs, code) in new (PlayerInputSet?, string)[]
        {
            (null, "PitchPrepare.player_inputs_missing"),
            (Fr.SetV2(_ => 50), "PitchPrepare.catcher_input_format"),
            (Fr.SetV1(_ => 50), "PitchPrepare.catcher_input_format"),
            (Gc.SetV3(_ => new Gc.Spec(50, (51, 51))), "PitchBranch.player_inputs_mismatch"),
        })
        {
            var run = Gx.Play(exec, Gc.T4, inputs, 5, maxEvents: 50, rules: NonAutomated, sheets: s => Gc.SheetsOf(s));
            Assert.Contains(run.Violations, v => v.Contains(code, StringComparison.Ordinal));
            Assert.True(run.Pitches <= 1);
            Assert.Empty(run.Receipts.Skip(1));                                                   // nothing but the opening approach was ever confirmed
        }
    }

    [Fact]
    public void A_pitcher_sheet_that_disagrees_with_the_pinned_control_stops_the_game_at_the_first_pitch()
    {
        var set = Gc.SetV3(_ => new Gc.Spec(50, (50, 50)));
        var run = Gx.Play(Gc.Exec(set), Gc.T4, set, 5, maxEvents: 50, rules: NonAutomated, sheets: s => Gc.SheetsOf(s, 51));
        Assert.Contains(run.Violations, v => v.Contains("PitchPrepare.pitcher_control_mismatch", StringComparison.Ordinal));
        Assert.Single(run.Receipts);
    }

    // ---------------------------------------------------------------- one compare-and-swap, many contenders

    [Fact]
    public void Of_many_commits_staged_from_the_same_v4_base_exactly_one_publishes_and_the_rest_lose_without_changing_anything()
    {
        foreach (var mode in new[] { "automated", "non_automated" })
        {
            var w = Gc.Game(RulesFor(mode), control: 60, calling: 70);
            var (g, p, c) = Gc.FindNonInPlay(w, _ => true, 60);
            var cursor = new GameCursor(g);
            var staged = Enumerable.Range(0, 8).Select(_ => Gc.Stage(g, p, c, w.Set, 60).Value).ToList();

            var results = new Result<CommitRecord>[staged.Count];
            Parallel.For(0, staged.Count, i => results[i] = cursor.Publish(staged[i]));

            Assert.Single(results, r => r.IsSuccess);
            Assert.All(results.Where(r => !r.IsSuccess), r => Assert.Contains(r.Error!.Value.Code, new[] { "Commit.revision_stale", "Commit.cas_lost" }));
            var winner = staged[Array.FindIndex(results, r => r.IsSuccess)];
            Assert.Same(winner.Next, cursor.Current);
            Assert.Equal(g.Revision + 1, cursor.Current.Revision);
            Assert.Equal(g.State.AppliedEvents.Count + 1, cursor.Current.State.AppliedEvents.Count);
            Assert.Same(c.NextRng, cursor.Current.Rng);                                           // the adopted position is the candidate's: verification drew nothing
        }
    }
}
