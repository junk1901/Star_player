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
/// Phase 3-C2B-2, Part B: the in-play planner and candidate builder on REAL prepared pitches. Every in-play pitch here comes from the real
/// <see cref="PitchPreparation.Prepare"/> (the real resolver on real seeded streams); no draft is injected except where a test forges a PreparedPitch with one changed field,
/// the way the 3-C2B-1 tests do, to reach a refusal. Expected RNG positions come from the independent Python oracle (phase3c2a_pitch_branch_golden.json).
/// </summary>
public class InPlayPitchPlanningTests
{
    private const string Gid = "game:3c2b2:probe";

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

    private static (PlayerSheet Batter, PlayerSheet Pitcher) Sheets(GameState s) => (Sheet(s.Batting.CurrentBatterSlot.Player), Sheet(s.Fielding.Pitcher));

    private static GameRandomSnapshot Rng(ulong seed = 7, string? context = null) => Snapshot(seed, context ?? Gid);

    private static PreparedPitch Prepared(GameState state, GameRandomSnapshot rng)
    {
        var (b, p) = Sheets(state);
        var made = PitchPreparation.Prepare(state, rng, Tuning, PitchIntent.Balanced, false, b, p);
        Assert.True(made.IsSuccess, made.Error?.Code + " " + made.Error?.Message);
        return made.Value;
    }

    private static Result<InPlayPlanOutcome> Build(PreparedPitch prepared, GameState state, GameRandomSnapshot rng, int last = 0, Action<string>? checkpoint = null,
        CancellationToken ct = default, TuningProfile? tuning = null)
    {
        var (b, p) = Sheets(state);
        return InPlayPitchPlanning.Build(prepared, state, rng, tuning ?? Tuning, b, p, last, checkpoint, ct);
    }

    private static string Code<T>(Result<T> r) => r.IsSuccess ? "(success)" : r.Error!.Value.Code;

    /// <summary>Scans fixed seeds (deterministic) for a real prepared in-play pitch whose draft satisfies <paramref name="want"/>.</summary>
    private static (PreparedPitch Prepared, GameRandomSnapshot Rng) FindInPlay(GameState state, Func<PitchBranchDraft, bool>? want = null)
    {
        for (ulong seed = 1; seed < 60000; seed++)
        {
            var rng = Rng(seed);
            var prepared = Prepared(state, rng);
            if (prepared.Draft.Kind == PitchBranchKind.InPlay && (want?.Invoke(prepared.Draft) ?? true))
                return (prepared, rng);
        }

        throw new InvalidOperationException("no seed produced the wanted in-play branch");
    }

    private static (PreparedPitch Prepared, GameRandomSnapshot Rng) FindNonInPlay(GameState state)
    {
        for (ulong seed = 1; seed < 4000; seed++)
        {
            var rng = Rng(seed);
            var prepared = Prepared(state, rng);
            if (prepared.Draft.Kind != PitchBranchKind.InPlay)
                return (prepared, rng);
        }

        throw new InvalidOperationException("no seed produced a non-in-play branch");
    }

    private static InPlayPitchCandidate Candidate(Result<InPlayPlanOutcome> built)
    {
        Assert.True(built.IsSuccess, built.Error?.Code + " " + built.Error?.Message);
        return Assert.IsType<InPlayPitchCandidate>(built.Value);
    }

    private static PendingAdvancement Pending(Result<InPlayPlanOutcome> built)
    {
        Assert.True(built.IsSuccess, built.Error?.Code + " " + built.Error?.Message);
        return Assert.IsType<PendingAdvancement>(built.Value);
    }

    private static string Snap(GameState s) =>
        $"{s.GameId}|{s.Count}|{s.Outs}|{s.Stage}|{s.Approach}|{s.AppliedEvents.Count}|{s.Plays.Count}|{s.Stats.Count}|{s.AwayRuns}-{s.HomeRuns}|{s.Bases.First?.RunnerKey}|{s.Bases.Second?.RunnerKey}|{s.Bases.Third?.RunnerKey}|{s.Status}";

    private static int Pitches(GameState s, GameState original) =>
        s.Team(original.FieldingSide).PitcherUsages.Where(u => u.Pitcher == original.Fielding.Pitcher).Sum(u => u.PitchCount);

    /// <summary>A real in-progress state of the fixture game with the given runners (bit mask 1 = first, 2 = second, 4 = third), outs and an approach chosen.</summary>
    private static GameState WithRunners(int occupancy, int outs = 0)
    {
        var n = 0;
        var state = StartState(NonAutomated, Gid);
        void Do(GameEvent e) => state = GameReducer.Apply(state, e) is { IsSuccess: true } ok ? ok.Value.State : throw new InvalidOperationException(e.GetType().Name);
        string Id() => $"x{++n}";
        void Walk() { Do(new ApproachEvent(Id(), ApproachKind.Normal)); for (var i = 0; i < 4; i++) Do(new PitchEvent(Id(), new PitchSpec(PitchOutcome.Ball))); }
        void Steal(int from) => Do(new RunnerPlayEvent(Id(), RunnerPlayKind.Steal, ImmutableList.Create(new RunnerMove(from, from + 1, MoveCause.Steal))));

        for (var i = 0; i < outs; i++)
        {
            Do(new ApproachEvent(Id(), ApproachKind.Normal));
            Do(new InPlayEvent(Id(), BattedBallType.Fly, InPlayResult.Out, false, null, ImmutableList.Create(new PositionCredit(FieldPosition.LeftField, CreditKind.Putout))));
        }

        switch (occupancy)
        {
            case 1: Walk(); break;
            case 2: Walk(); Steal(1); break;
            case 3: Walk(); Steal(1); Walk(); break;
            case 4: Walk(); Steal(1); Steal(2); break;
            case 5: Walk(); Steal(1); Steal(2); Walk(); break;
            case 7: Walk(); Walk(); Walk(); break;
        }

        Do(new ApproachEvent(Id(), ApproachKind.Normal));
        Assert.Equal(PaStage.InSequence, state.Stage);
        Assert.Equal(outs, state.Outs);
        return state;
    }

    // ---------------------------------------------------------------- a real in-play pitch becomes a reducer-validated ef2 candidate

    [Fact]
    public void A_real_in_play_pitch_becomes_a_reducer_validated_ef2_in_play_event_with_the_drafts_location()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (prepared, rng) = FindInPlay(state);

        var candidate = Candidate(Build(prepared, state, rng, last: 41));

        var evt = candidate.Event;
        Assert.Equal("game:3c2b2:probe:e000042", candidate.EventId);
        Assert.Equal(prepared.Draft.Location, evt.Location);
        Assert.Same(prepared.Draft.Location, evt.Location);                     // the very location the 3-C2A draw produced, never rebuilt or replaced
        Assert.Equal(candidate.Plan.OfficialResult, evt.Result);
        Assert.Equal("inplay-candidate-v1", candidate.ContractVersion);
        Assert.False(candidate.UnconfirmedStep.WasDuplicate);
        var receipt = candidate.UnconfirmedStep.State.AppliedEvents.Last();
        Assert.Equal((candidate.EventId, "ef2", EventFingerprint.Receipt(evt).Fingerprint), (receipt.EventId, receipt.FingerprintVersion, receipt.Fingerprint));
        Assert.Same(prepared, candidate.Plan.Prepared);
        Assert.Equal(Pitches(state, state) + 1, Pitches(candidate.UnconfirmedStep.State, state));          // one actual pitch for the original fielding team's pitcher
    }

    [Fact]
    public void An_in_play_pitch_is_one_event_only_with_no_separate_pitch_event()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (prepared, rng) = FindInPlay(state);

        var candidate = Candidate(Build(prepared, state, rng));

        Assert.IsType<InPlayEvent>(candidate.Event);
        Assert.Equal(state.AppliedEvents.Count + 1, candidate.UnconfirmedStep.State.AppliedEvents.Count);   // exactly one receipt was added
        Assert.Single(candidate.UnconfirmedStep.NewPlays);
    }

    [Fact]
    public void The_candidate_never_adopts_anything_and_leaves_the_state_the_rng_and_the_id_cursor_unchanged()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (prepared, rng) = FindInPlay(state);
        var ids = GameEventIds.Create(Gid, 5).Value;
        var before = (Snap(state), rng.Canonical(), rng.StateHash, ids.LastIssued, prepared.StartRngStateHash);

        var candidate = Candidate(Build(prepared, state, rng, last: ids.LastIssued));

        Assert.Equal(before, (Snap(state), rng.Canonical(), rng.StateHash, ids.LastIssued, prepared.StartRngStateHash));
        Assert.Equal(6, ids.ParseSequence(candidate.EventId).Value);
        Assert.Equal(5, ids.LastIssued);                                         // the authoritative cursor is only read
        Assert.NotSame(state, candidate.UnconfirmedStep.State);
        Assert.DoesNotContain(state.AppliedEvents, r => r.EventId == candidate.EventId);   // no receipt exists in the real state
    }

    [Fact]
    public void No_extra_random_number_is_drawn_after_the_pitch_draw()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (prepared, rng) = FindInPlay(state);

        var candidate = Candidate(Build(prepared, state, rng));

        Assert.Equal(prepared.Draft.NextRng.Canonical(), candidate.NextRng.Canonical());
        Assert.Equal(prepared.Draft.NextRng.StateHash, candidate.NextRng.StateHash);
        Assert.Equal(6, prepared.Draft.NextBoundedCalls);                          // target, location, decision, swing result, batted ball, defence result
        Assert.DoesNotContain(prepared.Draft.StreamUsage, u => u.Stream.Canonical == "game/runner@1");
        Assert.Equal(prepared.Draft.NextBoundedCalls, prepared.Draft.StreamUsage.Sum(u => u.Calls));
        // a second build starts from the same caller snapshot, and the caller's snapshot is the one that existed before Prepare
        var again = Candidate(Build(prepared, state, rng));
        Assert.Equal(candidate.NextRng.Canonical(), again.NextRng.Canonical());
        Assert.Equal(rng.StateHash, prepared.StartRngStateHash);
        Assert.NotEqual(rng.StateHash, candidate.NextRng.StateHash);              // the 3-C2A draw moved it; the in-play stage did not move it further
    }

    [Fact]
    public void The_same_input_gives_the_same_candidate_event_id_and_audit()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (prepared, rng) = FindInPlay(state);

        var a = Candidate(Build(prepared, state, rng, last: 9));
        var b = Candidate(Build(prepared, state, rng, last: 9));

        Assert.Equal(a.Event, b.Event);
        Assert.Equal(a.EventId, b.EventId);
        Assert.Equal(a.Audit.ToString(), b.Audit.ToString());
        Assert.Equal(EventFingerprint.Receipt(a.Event), EventFingerprint.Receipt(b.Event));
        Assert.Equal("game:3c2b2:probe:e000001", Candidate(Build(prepared, state, rng, last: 0)).EventId);
    }

    // ---------------------------------------------------------------- the independent golden scenarios (real chains of pitches)

    [Theory]
    [MemberData(nameof(PitchBranchResolverTests.ScenarioIds), MemberType = typeof(PitchBranchResolverTests))]
    public void Every_golden_in_play_pitch_becomes_the_expected_candidate_and_the_chain_ends_on_the_oracles_rng(string id)
    {
        var scenario = Golden.GetProperty("scenarios").EnumerateArray().Single(s => s.GetProperty("id").GetString() == id);
        var context = scenario.GetProperty("context").GetString()!;
        var rng = Snapshot(Hex64(scenario.GetProperty("seed")), context, scenario.GetProperty("prior_calls").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32()));
        foreach (var pitch in scenario.GetProperty("pitches").EnumerateArray())
        {
            var spec = pitch.GetProperty("spec");
            var expected = pitch.GetProperty("expected");
            var take = spec.GetProperty("explicit_take").GetBoolean();
            var state = StateFor(RulesFor(spec.GetProperty("called_pitch_mode").GetString()!), take, context);
            var bats = Bats(spec.GetProperty("batter_side").GetString()!);
            var throws = Throws(spec.GetProperty("pitcher_hand").GetString()!);
            var batter = Sheet(state.Batting.CurrentBatterSlot.Player, bats, throws);
            var pitcher = Sheet(state.Fielding.Pitcher, bats, throws);
            var prepared = PitchPreparation.Prepare(state, rng, Tuning, Intent(spec.GetProperty("intent").GetString()!), take, batter, pitcher).Value;

            if (expected.GetProperty("kind").GetString() != "in_play")
            {
                var other = PitchPreparation.BuildNonInPlayCandidate(prepared, state, rng, Tuning, batter, pitcher, 0);
                Assert.True(other.IsSuccess, other.Error?.Code);
                Assert.Equal("InPlayPlan.not_in_play", Code(InPlayPitchPlanning.Build(prepared, state, rng, Tuning, batter, pitcher, 0)));
                rng = other.Value.NextRng;
                continue;
            }

            var built = InPlayPitchPlanning.Build(prepared, state, rng, Tuning, batter, pitcher, 0);
            var candidate = Candidate(built);                                 // a fresh empty-bases state never leaves an extra-base decision open
            var batted = expected.GetProperty("batted_ball").GetString()!;
            var defense = expected.GetProperty("defense_result").GetString()!;
            Assert.Equal(batted, candidate.Plan.BattedBall);
            Assert.Equal(defense, candidate.Plan.OriginalDefenseResult);
            Assert.Equal(defense switch { "out" => InPlayResult.Out, "single" => InPlayResult.Single, "double" => InPlayResult.Double, "triple" => InPlayResult.Triple, "home_run" => InPlayResult.HomeRun, _ => InPlayResult.ReachedOnError },
                candidate.Event.Result);                                      // an ordinary first inning: the drawn result is the official result
            Assert.Equal(batted == "popup" ? BattedBallType.Pop : Enum.Parse<BattedBallType>(batted.Split('_')[0], true), candidate.Event.Batted);
            Assert.Equal(expected.GetProperty("location_zone").GetString(), candidate.Event.Location!.Zone.Value);
            Assert.Equal(expected.GetProperty("applied_side").GetString(), candidate.Event.Location.Side.ToString().ToLowerInvariant());
            Assert.Equal(expected.GetProperty("boundary").GetString(), candidate.Event.Location.Boundary.ToString().ToLowerInvariant());
            Assert.Equal(expected.GetProperty("total_calls").GetInt32(), prepared.Draft.NextBoundedCalls);
            Assert.Equal(context + ":e000001", candidate.EventId);
            rng = candidate.NextRng;
        }

        // no extra random number was ever drawn: the chain ends exactly where the independent oracle ends
        Assert.Equal(scenario.GetProperty("final_canonical").GetString(), rng.Canonical());
        Assert.Equal(scenario.GetProperty("final_state_hash").GetString(), rng.StateHash);
    }

    [Fact]
    public void The_golden_corpus_really_contains_in_play_pitches_of_several_kinds()
    {
        var results = Golden.GetProperty("scenarios").EnumerateArray().SelectMany(s => s.GetProperty("pitches").EnumerateArray())
            .Select(p => p.GetProperty("expected")).Where(e => e.GetProperty("kind").GetString() == "in_play").Select(e => e.GetProperty("defense_result").GetString()).ToHashSet();

        Assert.Superset(new HashSet<string?> { "out", "single", "double", "triple", "home_run", "reached_on_error" }, results);
    }

    // ---------------------------------------------------------------- pending advancement (real prepared pitches on real states with runners)

    [Fact]
    public void A_single_with_a_runner_on_first_is_a_pending_advancement_that_has_no_event_and_no_id()
    {
        var state = WithRunners(1);
        var (prepared, rng) = FindInPlay(state, d => d.DefenseResult == "single");

        var outcome = Build(prepared, state, rng, last: 7);

        var pending = Pending(outcome);
        Assert.Equal("single_runner_on_first", string.Join("+", pending.ContextKeys));
        Assert.False(pending.IsComposite);
        Assert.True(pending.Plan.AdvancementRequired);
        Assert.Same(prepared, pending.Plan.Prepared);
        Assert.Equal(prepared.Draft.NextRng.Canonical(), pending.RngAfterPitchDraw.Canonical());      // the future draw starts after the 3-C2A draw, never at the pitch start
        Assert.NotEqual(prepared.StartRngStateHash, pending.RngAfterPitchDraw.StateHash);
        Assert.Equal("pending", pending.Plan.Audit.Advancement);
        Assert.Null(pending.Plan.Audit.CandidateEventId);
        Assert.Equal(prepared.StartRngStateHash, pending.Plan.Audit.StartRngStateHash);
        Assert.Equal(prepared.Draft.NextRng.StateHash, pending.Plan.Audit.FinalRngStateHash);
        Assert.False(outcome.Value is InPlayPitchCandidate);
    }

    [Fact]
    public void A_pending_advancement_changes_nothing_issues_no_id_and_does_not_look_at_the_cursor()
    {
        var state = WithRunners(1);
        var (prepared, rng) = FindInPlay(state, d => d.DefenseResult == "single");
        var before = (Snap(state), rng.Canonical(), prepared.StartRngStateHash);

        var a = Pending(Build(prepared, state, rng, last: 0));
        var b = Pending(Build(prepared, state, rng, last: GameEventIds.MaxSequence));           // no id is issued, so even an exhausted cursor changes nothing
        var c = Pending(Build(prepared, state, rng, last: -1));

        Assert.Equal(before, (Snap(state), rng.Canonical(), prepared.StartRngStateHash));
        Assert.Equal(a.RngAfterPitchDraw.Canonical(), b.RngAfterPitchDraw.Canonical());
        Assert.Equal(a.RngAfterPitchDraw.Canonical(), c.RngAfterPitchDraw.Canonical());
    }

    [Fact]
    public void A_composite_pending_advancement_keeps_every_context_key_and_the_composite_marker()
    {
        var state = WithRunners(3);
        var (prepared, rng) = FindInPlay(state, d => d.DefenseResult == "single");

        var pending = Pending(Build(prepared, state, rng));

        Assert.Equal("single_runner_on_first+single_runner_on_second", string.Join("+", pending.ContextKeys));
        Assert.True(pending.IsComposite);
        Assert.Equal(2, pending.Plan.Moves.Length);
        Assert.Equal([pending.Plan.State.Bases.Second!.RunnerKey, pending.Plan.State.Bases.First!.RunnerKey], pending.Plan.Moves.Select(m => m.RunnerKey));
    }

    [Fact]
    public void A_fly_out_with_a_runner_on_third_and_fewer_than_two_outs_is_pending_and_with_two_outs_a_finished_candidate()
    {
        var open = WithRunners(4, 0);
        var (p1, r1) = FindInPlay(open, d => d.BattedBall!.StartsWith("fly", StringComparison.Ordinal) && d.DefenseResult == "out");
        Assert.Equal("fly_out_runner_on_third", string.Join("+", Pending(Build(p1, open, r1)).ContextKeys));

        var late = WithRunners(4, 2);
        var (p2, r2) = FindInPlay(late, d => d.BattedBall!.StartsWith("fly", StringComparison.Ordinal) && d.DefenseResult == "out");
        var done = Candidate(Build(p2, late, r2));
        Assert.True(done.Plan.EndsHalfInning);
        Assert.Equal("not_required", done.Audit.Advancement);
    }

    [Fact]
    public void A_finished_candidate_with_runners_moves_every_runner_and_the_reducer_agrees()
    {
        var state = WithRunners(7);
        var (prepared, rng) = FindInPlay(state, d => d.DefenseResult == "home_run");

        var candidate = Candidate(Build(prepared, state, rng));

        Assert.Equal(InPlayResult.HomeRun, candidate.Event.Result);
        Assert.Equal(4, candidate.UnconfirmedStep.NewPlays.Single().Runs.Count);
        Assert.Equal(3, candidate.Event.MoveList.Count);
        Assert.Equal(0, candidate.UnconfirmedStep.State.Bases.Count);
        Assert.Equal(4, candidate.UnconfirmedStep.State.AwayRuns);
    }

    // ---------------------------------------------------------------- audit data

    [Fact]
    public void The_audit_records_the_d5_evidence_and_stays_outside_the_event_fingerprint()
    {
        var state = WithRunners(2);
        var (prepared, rng) = FindInPlay(state, d => d.DefenseResult == "out" && d.BattedBall!.StartsWith("ground", StringComparison.Ordinal));

        var candidate = Candidate(Build(prepared, state, rng, last: 11));

        var a = candidate.Audit;
        Assert.Equal(("inplay-mapping-v1", "not_applicable", "inplay-template-v1"), (a.InPlayPolicyVersion, a.WalkOffCreditVersion, a.TemplateCatalogVersion));
        Assert.Equal((Gid, "game:3c2b2:probe:e000012"), (a.GameId, a.CandidateEventId));
        Assert.Equal((prepared.Draft.BattedBall, prepared.Draft.DefenseResult), (a.OriginalBattedBall, a.OriginalDefenseResult));
        Assert.Equal(prepared.Draft.BattedBall!.Split('_')[1], a.OriginalIntensity);
        Assert.Equal((InPlayResult.Out, "ipt1.ground.out", "ipt1.ground.out"), (a.OfficialResult, a.DrawnTemplateId, a.OfficialTemplateId));
        Assert.Equal((1, InningHalf.Top, 0, 0, 0), (a.PrePlay.Inning, a.PrePlay.Half, a.PrePlay.Outs, a.PrePlay.AwayRuns, a.PrePlay.HomeRuns));
        Assert.Equal(state.Bases.Second!.RunnerKey, Assert.Single(a.PrePlay.Runners).RunnerKey);
        Assert.Equal(0, a.BatterFinalBase);
        Assert.Equal("2>2", string.Join(",", a.RunnerMoves.Select(m => $"{m.From}>{m.To}")));
        Assert.Null(a.WinningRunner);
        Assert.Equal(("none", InPlayWalkOffEffect.None), (a.RecordChangeReason, a.WalkOffEffect));
        Assert.Equal((NonAutomated.Version, NonAutomated.ContentHash), (a.RulesVersion, a.RulesHash));
        Assert.Equal((Tuning.Tuning.Value, Tuning.ContentHash, Tuning.Model.Version.Value), (a.TuningVersion, a.TuningHash, a.ProbabilityModelVersion));
        Assert.Equal((rng.StateHash, prepared.Draft.NextRng.StateHash), (a.StartRngStateHash, a.FinalRngStateHash));
        Assert.Equal("not_required", a.Advancement);

        // the audit is not part of the canonical event: a hand-built event with the same facts has the same ef2 receipt
        var byHand = new InPlayEvent(candidate.EventId, BattedBallType.Ground, InPlayResult.Out, false, ImmutableList.Create(new RunnerMove(2, 2, MoveCause.Play)),
            ImmutableList.Create(new PositionCredit(FieldPosition.Shortstop, CreditKind.Assist), new PositionCredit(FieldPosition.FirstBase, CreditKind.Putout)), null, null, prepared.Draft.Location);
        Assert.Equal(EventFingerprint.Receipt(byHand), EventFingerprint.Receipt(candidate.Event));
        Assert.DoesNotContain("inplay", EventFingerprint.Canonical(candidate.Event));
        Assert.DoesNotContain("ipt1", EventFingerprint.Canonical(candidate.Event));
    }

    // ---------------------------------------------------------------- refusals

    [Fact]
    public void A_pitch_that_is_not_in_play_is_refused_without_side_effects()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (prepared, rng) = FindNonInPlay(state);
        var before = (Snap(state), rng.Canonical());

        Assert.Equal("InPlayPlan.not_in_play", Code(Build(prepared, state, rng)));
        Assert.Equal("InPlayPlan.not_in_play", Code(InPlayPitchPlanning.PlanBase(prepared, state, rng, Tuning, Sheets(state).Batter, Sheets(state).Pitcher)));
        Assert.Equal(before, (Snap(state), rng.Canonical()));
    }

    /// <summary>Creates a PreparedPitch with one draft replaced (the only way a test can forge one; production code cannot).</summary>
    private static PreparedPitch Forge(PreparedPitch source, PitchBranchDraft draft)
    {
        var ctor = typeof(PreparedPitch).GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).Single();
        var args = ctor.GetParameters().Select(p => p.Name == "draft"
            ? draft
            : typeof(PreparedPitch).GetProperty(p.Name![..1].ToUpperInvariant() + p.Name[1..], System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!.GetValue(source)).ToArray();
        return (PreparedPitch)ctor.Invoke(args);
    }

    [Theory]
    [InlineData("ground_weak", "double", "InPlayPlan.combination_not_approved")]
    [InlineData("ground_weak", "triple", "InPlayPlan.template_unsupported")]
    [InlineData("popup", "home_run", "InPlayPlan.template_unsupported")]
    [InlineData("bunt_weak", "out", "InPlayPlan.batted_ball_unknown")]
    [InlineData(null, "out", "InPlayPlan.batted_ball_unknown")]
    [InlineData("fly_hard", "fielders_choice", "InPlayPlan.defense_result_unknown")]
    [InlineData("fly_hard", null, "InPlayPlan.defense_result_unknown")]
    public void An_unknown_or_unapproved_draw_is_refused_and_nothing_is_changed(string? batted, string? defense, string code)
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (real, rng) = FindInPlay(state);
        var forged = Forge(real, real.Draft with { BattedBall = batted, DefenseResult = defense });
        var before = (Snap(state), rng.Canonical());

        Assert.Equal(code, Code(Build(forged, state, rng)));
        Assert.Equal(before, (Snap(state), rng.Canonical()));
    }

    [Fact]
    public void An_unsupported_draft_contract_and_missing_inputs_keep_the_shared_prepared_pitch_codes()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (ok, rng) = FindInPlay(state);
        var (batter, pitcher) = Sheets(state);
        var forged = Forge(ok, ok.Draft with { ContractVersion = "pitch-branch-v0" });

        Assert.Equal("PitchCandidate.draft_contract_unsupported", Code(Build(forged, state, rng)));
        Assert.Equal("PitchCandidate.prepared_missing", Code(InPlayPitchPlanning.Build(null, state, rng, Tuning, batter, pitcher, 0)));
        Assert.Equal("PitchCandidate.state_missing", Code(InPlayPitchPlanning.Build(ok, null, rng, Tuning, batter, pitcher, 0)));
        Assert.Equal("PitchCandidate.rng_missing", Code(InPlayPitchPlanning.Build(ok, state, null, Tuning, batter, pitcher, 0)));
        Assert.Equal("PitchCandidate.tuning_missing", Code(InPlayPitchPlanning.Build(ok, state, rng, null, batter, pitcher, 0)));
        Assert.Equal("PitchCandidate.batter_sheet_missing", Code(InPlayPitchPlanning.Build(ok, state, rng, Tuning, null, pitcher, 0)));
        Assert.Equal("PitchCandidate.pitcher_sheet_missing", Code(InPlayPitchPlanning.Build(ok, state, rng, Tuning, batter, null, 0)));
    }

    [Fact]
    public void A_stale_preparation_is_refused_with_the_same_codes_as_the_non_in_play_builder()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (ok, rng) = FindInPlay(state);
        var rebuilt = InSequenceState(NonAutomated, Gid);                  // same game and events, new object: what a save/load round trip produces
        var advanced = Snapshot(7, Gid, new Dictionary<string, int> { ["game/pitch@1"] = 1 });
        var otherTuning = TuningProfile.Create(TuningProfileTests.Draft() with { Provenance = TuningProfileTests.Draft().Provenance + "." }).Value;

        Assert.Equal("PitchCandidate.state_stale", Code(Build(ok, rebuilt, rng)));
        Assert.Equal("PitchCandidate.state_stale", Code(Build(ok, state with { Count = new PitchCountState(1, 0) }, rng)));
        Assert.Equal("PitchCandidate.rng_state_changed", Code(Build(ok, state, advanced)));
        Assert.Equal("PitchCandidate.rng_context_mismatch", Code(Build(ok, state, Rng(7, "game:3c2b2:other"))));
        Assert.Equal("PitchCandidate.game_mismatch", Code(Build(ok, state with { GameId = "game:3c2b2:other" }, rng)));
        Assert.Equal("PitchCandidate.rules_changed", Code(Build(ok, InSequenceState(Automated, Gid), rng)));
        Assert.Equal("PitchCandidate.tuning_changed", Code(Build(ok, state, rng, tuning: otherTuning)));
        Assert.Equal("PitchPrepare.batter_mismatch", Code(InPlayPitchPlanning.Build(ok, state, rng, Tuning, Sheet(state.Fielding.Pitcher), Sheets(state).Pitcher, 0)));
    }

    [Fact]
    public void A_stale_preparation_cannot_even_be_planned()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (ok, rng) = FindInPlay(state);
        var (batter, pitcher) = Sheets(state);

        Assert.Equal("PitchCandidate.state_stale", Code(InPlayPitchPlanning.PlanBase(ok, InSequenceState(NonAutomated, Gid), rng, Tuning, batter, pitcher)));
        Assert.True(InPlayPitchPlanning.PlanBase(ok, state, rng, Tuning, batter, pitcher).IsSuccess);
    }

    [Fact]
    public void A_colliding_cursor_and_the_sequence_limits_are_refused_without_wrapping_and_a_duplicate_is_never_a_new_success()
    {
        var plain = InSequenceState(NonAutomated, Gid);
        var taken = plain with { AppliedEvents = plain.AppliedEvents.Add(new EventReceipt("game:3c2b2:probe:e000001", "Pitch", "ef1", new string('0', 64))) };
        var (ok, rng) = FindInPlay(taken);

        Assert.Equal("InPlayCandidate.event_id_in_use", Code(Build(ok, taken, rng, last: 0)));
        Assert.True(Build(ok, taken, rng, last: 1).IsSuccess);
        Assert.Equal("Id.sequence_exhausted", Code(Build(ok, taken, rng, last: GameEventIds.MaxSequence)));
        Assert.Equal("Id.sequence_invalid", Code(Build(ok, taken, rng, last: -1)));
        Assert.Equal("Id.sequence_invalid", Code(Build(ok, taken, rng, last: GameEventIds.MaxSequence + 1)));
        Assert.Equal("game:3c2b2:probe:e999999", Candidate(Build(ok, taken, rng, last: GameEventIds.MaxSequence - 1)).EventId);
    }

    [Fact]
    public void A_candidate_the_reducer_rejects_is_a_failure_with_the_reducers_error_and_no_candidate()
    {
        var plain = InSequenceState(NonAutomated, Gid);
        var incomplete = plain.FieldingSide == TeamSide.Home ? plain with { Home = plain.Home with { DhActive = false } } : plain with { Away = plain.Away with { DhActive = false } };   // defence incomplete: the reducer refuses every pitch
        var (ok, rng) = FindInPlay(incomplete);
        var before = (Snap(incomplete), rng.Canonical());

        var built = Build(ok, incomplete, rng);

        Assert.Equal("Game.defense_incomplete", Code(built));
        Assert.Equal(before, (Snap(incomplete), rng.Canonical()));
    }

    // ---------------------------------------------------------------- cancellation: before preparation, while planning, around the reducer

    [Fact]
    public void Cancellation_at_every_stage_throws_and_leaves_state_rng_ids_and_receipts_untouched()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (prepared, rng) = FindInPlay(state);
        var ids = GameEventIds.Create(Gid, 3).Value;
        var before = (Snap(state), rng.Canonical(), ids.LastIssued, state.AppliedEvents.Count);

        foreach (var stage in new[] { "start", "validated", "planning", "planned", "before_reducer", "after_reducer", "verified" })
        {
            using var source = new CancellationTokenSource();
            var seen = new List<string>();

            Assert.Throws<OperationCanceledException>(() => Build(prepared, state, rng, ids.LastIssued, name => { seen.Add(name); if (name == stage) source.Cancel(); }, source.Token));

            Assert.Equal(stage, seen.Last());                                            // it stopped at the stage that was cancelled
            Assert.Equal(before, (Snap(state), rng.Canonical(), ids.LastIssued, state.AppliedEvents.Count));
        }

        // after all the cancellations the same input still works and gives the same id as before
        Assert.Equal("game:3c2b2:probe:e000004", Candidate(Build(prepared, state, rng, ids.LastIssued)).EventId);
    }

    [Fact]
    public void A_token_cancelled_before_the_call_throws_at_once()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (prepared, rng) = FindInPlay(state);
        var (batter, pitcher) = Sheets(state);
        using var source = new CancellationTokenSource();
        source.Cancel();
        var before = (Snap(state), rng.Canonical());

        Assert.Throws<OperationCanceledException>(() => InPlayPitchPlanning.Build(prepared, state, rng, Tuning, batter, pitcher, 0, source.Token));
        Assert.Throws<OperationCanceledException>(() => InPlayPitchPlanning.PlanBase(prepared, state, rng, Tuning, batter, pitcher, source.Token));
        Assert.Equal(before, (Snap(state), rng.Canonical()));
    }

    [Fact]
    public void The_stages_run_in_order_and_a_pending_advancement_stops_before_any_id_or_reducer_work()
    {
        var plain = InSequenceState(NonAutomated, Gid);
        var (p1, r1) = FindInPlay(plain);
        var seen = new List<string>();
        Candidate(Build(p1, plain, r1, 0, seen.Add));
        Assert.Equal(["start", "validated", "planning", "planned", "before_reducer", "after_reducer", "verified"], seen);

        var runners = WithRunners(1);
        var (p2, r2) = FindInPlay(runners, d => d.DefenseResult == "single");
        var pendingSeen = new List<string>();
        Pending(Build(p2, runners, r2, 0, pendingSeen.Add));
        Assert.Equal(["start", "validated", "planning", "planned"], pendingSeen);
    }

    // ---------------------------------------------------------------- the earlier boundaries are unchanged

    [Fact]
    public void The_non_in_play_builder_still_refuses_in_play_exactly_as_in_3C2B_1()
    {
        var state = InSequenceState(NonAutomated, Gid);
        var (prepared, rng) = FindInPlay(state);
        var (batter, pitcher) = Sheets(state);

        Assert.Equal("PitchCandidate.in_play_not_supported_yet", Code(PitchPreparation.BuildNonInPlayCandidate(prepared, state, rng, Tuning, batter, pitcher, 3)));
        Assert.Equal("pitch-candidate-v1", PitchPreparation.CandidateContractVersion);
        Assert.Equal("pitch-branch-v1", PitchBranchResolver.ContractVersion);
        Assert.Equal("ef2", EventFingerprint.VersionV2);
    }
}
