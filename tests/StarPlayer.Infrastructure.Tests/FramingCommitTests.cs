using System.Collections.Immutable;
using System.Text.Json;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;
using static StarPlayer.Infrastructure.Tests.Pc;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-E Package B, the COMMIT side (game-engine B-01, the F-01 principle): the evidence rebuilds the expected called-pitch condition, Framing input and distribution from the REAL state, the REAL
/// rules and the REAL player input set - never from the catcher, rating, condition or flag written in the prepared pitch or the recorded steps - and compares the recorded steps, roll and call with
/// that. Every forgery below is made only through the internal constructors the test assemblies see; each must be refused BEFORE the compare-and-swap and leave the aggregate, the cursor, the RNG
/// and the last commit exactly as they were.
/// </summary>
public class FramingCommitTests
{
    private const string Link = "Commit.candidate_link_mismatch";

    private sealed record Case(AuthoritativeGame G, PlayerInputSet Set, PreparedPitch P, NonInPlayPitchCandidate C);

    private static JsonElement Oracle(string mode, string zone, int rating) => FramingResolverTests.OracleCaseOf(mode, zone, rating);

    private static int[] OraclePpm(string mode, string zone, int rating) => Oracle(mode, zone, rating).GetProperty("ppm").EnumerateArray().Select(x => x.GetInt32()).ToArray();

    private static Case Make(int catcherFraming, Func<PitchBranchDraft, bool> want, bool take = false, ulong first = 1)
    {
        var (d, set) = Fr.NonAutomatedGame(catcherFraming, approach: !take);
        var (g, prepared) = Fr.Find(d, set, want, take, first: first);
        var built = Gx.BuildNonInPlay(g, prepared, Fr.T3, set);
        Assert.True(built.IsSuccess, built.Error?.Code);
        return new Case(g, set, prepared, built.Value);
    }

    private static readonly Lazy<Case> BorderBall = new(() => Make(0, d => Fr.Called(d) is { Outcome: "ball" } && Fr.IsBorderline(d)));

    private static readonly Lazy<Case> BorderStrike = new(() => Make(100, d => Fr.Called(d) is { Outcome: "strike" } && Fr.IsBorderline(d)));

    private static readonly Lazy<Case> ClearCall = new(() => Make(100, d => Fr.Called(d) is not null && !Fr.IsBorderline(d)));

    private static readonly Lazy<Case> Swing = new(() => Make(60, d => d.Kind == PitchBranchKind.SwingingStrike));

    private static readonly Lazy<Case> TakeBallCase = new(() => Make(30, d => Fr.Called(d) is { Outcome: "ball" } && Fr.IsBorderline(d), take: true));

    /// <summary>A real ball call (catcher rating 0) whose roll lies where framing 100 would have called a strike: the forger's favourite.</summary>
    private static readonly Lazy<Case> FlippableBall = new(() => Make(0, d =>
        Fr.Called(d) is { Outcome: "ball" } s && Fr.IsBorderline(d) && s.Roll >= OraclePpm("non_automated", d.Location.Zone.Value, 100)[0]));

    private static Result<StagedCommit> Stage(Case c, PreparedPitch? prepared = null, NonInPlayPitchCandidate? candidate = null, PlayerInputSet? set = null, TuningProfile? tuning = null)
    {
        var (b, p) = Sheets(c.G.State);
        return PitchCommitGate.Stage(c.G, prepared ?? c.P, candidate ?? c.C, tuning ?? Fr.T3, b, p, default, set ?? c.Set);
    }

    private static PreparedPitch EditCalled(PreparedPitch p, Func<PitchBranchStep, PitchBranchStep> edit)
    {
        var steps = p.Draft.Steps.Select(s => s.Stage == StageIds.CalledPitch ? edit(s) : s).ToImmutableArray();
        return p.With(p.Draft with { Steps = steps });
    }

    private static NonInPlayPitchCandidate Rebuilt(Case c, PreparedPitch forged)
    {
        var built = Gx.BuildNonInPlay(c.G, forged, Fr.T3, c.Set);
        Assert.True(built.IsSuccess, built.Error?.Code + " " + built.Error?.Message);
        return built.Value;
    }

    /// <summary>The Stage is refused with <paramref name="code"/> and the base aggregate / a cursor on it are exactly what they were.</summary>
    private static string Refused(Case c, string code, PreparedPitch? prepared = null, NonInPlayPitchCandidate? candidate = null, PlayerInputSet? set = null)
    {
        var cursor = new GameCursor(c.G);
        var (state, rng, last, revision, lastCommit) = (c.G.State, c.G.Rng, c.G.LastEventSequence, c.G.Revision, c.G.LastCommit);

        var result = Stage(c, prepared, candidate, set);

        Assert.Equal(code, Code(result));
        foreach (var unchanged in new[] { c.G, cursor.Current })
        {
            Assert.Same(state, unchanged.State);
            Assert.Same(rng, unchanged.Rng);
            Assert.Equal((last, revision), (unchanged.LastEventSequence, unchanged.Revision));
            Assert.Same(lastCommit, unchanged.LastCommit);
            Assert.NotEqual(GameStatus.Final, unchanged.State.Status);
        }

        Assert.Same(c.G, cursor.Current);
        return Message(result);
    }

    private static ProbabilityTrace CoherentTrace(string key, int? rating) =>
        StageProbability.Compute(Fr.T3, StageIds.CalledPitch, key, rating is { } r ? [ModifierInput.Linear("catcher.framing", r)] : null).Value.Trace;

    // ---------------------------------------------------------------- genuine pitches pass

    [Fact]
    public void Genuine_borderline_clear_take_and_swing_pitches_are_staged_and_published_once()
    {
        foreach (var lazy in new[] { BorderBall, BorderStrike, ClearCall, Swing, TakeBallCase })
        {
            var c = lazy.Value;
            var cursor = new GameCursor(c.G);
            var staged = Stage(c);
            Assert.True(staged.IsSuccess, Code(staged));

            var published = cursor.Publish(staged.Value);
            Assert.True(published.IsSuccess, Code(published));
            Assert.Equal(c.G.Revision + 1, cursor.Current.Revision);
            Assert.Equal(c.C.NextRng.StateHash, cursor.Current.Rng.StateHash);                 // the candidate's own H1, nothing newly drawn
            Assert.NotSame(c.G, cursor.Current);

            // the same staged commit cannot be published a second time (the compare-and-swap is single use) and the failure changes nothing
            var after = cursor.Current;
            Assert.False(cursor.Publish(staged.Value).IsSuccess);
            Assert.Same(after, cursor.Current);
        }
    }

    [Fact]
    public void The_cases_really_cover_what_they_claim()
    {
        Assert.Equal(("ball", true), (Fr.Called(BorderBall.Value.P.Draft)!.Outcome, Fr.IsBorderline(BorderBall.Value.P.Draft)));
        Assert.Equal(("strike", true), (Fr.Called(BorderStrike.Value.P.Draft)!.Outcome, Fr.IsBorderline(BorderStrike.Value.P.Draft)));
        Assert.False(Fr.IsBorderline(ClearCall.Value.P.Draft));
        Assert.Null(Fr.Called(Swing.Value.P.Draft));
        Assert.True(TakeBallCase.Value.P.Draft.ExplicitTake);
        var flip = FlippableBall.Value;
        var zone = flip.P.Draft.Location.Zone.Value;
        Assert.Equal("ball", Gx.Pick(["ball", "strike"], OraclePpm("non_automated", zone, 0), Fr.Called(flip.P.Draft)!.Roll));
        Assert.Equal("strike", Gx.Pick(["ball", "strike"], OraclePpm("non_automated", zone, 100), Fr.Called(flip.P.Draft)!.Roll));
    }

    // ---------------------------------------------------------------- the catcher is the real one, whatever the record says

    [Fact]
    public void A_different_catcher_written_consistently_into_the_preparation_is_found_from_the_real_state()
    {
        var c = BorderBall.Value;
        var real = c.P.Catcher!;
        var other = BasePlayers.First(p => p != real.Catcher);

        // the forger names another player AND keeps the rating (so the number looks right) - or names him with the rating that would flip the call
        foreach (var rating in new[] { real.ReceivingFraming, 100 })
        {
            var forged = c.P.WithCatcher(new FrozenCatcherInput(real.GameId, real.InputSetId, other, rating));
            Assert.Equal("PitchCandidate.catcher_changed", Code(Stage(c, forged, c.C)));
            Refused(c, "PitchCandidate.catcher_changed", forged, c.C);
        }
    }

    [Fact]
    public void The_real_catcher_with_a_different_rating_written_into_the_preparation_is_found_from_the_real_set()
    {
        var c = BorderBall.Value;
        var real = c.P.Catcher!;
        var forged = c.P.WithCatcher(new FrozenCatcherInput(real.GameId, real.InputSetId, real.Catcher, 100));
        Refused(c, "PitchCandidate.framing_changed", forged, c.C);
        Refused(c, "PitchCandidate.catcher_input_mismatch", c.P.WithCatcher(null), c.C);
    }

    [Fact]
    public void A_catcher_input_of_another_game_or_set_and_a_different_player_input_set_are_refused_at_the_commit_too()
    {
        var c = BorderBall.Value;
        var real = c.P.Catcher!;
        Refused(c, "PitchCandidate.catcher_game_mismatch", c.P.WithCatcher(new FrozenCatcherInput("game:other", real.InputSetId, real.Catcher, real.ReceivingFraming)), c.C);
        Refused(c, "PitchCandidate.player_inputs_changed", c.P.WithCatcher(new FrozenCatcherInput(real.GameId, new string('c', 64), real.Catcher, real.ReceivingFraming)), c.C);
        Refused(c, "PitchCandidate.player_inputs_changed", set: Fr.SetFor(c.G.State, 100));                       // another set, even one in which the catcher is better
        Refused(c, "PitchCandidate.player_inputs_changed", set: Fr.SetV1(_ => 0));                                  // a content-version-1 set
    }

    [Fact]
    public void A_wrong_tuning_is_refused_at_the_commit_by_the_unchanged_check()
    {
        var c = BorderBall.Value;
        var (b, p) = Sheets(c.G.State);
        Assert.Equal("PitchCandidate.tuning_changed", Code(PitchCommitGate.Stage(c.G, c.P, c.C, Gx.Tuning2, b, p, default, c.Set)));
    }

    // ---------------------------------------------------------------- the record of the call is recomputed

    [Fact]
    public void A_recorded_framing_rating_changed_alone_or_together_with_its_factor_is_refused()
    {
        var c = BorderBall.Value;
        ProbabilityTrace Modify(ProbabilityTrace t, Func<TraceModifier, TraceModifier> f) => t with { Modifiers = t.Modifiers.Select(f).ToImmutableArray() };

        // only the input text
        Refused(c, Link, EditCalled(c.P, s => s with { Trace = Modify(s.Trace, m => m with { Input = "99" }) }), c.C);
        // the input text and the factor it would give
        Refused(c, Link, EditCalled(c.P, s => s with { Trace = Modify(s.Trace, m => m with { Input = "99", FactorPpm = 1_049_000 }) }), c.C);
        // only the factor
        Refused(c, Link, EditCalled(c.P, s => s with { Trace = Modify(s.Trace, m => m with { FactorPpm = 1_000_000 }) }), c.C);
    }

    [Fact]
    public void A_recorded_applied_flag_that_is_flipped_is_refused()
    {
        var c = BorderBall.Value;
        Assert.True(Fr.Called(c.P.Draft)!.Trace.Modifiers.Single().Applied);
        Refused(c, Link, EditCalled(c.P, s => s with { Trace = s.Trace with { Modifiers = s.Trace.Modifiers.Select(m => m with { Applied = false }).ToImmutableArray() } }), c.C);

        var clear = ClearCall.Value;
        Assert.False(Fr.Called(clear.P.Draft)!.Trace.Modifiers.Single().Applied);
        Refused(clear, Link, EditCalled(clear.P, s => s with { Trace = s.Trace with { Modifiers = s.Trace.Modifiers.Select(m => m with { Applied = true }).ToImmutableArray() } }), clear.C);
        // the modifier entry removed altogether
        Refused(c, Link, EditCalled(c.P, s => s with { Trace = s.Trace with { Modifiers = [] } }), c.C);
    }

    [Fact]
    public void A_recorded_condition_key_or_rules_mode_that_does_not_match_the_real_rules_and_location_is_refused()
    {
        var c = BorderBall.Value;
        var zone = c.P.Draft.Location.Zone.Value;
        var other = PitchZoneModel.V1.Zones.First(z => z.Id.Value != zone && z.Boundary == BoundaryClass.Borderline).Id.Value;

        // the mode of the audit is not the mode of the rules in force
        var automatedKey = $"automated:{zone}";
        Refused(c, Link, EditCalled(c.P, s => s with { Condition = automatedKey, Trace = CoherentTrace(automatedKey, null) }), c.C);
        // another zone than the pitch location, with a perfectly coherent row for that zone
        var otherKey = $"non_automated:{other}";
        Refused(c, Link, EditCalled(c.P, s => s with { Condition = otherKey, Trace = CoherentTrace(otherKey, 0) }), c.C);
        // only the step's text or only the trace's text
        Refused(c, Link, EditCalled(c.P, s => s with { Condition = automatedKey }), c.C);
        Refused(c, Link, EditCalled(c.P, s => s with { Trace = s.Trace with { Condition = otherKey } }), c.C);
        // a mode token that is no mode at all
        Refused(c, Link, EditCalled(c.P, s => s with { Condition = $"NON_AUTOMATED:{zone}" }), c.C);
    }

    [Fact]
    public void A_recorded_distribution_that_is_not_the_recomputed_one_is_refused_however_small_the_change()
    {
        var c = BorderBall.Value;
        Refused(c, Link, EditCalled(c.P, s => s with { Trace = s.Trace with { Final = [s.Trace.Final[0] + 1, s.Trace.Final[1] - 1] } }), c.C);
        Refused(c, Link, EditCalled(c.P, s => s with { Trace = s.Trace with { Final = [s.Trace.Final[0], s.Trace.Final[1]], AfterModifiers = [s.Trace.AfterModifiers[0] - 1, s.Trace.AfterModifiers[1] + 1] } }), c.C);
        Refused(c, Link, EditCalled(c.P, s => s with { Trace = s.Trace with { Baseline = [s.Trace.Baseline[0] - 1000, s.Trace.Baseline[1] + 1000] } }), c.C);
        Refused(c, Link, EditCalled(c.P, s => s with { Trace = s.Trace with { CombinedFactorPpm = [1_000_000, 1_000_000] } }), c.C);
        Refused(c, Link, EditCalled(c.P, s => s with { Trace = s.Trace with { TuningHash = new string('0', 64) } }), c.C);
        // the whole record replaced by the row of ANOTHER rating, perfectly coherent in itself (what a forger who edits the audit would write)
        var key = Fr.Called(c.P.Draft)!.Condition;
        Refused(c, Link, EditCalled(c.P, s => s with { Trace = CoherentTrace(key, 100) }), c.C);
        // and the row of the neutral rating
        Refused(c, Link, EditCalled(c.P, s => s with { Trace = CoherentTrace(key, 50) }), c.C);
    }

    [Fact]
    public void A_favourable_call_forged_coherently_with_a_forged_rating_is_refused_because_the_real_catcher_decides()
    {
        var c = FlippableBall.Value;                                    // real catcher framing 0: the roll gives a BALL; framing 100 would give a strike
        var step = Fr.Called(c.P.Draft)!;
        var key = step.Condition;
        var fake = CoherentTrace(key, 100);
        Assert.Equal("strike", Gx.Pick(["ball", "strike"], fake.Final, step.Roll));

        // the recorded steps, the outcome, the kind and the event all say "strike, because the catcher is great" - and the frozen catcher input too, written consistently
        var forgedDraft = c.P.Draft with
        {
            Kind = PitchBranchKind.CalledStrike,
            CalledPitch = "strike",
            Steps = c.P.Draft.Steps.Select(s => s.Stage == StageIds.CalledPitch ? s with { Outcome = "strike", Trace = fake } : s).ToImmutableArray(),
        };

        // (a) the catcher input in the preparation is honest: only the audit lies. The event is rebuilt from the lying draft.
        var forged = c.P.With(forgedDraft);
        var candidate = Rebuilt(c, forged);
        Assert.Equal(PitchBranchKind.CalledStrike, candidate.Kind);
        Refused(c, Link, forged, candidate);

        // (b) the preparation lies too, with the same fake rating: the real set exposes it first
        var real = c.P.Catcher!;
        var bothLie = forged.WithCatcher(new FrozenCatcherInput(real.GameId, real.InputSetId, real.Catcher, 100));
        Refused(c, "PitchCandidate.framing_changed", bothLie, candidate);
    }

    [Fact]
    public void A_roll_that_does_not_give_the_recorded_call_is_refused_in_both_directions()
    {
        var ball = BorderBall.Value;
        var strike = BorderStrike.Value;
        // roll altered, call kept
        Refused(strike, Link, EditCalled(strike.P, s => s with { Roll = 0 }), strike.C);
        Refused(ball, Link, EditCalled(ball.P, s => s with { Roll = 999_999 }), ball.C);

        // call altered coherently everywhere (step, draft, event) but the roll still says the other thing
        foreach (var (c, fake) in new[] { (ball, "strike"), (strike, "ball") })
        {
            var forgedDraft = c.P.Draft with
            {
                Kind = fake == "ball" ? PitchBranchKind.CalledBall : PitchBranchKind.CalledStrike,
                CalledPitch = fake,
                Steps = c.P.Draft.Steps.Select(s => s.Stage == StageIds.CalledPitch ? s with { Outcome = fake } : s).ToImmutableArray(),
            };
            var forged = c.P.With(forgedDraft);
            Refused(c, Link, forged, Rebuilt(c, forged));
        }

        // outcome in the step and in the kind disagree
        Refused(ball, Link, ball.P.With(ball.P.Draft with { CalledPitch = "strike" }), ball.C);
    }

    [Fact]
    public void A_called_pitch_step_that_has_no_place_in_the_pitch_and_a_missing_one_are_both_refused()
    {
        var swing = Swing.Value;
        var call = BorderBall.Value;
        var extra = call.P.Draft.Steps.First(s => s.Stage == StageIds.CalledPitch);

        // a swing pitch with an invented called_pitch step (the counts kept consistent, so only the step itself is wrong)
        var invented = swing.P.With(swing.P.Draft with { Steps = swing.P.Draft.Steps.Add(extra), NextBoundedCalls = swing.P.Draft.NextBoundedCalls + 1 });
        Refused(swing, Link, invented, Rebuilt(swing, invented));
        Refused(swing, Link, swing.P.With(swing.P.Draft with { CalledPitch = "ball" }), swing.C);

        // a called pitch whose called_pitch step was removed
        var removed = call.P.With(call.P.Draft with { Steps = call.P.Draft.Steps.Where(s => s.Stage != StageIds.CalledPitch).ToImmutableArray(), NextBoundedCalls = call.P.Draft.NextBoundedCalls - 1 });
        Refused(call, Link, removed, Rebuilt(call, removed));
        // two called_pitch steps
        var doubled = call.P.With(call.P.Draft with { Steps = call.P.Draft.Steps.Add(extra), NextBoundedCalls = call.P.Draft.NextBoundedCalls + 1 });
        Refused(call, Link, doubled, Rebuilt(call, doubled));
    }

    [Fact]
    public void A_recorded_location_step_that_disagrees_with_the_pitch_location_is_refused()
    {
        var c = BorderBall.Value;
        var zone = c.P.Draft.Location.Zone.Value;
        var other = PitchZoneModel.V1.Zones.First(z => z.Id.Value != zone).Id.Value;
        var forged = c.P.With(c.P.Draft with { Steps = c.P.Draft.Steps.Select(s => s.Stage == StageIds.ActualLocation ? s with { Outcome = other } : s).ToImmutableArray() });
        Refused(c, Link, forged, c.C);
    }

    [Fact]
    public void A_non_called_pitch_is_unaffected_and_an_in_play_pitch_with_an_invented_called_pitch_step_is_refused()
    {
        // the genuine in-play pitch of a v3 game commits (the base in-play and its candidate), the invented step does not
        var (d, set) = Fr.NonAutomatedGame(60);
        var (g, prepared, candidate) = Gx.FindInPlay(d, Fr.T3, inputs: set);
        var staged = Gx.Stage(g, candidate, Fr.T3, set);
        Assert.True(staged.IsSuccess, Code(staged));

        var extra = Fr.Find(d, set, x => Fr.Called(x) is not null).Prepared.Draft.Steps.First(s => s.Stage == StageIds.CalledPitch);
        var forgedPrepared = prepared.With(prepared.Draft with { Steps = prepared.Draft.Steps.Add(extra), NextBoundedCalls = prepared.Draft.NextBoundedCalls + 1 });
        var rebuilt = Gx.BuildInPlay(g, forgedPrepared, Fr.T3, set);
        if (rebuilt.IsSuccess && rebuilt.Value is InPlayPitchCandidate forgedCandidate)
        {
            var result = Gx.Stage(g, forgedCandidate, Fr.T3, set);
            Assert.False(result.IsSuccess);
            Assert.Equal(Link, Code(result));
        }
        else
            Assert.False(rebuilt.IsSuccess && rebuilt.Value is InPlayPitchCandidate);   // the planner itself may refuse such a draft first; either way it is never committed
    }

    // ---------------------------------------------------------------- one-shot: the whole forged-case list leaves a fresh cursor untouched

    [Fact]
    public void After_every_refusal_the_same_genuine_pitch_is_still_confirmable_and_the_rng_position_is_the_candidates_own()
    {
        var c = BorderBall.Value;
        Refused(c, Link, EditCalled(c.P, s => s with { Roll = 999_999 }), c.C);
        Refused(c, "PitchCandidate.catcher_changed", c.P.WithCatcher(new FrozenCatcherInput(Gid, c.Set.Id, BasePlayers.First(p => p != c.P.Catcher!.Catcher), 0)), c.C);

        var staged = Stage(c);
        Assert.True(staged.IsSuccess);
        var cursor = new GameCursor(c.G);
        Assert.True(cursor.Publish(staged.Value).IsSuccess);
        Assert.Equal(c.C.NextRng.StateHash, cursor.Current.Rng.StateHash);
        Assert.Equal(c.P.Draft.NextRng.StateHash, cursor.Current.Rng.StateHash);       // H0 -> H1: exactly the draft's own draws, none added for the framing
    }
}
