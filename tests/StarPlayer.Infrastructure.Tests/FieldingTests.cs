using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;
using static StarPlayer.Infrastructure.Tests.Pc;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-D v2 Part B (owner decisions R1, R5, S1): the <c>defense.fielding</c> slot gets the base Fielding rating of the batted ball's representative fielder (ground and line: shortstop,
/// fly: centre field, pop-up: second base), read from the real game state and an immutable player input set, frozen into the prepared pitch and re-checked when it is used. Expected
/// distributions come from the independent Python oracle; nothing here recomputes a probability with production code.
/// </summary>
public class FieldingTests
{
    private static readonly string[] Batted =
        ["ground_weak", "ground_medium", "ground_hard", "line_weak", "line_medium", "line_hard", "fly_weak", "fly_medium", "fly_hard", "popup"];

    private static Driver Legacy() => Situation(0, 0, true);

    private static string Slot(string text, PitchBranchDraft d) => d.Steps.Single(s => s.Stage == text).Outcome;

    // ---------------------------------------------------------------- the representative fielder of each batted ball

    [Fact]
    public void Each_batted_ball_condition_maps_to_the_approved_representative_position()
    {
        Assert.Equal(
            [FieldPosition.Shortstop, FieldPosition.Shortstop, FieldPosition.Shortstop, FieldPosition.Shortstop, FieldPosition.Shortstop, FieldPosition.Shortstop,
                FieldPosition.CenterField, FieldPosition.CenterField, FieldPosition.CenterField, FieldPosition.SecondBase],
            Batted.Select(b => PitchFieldingInputs.ResponsiblePosition(b)!.Value));
        foreach (var notBattedBall in new[] { null, "", "single", "out", "heart", "ground", "fly", "Ground_weak", "groundweak", "pop", "popup_hard" })
            Assert.Null(PitchFieldingInputs.ResponsiblePosition(notBattedBall));
    }

    [Fact]
    public void The_ten_real_conditions_of_the_defense_result_stage_are_exactly_the_mapped_keys()
    {
        var keys = ProbabilityModel.V2.Stage(StageIds.DefenseResult)!.Conditions.Select(c => c.Key).ToArray();

        Assert.Equal(Batted.Order(StringComparer.Ordinal), keys.Order(StringComparer.Ordinal));
        Assert.All(keys, key => Assert.Contains(PitchFieldingInputs.ResponsiblePosition(key)!.Value, PitchFieldingInputs.Positions));
        Assert.Equal([FieldPosition.Shortstop, FieldPosition.CenterField, FieldPosition.SecondBase], PitchFieldingInputs.Positions.ToArray());
    }

    [Fact]
    public void The_representative_position_is_the_first_fielder_of_the_approved_out_template_but_not_claimed_for_hits_or_errors()
    {
        // consistency with the fixed-fielder prototype templates (ADR-007 U-3) for the OUT result: ground SS (assist then 1B), line SS, fly CF, pop 2B
        foreach (var (cls, batted) in new[] { ("ground", "ground_hard"), ("line", "line_weak"), ("fly", "fly_medium"), ("pop", "popup") })
        {
            var template = InPlayTemplateCatalog.Find(cls, InPlayResult.Out)!;
            Assert.Equal(template.Credits[0].Position, PitchFieldingInputs.ResponsiblePosition(batted));
        }

        // for hits and one error the template's retriever / error position is another fielder (ground single: left field; pop-up error: centre field): R1 does not use it
        Assert.Equal(FieldPosition.LeftField, InPlayTemplateCatalog.Find("ground", InPlayResult.Single)!.Retriever);
        Assert.Equal(FieldPosition.CenterField, InPlayTemplateCatalog.Find("pop", InPlayResult.ReachedOnError)!.Error);
        Assert.Equal(FieldPosition.SecondBase, PitchFieldingInputs.ResponsiblePosition("popup"));
    }

    // ---------------------------------------------------------------- the player input set

    [Fact]
    public void The_input_set_copies_the_base_ratings_and_has_a_stable_identity()
    {
        var a = Gx.Sheet(PlayerId.Create("p-a").Value, 10, 20, 30);
        var b = Gx.Sheet(PlayerId.Create("p-b").Value, 40, 50, 60);

        var forward = Ok(PlayerInputSet.Create([a, b]));
        var backward = Ok(PlayerInputSet.Create([b, a]));

        Assert.Equal(forward.Id, backward.Id);                                   // the identity does not depend on the order the sheets came in
        Assert.Matches("^[0-9a-f]{64}$", forward.Id);
        Assert.Equal((10, 20, 30), (forward.Find(a.Id)!.Fielding, forward.Find(a.Id)!.Speed, forward.Find(a.Id)!.Arm));
        Assert.Equal(2, forward.Count);
        Assert.Null(forward.Find(PlayerId.Create("p-zz").Value));
        Assert.NotEqual(forward.Id, Ok(PlayerInputSet.Create([Gx.Sheet(a.Id, 11, 20, 30), b])).Id);          // any single rating
        Assert.NotEqual(forward.Id, Ok(PlayerInputSet.Create([Gx.Sheet(a.Id, 10, 21, 30), b])).Id);
        Assert.NotEqual(forward.Id, Ok(PlayerInputSet.Create([Gx.Sheet(a.Id, 10, 20, 31), b])).Id);
        Assert.NotEqual(forward.Id, Ok(PlayerInputSet.Create([a])).Id);                                      // membership
        Assert.NotEqual(forward.Id, Ok(PlayerInputSet.Create([a, Gx.Sheet(PlayerId.Create("p-c").Value, 40, 50, 60)])).Id);   // the id of a member
    }

    [Fact]
    public void A_pure_pitcher_has_no_fielding_speed_or_arm_and_the_set_says_so_instead_of_inventing_fifty()
    {
        var pitcher = Gx.Sheet(PlayerId.Create("p-pitch").Value, pitcherOnly: true);

        var entry = Ok(PlayerInputSet.Create([pitcher])).Find(pitcher.Id)!;

        Assert.Equal((null, null, null), (entry.Fielding, entry.Speed, entry.Arm));
    }

    [Fact]
    public void The_input_set_refuses_a_missing_list_a_missing_sheet_and_a_repeated_player()
    {
        var a = Gx.Sheet(PlayerId.Create("p-a").Value);

        Assert.Equal("PlayerInputSet.sheets_missing", Code(PlayerInputSet.Create(null)));
        Assert.Equal("PlayerInputSet.sheet_missing", Code(PlayerInputSet.Create([a, null])));
        Assert.Equal("PlayerInputSet.duplicate_player", Code(PlayerInputSet.Create([a, Gx.Sheet(a.Id, 99)])));
        Assert.True(PlayerInputSet.Create([]).IsSuccess);                       // an empty set is a valid (useless) set: using it fails later with a precise reason
    }

    [Fact]
    public void The_input_set_is_a_copy_a_later_change_of_a_sheet_collection_cannot_reach_it()
    {
        var players = new List<PlayerSheet?> { Gx.Sheet(PlayerId.Create("p-a").Value, 70) };
        var set = Ok(PlayerInputSet.Create(players));
        var id = set.Id;

        players.Clear();
        players.Add(Gx.Sheet(PlayerId.Create("p-a").Value, 5));

        Assert.Equal(id, set.Id);
        Assert.Equal(70, set.Find(PlayerId.Create("p-a").Value)!.Fielding);
        Assert.IsType<ImmutableArray<PlayerRunFieldInput>>(set.Entries);
    }

    // ---------------------------------------------------------------- freezing the fielders (R5: errors, never fifty)

    [Fact]
    public void Preparation_freezes_the_three_current_defenders_and_their_base_fielding_ratings()
    {
        var d = Legacy();
        var set = Gx.SetWithFielders(d.State, (FieldPosition.Shortstop, 91), (FieldPosition.CenterField, 12), (FieldPosition.SecondBase, 66));
        var g = Aggregate(d, 3);

        var prepared = Gx.Prepare(g, Gx.Tuning1, inputs: set);

        var frozen = prepared.Fielding!;
        Assert.Equal(set.Id, frozen.InputSetId);
        Assert.Equal(set.Id, prepared.PlayerInputsId);
        Assert.Equal(
            [(FieldPosition.Shortstop, Gx.Defender(d.State, FieldPosition.Shortstop), 91), (FieldPosition.CenterField, Gx.Defender(d.State, FieldPosition.CenterField), 12),
                (FieldPosition.SecondBase, Gx.Defender(d.State, FieldPosition.SecondBase), 66)],
            frozen.Entries.Select(e => (e.Position, e.Player, e.Fielding)));
        Assert.Equal((91, 12, 66), (frozen.FieldingFor("ground_hard"), frozen.FieldingFor("fly_hard"), frozen.FieldingFor("popup")));
        Assert.Null(frozen.FieldingFor("single"));
        Assert.Null(Gx.Prepare(g, Gx.Tuning1).Fielding);                         // no set, no fielding input, as before
    }

    [Fact]
    public void A_defender_who_is_missing_not_in_the_set_or_without_hitter_ratings_is_a_precise_error()
    {
        var d = Legacy();
        var g = Aggregate(d, 3);
        var ss = Gx.Defender(d.State, FieldPosition.Shortstop);

        // not in the set
        var withoutSs = Ok(PlayerInputSet.Create(BasePlayers.Where(p => p != ss).Select(p => Gx.Sheet(p))));
        Assert.Equal("PitchPrepare.defender_input_missing", Code(Gx.TryPrepare(g.State, g.Rng, Gx.Tuning1, withoutSs)));
        // a pure pitcher at shortstop (no hitter ratings)
        var pitcherAtSs = Gx.Set(pitcherOnly: p => p == ss);
        Assert.Equal("PitchPrepare.defender_ratings_missing", Code(Gx.TryPrepare(g.State, g.Rng, Gx.Tuning1, pitcherAtSs)));
        // nobody at shortstop in the state
        var emptied = g.State with { Home = g.State.Home with { Lineup = g.State.Home.Lineup.RemoveAll(s => s.Position == FieldPosition.Shortstop) } };
        Assert.Equal("PitchPrepare.defender_missing", Code(Gx.TryPrepare(emptied, g.Rng, Gx.Tuning1, Gx.Set())));
        // an empty set
        Assert.Equal("PitchPrepare.defender_input_missing", Code(Gx.TryPrepare(g.State, g.Rng, Gx.Tuning1, Ok(PlayerInputSet.Create([])))));
    }

    [Fact]
    public void The_resolver_itself_refuses_fielding_inputs_that_are_not_the_current_defenders_or_not_plain_ratings()
    {
        var d = Legacy();
        var g = Aggregate(d, 3);
        var frozen = Gx.Prepare(g, Gx.Tuning1, inputs: Gx.Set()).Fielding!;
        PitchBranchRequest Request(PitchFieldingInputs f) => new(g.State, Gx.Tuning1, PitchIntent.Balanced, false, BattingSide.Right, ThrowingHand.Right, g.Rng, null, null, f);
        var other = PlayerId.Create("p-other").Value;

        Assert.Equal("PitchBranch.fielder_mismatch", Code(PitchBranchResolver.Resolve(Request(new PitchFieldingInputs(frozen.InputSetId, frozen.Entries.SetItem(0, frozen.Entries[0] with { Player = other }))))));
        Assert.Equal("PitchBranch.ability_out_of_range", Code(PitchBranchResolver.Resolve(Request(new PitchFieldingInputs(frozen.InputSetId, frozen.Entries.SetItem(1, frozen.Entries[1] with { Fielding = 101 }))))));
        Assert.Equal("PitchBranch.ability_out_of_range", Code(PitchBranchResolver.Resolve(Request(new PitchFieldingInputs(frozen.InputSetId, frozen.Entries.SetItem(1, frozen.Entries[1] with { Fielding = -1 }))))));
        Assert.Equal("PitchBranch.fielding_inputs_invalid", Code(PitchBranchResolver.Resolve(Request(new PitchFieldingInputs(frozen.InputSetId, frozen.Entries.RemoveAt(2))))));
        Assert.Equal("PitchBranch.fielding_inputs_invalid", Code(PitchBranchResolver.Resolve(Request(new PitchFieldingInputs(frozen.InputSetId, [frozen.Entries[1], frozen.Entries[0], frozen.Entries[2]])))));
        Assert.True(PitchBranchResolver.Resolve(Request(frozen)).IsSuccess);
    }

    // ---------------------------------------------------------------- the effect on defense_result (oracle) and the RNG contract

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(25)]
    public void The_representative_fielders_rating_gives_exactly_the_oracle_distribution_for_every_in_play_pitch(int rating)
    {
        var d = Legacy();
        var set = Gx.Set(_ => (rating, 50, 50));
        var checkedCases = new HashSet<string>();
        for (ulong seed = 1; seed <= 900; seed++)
        {
            var prepared = Gx.Prepare(Aggregate(d, seed), Gx.Tuning1, inputs: set);
            if (prepared.Draft.Steps.SingleOrDefault(s => s.Stage == StageIds.DefenseResult) is not { } step)
                continue;
            Assert.Equal(Gx.OracleDefense(step.Condition, null, rating), step.Trace.Final);
            Assert.Equal(Gx.Pick(step.Trace.Outcomes, Gx.OracleDefense(step.Condition, null, rating), step.Roll), step.Outcome);
            var fielding = Assert.Single(step.Trace.Modifiers);
            Assert.Equal(("defense.fielding", rating.ToString(), 1_000_000 + (rating - 50) * 800, true), (fielding.Slot, fielding.Input, fielding.FactorPpm, fielding.Applied));
            checkedCases.Add(step.Condition);
        }

        Assert.True(checkedCases.Count >= 6, string.Join(",", checkedCases));       // several different batted balls were really exercised
    }

    [Fact]
    public void Each_batted_ball_reads_only_its_own_representative_fielder()
    {
        var d = Legacy();
        var set = Gx.SetWithFielders(d.State, (FieldPosition.Shortstop, 100), (FieldPosition.CenterField, 0), (FieldPosition.SecondBase, 80));
        var seen = new Dictionary<string, int>();
        for (ulong seed = 1; seed <= 1500; seed++)
        {
            var prepared = Gx.Prepare(Aggregate(d, seed), Gx.Tuning1, inputs: set);
            if (prepared.Draft.Steps.SingleOrDefault(s => s.Stage == StageIds.DefenseResult) is not { } step)
                continue;
            var expected = step.Condition.StartsWith("fly_", StringComparison.Ordinal) ? 0 : step.Condition == "popup" ? 80 : 100;
            Assert.Equal(expected.ToString(), Assert.Single(step.Trace.Modifiers).Input);
            seen[step.Condition] = expected;
        }

        Assert.Contains(seen.Values, v => v == 0);
        Assert.Contains(seen.Values, v => v == 100);
        Assert.Contains(seen.Keys, k => k.StartsWith("ground_", StringComparison.Ordinal) || k.StartsWith("line_", StringComparison.Ordinal));
    }

    [Fact]
    public void Fielding_changes_the_out_weight_directly_and_the_others_only_through_normalisation()
    {
        foreach (var batted in Batted)
        {
            var best = StageProbability.Compute(Gx.Tuning2, StageIds.DefenseResult, batted, [ModifierInput.Linear("defense.fielding", 100)]).Value;
            var worst = StageProbability.Compute(Gx.Tuning2, StageIds.DefenseResult, batted, [ModifierInput.Linear("defense.fielding", 0)]).Value;
            var neutral = StageProbability.Compute(Gx.Tuning2, StageIds.DefenseResult, batted).Value;

            Assert.Equal([1_040_000, 1_000_000, 1_000_000, 1_000_000, 1_000_000, 1_000_000], best.Trace.CombinedFactorPpm);
            Assert.Equal([960_000, 1_000_000, 1_000_000, 1_000_000, 1_000_000, 1_000_000], worst.Trace.CombinedFactorPpm);
            Assert.True(best.Distribution.Of("out").Value > neutral.Distribution.Of("out").Value);
            Assert.True(worst.Distribution.Of("out").Value < neutral.Distribution.Of("out").Value);
            for (var i = 1; i < 6; i++)
            {
                Assert.True(best.Distribution.Ppm[i] <= neutral.Distribution.Ppm[i]);       // indirect: normalisation only
                Assert.Equal(neutral.Distribution.Ppm[i] > 0, best.Distribution.Ppm[i] > 0);
            }
        }
    }

    [Fact]
    public void Fielding_adds_no_draw_and_no_roll_on_an_identical_path_and_neutral_fielding_changes_nothing_at_all()
    {
        var d = Legacy();
        var good = Gx.Set(_ => (100, 50, 50));
        var bad = Gx.Set(_ => (0, 50, 50));
        var neutral = Gx.Set();
        for (ulong seed = 1; seed <= 400; seed++)
        {
            var g = Aggregate(d, seed);
            var none = Gx.Prepare(g, Gx.Tuning1).Draft;
            var n = Gx.Prepare(g, Gx.Tuning1, inputs: neutral).Draft;
            var hi = Gx.Prepare(g, Gx.Tuning1, inputs: good).Draft;
            var lo = Gx.Prepare(g, Gx.Tuning1, inputs: bad).Draft;

            Assert.Equal(Summary(none), Summary(n));                              // a rating of 50 is the unchanged path, pitch for pitch
            foreach (var other in new[] { hi, lo })
            {
                Assert.Equal(none.NextBoundedCalls, other.NextBoundedCalls);       // defense_result is the last stage, so the path (and the call count) cannot differ
                Assert.Equal(none.NextRng.StateHash, other.NextRng.StateHash);
                Assert.Equal(none.Steps.Select(s => (s.Stage, s.Condition, s.Stream.Canonical, s.Roll, s.RawDraws)), other.Steps.Select(s => (s.Stage, s.Condition, s.Stream.Canonical, s.Roll, s.RawDraws)));
                Assert.Equal(none.Steps.Where(s => s.Stage != StageIds.DefenseResult).Select(s => s.Outcome), other.Steps.Where(s => s.Stage != StageIds.DefenseResult).Select(s => s.Outcome));
            }
        }
    }

    [Fact]
    public void Fielding_does_not_reach_any_other_stage_or_slot()
    {
        var d = Legacy();
        var set = Gx.Set(_ => (100, 100, 100));
        for (ulong seed = 1; seed <= 400; seed++)
        {
            var draft = Gx.Prepare(Aggregate(d, seed), Gx.Tuning1, inputs: set).Draft;
            var slots = draft.Steps.SelectMany(s => s.Trace.Modifiers.Select(m => (s.Stage, m.Slot))).ToArray();
            Assert.DoesNotContain(slots, x => x.Slot is "runner.speed" or "defense.arm" || x.Slot.StartsWith("park.", StringComparison.Ordinal) || x.Slot.StartsWith("catcher.", StringComparison.Ordinal) || x.Slot.StartsWith("battery.", StringComparison.Ordinal));
            Assert.All(slots.Where(x => x.Slot == "defense.fielding"), x => Assert.Equal(StageIds.DefenseResult, x.Stage));
        }
    }

    [Fact]
    public void Fielding_and_the_park_act_together_on_defense_result_without_a_second_application()
    {
        var execution = Gx.Exec(Gx.Park(1100, 1020, 1050), inputsId: null);
        var d = Gx.InSequence(execution);
        var set = Gx.SetWithFielders(d.State, (FieldPosition.Shortstop, 100), (FieldPosition.CenterField, 0), (FieldPosition.SecondBase, 75));
        var pinned = Gx.InSequence(Gx.Exec(Gx.Park(1100, 1020, 1050), inputsId: set.Id));
        var coverage = 0;
        for (ulong seed = 1; seed <= 600; seed++)
        {
            var prepared = Gx.Prepare(Aggregate(pinned, seed), Gx.Tuning2, inputs: set);
            if (prepared.Draft.Steps.SingleOrDefault(s => s.Stage == StageIds.DefenseResult) is not { } step)
                continue;
            var rating = prepared.Fielding!.FieldingFor(step.Condition)!.Value;
            Assert.Equal(Gx.OracleDefense(step.Condition, [1100, 1020, 1050], rating), step.Trace.Final);
            Assert.Equal(["defense.fielding", "park.home_runs", "park.hits", "park.extra_base_hits"], step.Trace.Modifiers.Select(m => m.Slot));    // each slot exactly once
            Assert.All(step.Trace.CombinedCapped, c => Assert.False(c));
            coverage++;
        }

        Assert.True(coverage >= 30);
    }

    // ---------------------------------------------------------------- the game pin (S3): the pinned set is required and exact

    [Fact]
    public void A_game_that_pins_an_input_set_requires_exactly_that_set()
    {
        var d0 = Legacy();
        var set = Gx.SetWithFielders(d0.State, (FieldPosition.Shortstop, 70));
        var other = Gx.SetWithFielders(d0.State, (FieldPosition.Shortstop, 71));
        var pinned = Gx.InSequence(Gx.Exec(inputsId: set.Id));
        var g = Aggregate(pinned, 4);

        Assert.True(Gx.TryPrepare(g.State, g.Rng, Gx.Tuning2, set).IsSuccess);
        Assert.Equal("PitchPrepare.player_inputs_missing", Code(Gx.TryPrepare(g.State, g.Rng, Gx.Tuning2)));
        Assert.Equal("PitchBranch.player_inputs_mismatch", Code(Gx.TryPrepare(g.State, g.Rng, Gx.Tuning2, other)));
    }

    [Fact]
    public void A_game_that_pins_no_input_set_refuses_one_so_the_fielding_input_cannot_appear_mid_game()
    {
        var pinned = Gx.InSequence(Gx.Exec(inputsId: null));
        var g = Aggregate(pinned, 4);

        Assert.Equal("PitchBranch.player_inputs_not_pinned", Code(Gx.TryPrepare(g.State, g.Rng, Gx.Tuning2, Gx.Set())));
        Assert.True(Gx.TryPrepare(g.State, g.Rng, Gx.Tuning2).IsSuccess);        // and with none the fielding slot is neutral
    }

    // ---------------------------------------------------------------- stale preparations (S1)

    private static (AuthoritativeGame Game, PreparedPitch Prepared, PlayerInputSet Set) PreparedUnderSet(int ss = 80)
    {
        var d = Legacy();
        var set = Gx.SetWithFielders(d.State, (FieldPosition.Shortstop, ss), (FieldPosition.CenterField, 40), (FieldPosition.SecondBase, 60));
        for (ulong seed = 1; seed <= 600; seed++)
        {
            var g = Aggregate(d, seed);
            var prepared = Gx.Prepare(g, Gx.Tuning1, inputs: set);
            if (prepared.Draft.Kind == PitchBranchKind.InPlay && Gx.BuildInPlay(g, prepared, Gx.Tuning1, set) is { IsSuccess: true })
                return (g, prepared, set);
        }

        throw new InvalidOperationException("no in-play seed");
    }

    [Fact]
    public void The_matching_set_is_accepted_at_every_step_planning_and_commit()
    {
        var (g, prepared, set) = PreparedUnderSet();

        var built = Ok(Gx.BuildInPlay(g, prepared, Gx.Tuning1, set));
        var candidate = Assert.IsType<InPlayPitchCandidate>(built);
        var staged = Ok(Gx.Stage(g, candidate, Gx.Tuning1, set));

        Assert.Equal(g.LastEventSequence + 1, staged.Record.EventSequence);
    }

    [Fact]
    public void A_changed_rating_of_the_same_player_makes_the_preparation_stale_at_planning_and_at_commit()
    {
        var (g, prepared, set) = PreparedUnderSet(ss: 80);
        var ss = Gx.Defender(g.State, FieldPosition.Shortstop);
        var changed = Gx.SetWithFielders(g.State, (FieldPosition.Shortstop, 79), (FieldPosition.CenterField, 40), (FieldPosition.SecondBase, 60));

        Assert.Equal(ss, Gx.Defender(g.State, FieldPosition.Shortstop));                       // the same PlayerId stands at shortstop in both sets
        Assert.NotEqual(set.Id, changed.Id);
        Assert.Equal("PitchCandidate.player_inputs_changed", Code(Gx.BuildInPlay(g, prepared, Gx.Tuning1, changed)));
        var candidate = Assert.IsType<InPlayPitchCandidate>(Ok(Gx.BuildInPlay(g, prepared, Gx.Tuning1, set)));
        Assert.Equal("PitchCandidate.player_inputs_changed", Code(Gx.Stage(g, candidate, Gx.Tuning1, changed)));
    }

    [Fact]
    public void Leaving_the_set_out_at_use_time_or_swapping_it_for_none_is_stale_too()
    {
        var (g, prepared, set) = PreparedUnderSet();
        var candidate = Assert.IsType<InPlayPitchCandidate>(Ok(Gx.BuildInPlay(g, prepared, Gx.Tuning1, set)));

        Assert.Equal("PitchCandidate.player_inputs_changed", Code(Gx.BuildInPlay(g, prepared, Gx.Tuning1)));
        Assert.Equal("PitchCandidate.player_inputs_changed", Code(Gx.Stage(g, candidate, Gx.Tuning1)));
        // and the reverse: prepared with no set, committed with one
        var plain = Gx.Prepare(g, Gx.Tuning1);
        Assert.Equal("PitchCandidate.player_inputs_changed", Code(Gx.BuildInPlay(g, plain, Gx.Tuning1, set)));
    }

    [Fact]
    public void A_frozen_entry_that_disagrees_with_the_current_state_or_ratings_is_found_even_when_the_set_identity_matches()
    {
        var (g, prepared, set) = PreparedUnderSet();
        var frozen = prepared.Fielding!;
        var other = PlayerId.Create("p-other").Value;

        // forged preparations (internal constructor) that claim the right set identity but a different rating / defender
        var wrongRating = new PreparedPitch(prepared.State, prepared.GameId, prepared.StartRngStateHash, prepared.BatterId, prepared.PitcherId, prepared.BatterDeclaredSide,
            prepared.PitcherThrows, prepared.Draft, prepared.RulesVersion, prepared.RulesHash, prepared.ProbabilityModelVersion, prepared.TuningVersion, prepared.TuningHash,
            new PitchFieldingInputs(frozen.InputSetId, frozen.Entries.SetItem(0, frozen.Entries[0] with { Fielding = frozen.Entries[0].Fielding + 1 })));
        var wrongPlayer = new PreparedPitch(prepared.State, prepared.GameId, prepared.StartRngStateHash, prepared.BatterId, prepared.PitcherId, prepared.BatterDeclaredSide,
            prepared.PitcherThrows, prepared.Draft, prepared.RulesVersion, prepared.RulesHash, prepared.ProbabilityModelVersion, prepared.TuningVersion, prepared.TuningHash,
            new PitchFieldingInputs(frozen.InputSetId, frozen.Entries.SetItem(1, frozen.Entries[1] with { Player = other })));

        Assert.Equal("PitchCandidate.fielding_changed", Code(Gx.BuildInPlay(g, wrongRating, Gx.Tuning1, set)));
        Assert.Equal("PitchCandidate.fielder_changed", Code(Gx.BuildInPlay(g, wrongPlayer, Gx.Tuning1, set)));
        Assert.True(Gx.BuildInPlay(g, prepared, Gx.Tuning1, set).IsSuccess);
    }

    [Theory]
    [InlineData(FieldPosition.Shortstop, "ground_hard")]
    [InlineData(FieldPosition.CenterField, "fly_medium")]
    [InlineData(FieldPosition.SecondBase, "popup")]
    public void A_defensive_substitution_makes_the_old_preparation_unusable_and_the_new_one_reads_the_new_fielder(FieldPosition position, string battedBall)
    {
        var d = Situation(0, 0, false);                                              // between plate appearances: a substitution is legal, an explicit Take is the pitch
        var before = Gx.Defender(d.State, position);
        var slot = d.State.Home.Lineup.Single(s => s.Position == position).Order;
        var bench = d.State.Home.Bench[0];
        var set = Gx.Set(id => id == before ? (90, 50, 50) : id == bench ? (10, 50, 50) : null);
        var g = Aggregate(d, 5);
        var old = Gx.Prepare(g, Gx.Tuning1, take: true, inputs: set);
        Assert.Equal(90, old.Fielding!.FieldingFor(battedBall));

        // the home team (fielding in the top half) brings its bench player to that position; that is a new GameState
        var id = "game:3c3b:probe:e" + (d.LastIssued + 1).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        var swapped = Ok(GameReducer.Apply(d.State, new SubstitutionEvent(id, SubstitutionKind.DefensiveSub, TeamSide.Home, bench, Slot: slot))).State;
        var after = Aggregate(swapped, d.LastIssued + 1, 5);

        Assert.Equal(bench, Gx.Defender(after.State, position));
        var (batter, pitcher) = Sheets(after.State);
        Assert.Equal("PitchCandidate.fielder_changed",
            Code(PitchPreparation.BuildNonInPlayCandidate(old, after.State, after.Rng, Gx.Tuning1, batter, pitcher, after.LastEventSequence, default, set)));
        var fresh = Gx.Prepare(after, Gx.Tuning1, take: true, inputs: set);
        Assert.Equal(10, fresh.Fielding!.FieldingFor(battedBall));                           // the new fielder's own rating
        Assert.Equal(bench, fresh.Fielding.Entries.Single(e => e.Position == position).Player);
    }

    [Fact]
    public void Two_players_with_the_same_name_are_told_apart_by_their_id_and_their_own_rating()
    {
        var d = Legacy();
        var ss = Gx.Defender(d.State, FieldPosition.Shortstop);
        var namesake = d.State.Home.Bench[0];
        var set = Ok(PlayerInputSet.Create(BasePlayers.Select(p => p == ss ? Gx.Sheet(p, 85, name: "동명이인") : p == namesake ? Gx.Sheet(p, 5, name: "동명이인") : Gx.Sheet(p))));

        var prepared = Gx.Prepare(Aggregate(d, 5), Gx.Tuning1, inputs: set);

        Assert.Equal(85, prepared.Fielding!.FieldingFor("line_hard"));
    }

    [Fact]
    public void A_stale_commit_leaves_the_cursor_and_the_aggregate_exactly_as_they_were()
    {
        var (g, prepared, set) = PreparedUnderSet();
        var candidate = Assert.IsType<InPlayPitchCandidate>(Ok(Gx.BuildInPlay(g, prepared, Gx.Tuning1, set)));
        var cursor = new GameCursor(g);
        var before = cursor.Current;

        var staged = Gx.Stage(g, candidate, Gx.Tuning1, Gx.Set());

        Assert.Equal("PitchCandidate.player_inputs_changed", Code(staged));
        Assert.Same(before, cursor.Current);
        Assert.Equal(before.LastEventSequence, g.LastEventSequence);
        Assert.Same(before.State, g.State);
        Assert.True(Gx.Stage(g, candidate, Gx.Tuning1, set).IsSuccess);                      // nothing was consumed by the failure
    }
}
