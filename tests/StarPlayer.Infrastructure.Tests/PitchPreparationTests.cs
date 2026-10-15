using System.Reflection;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-C2B-1: coupling a 3-C2A draft to the real game state and turning the five non-in-play branches into a legal, reducer-checked, UNCONFIRMED event candidate.
/// Expected branch/location/RNG values come from the independent Python oracle (phase3c2a_pitch_branch_golden.json); event ids and error codes are written as literals.
/// </summary>
public class PitchPreparationTests
{
    private const string Gid = "game:3c2b1:probe";

    // ---------------------------------------------------------------- helpers

    private static PlayerSheet Sheet(PlayerId id, BattingSide bats = BattingSide.Right, ThrowingHand throws = ThrowingHand.Right)
    {
        SkillValue S(int v) => SkillValue.Create(v).Value;
        var hitter = new HitterRatings(S(50), S(50), S(50), S(50), S(50), S(50));
        var pitcher = new PitcherRatings(S(50), S(50), S(50), S(50), PitchRepertoire.Create([new PitchRating(PitchType.Fastball, S(50))]).Value);
        var player = Player.Create(id, "시험선수", new DateOnly(2000, 1, 1), throws, bats, BodyMeasurements.Create(180, 80).Value, [], "").Value;
        var status = PlayerStatus.Create([new OriginRecord(OriginType.HighSchool, null, null, null)], EntryPath.Draft, RegistrationStatus.Registered,
            LeagueAssignment.FirstTeam, [new ClubAffiliation(ClubId.Create("club-a").Value, new DateOnly(2020, 1, 1), null)], CareerStatus.Active).Value;
        return PlayerSheet.Create(player, Ratings.Create(hitter, pitcher, null).Value, new PlayerState(S(50), S(50), S(50), S(50), S(50), S(50)),
            ProficiencyProfile.Create(FieldPosition.Pitcher, Array.Empty<(FieldPosition, SkillValue)>(), []).Value, status).Value;
    }

    private static (PlayerSheet Batter, PlayerSheet Pitcher) Sheets(GameState s, BattingSide bats = BattingSide.Right, ThrowingHand throws = ThrowingHand.Right) =>
        (Sheet(s.Batting.CurrentBatterSlot.Player, bats, throws), Sheet(s.Fielding.Pitcher, bats, throws));

    private static GameRandomSnapshot Rng(ulong seed = 7, string? context = null) => Snapshot(seed, context ?? Gid);

    private static PreparedPitch Prepared(GameState state, GameRandomSnapshot rng, bool take = false, PitchIntent intent = PitchIntent.Balanced,
        BattingSide bats = BattingSide.Right, ThrowingHand throws = ThrowingHand.Right)
    {
        var (b, p) = Sheets(state, bats, throws);
        var made = PitchPreparation.Prepare(state, rng, Tuning, intent, take, b, p);
        Assert.True(made.IsSuccess, made.Error?.Code + " " + made.Error?.Message);
        return made.Value;
    }

    private static Result<NonInPlayPitchCandidate> Build(PreparedPitch prepared, GameState state, GameRandomSnapshot rng, int last = 0, TuningProfile? tuning = null,
        PlayerSheet? batter = null, PlayerSheet? pitcher = null, BattingSide bats = BattingSide.Right, ThrowingHand throws = ThrowingHand.Right)
    {
        var (b, p) = Sheets(state, bats, throws);
        return PitchPreparation.BuildNonInPlayCandidate(prepared, state, rng, tuning ?? Tuning, batter ?? b, pitcher ?? p, last);
    }

    private static NonInPlayPitchCandidate Candidate(GameState state, GameRandomSnapshot rng, bool take = false, int last = 0)
    {
        var built = Build(Prepared(state, rng, take), state, rng, last);
        Assert.True(built.IsSuccess, built.Error?.Code + " " + built.Error?.Message);
        return built.Value;
    }

    private static string Code<T>(Result<T> r) => r.IsSuccess ? "(success)" : r.Error!.Value.Code;

    /// <summary>Scans fixed seeds (deterministic) for a real prepared pitch whose draft satisfies <paramref name="want"/>.</summary>
    private static (PreparedPitch Prepared, GameRandomSnapshot Rng) Find(GameState state, bool take, Func<PitchBranchDraft, bool> want)
    {
        for (ulong seed = 1; seed < 4000; seed++)
        {
            var rng = Rng(seed);
            var prepared = Prepared(state, rng, take);
            if (want(prepared.Draft))
                return (prepared, rng);
        }

        throw new InvalidOperationException("no seed produced the wanted branch");
    }

    private static GameState WithBattingTeam(GameState s, Func<TeamGameState, TeamGameState> edit) =>
        s.BattingSide == TeamSide.Away ? s with { Away = edit(s.Away) } : s with { Home = edit(s.Home) };

    private static GameState WithFieldingTeam(GameState s, Func<TeamGameState, TeamGameState> edit) =>
        s.FieldingSide == TeamSide.Away ? s with { Away = edit(s.Away) } : s with { Home = edit(s.Home) };

    private static string Snap(GameState s) => $"{s.GameId}|{s.Count}|{s.Outs}|{s.Stage}|{s.Approach}|{s.AppliedEvents.Count}|{s.Plays.Count}|{s.Stats.Count}|{s.AwayRuns}-{s.HomeRuns}";

    // ---------------------------------------------------------------- Prepare

    [Fact]
    public void Prepare_calls_the_real_resolver_with_the_sheets_handedness_and_keeps_the_start_position()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var rng = Rng(11);
        var expected = Resolved(Request(state, rng, PitchIntent.Attack, false, BattingSide.Left, ThrowingHand.Left));

        var prepared = Prepared(state, rng, intent: PitchIntent.Attack, bats: BattingSide.Left, throws: ThrowingHand.Left);

        Assert.Equal(Summary(expected), Summary(prepared.Draft));
        Assert.Same(state, prepared.State);
        Assert.Equal(Gid, prepared.GameId);
        Assert.Equal(rng.StateHash, prepared.StartRngStateHash);
        Assert.Equal(state.Batting.CurrentBatterSlot.Player, prepared.BatterId);
        Assert.Equal(state.Fielding.Pitcher, prepared.PitcherId);
        Assert.Equal(BattingSide.Left, prepared.BatterDeclaredSide);
        Assert.Equal(ThrowingHand.Left, prepared.PitcherThrows);
        Assert.Equal((NonAutomated.Version, NonAutomated.ContentHash), (prepared.RulesVersion, prepared.RulesHash));
        Assert.Equal((Tuning.Model.Version.Value, Tuning.Tuning.Value, Tuning.ContentHash), (prepared.ProbabilityModelVersion, prepared.TuningVersion, prepared.TuningHash));
    }

    [Theory]
    [InlineData(BattingSide.Right, ThrowingHand.Right, AppliedBatterSide.Right)]
    [InlineData(BattingSide.Right, ThrowingHand.Left, AppliedBatterSide.Right)]
    [InlineData(BattingSide.Left, ThrowingHand.Right, AppliedBatterSide.Left)]
    [InlineData(BattingSide.Left, ThrowingHand.Left, AppliedBatterSide.Left)]
    [InlineData(BattingSide.Switch, ThrowingHand.Right, AppliedBatterSide.Left)]
    [InlineData(BattingSide.Switch, ThrowingHand.Left, AppliedBatterSide.Right)]
    public void The_applied_batter_side_comes_from_the_sheets_never_from_the_caller(BattingSide bats, ThrowingHand throws, AppliedBatterSide applied)
    {
        var state = InSequenceState(NonAutomated, Gid);

        var prepared = Prepared(state, Rng(), bats: bats, throws: throws);

        Assert.Equal(applied, prepared.Draft.AppliedSide);
        Assert.Equal(applied, prepared.Draft.Location.Side);
    }

    [Fact]
    public void A_sheet_for_another_batter_or_pitcher_is_refused()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (batter, pitcher) = Sheets(state);
        var other = state.Batting.SlotByOrder((state.Batting.CurrentBatterSlot.Order % 9) + 1).Player;

        Assert.Equal("PitchPrepare.batter_mismatch", Code(PitchPreparation.Prepare(state, Rng(), Tuning, PitchIntent.Balanced, false, Sheet(other), pitcher)));
        Assert.Equal("PitchPrepare.pitcher_mismatch", Code(PitchPreparation.Prepare(state, Rng(), Tuning, PitchIntent.Balanced, false, batter, Sheet(other))));
        Assert.Equal("PitchPrepare.pitcher_mismatch", Code(PitchPreparation.Prepare(state, Rng(), Tuning, PitchIntent.Balanced, false, batter, batter))); // the batter's sheet is not the pitcher
    }

    [Fact]
    public void Missing_inputs_are_refused_with_explicit_codes()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (batter, pitcher) = Sheets(state);
        var rng = Rng();

        Assert.Equal("PitchPrepare.state_missing", Code(PitchPreparation.Prepare(null, rng, Tuning, PitchIntent.Balanced, false, batter, pitcher)));
        Assert.Equal("PitchPrepare.rng_missing", Code(PitchPreparation.Prepare(state, null, Tuning, PitchIntent.Balanced, false, batter, pitcher)));
        Assert.Equal("PitchPrepare.tuning_missing", Code(PitchPreparation.Prepare(state, rng, null, PitchIntent.Balanced, false, batter, pitcher)));
        Assert.Equal("PitchPrepare.batter_sheet_missing", Code(PitchPreparation.Prepare(state, rng, Tuning, PitchIntent.Balanced, false, null, pitcher)));
        Assert.Equal("PitchPrepare.pitcher_sheet_missing", Code(PitchPreparation.Prepare(state, rng, Tuning, PitchIntent.Balanced, false, batter, null)));
    }

    [Fact]
    public void A_game_and_rng_context_mismatch_and_a_broken_snapshot_keep_the_resolvers_own_errors()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (batter, pitcher) = Sheets(state);
        var broken = Rng() with { FormatVersion = "rngset0" };

        Assert.Equal("PitchBranch.game_context_mismatch",
            Code(PitchPreparation.Prepare(state, Rng(7, "game:3c2b1:other"), Tuning, PitchIntent.Balanced, false, batter, pitcher)));
        Assert.Equal(GameRandom.Restore(broken).Error!.Value.Code, Code(PitchPreparation.Prepare(state, broken, Tuning, PitchIntent.Balanced, false, batter, pitcher)));
    }

    [Fact]
    public void Take_and_approach_legality_and_a_finished_game_keep_the_resolvers_own_errors()
    {
        var start = StartState(NonAutomated, Gid);
        var (batter, pitcher) = Sheets(start);

        Assert.Equal("PitchBranch.pitch_not_legal", Code(PitchPreparation.Prepare(start, Rng(), Tuning, PitchIntent.Balanced, false, batter, pitcher)));
        Assert.Equal("PitchBranch.game_not_in_progress", Code(PitchPreparation.Prepare(start with { Status = GameStatus.Final }, Rng(), Tuning, PitchIntent.Balanced, true, batter, pitcher)));
    }

    [Fact]
    public void Prepare_is_deterministic_and_changes_neither_the_state_nor_the_snapshot()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var rng = Rng(13);
        var before = (Snap(state), rng.Canonical());

        var a = Prepared(state, rng);
        var b = Prepared(state, rng);

        Assert.Equal(Summary(a.Draft), Summary(b.Draft));
        Assert.Equal(before, (Snap(state), rng.Canonical()));
    }

    [Fact]
    public void A_cancelled_prepare_throws_and_changes_nothing()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var rng = Rng();
        var (batter, pitcher) = Sheets(state);
        var before = (Snap(state), rng.Canonical());
        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.Throws<OperationCanceledException>(() => PitchPreparation.Prepare(state, rng, Tuning, PitchIntent.Balanced, false, batter, pitcher, source.Token));

        Assert.Equal(before, (Snap(state), rng.Canonical()));
    }

    // ---------------------------------------------------------------- stale PreparedPitch

    [Fact]
    public void A_matching_state_snapshot_and_sheets_build_a_candidate()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (ok, rng) = Find(state, false, d => d.Kind != PitchBranchKind.InPlay);

        var built = Build(ok, state, rng);

        Assert.True(built.IsSuccess, built.Error?.Code);
    }

    [Fact]
    public void A_state_rebuilt_by_replay_is_a_different_instance_and_the_prepared_pitch_is_refused()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (ok, rng) = Find(state, false, d => d.Kind != PitchBranchKind.InPlay);
        var rebuilt = InSequenceState(NonAutomated, Gid); // same game, same events, new object (what a save/load round trip produces)

        Assert.Equal("PitchCandidate.state_stale", Code(Build(ok, rebuilt, rng)));
    }

    [Fact]
    public void A_changed_count_batter_pitcher_or_approach_makes_the_prepared_pitch_unusable()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (ok, rng) = Find(state, false, d => d.Kind != PitchBranchKind.InPlay);
        var otherPitcher = state.Fielding.SlotByOrder(1).Player;

        Assert.Equal("PitchCandidate.state_stale", Code(Build(ok, state with { Count = new PitchCountState(1, 0) }, rng)));
        Assert.Equal("PitchCandidate.batter_changed",
            Code(Build(ok, WithBattingTeam(state, t => t with { NextBatterIndex = t.NextBatterIndex + 1 }), rng)));
        Assert.Equal("PitchCandidate.pitcher_changed", Code(Build(ok, WithFieldingTeam(state, t => t with { Pitcher = otherPitcher }), rng)));
        Assert.Equal("PitchCandidate.state_stale", Code(Build(ok, state with { Approach = ApproachKind.Aggressive }, rng)));
    }

    [Fact]
    public void An_applied_event_makes_the_previous_prepared_pitch_unusable()
    {
        var start = StartState(NonAutomated, Gid);
        var (ok, rng) = Find(start, true, d => d.Kind != PitchBranchKind.InPlay);
        var afterApproach = GameReducer.Apply(start, new ApproachEvent("pb-late", ApproachKind.Patient)).Value.State;

        Assert.Equal("PitchCandidate.state_stale", Code(Build(ok, afterApproach, rng)));
    }

    [Fact]
    public void Another_rng_position_or_context_makes_the_prepared_pitch_unusable()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (ok, rng) = Find(state, false, d => d.Kind != PitchBranchKind.InPlay);
        var advanced = Snapshot(7, Gid, new Dictionary<string, int> { ["game/pitch@1"] = 1 });

        Assert.Equal("PitchCandidate.rng_state_changed", Code(Build(ok, state, advanced)));
        Assert.Equal("PitchCandidate.rng_context_mismatch", Code(Build(ok, state, Rng(7, "game:3c2b1:other"))));
        Assert.Equal(GameRandom.Restore(rng with { FormatVersion = "rngset0" }).Error!.Value.Code, Code(Build(ok, state, rng with { FormatVersion = "rngset0" })));
    }

    [Fact]
    public void Another_game_id_rules_hash_or_tuning_hash_makes_the_prepared_pitch_unusable()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (ok, rng) = Find(state, false, d => d.Kind != PitchBranchKind.InPlay);
        var otherRules = InSequenceState(Automated, Gid);
        var draft = TuningProfileTests.Draft();
        var otherTuning = TuningProfile.Create(draft with { Provenance = draft.Provenance + "." }).Value;

        Assert.Equal("PitchCandidate.game_mismatch", Code(Build(ok, state with { GameId = "game:3c2b1:other" }, rng)));
        Assert.Equal("PitchCandidate.rules_changed", Code(Build(ok, otherRules, rng)));
        Assert.Equal("PitchCandidate.tuning_changed", Code(Build(ok, state, rng, tuning: otherTuning)));
    }

    [Fact]
    public void Sheets_with_another_player_or_another_handedness_are_refused()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (ok, rng) = Find(state, false, d => d.Kind != PitchBranchKind.InPlay);
        var (batter, pitcher) = Sheets(state);
        var other = state.Batting.SlotByOrder(state.Batting.CurrentBatterSlot.Order % 9 + 1).Player;

        Assert.Equal("PitchPrepare.batter_mismatch", Code(Build(ok, state, rng, batter: Sheet(other))));
        Assert.Equal("PitchPrepare.pitcher_mismatch", Code(Build(ok, state, rng, pitcher: Sheet(other))));
        Assert.Equal("PitchCandidate.batter_side_changed", Code(PitchPreparation.BuildNonInPlayCandidate(ok, state, rng, Tuning,
            Sheet(batter.Id, BattingSide.Left), pitcher, 0)));
        Assert.Equal("PitchCandidate.pitcher_hand_changed", Code(PitchPreparation.BuildNonInPlayCandidate(ok, state, rng, Tuning,
            batter, Sheet(pitcher.Id, BattingSide.Right, ThrowingHand.Left), 0)));
    }

    [Fact]
    public void An_unsupported_draft_contract_and_missing_inputs_are_refused()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (ok, rng) = Find(state, false, d => d.Kind != PitchBranchKind.InPlay);
        var forged = Forge(ok, draft: ok.Draft with { ContractVersion = "pitch-branch-v0" });
        var (batter, pitcher) = Sheets(state);

        Assert.Equal("PitchCandidate.draft_contract_unsupported", Code(Build(forged, state, rng)));
        Assert.Equal("PitchCandidate.prepared_missing", Code(PitchPreparation.BuildNonInPlayCandidate(null, state, rng, Tuning, batter, pitcher, 0)));
        Assert.Equal("PitchCandidate.state_missing", Code(PitchPreparation.BuildNonInPlayCandidate(ok, null, rng, Tuning, batter, pitcher, 0)));
        Assert.Equal("PitchCandidate.rng_missing", Code(PitchPreparation.BuildNonInPlayCandidate(ok, state, null, Tuning, batter, pitcher, 0)));
        Assert.Equal("PitchCandidate.tuning_missing", Code(PitchPreparation.BuildNonInPlayCandidate(ok, state, rng, null, batter, pitcher, 0)));
        Assert.Equal("PitchCandidate.batter_sheet_missing", Code(PitchPreparation.BuildNonInPlayCandidate(ok, state, rng, Tuning, null, pitcher, 0)));
        Assert.Equal("PitchCandidate.pitcher_sheet_missing", Code(PitchPreparation.BuildNonInPlayCandidate(ok, state, rng, Tuning, batter, null, 0)));
    }

    /// <summary>Creates a PreparedPitch with one value replaced (the only way a test can forge one; production code cannot).</summary>
    private static PreparedPitch Forge(PreparedPitch source, PitchBranchDraft? draft = null)
    {
        var ctor = typeof(PreparedPitch).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Single();
        var args = ctor.GetParameters().Select(p => p.Name == "draft"
            ? draft ?? source.Draft
            : typeof(PreparedPitch).GetProperty(p.Name![..1].ToUpperInvariant() + p.Name[1..], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(source)).ToArray();
        return (PreparedPitch)ctor.Invoke(args);
    }

    // ---------------------------------------------------------------- event ids

    [Fact]
    public void The_candidate_id_is_the_exact_next_sequence_and_repeats_for_the_same_input()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (ok, rng) = Find(state, false, d => d.Kind != PitchBranchKind.InPlay);

        var first = Build(ok, state, rng, last: 41).Value;
        var again = Build(ok, state, rng, last: 41).Value;

        Assert.Equal("game:3c2b1:probe:e000042", first.EventId);
        Assert.Equal(first.EventId, again.EventId);
        Assert.Equal(first.Event, again.Event);
        Assert.Equal(first.NextRng.Canonical(), again.NextRng.Canonical());
        Assert.Equal("game:3c2b1:probe:e000001", Build(ok, state, rng, last: 0).Value.EventId);
    }

    [Fact]
    public void A_colliding_or_stale_last_sequence_is_refused_and_a_duplicate_is_never_a_new_success()
    {
        var plain = InSequenceState(NonAutomated, Gid);
        var taken = plain with { AppliedEvents = plain.AppliedEvents.Add(new EventReceipt("game:3c2b1:probe:e000001", "Pitch", "ef1", new string('0', 64))) };
        var (ok, rng) = Find(taken, false, d => d.Kind != PitchBranchKind.InPlay);

        Assert.Equal("PitchCandidate.event_id_in_use", Code(Build(ok, taken, rng, last: 0))); // the cursor is behind the receipts
        Assert.True(Build(ok, taken, rng, last: 1).IsSuccess);

        // the same pitch applied for real, then the same id offered again: the reducer would call it a duplicate; the candidate is still a failure
        var candidate = Build(ok, taken, rng, last: 1).Value;
        var applied = GameReducer.Apply(taken, candidate.Event).Value.State;
        var (again, againRng) = Find(applied, false, d => d.Kind != PitchBranchKind.InPlay);
        Assert.Equal("PitchCandidate.event_id_in_use", Code(Build(again, applied, againRng, last: 1)));
    }

    [Fact]
    public void The_sequence_limit_and_an_invalid_cursor_are_refused_without_wrapping()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (ok, rng) = Find(state, false, d => d.Kind != PitchBranchKind.InPlay);

        Assert.Equal("Id.sequence_exhausted", Code(Build(ok, state, rng, last: GameEventIds.MaxSequence)));
        Assert.Equal("game:3c2b1:probe:e999999", Build(ok, state, rng, last: GameEventIds.MaxSequence - 1).Value.EventId);
        Assert.Equal("Id.sequence_invalid", Code(Build(ok, state, rng, last: -1)));
        Assert.Equal("Id.sequence_invalid", Code(Build(ok, state, rng, last: GameEventIds.MaxSequence + 1)));
    }

    [Fact]
    public void Building_a_candidate_never_moves_the_authoritative_id_cursor()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (ok, rng) = Find(state, false, d => d.Kind != PitchBranchKind.InPlay);
        var ids = GameEventIds.Create(Gid, 5).Value;

        var built = Build(ok, state, rng, last: ids.LastIssued);

        Assert.Equal(5, ids.LastIssued);
        Assert.Equal(6, ids.ParseSequence(built.Value.EventId).Value);
    }

    // ---------------------------------------------------------------- the independent golden scenarios

    private static string Text(System.Text.Json.JsonElement e, string name) => e.GetProperty(name).GetString()!;

    [Theory]
    [MemberData(nameof(PitchBranchResolverTests.ScenarioIds), MemberType = typeof(PitchBranchResolverTests))]
    public void Every_golden_pitch_becomes_the_expected_legal_event_or_the_explicit_in_play_limit_and_uses_no_extra_randomness(string id)
    {
        var scenario = Golden.GetProperty("scenarios").EnumerateArray().Single(s => s.GetProperty("id").GetString() == id);
        var context = Text(scenario, "context");
        var rng = Snapshot(Hex64(scenario.GetProperty("seed")), context,
            scenario.GetProperty("prior_calls").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32()));
        foreach (var pitch in scenario.GetProperty("pitches").EnumerateArray())
        {
            var spec = pitch.GetProperty("spec");
            var expected = pitch.GetProperty("expected");
            var take = spec.GetProperty("explicit_take").GetBoolean();
            var state = StateFor(RulesFor(Text(spec, "called_pitch_mode")), take, context);
            var bats = Bats(Text(spec, "batter_side"));
            var throws = Throws(Text(spec, "pitcher_hand"));
            var prepared = Prepared(state, rng, take, Intent(Text(spec, "intent")), bats, throws);
            var built = Build(prepared, state, rng, 0, bats: bats, throws: throws);
            var kind = Text(expected, "kind");

            if (kind == "in_play")
            {
                Assert.Equal("PitchCandidate.in_play_not_supported_yet", Code(built));
                rng = prepared.Draft.NextRng; // the scenario continues from the oracle's position even though no candidate exists
                continue;
            }

            Assert.True(built.IsSuccess, built.Error?.Code);
            var candidate = built.Value;
            var outcome = kind switch
            {
                "hit_by_pitch" => PitchOutcome.HitByPitch, "called_ball" => PitchOutcome.Ball, "called_strike" => PitchOutcome.CalledStrike,
                "swinging_strike" => PitchOutcome.SwingingStrike, _ => PitchOutcome.Foul,
            };
            var pitchSpec = candidate.Event switch { PitchEvent p => p.Spec, TakeEvent t => t.Spec, _ => throw new Xunit.Sdk.XunitException("not a pitch event") };
            Assert.Equal(take, candidate.Event is TakeEvent);
            Assert.Equal(outcome, pitchSpec.Outcome);
            Assert.Equal(PitchMishap.None, pitchSpec.Mishap);
            Assert.Equal(PitchMishap.None, pitchSpec.DroppedThirdStrike);
            Assert.Empty(pitchSpec.MoveList);
            Assert.Null(pitchSpec.StrikeoutCredits);
            Assert.Equal(Text(expected, "location_zone"), pitchSpec.Location!.Zone.Value);
            Assert.Equal(Text(expected, "applied_side"), pitchSpec.Location.Side.ToString().ToLowerInvariant());
            Assert.Equal(Text(expected, "boundary"), pitchSpec.Location.Boundary.ToString().ToLowerInvariant());
            Assert.Equal(expected.GetProperty("total_calls").GetInt32(), candidate.NextBoundedCalls);

            Assert.Equal("pitch-candidate-v1", candidate.ContractVersion);
            Assert.False(candidate.UnconfirmedStep.WasDuplicate);
            var receipt = candidate.UnconfirmedStep.State.AppliedEvents.Last();
            Assert.Equal((candidate.EventId, "ef2"), (receipt.EventId, receipt.FingerprintVersion));
            Assert.Equal(context + ":e000001", candidate.EventId);
            rng = candidate.NextRng;
        }

        // no extra random number was ever drawn: the chain ends exactly where the independent oracle ends
        Assert.Equal(Text(scenario, "final_canonical"), rng.Canonical());
        Assert.Equal(Text(scenario, "final_state_hash"), rng.StateHash);
    }

    [Fact]
    public void The_golden_corpus_exercises_all_five_branches_both_ways_and_the_in_play_limit()
    {
        var pairs = Golden.GetProperty("scenarios").EnumerateArray().SelectMany(s => s.GetProperty("pitches").EnumerateArray())
            .Select(p => (p.GetProperty("spec").GetProperty("explicit_take").GetBoolean(), Text(p.GetProperty("expected"), "kind"))).ToHashSet();

        foreach (var kind in new[] { "hit_by_pitch", "called_ball", "called_strike" })
        {
            Assert.Contains((true, kind), pairs);
            Assert.Contains((false, kind), pairs);
        }

        Assert.Contains((false, "swinging_strike"), pairs);
        Assert.Contains((false, "foul"), pairs);
        Assert.Contains((false, "in_play"), pairs);
    }

    // ---------------------------------------------------------------- reducer-checked consequences

    [Fact]
    public void A_ball_at_three_balls_is_a_walk_and_a_third_strike_is_a_strikeout_through_the_reducer()
    {
        var walkBase = InSequenceState(NonAutomated, Gid) with { Count = new PitchCountState(3, 0) };
        var (walkPrepared, walkRng) = Find(walkBase, false, d => d.Kind == PitchBranchKind.CalledBall);
        var walk = Build(walkPrepared, walkBase, walkRng).Value.UnconfirmedStep.State;
        Assert.NotNull(walk.Bases.First);
        Assert.Equal(PitchCountState.Zero, walk.Count);

        var strikeBase = InSequenceState(NonAutomated, Gid) with { Count = new PitchCountState(0, 2) };
        foreach (var kind in new[] { PitchBranchKind.CalledStrike, PitchBranchKind.SwingingStrike })
        {
            var (p, r) = Find(strikeBase, false, d => d.Kind == kind);
            var struckOut = Build(p, strikeBase, r).Value.UnconfirmedStep.State;
            Assert.Equal(1, struckOut.Outs);
            Assert.Equal(PitchCountState.Zero, struckOut.Count);
        }
    }

    [Fact]
    public void A_hit_by_pitch_puts_the_batter_on_first_and_a_foul_with_two_strikes_keeps_the_count()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (hp, hr) = Find(state, false, d => d.Kind == PitchBranchKind.HitByPitch);
        Assert.NotNull(Build(hp, state, hr).Value.UnconfirmedStep.State.Bases.First);

        var twoStrikes = state with { Count = new PitchCountState(1, 2) };
        var (fp, fr) = Find(twoStrikes, false, d => d.Kind == PitchBranchKind.Foul);
        var fouled = Build(fp, twoStrikes, fr).Value.UnconfirmedStep.State;
        Assert.Equal(new PitchCountState(1, 2), fouled.Count);
        Assert.Equal(0, fouled.Outs);
    }

    [Fact]
    public void Exactly_one_actual_pitch_is_counted_for_the_pitcher()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (ok, rng) = Find(state, false, d => d.Kind != PitchBranchKind.InPlay);
        var pitcher = state.Fielding.Pitcher;
        int Count(GameState s) => s.Team(state.FieldingSide).PitcherUsages.Last(u => u.Pitcher == pitcher).PitchCount;

        Assert.Equal(Count(state) + 1, Count(Build(ok, state, rng).Value.UnconfirmedStep.State));
    }

    [Fact]
    public void An_explicit_take_with_a_swing_or_foul_draft_is_refused_and_in_play_is_an_explicit_limit_without_side_effects()
    {
        var start = StartState(NonAutomated, Gid);
        var (take, rng) = Find(start, true, d => d.Kind == PitchBranchKind.CalledBall);
        var impossible = Forge(take, draft: take.Draft with { Kind = PitchBranchKind.SwingingStrike });
        var foul = Forge(take, draft: take.Draft with { Kind = PitchBranchKind.Foul });

        Assert.Equal("PitchCandidate.take_cannot_swing", Code(Build(impossible, start, rng)));
        Assert.Equal("PitchCandidate.take_cannot_swing", Code(Build(foul, start, rng)));

        var state = InSequenceState(NonAutomated, Gid);
        var (inPlay, inRng) = Find(state, false, d => d.Kind == PitchBranchKind.InPlay);
        var before = (Snap(state), inRng.Canonical());
        var failed = Build(inPlay, state, inRng, last: 3);
        Assert.Equal("PitchCandidate.in_play_not_supported_yet", Code(failed));
        Assert.Equal(before, (Snap(state), inRng.Canonical()));
        Assert.Equal(inRng.StateHash, inPlay.StartRngStateHash); // the prepared pitch kept the start position; nothing was adopted
    }

    [Fact]
    public void A_candidate_the_reducer_rejects_is_a_failure_with_the_reducers_error()
    {
        var state = WithFieldingTeam(InSequenceState(NonAutomated, Gid), t => t with { DhActive = false }); // defence incomplete: the reducer refuses every pitch
        var (ok, rng) = Find(state, false, d => d.Kind != PitchBranchKind.InPlay);

        Assert.Equal("Game.defense_incomplete", Code(Build(ok, state, rng)));
    }

    // ---------------------------------------------------------------- no side effects, cancellation, boundaries

    [Fact]
    public void Build_changes_neither_the_state_nor_the_snapshot_and_the_next_rng_is_the_drafts()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (ok, rng) = Find(state, false, d => d.Kind != PitchBranchKind.InPlay);
        var before = (Snap(state), rng.Canonical());

        var candidate = Build(ok, state, rng).Value;

        Assert.Equal(before, (Snap(state), rng.Canonical()));
        Assert.Equal(ok.Draft.NextRng.Canonical(), candidate.NextRng.Canonical());
        Assert.Equal(state.AppliedEvents.Count + 1, candidate.UnconfirmedStep.State.AppliedEvents.Count);
    }

    [Fact]
    public void A_cancelled_build_throws_and_changes_nothing()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (ok, rng) = Find(state, false, d => d.Kind != PitchBranchKind.InPlay);
        var (batter, pitcher) = Sheets(state);
        var before = (Snap(state), rng.Canonical());
        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.Throws<OperationCanceledException>(() => PitchPreparation.BuildNonInPlayCandidate(ok, state, rng, Tuning, batter, pitcher, 0, source.Token));

        Assert.Equal(before, (Snap(state), rng.Canonical()));
    }

    [Fact]
    public void The_new_types_have_no_public_constructor_and_no_public_setter()
    {
        foreach (var type in new[] { typeof(PreparedPitch), typeof(NonInPlayPitchCandidate) })
        {
            Assert.Empty(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
            Assert.All(type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), p => Assert.True(p.SetMethod is null || p.SetMethod.IsPrivate));
        }
    }

    [Fact]
    public void The_resolver_and_golden_identities_are_unchanged()
    {
        Assert.Equal(TuningProfileTests.GoldenHash, Tuning.ContentHash);
        Assert.Equal("pitch-branch-v1", PitchBranchResolver.ContractVersion);
        Assert.Equal("ef2", EventFingerprint.VersionV2);
    }

    [Fact]
    public void Prepare_hands_the_batters_hidden_tendency_to_the_resolver_and_defaults_to_neutral()
    {
        var state = InSequenceState(Automated, Gid);
        var (b, p) = Sheets(state);
        var mixed = BatterZoneTendency.Create([-60, 10, 20, 30, 40, -50, -40, 50, 0], ZoneReferenceDistribution.UniformV1).Value;
        var changed = 0;
        for (ulong seed = 1; seed <= 300; seed++)
        {
            var rng = Rng(seed);
            var plain = PitchPreparation.Prepare(state, rng, Tuning, PitchIntent.Balanced, false, b, p).Value.Draft;
            var neutral = PitchPreparation.Prepare(state, rng, Tuning, PitchIntent.Balanced, false, b, p, batterTendency: BatterZoneTendency.Neutral).Value.Draft;
            var shifted = PitchPreparation.Prepare(state, rng, Tuning, PitchIntent.Balanced, false, b, p, batterTendency: mixed).Value.Draft;
            var direct = PitchBranchResolver.Resolve(new PitchBranchRequest(state, Tuning, PitchIntent.Balanced, false, BattingSide.Right, ThrowingHand.Right, rng, mixed)).Value;
            Assert.Equal(Summary(plain), Summary(neutral));
            Assert.Equal(Summary(direct), Summary(shifted));
            if (Summary(plain) != Summary(shifted))
                changed++;
        }

        Assert.True(changed > 0);
    }
}
