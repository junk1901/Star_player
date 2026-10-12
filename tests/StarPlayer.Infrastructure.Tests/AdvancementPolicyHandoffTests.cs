using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-C2B-3-P, RNG hand-over (H0 -> H1 -> H2) on REAL prepared pitches. TEST-ONLY. The "future draw" below is a stand-in written in the test (K rolls on
/// <c>game/runner@1</c> from the pending position); it is NOT the 3-C2B-3 engine, which does not exist. What is proved is the production side of the hand-over: the
/// position a <see cref="PendingAdvancement"/> exposes, that building it consumes nothing, and what the existing <see cref="GameRandom"/> primitives guarantee when a later
/// draw works on a private copy, fails or is cancelled.
/// </summary>
public class AdvancementPolicyHandoffTests
{
    private const string Gid = "game:3c2b3p:probe";

    private static PlayerSheet Sheet(PlayerId id)
    {
        SkillValue S(int v) => SkillValue.Create(v).Value;
        var hitter = new HitterRatings(S(50), S(50), S(50), S(50), S(50), S(50));
        var pitcher = new PitcherRatings(S(50), S(50), S(50), S(50), PitchRepertoire.Create([new PitchRating(PitchType.Fastball, S(50))]).Value);
        var player = Player.Create(id, "시험선수", new DateOnly(2000, 1, 1), ThrowingHand.Right, BattingSide.Right, BodyMeasurements.Create(180, 80).Value, [], "").Value;
        var status = PlayerStatus.Create([new OriginRecord(OriginType.HighSchool, null, null, null)], EntryPath.Draft, RegistrationStatus.Registered,
            LeagueAssignment.FirstTeam, [new ClubAffiliation(ClubId.Create("club-a").Value, new DateOnly(2020, 1, 1), null)], CareerStatus.Active).Value;
        return PlayerSheet.Create(player, Ratings.Create(hitter, pitcher, null).Value, new PlayerState(S(50), S(50), S(50), S(50), S(50), S(50)),
            ProficiencyProfile.Create(FieldPosition.Pitcher, Array.Empty<(FieldPosition, SkillValue)>(), []).Value, status).Value;
    }

    private static (PlayerSheet Batter, PlayerSheet Pitcher) Sheets(GameState s) => (Sheet(s.Batting.CurrentBatterSlot.Player), Sheet(s.Fielding.Pitcher));

    private static PreparedPitch Prepared(GameState state, GameRandomSnapshot rng)
    {
        var (b, p) = Sheets(state);
        var made = PitchPreparation.Prepare(state, rng, Tuning, PitchIntent.Balanced, false, b, p);
        Assert.True(made.IsSuccess, made.Error?.Code + " " + made.Error?.Message);
        return made.Value;
    }

    private static (PreparedPitch Prepared, GameRandomSnapshot Rng) FindInPlay(GameState state, Func<PitchBranchDraft, bool> want)
    {
        for (ulong seed = 1; seed < 60000; seed++)
        {
            var rng = Snapshot(seed, Gid);
            var prepared = Prepared(state, rng);
            if (prepared.Draft.Kind == PitchBranchKind.InPlay && want(prepared.Draft))
                return (prepared, rng);
        }

        throw new InvalidOperationException("no seed produced the wanted in-play branch");
    }

    /// <summary>First AND second occupied, nobody out, an approach chosen: a real single here leaves two open extra-base decisions.</summary>
    private static GameState FirstAndSecond(GameRuleSnapshot? rules = null, string gameId = Gid)
    {
        var n = 0;
        var state = StartState(rules ?? NonAutomated, gameId);
        void Do(GameEvent e) => state = GameReducer.Apply(state, e) is { IsSuccess: true } ok ? ok.Value.State : throw new InvalidOperationException(e.GetType().Name);
        string Id() => $"x{++n}";
        void Walk() { Do(new ApproachEvent(Id(), ApproachKind.Normal)); for (var i = 0; i < 4; i++) Do(new PitchEvent(Id(), new PitchSpec(PitchOutcome.Ball))); }
        Walk();
        Do(new RunnerPlayEvent(Id(), RunnerPlayKind.Steal, ImmutableList.Create(new RunnerMove(1, 2, MoveCause.Steal))));
        Walk();
        Do(new ApproachEvent(Id(), ApproachKind.Normal));
        Assert.Equal(PaStage.InSequence, state.Stage);
        return state;
    }

    private static PendingAdvancement RealPending(out PreparedPitch prepared, out GameRandomSnapshot h0, out GameState state)
    {
        state = FirstAndSecond();
        (prepared, h0) = FindInPlay(state, d => d.DefenseResult == "single");
        var (b, p) = Sheets(state);
        var built = InPlayPitchPlanning.Build(prepared, state, h0, Tuning, b, p, 41);
        Assert.True(built.IsSuccess, built.Error?.Code);
        return Assert.IsType<PendingAdvancement>(built.Value);
    }

    /// <summary>The stand-in for the future draw: <paramref name="calls"/> rolls on the runner stream of a private copy restored from <paramref name="start"/>.</summary>
    private static (GameRandomSnapshot After, uint[] Rolls) DrawFrom(GameRandomSnapshot start, int calls)
    {
        var work = GameRandom.Restore(start).Value;
        var stream = work.Stream(GameRngStreams.Runner);
        var rolls = Enumerable.Range(0, calls).Select(_ => stream.NextBounded(ProbabilityScale.RollBound)).ToArray();
        return (work.Snapshot(), rolls);
    }

    [Fact]
    public void A_real_pending_advancement_exposes_H1_not_H0_and_building_it_consumed_no_random_number()
    {
        var pending = RealPending(out var prepared, out var h0, out _);

        Assert.True(pending.IsComposite);
        Assert.Equal(["single_runner_on_first", "single_runner_on_second"], pending.ContextKeys.ToArray());
        Assert.Equal(prepared.Draft.NextRng.StateHash, pending.RngAfterPitchDraw.StateHash);       // H1 = the position after the 3-C2A draw
        Assert.NotEqual(h0.StateHash, pending.RngAfterPitchDraw.StateHash);                         // H0 and H1 differ
        Assert.Equal(h0.StateHash, prepared.StartRngStateHash);                                     // the preparation keeps H0
        Assert.DoesNotContain(pending.RngAfterPitchDraw.Streams, s => s.Stream.Canonical == GameRngStreams.Runner.Canonical);   // game/runner@1 has not been used by the C2A draw or by C2B-2
    }

    [Fact]
    public void The_future_draw_from_H1_is_deterministic_touches_only_the_runner_stream_and_never_changes_H1()
    {
        var pending = RealPending(out _, out _, out _);
        var h1 = pending.RngAfterPitchDraw;
        var h1Hash = h1.StateHash;

        var (h2, rolls) = DrawFrom(h1, 2);
        var (h2Again, rollsAgain) = DrawFrom(h1, 2);

        Assert.Equal(h1Hash, pending.RngAfterPitchDraw.StateHash);                                  // H1 is immutable
        Assert.Equal(rolls, rollsAgain);
        Assert.Equal(h2.StateHash, h2Again.StateHash);
        Assert.NotEqual(h1Hash, h2.StateHash);
        Assert.Equal(h1.Streams.Length + 1, h2.Streams.Length);                                     // exactly one stream is added
        Assert.Contains(h2.Streams, s => s.Stream.Canonical == GameRngStreams.Runner.Canonical && s.Draws >= 2);
        foreach (var s in h1.Streams)
            Assert.Contains(h2.Streams, t => t.Stream.Canonical == s.Stream.Canonical && t.State == s.State && t.Draws == s.Draws);   // no other stream moved
    }

    [Fact]
    public void A_failed_or_cancelled_future_draw_leaves_the_position_unchanged_and_a_retry_from_H1_repeats_the_same_rolls()
    {
        var pending = RealPending(out _, out _, out _);
        var h1 = pending.RngAfterPitchDraw;
        var live = GameRandom.Restore(h1, h1.StateHash).Value;

        var failed = live.Atomically<int>(random =>
        {
            random.Stream(GameRngStreams.Runner).NextBounded(ProbabilityScale.RollBound);
            return Result<int>.Failure(new Error("Test.reducer_refused_the_composed_event", "the composed event was refused"));
        });
        Assert.False(failed.IsSuccess);
        Assert.Equal(h1.StateHash, live.Snapshot().StateHash);                                       // a refusal after drawing publishes nothing

        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => live.Atomically<int>(random =>
        {
            random.Stream(GameRngStreams.Runner).NextBounded(ProbabilityScale.RollBound);
            cancellation.Cancel();
            return Result<int>.Success(1);
        }, cancellation.Token));
        Assert.Equal(h1.StateHash, live.Snapshot().StateHash);                                       // cancellation after drawing publishes nothing

        var (_, first) = DrawFrom(h1, 2);
        var (_, retry) = DrawFrom(h1, 2);
        Assert.Equal(first, retry);                                                                  // a retry re-draws the identical rolls: no hidden re-roll
        var ok = live.Atomically(random =>
            Result<uint[]>.Success(Enumerable.Range(0, 2).Select(_ => random.Stream(GameRngStreams.Runner).NextBounded(ProbabilityScale.RollBound)).ToArray()));
        Assert.Equal(first, ok.Value);
        Assert.NotEqual(h1.StateHash, live.Snapshot().StateHash);                                    // only a successful scope publishes H2
    }

    [Fact]
    public void Planning_the_same_real_pitch_again_gives_the_same_draft_plan_and_H1_so_the_C2A_draw_is_unchanged()
    {
        var state = FirstAndSecond();
        var (prepared, h0) = FindInPlay(state, d => d.DefenseResult == "single");

        var again = Prepared(state, h0);

        Assert.Equal(prepared.Draft.BattedBall, again.Draft.BattedBall);
        Assert.Equal(prepared.Draft.DefenseResult, again.Draft.DefenseResult);
        Assert.Equal(prepared.Draft.NextRng.StateHash, again.Draft.NextRng.StateHash);
        var (b, p) = Sheets(state);
        var plan1 = InPlayPitchPlanning.Build(prepared, state, h0, Tuning, b, p, 41);
        var plan2 = InPlayPitchPlanning.Build(again, state, h0, Tuning, b, p, 41);
        Assert.Equal(((PendingAdvancement)plan1.Value).RngAfterPitchDraw.StateHash, ((PendingAdvancement)plan2.Value).RngAfterPitchDraw.StateHash);
    }

    // ---- 3-C2B-3-P review fix (FIX-1): the stale-input contract a future Resolve must reuse. TEST-ONLY and limited to what EXISTS: a real PendingAdvancement owns a real
    // PreparedPitch, and PitchPreparation.CheckPrepared (reached through the unchanged PlanBase/Build) is the very check a resolver has to run first. No resolver API is called
    // or invented here; the event-id boundary and the H1 start are future-implementation tests (handoff section 6).

    private static string Code<T>(Result<T> r) => r.IsSuccess ? "(success)" : r.Error!.Value.Code;

    [Fact]
    public void A_real_pendings_prepared_pitch_is_accepted_again_on_the_untouched_inputs_with_H0_as_the_current_rng_and_gives_the_same_pending_and_H1()
    {
        var pending = RealPending(out var prepared, out var h0, out var state);
        var (b, p) = Sheets(state);

        var again = InPlayPitchPlanning.Build(pending.Plan.Prepared, state, h0, Tuning, b, p, 41);

        var second = Assert.IsType<PendingAdvancement>(again.Value);
        Assert.Same(prepared, pending.Plan.Prepared);
        Assert.Equal(pending.ContextKeys.ToArray(), second.ContextKeys.ToArray());
        Assert.Equal(pending.RngAfterPitchDraw.StateHash, second.RngAfterPitchDraw.StateHash);     // a retry on the same inputs: the same H1 (so the same later H2)
        Assert.Equal(prepared.StartRngStateHash, h0.StateHash);                                    // the authoritative position before adoption is H0, not H1
    }

    [Fact]
    public void A_real_pendings_prepared_pitch_is_refused_by_the_shared_stale_check_for_every_changed_input_and_the_originals_stay_untouched()
    {
        var pending = RealPending(out _, out var h0, out var state);
        var prepared = pending.Plan.Prepared;
        var (b, p) = Sheets(state);
        var before = (state.GameId, state.AppliedEvents.Count, state.Count, state.Outs, h0.StateHash, pending.RngAfterPitchDraw.StateHash);
        var otherTuning = TuningProfile.Create(TuningProfileTests.Draft() with { Provenance = TuningProfileTests.Draft().Provenance + "." }).Value;
        var advancedH0 = Snapshot(1, Gid, new Dictionary<string, int> { ["game/pitch@1"] = 1 });

        Assert.Equal("PitchCandidate.state_stale", Code(InPlayPitchPlanning.PlanBase(prepared, FirstAndSecond(), h0, Tuning, b, p)));                    // a rebuilt state object
        Assert.Equal("PitchCandidate.game_mismatch", Code(InPlayPitchPlanning.PlanBase(prepared, FirstAndSecond(gameId: "game:3c2b3p:other"), Snapshot(1, "game:3c2b3p:other"), Tuning, b, p)));
        Assert.Equal("PitchCandidate.rng_context_mismatch", Code(InPlayPitchPlanning.PlanBase(prepared, state, Snapshot(1, "game:3c2b3p:other"), Tuning, b, p)));
        Assert.Equal("PitchCandidate.rng_state_changed", Code(InPlayPitchPlanning.PlanBase(prepared, state, advancedH0, Tuning, b, p)));                // H0 changed
        Assert.Equal("PitchCandidate.rng_state_changed", Code(InPlayPitchPlanning.PlanBase(prepared, state, pending.RngAfterPitchDraw, Tuning, b, p)));   // H1 is NOT the current authoritative rng
        Assert.Equal("PitchPrepare.batter_mismatch", Code(InPlayPitchPlanning.PlanBase(prepared, state, h0, Tuning, Sheet(state.Fielding.Pitcher), p)));
        Assert.Equal("PitchPrepare.pitcher_mismatch", Code(InPlayPitchPlanning.PlanBase(prepared, state, h0, Tuning, b, Sheet(state.Batting.CurrentBatterSlot.Player))));
        Assert.Equal("PitchCandidate.rules_changed", Code(InPlayPitchPlanning.PlanBase(prepared, FirstAndSecond(Automated), h0, Tuning, b, p)));
        Assert.Equal("PitchCandidate.tuning_changed", Code(InPlayPitchPlanning.PlanBase(prepared, state, h0, otherTuning, b, p)));
        Assert.Equal("PitchCandidate.prepared_missing", Code(InPlayPitchPlanning.PlanBase(null, state, h0, Tuning, b, p)));

        Assert.Equal(before, (state.GameId, state.AppliedEvents.Count, state.Count, state.Outs, h0.StateHash, pending.RngAfterPitchDraw.StateHash));    // nothing was changed
        Assert.True(InPlayPitchPlanning.PlanBase(prepared, state, h0, Tuning, b, p).IsSuccess);                                                          // and the untouched inputs still work
    }

    [Fact]
    public void Building_a_pending_does_not_look_at_the_event_id_cursor_so_a_future_resolver_must_check_the_id_boundary_itself()
    {
        var pending = RealPending(out _, out var h0, out var state);
        var (b, p) = Sheets(state);
        var taken = state with { AppliedEvents = state.AppliedEvents.Add(new EventReceipt($"{Gid}:e000001", "Pitch", "ef1", new string('0', 64))) };

        // Build returns a PendingAdvancement before any id work, for any cursor and any receipt list: an id-boundary check does not exist at this layer yet
        foreach (var cursor in new[] { -1, 0, 1, GameEventIds.MaxSequence, GameEventIds.MaxSequence + 1 })
            Assert.IsType<PendingAdvancement>(InPlayPitchPlanning.Build(pending.Plan.Prepared, state, h0, Tuning, b, p, cursor).Value);
        Assert.Equal("PitchCandidate.state_stale", Code(InPlayPitchPlanning.Build(pending.Plan.Prepared, taken, h0, Tuning, b, p, 0)));   // a changed state object is still caught first
    }
}
