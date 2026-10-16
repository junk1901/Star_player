using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;
using static StarPlayer.Infrastructure.Tests.Pc;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-E Package B, the INPUT side: the two player-input-set content versions (v1 identities unchanged, v2 adds ReceivingFraming), the frozen catcher input read at Prepare, and the
/// freshness checks of <c>CheckPrepared</c>. Expected identities are computed by an independent Python implementation of the documented text format (pinned literals) and by a second
/// implementation written in this file; nothing here asks the production digest what it should be.
/// </summary>
public class FramingInputTests
{
    // sheets (p-a: fielding 10, speed 20, arm 30, framing 70) and (p-b: 40, 50, 60, no catcher ratings): hashes computed by the independent Python implementation
    private const string GoldenV1 = "a22406151d4fd0fc9acee7fb1c2f0bbd4b61eeea9fe75222e17f736d4e2a7987";
    private const string GoldenV2 = "a3ab8c93b4ff011ed22293259198c00657f9609a0e77fe5bf4a7632bdb24146f";
    private const string GoldenV2NoCatcherRatings = "9eb1ef7bb661168ea2693df3dfdbe5887fe9b4a8c00f0fd8e04ec83d02fa1124";

    private static readonly PlayerId A = PlayerId.Create("p-a").Value;
    private static readonly PlayerId B = PlayerId.Create("p-b").Value;

    private static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    // ---------------------------------------------------------------- content version 1: nothing changed

    [Fact]
    public void Content_version_1_keeps_its_exact_identity_and_ignores_catcher_ratings_entirely()
    {
        var plain = Ok(PlayerInputSet.Create([Gx.Sheet(A, 10, 20, 30), Gx.Sheet(B, 40, 50, 60)]));
        var withCatcher = Ok(PlayerInputSet.Create([Fr.Sheet(A, 70, 10, 20, 30), Fr.Sheet(B, null, 40, 50, 60)]));

        Assert.Equal(GoldenV1, plain.Id);                                           // the pre-Phase-3-E identity of these sheets
        Assert.Equal(GoldenV1, withCatcher.Id);                                     // a v1 set never sees ReceivingFraming ...
        Assert.Equal(GoldenV1, Sha("player-input-set-v1\np-a|fielding=10|speed=20|arm=30\np-b|fielding=40|speed=50|arm=60"));   // ... and the documented text is what is hashed
        Assert.Equal(PlayerInputSet.ContentV1, plain.ContentVersion);
        Assert.Equal("player-input-set-v1", PlayerInputSet.ContentV1);
        Assert.All(withCatcher.Entries, e => Assert.Null(e.ReceivingFraming));      // ... so it cannot hold a meaningful value its identity ignores
        Assert.Equal((10, 20, 30), (withCatcher.Find(A)!.Fielding, withCatcher.Find(A)!.Speed, withCatcher.Find(A)!.Arm));
    }

    [Fact]
    public void A_v1_identity_does_not_change_when_only_a_catcher_rating_changes()
    {
        var first = Ok(PlayerInputSet.Create([Fr.Sheet(A, 0), Fr.Sheet(B, 100)]));
        var second = Ok(PlayerInputSet.Create([Fr.Sheet(A, 99), Fr.Sheet(B, null)]));
        Assert.Equal(first.Id, second.Id);
    }

    // ---------------------------------------------------------------- content version 2

    [Fact]
    public void Content_version_2_has_its_own_identity_covering_the_framing_rating_with_a_fixed_null_token()
    {
        var set = Ok(PlayerInputSet.CreateForFraming([Fr.Sheet(A, 70, 10, 20, 30), Fr.Sheet(B, null, 40, 50, 60)]));

        Assert.Equal(GoldenV2, set.Id);
        Assert.Equal(GoldenV2, Sha("player-input-set-v2\np-a|fielding=10|speed=20|arm=30|framing=70\np-b|fielding=40|speed=50|arm=60|framing=-"));
        Assert.Equal(PlayerInputSet.ContentV2, set.ContentVersion);
        Assert.Equal("player-input-set-v2", PlayerInputSet.ContentV2);
        Assert.Equal((70, (int?)null), (set.Find(A)!.ReceivingFraming, set.Find(B)!.ReceivingFraming));
        Assert.Equal(GoldenV2NoCatcherRatings, Ok(PlayerInputSet.CreateForFraming([Fr.Sheet(A, null, 10, 20, 30), Fr.Sheet(B, null, 40, 50, 60)])).Id);
        Assert.NotEqual(GoldenV1, set.Id);                                          // the same sheets are a different identity in the other content version
    }

    [Fact]
    public void The_v2_identity_is_deterministic_order_free_and_changes_with_every_framing_value_and_with_membership()
    {
        PlayerInputSet Make(int? a, int? b) => Ok(PlayerInputSet.CreateForFraming([Fr.Sheet(A, a, 10, 20, 30), Fr.Sheet(B, b, 40, 50, 60)]));

        Assert.Equal(Make(70, null).Id, Make(70, null).Id);                                                                     // same content, same id
        Assert.Equal(Make(70, null).Id, Ok(PlayerInputSet.CreateForFraming([Fr.Sheet(B, null, 40, 50, 60), Fr.Sheet(A, 70, 10, 20, 30)])).Id);   // order of the sheets is irrelevant
        var ids = new[] { 0, 1, 49, 50, 51, 99, 100 }.Select(r => Make(r, null).Id).Append(Make(null, null).Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());                                                                        // every rating (and "none") is its own identity
        Assert.NotEqual(Make(70, null).Id, Make(70, 70).Id);                                                                    // a second player's rating
        Assert.NotEqual(Make(null, 70).Id, Make(70, null).Id);                                                                  // the rating belongs to ITS player
        Assert.NotEqual(Make(70, null).Id, Ok(PlayerInputSet.CreateForFraming([Fr.Sheet(A, 70, 10, 20, 30)])).Id);              // membership
        Assert.NotEqual(Make(50, null).Id, Make(null, null).Id);                                                                // 50 is a rating, "no catcher ratings" is not 50
    }

    [Fact]
    public void Both_content_versions_refuse_a_missing_list_a_missing_sheet_and_a_repeated_player_without_skipping_anything()
    {
        foreach (var create in new Func<IEnumerable<PlayerSheet?>?, Result<PlayerInputSet>>[] { PlayerInputSet.Create, PlayerInputSet.CreateForFraming })
        {
            Assert.Equal("PlayerInputSet.sheets_missing", Code(create(null)));
            Assert.Equal("PlayerInputSet.sheet_missing", Code(create([Fr.Sheet(A, 1), null])));
            Assert.Equal("PlayerInputSet.duplicate_player", Code(create([Fr.Sheet(A, 1), Fr.Sheet(A, 2)])));
            Assert.True(create([]).IsSuccess);
        }
    }

    [Fact]
    public void A_set_is_a_value_copy_changing_or_emptying_the_source_list_afterwards_changes_nothing()
    {
        var source = new List<PlayerSheet?> { Fr.Sheet(A, 70, 10, 20, 30), Fr.Sheet(B, 33, 40, 50, 60) };
        var set = Ok(PlayerInputSet.CreateForFraming(source));
        var (id, entries) = (set.Id, set.Entries);

        source.Clear();
        source.Add(Fr.Sheet(A, 1));

        Assert.Equal(id, set.Id);
        Assert.Equal(2, set.Count);
        Assert.Equal(entries, set.Entries);
        Assert.Equal(70, set.Find(A)!.ReceivingFraming);
        Assert.Equal(typeof(ImmutableArray<PlayerRunFieldInput>), set.Entries.GetType());   // the published entries are an immutable array of immutable records
    }

    [Fact]
    public void The_content_version_is_a_property_of_the_type_not_a_guess_from_the_identity_text()
    {
        var v1 = Fr.SetV1(_ => 50);
        var v2 = Fr.SetV2(_ => 50);
        Assert.Equal((PlayerInputSet.ContentV1, PlayerInputSet.ContentV2), (v1.ContentVersion, v2.ContentVersion));
        Assert.NotEqual(v1.Id, v2.Id);
        Assert.Contains("player-input-set-v2", v2.ToString());
    }

    // ---------------------------------------------------------------- the frozen catcher at Prepare (model v3 + non_automated)

    [Fact]
    public void Prepare_freezes_the_real_catcher_his_rating_and_the_set_identity_before_the_first_draw()
    {
        var (d, set) = Fr.NonAutomatedGame(catcherFraming: 77);
        var g = Aggregate(d, 5);
        var catcher = Fr.CatcherOf(g.State);

        var prepared = Fr.Prepare(g, set);

        var frozen = Assert.IsType<FrozenCatcherInput>(prepared.Catcher);
        Assert.Equal((catcher, 77, set.Id, Gid), (frozen.Catcher, frozen.ReceivingFraming, frozen.InputSetId, frozen.GameId));
        Assert.Equal(set.Id, prepared.PlayerInputsId);
        Assert.Equal(frozen.InputSetId, prepared.Fielding!.InputSetId);                        // the fielders and the catcher come from the very same set
        Assert.Equal(("prob-model-v3", "tuning-virtual-proto-v3", Fr.T3.ContentHash), (prepared.ProbabilityModelVersion, prepared.TuningVersion, prepared.TuningHash));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void The_extreme_and_neutral_ratings_are_frozen_as_they_are_never_replaced_by_a_default(int rating)
    {
        var (d, set) = Fr.NonAutomatedGame(rating);
        var prepared = Fr.Prepare(Aggregate(d, 6), set);
        Assert.Equal(rating, prepared.Catcher!.ReceivingFraming);
    }

    [Fact]
    public void Another_catchers_rating_is_never_used_only_the_players_standing_at_catcher_counts()
    {
        var (d, _) = Fr.NonAutomatedGame(10);
        var catcher = Fr.CatcherOf(d.State);
        // every OTHER player has framing 95, the real catcher 10
        var set = Fr.SetV2(new Dictionary<PlayerId, int> { [catcher] = 10 }, others: 95);
        var probe = Fr.InSequence(NonAutomated, Fr.Exec(set));

        Assert.Equal(10, Fr.Prepare(Aggregate(probe, 7), set).Catcher!.ReceivingFraming);
    }

    [Fact]
    public void Automated_rules_under_model_v3_freeze_no_catcher_and_need_no_framing_rating_at_all()
    {
        // a content-version-2 set in which NOBODY has catcher ratings, and a content-version-1 set: both are valid for an automated v3 game
        foreach (var set in new[] { Fr.SetV2(_ => null), Fr.SetV1(_ => 50), Fr.SetV2(_ => 80) })
        {
            var (d, pinned) = Fr.AutomatedGame(set);
            var prepared = Fr.Prepare(Aggregate(d, 8), pinned);
            Assert.Null(prepared.Catcher);
            Assert.Equal(pinned.Id, prepared.PlayerInputsId);
            Assert.DoesNotContain(prepared.Draft.Steps, s => s.Trace.Modifiers.Any(m => m.Slot == "catcher.framing"));
        }
    }

    [Fact]
    public void Models_v1_and_v2_freeze_no_catcher_and_keep_accepting_the_content_version_1_set()
    {
        var v1Set = Gx.Set();
        var exec = Gx.Exec(inputsId: v1Set.Id, tuning: Gx.Tuning2);
        var d = Gx.InSequence(exec);
        var prepared = Gx.Prepare(Aggregate(d, 9), Gx.Tuning2, inputs: v1Set);

        Assert.Null(prepared.Catcher);
        Assert.Equal(PlayerInputSet.ContentV1, v1Set.ContentVersion);
        Assert.Equal("prob-model-v2", prepared.ProbabilityModelVersion);
        // a legacy game (no execution) on the v1 tuning is unchanged as well
        var legacy = new Driver(StartState(NonAutomated, Gid), Gid).Approach();
        Assert.Null(Prepare(Aggregate(legacy, 9)).Catcher);
    }

    [Fact]
    public void A_non_automated_v3_pitch_needs_its_catcher_input_and_each_missing_piece_has_its_own_typed_error_before_any_draw()
    {
        var probe = Fr.AtStart(NonAutomated, null).State;
        var catcher = Fr.CatcherOf(probe);

        // no set passed although the game pins one
        var (d, set) = Fr.NonAutomatedGame(60);
        var g = Aggregate(d, 3);
        Assert.Equal("PitchPrepare.player_inputs_missing", Code(Fr.TryPrepare(g, null)));

        // the pinned set is a content-version-1 set: it cannot carry Framing
        var v1 = Fr.SetV1(_ => 60);
        var gv1 = Aggregate(Fr.InSequence(NonAutomated, Fr.Exec(v1)), 3);
        Assert.Equal("PitchPrepare.catcher_input_format", Code(Fr.TryPrepare(gv1, v1)));

        // the catcher is not in the set
        var withoutCatcher = Ok(PlayerInputSet.CreateForFraming(BasePlayers.Where(p => p != catcher).Select(p => Fr.Sheet(p, 50))));
        var gNone = Aggregate(Fr.InSequence(NonAutomated, Fr.Exec(withoutCatcher)), 3);
        Assert.Equal("PitchPrepare.catcher_input_missing", Code(Fr.TryPrepare(gNone, withoutCatcher)));

        // the catcher has no catcher ratings: the rating is NOT replaced by 50
        var noRatingSet = Fr.SetV2(p => p == catcher ? null : 50);
        var gNoRating = Aggregate(Fr.InSequence(NonAutomated, Fr.Exec(noRatingSet)), 3);
        Assert.Equal("PitchPrepare.catcher_ratings_missing", Code(Fr.TryPrepare(gNoRating, noRatingSet)));

        // an explicit Take is checked the same way, and the failure changed nothing (a Prepare never draws)
        var take = Aggregate(Fr.AtStart(NonAutomated, Fr.Exec(noRatingSet)), 3);
        Assert.Equal("PitchPrepare.catcher_ratings_missing", Code(Fr.TryPrepare(take, noRatingSet, take: true)));
        Assert.Equal(g.Rng.StateHash, Aggregate(d, 3).Rng.StateHash);
        Assert.Equal(set.Id, g.State.Execution!.PlayerInputsId);
    }

    [Fact]
    public void A_model_v3_non_automated_game_that_pins_no_input_set_cannot_even_start()
    {
        var exec = Fr.Exec(null);
        var started = GameReducer.Start(new GameStart(Gid, NonAutomated, AwaySetup, HomeSetup, exec));
        Assert.Equal("Game.execution_inputs_required_framing", Code(started));
        // the same execution is fine under automated rules (no Framing is read) and a v2 execution is fine under non_automated rules (Framing is reserved)
        Assert.True(GameReducer.Start(new GameStart(Gid, Automated, AwaySetup, HomeSetup, exec)).IsSuccess);
        Assert.True(GameReducer.Start(new GameStart(Gid, NonAutomated, AwaySetup, HomeSetup, Gx.Exec())).IsSuccess);
    }

    // ---------------------------------------------------------------- execution contract: explicit model/tuning pairs

    [Fact]
    public void The_game_execution_names_one_of_the_two_active_pairs_and_the_park_range_is_the_selected_models_own()
    {
        var set = Fr.SetV2(_ => 50);
        foreach (var (model, tuning) in new[] { ("prob-model-v2", "tuning-virtual-proto-v2"), ("prob-model-v3", "tuning-virtual-proto-v3") })
        {
            var good = Gx.Exec(inputsId: set.Id) with { ProbabilityModelVersion = model, TuningVersion = tuning };
            Assert.True(GameReducer.Start(new GameStart(Gid, Automated, AwaySetup, HomeSetup, good)).IsSuccess, model);
        }

        // the crossed pairs, the v1 pair and an unknown model are refused with the existing typed codes
        var baseExec = Gx.Exec(inputsId: set.Id);
        (string Model, string Tuning, string Code)[] bad =
        [
            ("prob-model-v2", "tuning-virtual-proto-v3", "Game.execution_tuning_unsupported"),
            ("prob-model-v3", "tuning-virtual-proto-v2", "Game.execution_tuning_unsupported"),
            ("prob-model-v3", "tuning-virtual-proto-v1", "Game.execution_tuning_unsupported"),
            ("prob-model-v2", "tuning-virtual-proto-v1", "Game.execution_tuning_unsupported"),
            ("prob-model-v1", "tuning-virtual-proto-v1", "Game.execution_model_unsupported"),
            ("prob-model-v4", "tuning-virtual-proto-v3", "Game.execution_model_unsupported"),
        ];
        foreach (var (model, tuning, code) in bad)
            Assert.Equal(code, Code(GameReducer.Start(new GameStart(Gid, Automated, AwaySetup, HomeSetup, baseExec with { ProbabilityModelVersion = model, TuningVersion = tuning }))));

        // the park range comes from the selected model: 799 and 1251 are refused under v3 exactly as under v2 (v3 does not widen or narrow it)
        foreach (var tuning in new[] { Gx.Tuning2, Fr.T3 })
            foreach (var permille in new[] { 799, 1251 })
                Assert.Equal("Game.park_out_of_model_range", Code(GameReducer.Start(new GameStart(Gid, Automated, AwaySetup, HomeSetup,
                    Gx.Exec(Gx.Park(homeRuns: permille), inputsId: set.Id, tuning: tuning)))));
        Assert.True(GameReducer.Start(new GameStart(Gid, Automated, AwaySetup, HomeSetup, Gx.Exec(Gx.Park(800, 1000, 1250), inputsId: set.Id, tuning: Fr.T3))).IsSuccess);
    }

    // ---------------------------------------------------------------- CheckPrepared: the catcher is looked at again

    private static Result<NonInPlayPitchCandidate> Rebuild(AuthoritativeGame g, PreparedPitch prepared, GameState nowState, PlayerInputSet? set, TuningProfile? tuning = null)
    {
        var (b, p) = Sheets(nowState);
        return PitchPreparation.BuildNonInPlayCandidate(prepared, nowState, g.Rng, tuning ?? Fr.T3, b, p, g.LastEventSequence, default, set);
    }

    private static GameState SwapCatcherWithFirstBase(GameState state)
    {
        var side = state.FieldingSide;
        var a = state.Team(side).SlotAt(FieldPosition.Catcher)!.Order;
        var b = state.Team(side).SlotAt(FieldPosition.FirstBase)!.Order;
        return Ok(GameReducer.Apply(state, new SubstitutionEvent("sub-swap", SubstitutionKind.PositionSwap, side, default, null, a, b))).State;
    }

    [Fact]
    public void A_catcher_change_after_the_preparation_is_found_with_its_own_reason_even_when_the_new_catcher_has_the_same_rating()
    {
        foreach (var rating in new[] { 50, 80 })
        {
            var (d, set) = Fr.NonAutomatedGame(rating, approach: false, others: 50);
            var g = Aggregate(d, 11);
            var prepared = Fr.Prepare(g, set, take: true);
            Assert.Null(Rebuild(g, prepared, g.State, set).Error);                       // control: nothing changed, the candidate is built

            var swapped = SwapCatcherWithFirstBase(g.State);
            Assert.NotEqual(Fr.CatcherOf(g.State), Fr.CatcherOf(swapped));
            var result = Rebuild(g, prepared, swapped, set);

            Assert.Equal("PitchCandidate.catcher_changed", Code(result));               // rating 50 vs 50 included: the PlayerId alone differs
        }
    }

    [Fact]
    public void The_same_catcher_with_another_rating_is_another_input_set_and_is_refused()
    {
        var (d, set) = Fr.NonAutomatedGame(60);
        var g = Aggregate(d, 12);
        var prepared = Fr.Prepare(g, set);
        var catcher = Fr.CatcherOf(g.State);
        var otherRating = Fr.SetV2(new Dictionary<PlayerId, int> { [catcher] = 61 }, others: 50);

        Assert.NotEqual(set.Id, otherRating.Id);
        Assert.Equal("PitchCandidate.player_inputs_changed", Code(Rebuild(g, prepared, g.State, otherRating)));
        Assert.Equal("PitchCandidate.player_inputs_changed", Code(Rebuild(g, prepared, g.State, null)));
        Assert.Equal("PitchCandidate.player_inputs_changed", Code(Rebuild(g, prepared, g.State, Fr.SetV1(_ => 60))));
    }

    [Fact]
    public void A_frozen_input_with_a_rating_that_differs_from_the_real_set_is_refused_even_under_the_same_set_identity()
    {
        var (d, set) = Fr.NonAutomatedGame(60);
        var g = Aggregate(d, 13);
        var prepared = Fr.Prepare(g, set);
        var f = prepared.Catcher!;

        var forged = prepared.WithCatcher(new FrozenCatcherInput(f.GameId, f.InputSetId, f.Catcher, 99));
        Assert.Equal("PitchCandidate.framing_changed", Code(Rebuild(g, forged, g.State, set)));

        var otherCatcher = BasePlayers.First(p => p != f.Catcher);
        var wrongPlayer = prepared.WithCatcher(new FrozenCatcherInput(f.GameId, f.InputSetId, otherCatcher, f.ReceivingFraming));
        Assert.Equal("PitchCandidate.catcher_changed", Code(Rebuild(g, wrongPlayer, g.State, set)));       // the player written in the preparation is not trusted; the real catcher is re-read
    }

    [Fact]
    public void A_catcher_input_of_another_game_or_another_set_cannot_be_reused()
    {
        var (d, set) = Fr.NonAutomatedGame(60);
        var g = Aggregate(d, 14);
        var prepared = Fr.Prepare(g, set);
        var f = prepared.Catcher!;

        Assert.Equal("PitchCandidate.catcher_game_mismatch", Code(Rebuild(g, prepared.WithCatcher(new FrozenCatcherInput("game:other", f.InputSetId, f.Catcher, f.ReceivingFraming)), g.State, set)));
        Assert.Equal("PitchCandidate.player_inputs_changed", Code(Rebuild(g, prepared.WithCatcher(new FrozenCatcherInput(f.GameId, new string('a', 64), f.Catcher, f.ReceivingFraming)), g.State, set)));
    }

    [Fact]
    public void Whether_a_catcher_input_is_needed_is_decided_by_the_current_model_and_rules_not_by_the_preparation()
    {
        var (d, set) = Fr.NonAutomatedGame(60);
        var g = Aggregate(d, 15);
        var prepared = Fr.Prepare(g, set);
        Assert.Equal("PitchCandidate.catcher_input_mismatch", Code(Rebuild(g, prepared.WithCatcher(null), g.State, set)));          // a required input stripped

        var (da, setA) = Fr.AutomatedGame();
        var ga = Aggregate(da, 15);
        var preparedA = Fr.Prepare(ga, setA);
        var attached = new FrozenCatcherInput(Gid, setA.Id, Fr.CatcherOf(ga.State), 50);
        Assert.Equal("PitchCandidate.catcher_input_mismatch", Code(Rebuild(ga, preparedA.WithCatcher(attached), ga.State, setA)));  // an unneeded input attached
    }

    [Fact]
    public void A_wrong_model_or_tuning_is_still_found_by_the_unchanged_checks()
    {
        var (d, set) = Fr.NonAutomatedGame(60);
        var g = Aggregate(d, 16);
        var prepared = Fr.Prepare(g, set);

        Assert.Equal("PitchCandidate.tuning_changed", Code(Rebuild(g, prepared, g.State, set, Gx.Tuning2)));
        Assert.Equal("PitchCandidate.tuning_changed", Code(Rebuild(g, prepared, g.State, set, Gx.Tuning1)));
    }

    [Fact]
    public void The_rules_mode_in_force_is_what_decides_a_stale_automated_preparation_cannot_pass_as_a_non_automated_one()
    {
        // the same players, the same model: a preparation made under automated rules is refused under non_automated rules by the unchanged rules check
        var (da, setA) = Fr.AutomatedGame();
        var ga = Aggregate(da, 17);
        var preparedA = Fr.Prepare(ga, setA);
        var (dn, setN) = Fr.NonAutomatedGame(60);
        var gn = Aggregate(dn, 17);

        var (b, p) = Sheets(gn.State);
        var result = PitchPreparation.BuildNonInPlayCandidate(preparedA, gn.State, gn.Rng, Fr.T3, b, p, gn.LastEventSequence, default, setN);
        Assert.False(result.IsSuccess);
        Assert.Contains(Code(result), new[] { "PitchCandidate.rules_changed", "PitchCandidate.player_inputs_changed", "PitchCandidate.execution_mismatch", "PitchCandidate.rng_state_changed" });
    }
}
