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
/// Phase 3-C2B-3, stage B: <see cref="AdvancementPlanning.Resolve"/> on REAL prepared pitches. Every pending advancement here comes from the real
/// <see cref="PitchPreparation.Prepare"/> (the real 3-C2A resolver on the real seeded streams) and the real <see cref="InPlayPitchPlanning.Build"/>; the only forged objects are the
/// ones a test needs to reach a refusal and say so. The expected RNG positions are derived in the test with the existing <see cref="GameRandom"/> primitives (K rolls on
/// <c>game/runner@1</c> from H1), the expected approved probabilities from the tuning profile, and the official record from the unchanged <see cref="GameReducer"/>; the resolver under
/// test never produces an expected value.
/// </summary>
public class AdvancementPlanningTests
{
    private const string Gid = "game:3c2b3:probe";

    // ---------------------------------------------------------------- inputs

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

    /// <summary>Plays real events through the public reducer with canonical event ids, so the state's receipts and the id cursor agree.</summary>
    private sealed class Driver
    {
        private readonly GameEventIds _ids;

        public Driver(GameState start, string gameId)
        {
            State = start;
            _ids = GameEventIds.Create(gameId).Value;
        }

        public GameState State { get; private set; }

        public int LastIssued => _ids.LastIssued;

        private string Id() => _ids.Next().Value;

        private Driver Do(Func<string, GameEvent> make)
        {
            var applied = GameReducer.Apply(State, make(Id()));
            Assert.True(applied.IsSuccess, applied.Error?.Code + " " + applied.Error?.Message);
            State = applied.Value.State;
            return this;
        }

        public Driver Approach() => Do(id => new ApproachEvent(id, ApproachKind.Normal));

        public Driver Walk()
        {
            Approach();
            for (var i = 0; i < 4; i++)
                Do(id => new PitchEvent(id, new PitchSpec(PitchOutcome.Ball)));
            return this;
        }

        public Driver Steal(int from) => Do(id => new RunnerPlayEvent(id, RunnerPlayKind.Steal, ImmutableList.Create(new RunnerMove(from, from + 1, MoveCause.Steal))));

        public Driver FlyOut() =>
            Approach().Do(id => new InPlayEvent(id, BattedBallType.Fly, InPlayResult.Out, false, null, ImmutableList.Create(new PositionCredit(FieldPosition.LeftField, CreditKind.Putout))));

        public Driver HomeRun() => Approach().Do(id => new InPlayEvent(id, BattedBallType.Fly, InPlayResult.HomeRun));

        public Driver Outs(int count)
        {
            for (var i = 0; i < count; i++)
                FlyOut();
            return this;
        }

        public Driver Runners(int occupancy)
        {
            switch (occupancy)
            {
                case 1: Walk(); break;
                case 2: Walk().Steal(1); break;
                case 3: Walk().Steal(1).Walk(); break;
                case 4: Walk().Steal(1).Steal(2); break;
                case 5: Walk().Steal(1).Steal(2).Walk(); break;
                case 6: Walk().Steal(1).Steal(2).Walk().Steal(1); break;
                case 7: Walk().Walk().Walk(); break;
            }

            return this;
        }
    }

    /// <summary>A real plate appearance in progress (an approach chosen) with the given runners (bit mask 1 first, 2 second, 4 third) and outs, in the 1st inning; or, with
    /// <paramref name="awayLeadsBy"/> set, the bottom of the 9th with the away team ahead by that many runs.</summary>
    private static Driver Situation(int occupancy, int outs, int? awayLeadsBy = null, string gameId = Gid, GameRuleSnapshot? rules = null)
    {
        var d = new Driver(StartState(rules ?? NonAutomated, gameId), gameId);
        if (awayLeadsBy is { } lead)
        {
            for (var i = 0; i < lead; i++)
                d.HomeRun();
            d.Outs(3);
            for (var i = 0; i < 16; i++)
                d.Outs(3);
            Assert.Equal((9, InningHalf.Bottom), (d.State.Inning, d.State.Half));
        }

        d.Outs(outs).Runners(occupancy).Approach();
        Assert.Equal(PaStage.InSequence, d.State.Stage);
        Assert.Equal(outs, d.State.Outs);
        Assert.Equal(occupancy, (d.State.Bases.First is null ? 0 : 1) | (d.State.Bases.Second is null ? 0 : 2) | (d.State.Bases.Third is null ? 0 : 4));
        return d;
    }

    private static PreparedPitch Prepared(GameState state, GameRandomSnapshot rng)
    {
        var (b, p) = Sheets(state);
        var made = PitchPreparation.Prepare(state, rng, Tuning, PitchIntent.Balanced, false, b, p);
        Assert.True(made.IsSuccess, made.Error?.Code + " " + made.Error?.Message);
        return made.Value;
    }

    private static string Code<T>(Result<T> r) => r.IsSuccess ? "(success)" : r.Error!.Value.Code;

    private static string Snap(GameState s) =>
        $"{s.GameId}|{s.Count}|{s.Outs}|{s.Stage}|{s.AppliedEvents.Count}|{s.Plays.Count}|{s.Stats.Count}|{s.AwayRuns}-{s.HomeRuns}|{s.Bases.First?.RunnerKey}|{s.Bases.Second?.RunnerKey}|{s.Bases.Third?.RunnerKey}|{s.Status}";

    /// <summary>One real pending advancement together with the authoritative RNG position H0 it was prepared from, and the candidate the resolver made of it.</summary>
    private sealed record Hit(ulong Seed, GameState State, GameRandomSnapshot H0, PendingAdvancement Pending, InPlayPitchCandidate Candidate, int Cursor)
    {
        public AdvancementAudit Detail => Candidate.Audit.AdvancementDetail!;

        /// <summary>Drawn and applied results in processing order, e.g. <c>E>E/O>V</c> (drawn&gt;applied; E extra, H hold, O out, V void).</summary>
        public string Vector => string.Join("/", Detail.Decisions.Select(d => $"{Letter(d.Drawn)}>{Letter(d.Applied)}"));

        public string Applied => string.Concat(Detail.Decisions.Select(d => Letter(d.Applied)));

        private static char Letter(AdvancementOutcome o) => o switch { AdvancementOutcome.Extra => 'E', AdvancementOutcome.Hold => 'H', AdvancementOutcome.Out => 'O', _ => 'V' };
    }

    /// <summary>A situation scanned over fixed seeds (deterministic) for real in-play pitches whose branch the filter accepts and that leave an extra-base decision open.</summary>
    private sealed class Scenario
    {
        private readonly Lazy<IReadOnlyList<Hit>> _hits;

        public Scenario(Func<Driver> make, Func<PitchBranchDraft, bool> filter, int target, ulong maxSeeds = 60000)
        {
            var driver = make();
            State = driver.State;
            Cursor = driver.LastIssued;
            _hits = new Lazy<IReadOnlyList<Hit>>(() =>
            {
                var list = new List<Hit>();
                var (b, p) = Sheets(State);
                for (ulong seed = 1; seed <= maxSeeds && list.Count < target; seed++)
                {
                    var h0 = Snapshot(seed, Gid);
                    var prepared = Prepared(State, h0);
                    if (prepared.Draft.Kind != PitchBranchKind.InPlay || !filter(prepared.Draft))
                        continue;
                    var built = InPlayPitchPlanning.Build(prepared, State, h0, Tuning, b, p, Cursor);
                    if (!built.IsSuccess || built.Value is not PendingAdvancement pending)
                        continue;
                    var resolved = AdvancementPlanning.Resolve(pending, State, h0, Tuning, b, p, Cursor);
                    Assert.True(resolved.IsSuccess, $"seed {seed}: {resolved.Error?.Code} {resolved.Error?.Message}");
                    list.Add(new Hit(seed, State, h0, pending, resolved.Value, Cursor));
                }

                return list;
            });
        }

        public GameState State { get; }

        public int Cursor { get; }

        public IReadOnlyList<Hit> Hits => _hits.Value;
    }

    private static bool Single(PitchBranchDraft d) => d.DefenseResult == "single";

    private static bool FlyOut(PitchBranchDraft d) => d.BattedBall?.StartsWith("fly", StringComparison.Ordinal) == true && d.DefenseResult == "out";

    private static readonly Lazy<Scenario> FirstAndSecond0 = new(() => new Scenario(() => Situation(3, 0), Single, 500));
    private static readonly Lazy<Scenario> FirstAndSecond2 = new(() => new Scenario(() => Situation(3, 2), Single, 900));
    private static readonly Lazy<Scenario> SecondOnly0 = new(() => new Scenario(() => Situation(2, 0), Single, 300));
    private static readonly Lazy<Scenario> SecondOnly1 = new(() => new Scenario(() => Situation(2, 1), Single, 300));
    private static readonly Lazy<Scenario> SecondOnly2 = new(() => new Scenario(() => Situation(2, 2), Single, 300));
    private static readonly Lazy<Scenario> ThirdFly0 = new(() => new Scenario(() => Situation(4, 0), FlyOut, 400));
    private static readonly Lazy<Scenario> ThirdFly1 = new(() => new Scenario(() => Situation(4, 1), FlyOut, 400));
    private static readonly Lazy<Scenario> WalkOffTiedFirstSecond = new(() => new Scenario(() => Situation(3, 0, 0), Single, 300));
    private static readonly Lazy<Scenario> WalkOffTiedThirdFly = new(() => new Scenario(() => Situation(4, 1, 0), FlyOut, 120));
    private static readonly Lazy<Scenario> FirstDouble0 = new(() => new Scenario(() => Situation(1, 0), d => d.DefenseResult == "double", 200));

    private static Hit First(Scenario s, Func<Hit, bool> where) => s.Hits.FirstOrDefault(where) ?? throw new InvalidOperationException($"no seed gave the wanted resolution in {s.Hits.Count} pending plays");

    /// <summary>A real composite pending (first and second, nobody out) with its inputs, taken from the first scanned seed.</summary>
    private static Hit One() => FirstAndSecond0.Value.Hits[0];

    /// <summary>Independent derivation of the position after <paramref name="calls"/> whole rolls on the runner stream from <paramref name="start"/> (the existing RNG primitives).</summary>
    private static (GameRandomSnapshot After, uint[] Rolls, long RawDraws) DrawFrom(GameRandomSnapshot start, int calls)
    {
        var work = GameRandom.Restore(start).Value;
        var stream = work.Stream(GameRngStreams.Runner);
        var rolls = Enumerable.Range(0, calls).Select(_ => stream.NextBounded(ProbabilityScale.RollBound)).ToArray();
        return (work.Snapshot(), rolls, stream.Draws);
    }

    private static readonly string[] AdvancementOutcomes = ["advance_extra", "hold", "out_on_bases"];

    // ================================================================ a real pending becomes a reducer-validated, unconfirmed candidate

    [Fact]
    public void A_real_pending_advancement_becomes_one_reducer_validated_ef2_in_play_candidate_with_the_prepared_pitchs_location_and_H2()
    {
        var hit = One();
        var candidate = hit.Candidate;
        var prepared = hit.Pending.Plan.Prepared;

        Assert.Same(hit.Pending.Plan, candidate.Plan);
        Assert.Equal($"{Gid}:e{hit.Cursor + 1:D6}", candidate.EventId);                                       // the working id: next after the read-only cursor
        Assert.Equal(InPlayPolicy.CandidateContractVersion, candidate.ContractVersion);
        Assert.Equal(prepared.Draft.Location, candidate.Event.Location);                                 // the confirmed location is carried over unchanged
        Assert.Equal(prepared.Draft.Location, hit.Pending.Plan.Prepared.Draft.Location);
        Assert.Equal(hit.Pending.Plan.OfficialResult, candidate.Event.Result);                            // the batting result is the base plan's real InPlayResult
        Assert.Equal(candidate.EventId, candidate.UnconfirmedStep.State.AppliedEvents.Last().EventId);
        Assert.Equal(EventFingerprint.VersionV2, candidate.UnconfirmedStep.State.AppliedEvents.Last().FingerprintVersion);
        Assert.False(candidate.UnconfirmedStep.WasDuplicate);
        Assert.Single(candidate.UnconfirmedStep.NewPlays);                                                // one InPlayEvent, one scored play, no separate PitchEvent
        Assert.Equal(hit.State.AppliedEvents.Count + 1, candidate.UnconfirmedStep.State.AppliedEvents.Count);
        Assert.Equal(PitchPreparation.PitchesOf(hit.State, hit.State) + 1, PitchPreparation.PitchesOf(candidate.UnconfirmedStep.State, hit.State));   // the original pitcher's count, once

        // H0 / H1 / H2: three different positions; H2 is what a fresh private copy reaches from H1 after K whole rolls on the runner stream
        var h1 = hit.Pending.RngAfterPitchDraw;
        var (expectedH2, rolls, raw) = DrawFrom(h1, hit.Detail.Decisions.Length);
        Assert.Equal(hit.H0.StateHash, hit.Detail.StartRngStateHash);
        Assert.Equal(h1.StateHash, hit.Detail.AfterPitchRngStateHash);
        Assert.Equal(candidate.NextRng.StateHash, hit.Detail.FinalRngStateHash);
        Assert.Equal(expectedH2.Canonical(), candidate.NextRng.Canonical());
        Assert.NotEqual(hit.H0.StateHash, h1.StateHash);
        Assert.NotEqual(h1.StateHash, candidate.NextRng.StateHash);
        Assert.Equal(rolls, hit.Detail.Decisions.Select(d => d.Roll));
        Assert.Equal(raw, hit.Detail.RawDraws);
        Assert.Equal(raw, hit.Detail.Decisions.Sum(d => d.RawDraws));
        Assert.Equal(2, hit.Detail.LogicalDraws);
        Assert.Equal(GameRngStreams.Runner.Canonical, hit.Detail.DrawStream);
    }

    [Fact]
    public void Resolving_changes_nothing_of_the_callers_state_rng_pending_or_cursor_and_the_same_inputs_give_the_same_candidate()
    {
        var hit = One();
        var (b, p) = Sheets(hit.State);
        var before = (Snap(hit.State), hit.H0.Canonical(), hit.Pending.RngAfterPitchDraw.Canonical(), hit.State.AppliedEvents.Count);

        var again = AdvancementPlanning.Resolve(hit.Pending, hit.State, hit.H0, Tuning, b, p, hit.Cursor).Value;

        Assert.Equal(before, (Snap(hit.State), hit.H0.Canonical(), hit.Pending.RngAfterPitchDraw.Canonical(), hit.State.AppliedEvents.Count));
        Assert.Equal(hit.Candidate.EventId, again.EventId);
        Assert.Equal(EventFingerprint.Receipt(hit.Candidate.Event), EventFingerprint.Receipt(again.Event));   // the canonical text: every move, credit, sequence and the location
        Assert.Equal(hit.Candidate.NextRng.Canonical(), again.NextRng.Canonical());
        Assert.Equal(EventFingerprint.Receipt(hit.Candidate.Event), EventFingerprint.Receipt(again.Event));
        Assert.Equal(hit.Vector, new Hit(0, hit.State, hit.H0, hit.Pending, again, hit.Cursor).Vector);
        Assert.Equal(hit.Candidate.UnconfirmedStep.State.Plays.Last().Runs.Count, again.UnconfirmedStep.State.Plays.Last().Runs.Count);

        // the id comes from the cursor only: another cursor changes the id and nothing else
        var other = AdvancementPlanning.Resolve(hit.Pending, hit.State, hit.H0, Tuning, b, p, 7000).Value;
        Assert.Equal($"{Gid}:e007001", other.EventId);
        Assert.Equal(hit.Candidate.NextRng.Canonical(), other.NextRng.Canonical());
        Assert.Equal(hit.Vector, new Hit(0, hit.State, hit.H0, hit.Pending, other, 7000).Vector);
        Assert.Equal(EventFingerprint.Receipt(hit.Candidate.Event with { Id = other.EventId }), EventFingerprint.Receipt(other.Event));
    }

    [Fact]
    public void A_pending_cannot_be_mistaken_for_a_candidate_and_the_resolver_returns_the_sibling_candidate_type_only()
    {
        Assert.False(typeof(PendingAdvancement).IsAssignableFrom(typeof(InPlayPitchCandidate)));
        Assert.False(typeof(InPlayPitchCandidate).IsAssignableFrom(typeof(PendingAdvancement)));
        Assert.DoesNotContain(typeof(PendingAdvancement).GetMethods().Concat(typeof(InPlayPitchCandidate).GetMethods()), m => m.Name is "op_Implicit" or "op_Explicit");
        var resolve = typeof(AdvancementPlanning).GetMethods().Single(m => m.Name == "Resolve" && m.IsPublic);
        Assert.Equal(typeof(PendingAdvancement), resolve.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(Result<InPlayPitchCandidate>), resolve.ReturnType);
        var hit = One();
        var built = InPlayPitchPlanning.Build(hit.Pending.Plan.Prepared, hit.State, hit.H0, Tuning, Sheets(hit.State).Batter, Sheets(hit.State).Pitcher, hit.Cursor);
        Assert.IsType<PendingAdvancement>(built.Value);                                                   // the C2B-2 builder is unchanged: it still stops at the pending
    }

    // ================================================================ P-07: one logical draw per declared context, whatever the result

    [Fact]
    public void Every_resolution_draws_exactly_one_roll_per_declared_context_on_the_runner_stream_even_when_the_play_is_over_or_the_base_is_blocked()
    {
        foreach (var scenario in new[] { FirstAndSecond0, FirstAndSecond2, SecondOnly0 })
            foreach (var hit in scenario.Value.Hits.Take(150))
            {
                var k = hit.Pending.ContextKeys.Length;
                var (expected, rolls, raw) = DrawFrom(hit.Pending.RngAfterPitchDraw, k);
                Assert.Equal(k, hit.Detail.Decisions.Length);
                Assert.Equal(k, hit.Detail.LogicalDraws);
                Assert.Equal(expected.Canonical(), hit.Candidate.NextRng.Canonical());                  // the same K whole rolls, whatever was applied
                Assert.Equal(rolls, hit.Detail.Decisions.Select(d => d.Roll));
                Assert.True(hit.Detail.RawDraws >= k);                                                  // rejection sampling may use more raw outputs than calls
                Assert.Equal(raw, hit.Detail.RawDraws);

                // only the runner stream moved relative to H1
                var h1 = hit.Pending.RngAfterPitchDraw;
                Assert.Equal(h1.Streams.Length + 1, hit.Candidate.NextRng.Streams.Length);
                foreach (var s in h1.Streams)
                    Assert.Contains(hit.Candidate.NextRng.Streams, t => t.Stream.Canonical == s.Stream.Canonical && t.State == s.State && t.Draws == s.Draws);
            }

        // both kinds of "not as drawn" really occur in the scanned plays, so the fixed consumption was exercised for them
        Assert.Contains(FirstAndSecond2.Value.Hits, h => h.Detail.Decisions.Any(d => d.Applied == AdvancementOutcome.Void));
        Assert.Contains(FirstAndSecond0.Value.Hits, h => h.Detail.Decisions.Any(d => d.Reason == AdvancementReason.OccupiedBase));
    }

    [Fact]
    public void The_drawn_results_are_the_approved_rows_applied_to_the_exact_rolls_and_the_ppm_are_the_neutral_baseline()
    {
        var approved = new Dictionary<string, int[]>
        {
            ["single_runner_on_second"] = [590000, 380000, 30000],
            ["single_runner_on_first"] = [270000, 700000, 30000],
            ["double_runner_on_first"] = [560000, 400000, 40000],
            ["fly_out_runner_on_third"] = [700000, 280000, 20000],
        };
        foreach (var hit in FirstAndSecond0.Value.Hits.Concat(FirstDouble0.Value.Hits).Concat(ThirdFly0.Value.Hits).Take(900))
            foreach (var decision in hit.Detail.Decisions)
            {
                var row = Tuning.Baseline("advancement", decision.ContextKey).Value.Ppm;
                Assert.Equal(approved[decision.ContextKey], row.ToArray());                               // the tuning still holds the approved row
                Assert.Equal(row.ToArray(), decision.Ppm.ToArray());                                      // and the resolver used exactly that row: no modifier, no renormalisation
                var index = Distribution.Create(AdvancementOutcomes, row).Value.SelectIndex(decision.Roll).Value;
                var drawn = (AdvancementOutcome)index;                                                    // Extra = 0, Hold = 1, Out = 2 follow the row's outcome order
                Assert.Equal(drawn, decision.Drawn);
            }
    }

    [Fact]
    public void The_drawn_results_follow_the_approved_probabilities_for_a_lone_second_base_runner()
    {
        var hits = SecondOnly0.Value.Hits;
        Assert.True(hits.Count >= 280, $"{hits.Count} plays scanned");
        double n = hits.Count;
        foreach (var (outcome, p) in new[] { (AdvancementOutcome.Extra, 0.59), (AdvancementOutcome.Hold, 0.38), (AdvancementOutcome.Out, 0.03) })
        {
            var seen = hits.Count(h => h.Detail.Decisions[0].Drawn == outcome);
            var sigma = Math.Sqrt(n * p * (1 - p));
            Assert.True(Math.Abs(seen - n * p) <= 5 * sigma, $"{outcome}: {seen} of {n}, expected {n * p:F1} +- {5 * sigma:F1}");
        }

        Assert.All(hits, h => Assert.Equal(h.Detail.Decisions[0].Drawn, h.Detail.Decisions[0].Applied));   // a lone runner with an open target: applied as drawn
    }

    // ================================================================ composite plays: P-01 / P-02 / P-03

    [Fact]
    public void A_composite_play_is_resolved_lead_runner_first_and_every_reachable_combination_occurs_with_the_reducers_official_record()
    {
        var seen = new HashSet<string>();
        foreach (var hit in FirstAndSecond0.Value.Hits)
        {
            Assert.Equal(["single_runner_on_second", "single_runner_on_first"], hit.Detail.Decisions.Select(d => d.ContextKey).ToArray());   // 3 -> 2 -> 1 order
            Assert.Equal([1, 2], hit.Detail.Decisions.Select(d => d.Order).ToArray());
            Assert.Equal(["single_runner_on_first", "single_runner_on_second"], hit.Pending.ContextKeys.ToArray());                         // the stored order is untouched
            seen.Add(hit.Applied);

            var play = hit.Candidate.UnconfirmedStep.NewPlays.Single();
            Assert.Equal(hit.Detail.ReducerRuns, play.Runs.Count);
            Assert.Equal(hit.Detail.ReducerOutsAfter, play.OutsAfter);
            Assert.Equal(hit.Detail.EventResult, hit.Candidate.Event.Result);
            Assert.Equal(hit.Detail.Decisions.Count(d => d.Applied == AdvancementOutcome.Out), play.Moves.Count(m => m.To == 0));
        }

        // nobody out: the lead runner's result is applied before the trailing runner is judged, so only these seven combinations can ever be applied
        // (a lead runner who holds blocks third base: HE and HO do not exist, they are HH); the frequent ones all occur in the scanned plays
        string[] reachable = ["EE", "EH", "EO", "HH", "OE", "OH", "OO"];
        Assert.Empty(seen.Except(reachable));
        Assert.Subset(seen, new HashSet<string> { "EE", "EH", "EO", "HH", "OH" });
    }

    [Fact]
    public void A_blocked_extra_base_or_out_is_attributed_to_Hold_and_both_the_drawn_and_the_applied_result_are_kept()
    {
        var blocked = FirstAndSecond0.Value.Hits.Where(h => h.Detail.Decisions[1].Reason == AdvancementReason.OccupiedBase).ToList();
        Assert.NotEmpty(blocked);
        Assert.Contains(blocked, h => h.Detail.Decisions[1].Drawn == AdvancementOutcome.Extra);          // an extra base drawn onto an occupied base
        Assert.Contains(blocked, h => h.Detail.Decisions[1].Drawn == AdvancementOutcome.Out);            // a thrown-out draw onto an occupied base is impossible too
        foreach (var h in blocked)
        {
            var lead = h.Detail.Decisions[0];
            var trailing = h.Detail.Decisions[1];
            Assert.Equal(AdvancementOutcome.Hold, lead.Applied);                                          // the lead runner stayed on third
            Assert.Equal(AdvancementOutcome.Hold, trailing.Applied);
            Assert.Equal(trailing.PlanTo, trailing.FinalTo);                                              // he stays where the base plan put him
            Assert.NotEqual(trailing.Drawn, trailing.Applied);
        }
    }

    [Fact]
    public void A_decision_after_the_third_out_is_Void_not_Hold_while_its_roll_is_still_consumed_and_recorded()
    {
        var voided = FirstAndSecond2.Value.Hits.Where(h => h.Detail.Decisions.Any(d => d.Applied == AdvancementOutcome.Void)).ToList();
        Assert.NotEmpty(voided);
        Assert.Contains(voided, h => h.Detail.Decisions[1].Drawn == AdvancementOutcome.Extra);
        Assert.Contains(voided, h => h.Detail.Decisions[1].Drawn == AdvancementOutcome.Hold);            // even a drawn Hold becomes Void once the play is over: they are different facts
        foreach (var h in voided)
        {
            Assert.Equal(AdvancementOutcome.Out, h.Detail.Decisions[0].Applied);                          // the lead runner's out was the third out
            var trailing = h.Detail.Decisions[1];
            Assert.Equal((AdvancementOutcome.Void, AdvancementReason.PlayOver), (trailing.Applied, trailing.Reason));
            Assert.Equal(trailing.PlanTo, trailing.FinalTo);                                              // a Void runner stays on the base the plan gave him
            Assert.Equal(2, h.Detail.LogicalDraws);                                                       // ... and the roll is not saved
            Assert.Equal(3, h.Detail.ReducerOutsAfter);
            Assert.True(h.Detail.ReducerHalfOver);
        }
    }

    // ================================================================ third out and sequence (P-05)

    [Fact]
    public void A_run_before_a_non_force_third_out_counts_because_the_lead_runner_comes_first_and_the_reducers_numbers_agree()
    {
        var counted = FirstAndSecond2.Value.Hits.Where(h => h.Applied == "EO").ToList();
        Assert.NotEmpty(counted);
        foreach (var h in counted)
        {
            var play = h.Candidate.UnconfirmedStep.NewPlays.Single();
            Assert.Equal(3, play.OutsAfter);                                                              // the trailing runner is thrown out for the third out ...
            Assert.Equal(1, h.Detail.ReducerRuns);                                                        // ... after the lead runner's run, which counts
            Assert.Single(play.Runs);
            Assert.DoesNotContain(play.Moves, m => m.RunVoided);
            Assert.Equal(BatterResult.Single, h.Detail.ReducerBatterResult);
            Assert.Equal(1, h.Detail.ReducerRbi);
            Assert.True(h.Detail.ReducerHalfOver);
            // the explicit sequence: the lead runner's run is 1, the trailing runner's out is 2
            var moves = h.Candidate.Event.MoveList.OrderByDescending(m => m.From).ToList();
            Assert.Equal([(2, 4, 1), (1, 0, 2)], moves.Select(m => (m.From, m.To, m.Sequence!.Value)).ToArray());
            Assert.All(h.Detail.FinalMoves.Where(m => m.IsRun || m.IsOut), m => Assert.NotNull(m.Sequence));
        }
    }

    [Fact]
    public void A_lone_runner_thrown_out_at_home_is_an_ordinary_out_with_zero_one_or_two_outs_and_ends_the_inning_only_with_two()
    {
        foreach (var (scenario, outs) in new[] { (SecondOnly0, 0), (SecondOnly1, 1), (SecondOnly2, 2) })
        {
            var thrown = scenario.Value.Hits.Where(h => h.Applied == "O").ToList();
            Assert.NotEmpty(thrown);
            foreach (var h in thrown)
            {
                var play = h.Candidate.UnconfirmedStep.NewPlays.Single();
                Assert.Equal(outs + 1, play.OutsAfter);
                Assert.Equal(outs == 2, h.Detail.ReducerHalfOver);
                Assert.Empty(play.Runs);
                var move = h.Candidate.Event.MoveList.Single();
                Assert.Equal((2, 0, OutKind.Tag, 4), (move.From, move.To, move.OutKind, move.OutAt));
                var batterRecord = play.Moves.Single(m => m.From == 0);
                Assert.Equal(1, batterRecord.To);                                                          // the batter reached first on the single
                Assert.Equal(outs == 2 ? 1 : 0, h.Detail.ReducerLeftOnBase);                              // at the end of the inning only the batter-runner is left
            }
        }
    }

    // ================================================================ sacrifice fly (P-06)

    [Fact]
    public void A_fly_out_with_the_runner_from_third_scoring_has_an_Out_event_and_the_reducer_derives_the_sacrifice_fly()
    {
        var scored = First(ThirdFly0.Value, h => h.Applied == "E");
        var play = scored.Candidate.UnconfirmedStep.NewPlays.Single();
        Assert.Equal(InPlayResult.Out, scored.Candidate.Event.Result);                                    // InPlayResult has no SacrificeFly: the event input is Out
        Assert.Equal(BattedBallType.Fly, scored.Candidate.Event.Batted);
        Assert.Equal(BatterResult.SacrificeFly, play.Result);                                             // the official record is what the reducer derived
        Assert.Equal((InPlayResult.Out, BatterResult.SacrificeFly), (scored.Detail.EventResult, scored.Detail.ReducerBatterResult));
        Assert.False(scored.Detail.ReducerAtBat);                                                         // no at-bat
        Assert.Equal(1, scored.Detail.ReducerRbi);
        Assert.Equal(1, scored.Detail.ReducerRuns);
        Assert.Equal(1, scored.Detail.ReducerOutsAfter);
        Assert.Contains("batter_result_sacrifice_fly", scored.Detail.RecordChanges);                       // the audit says what changed from the base plan
        Assert.Equal(1, scored.Candidate.UnconfirmedStep.Delta.Entries.Where(e => e.Key == StatKey.BatSF).Sum(e => e.Amount));
        Assert.Equal(0, scored.Candidate.UnconfirmedStep.Delta.Entries.Where(e => e.Key == StatKey.BatAB).Sum(e => e.Amount));

        var held = First(ThirdFly0.Value, h => h.Applied == "H");
        Assert.Equal((InPlayResult.Out, BatterResult.Out, true, 0), (held.Detail.EventResult, held.Detail.ReducerBatterResult, held.Detail.ReducerAtBat, held.Detail.ReducerRuns));
        Assert.DoesNotContain("batter_result_sacrifice_fly", held.Detail.RecordChanges);

        var thrown = First(ThirdFly0.Value, h => h.Applied == "O");
        Assert.Equal((InPlayResult.Out, BatterResult.Out, 2), (thrown.Detail.EventResult, thrown.Detail.ReducerBatterResult, thrown.Detail.ReducerOutsAfter));
        Assert.Equal(0, thrown.Detail.ReducerRuns);                                                       // two outs on the play: not a sacrifice fly
    }

    [Fact]
    public void A_sacrifice_fly_that_scores_with_one_out_and_the_runner_thrown_out_for_the_third_out_are_kept_apart()
    {
        var scored = First(ThirdFly1.Value, h => h.Applied == "E");
        Assert.Equal(BatterResult.SacrificeFly, scored.Detail.ReducerBatterResult);
        Assert.Equal(2, scored.Detail.ReducerOutsAfter);
        Assert.False(scored.Detail.ReducerHalfOver);

        var thrown = First(ThirdFly1.Value, h => h.Applied == "O");
        Assert.Equal(3, thrown.Detail.ReducerOutsAfter);                                                  // batter's out + the runner's out = the third out, the inning is over
        Assert.True(thrown.Detail.ReducerHalfOver);
        Assert.Equal(0, thrown.Detail.ReducerRuns);
    }

    // ================================================================ walk-off (P-06)

    [Fact]
    public void An_extra_base_that_scores_the_winning_run_ends_the_game_names_the_winner_and_voids_the_trailing_decision()
    {
        var wins = First(WalkOffTiedFirstSecond.Value, h => h.Detail.Decisions[0].Applied == AdvancementOutcome.Extra);
        var winner = wins.Detail.WinningRunner!;
        Assert.Equal(InPlayScorerKind.ExistingRunner, winner.Kind);
        Assert.Equal(wins.Detail.Decisions[0].RunnerKey, winner.RunnerKey);                               // the runner from second, not the base plan's winner (there is none)
        Assert.Equal(2, winner.StartingBase);
        Assert.Null(wins.Pending.Plan.WinningRunner);                                                     // the base plan had no winner: it is recalculated from the final plan
        Assert.True(wins.Detail.GameEnded);
        Assert.Equal((GameStatus.Final, GameOutcome.HomeWin), (wins.Candidate.UnconfirmedStep.State.Status, wins.Candidate.UnconfirmedStep.State.Outcome));
        Assert.Equal(InPlayWalkOffEffect.GameEnds, wins.Candidate.Audit.WalkOffEffect);
        Assert.Contains("walkoff_game_ends", wins.Detail.RecordChanges);
        var trailing = wins.Detail.Decisions[1];
        Assert.Equal((AdvancementOutcome.Void, AdvancementReason.PlayOver), (trailing.Applied, trailing.Reason));   // nothing applies after the winning run
        Assert.Equal(trailing.PlanTo, trailing.FinalTo);
        Assert.Equal(2, wins.Detail.LogicalDraws);                                                        // ... but its roll was drawn
        Assert.Equal(DrawFrom(wins.Pending.RngAfterPitchDraw, 2).After.Canonical(), wins.Candidate.NextRng.Canonical());
        Assert.Equal(1, wins.Detail.ReducerRuns);
        Assert.Equal(BatterResult.Single, wins.Detail.ReducerBatterResult);                               // a single stays a single: the winner advanced as far as the hit
        Assert.Equal(InPlayResult.Single, wins.Candidate.Event.Result);
    }

    [Fact]
    public void A_failed_extra_base_in_a_walk_off_context_keeps_the_game_going_and_the_base_plans_hold_of_the_winner_does_not_survive()
    {
        var goesOn = First(WalkOffTiedFirstSecond.Value, h => h.Detail.Decisions[0].Applied == AdvancementOutcome.Hold);
        Assert.False(goesOn.Detail.GameEnded);
        Assert.Null(goesOn.Detail.WinningRunner);
        Assert.Equal(GameStatus.InProgress, goesOn.Candidate.UnconfirmedStep.State.Status);
        Assert.Equal(InPlayWalkOffEffect.None, goesOn.Candidate.Audit.WalkOffEffect);
        Assert.Equal(0, goesOn.Detail.ReducerRuns);

        var thrown = First(WalkOffTiedFirstSecond.Value, h => h.Detail.Decisions[0].Applied == AdvancementOutcome.Out);
        Assert.False(thrown.Detail.GameEnded);
        Assert.Equal(1, thrown.Detail.ReducerOutsAfter);
        Assert.NotEqual(AdvancementOutcome.Void, thrown.Detail.Decisions[1].Applied);                     // the trailing runner is judged normally, third base is free again
    }

    [Fact]
    public void A_sacrifice_fly_can_win_the_game_and_the_scorer_is_the_runner_from_third()
    {
        var wins = First(WalkOffTiedThirdFly.Value, h => h.Applied == "E");
        Assert.Equal(BatterResult.SacrificeFly, wins.Detail.ReducerBatterResult);
        Assert.Equal(InPlayResult.Out, wins.Candidate.Event.Result);
        Assert.True(wins.Detail.GameEnded);
        Assert.Equal(3, wins.Detail.WinningRunner!.StartingBase);
        Assert.Equal(wins.Detail.Decisions[0].RunnerKey, wins.Detail.WinningRunner.RunnerKey);
        Assert.Equal(GameOutcome.HomeWin, wins.Candidate.UnconfirmedStep.State.Outcome);
        Assert.Contains("batter_result_sacrifice_fly", wins.Detail.RecordChanges);
        Assert.Contains("walkoff_game_ends", wins.Detail.RecordChanges);
        Assert.False(wins.Detail.ReducerAtBat);
        Assert.Equal(1, wins.Detail.ReducerRbi);
    }

    [Fact]
    public void A_walk_off_context_and_a_double_with_the_runner_from_first_scoring_keep_the_credited_double()
    {
        var hits = FirstDouble0.Value.Hits;
        Assert.NotEmpty(hits);
        Assert.Contains(hits, h => h.Applied == "E");
        Assert.Contains(hits, h => h.Applied == "H");
        Assert.All(hits, h => Assert.Equal(InPlayResult.Double, h.Candidate.Event.Result));
        Assert.All(hits.Where(h => h.Applied == "E"), h => Assert.Equal(1, h.Detail.ReducerRuns));
    }

    // ================================================================ stale inputs (P-10): the existing CheckPrepared contract, in its existing order

    private static Result<InPlayPitchCandidate> Call(Hit hit, GameState? state = null, GameRandomSnapshot? rng = null, TuningProfile? tuning = null,
        PlayerSheet? batter = null, PlayerSheet? pitcher = null, int? cursor = null, PendingAdvancement? pending = null, Action<string>? hook = null)
    {
        var (b, p) = Sheets(hit.State);
        return AdvancementPlanning.Resolve(pending ?? hit.Pending, state ?? hit.State, rng ?? hit.H0, tuning ?? Tuning, batter ?? b, pitcher ?? p, cursor ?? hit.Cursor, hook, default);
    }

    [Fact]
    public void A_pending_made_for_other_inputs_is_refused_by_the_shared_stale_check_before_any_random_number_or_id_is_used()
    {
        var hit = One();
        var (b, p) = Sheets(hit.State);
        var otherTuning = TuningProfile.Create(TuningProfileTests.Draft() with { Provenance = TuningProfileTests.Draft().Provenance + "." }).Value;
        var advancedH0 = Snapshot(1, Gid, new Dictionary<string, int> { ["game/pitch@1"] = 1 });
        var rebuilt = Situation(3, 0).State;                                                              // an equal-looking but different state object (what a save/load gives)
        var otherGame = Situation(3, 0, gameId: "game:3c2b3:other");
        var automated = Situation(3, 0, rules: Automated);
        var seen = new List<string>();

        void Refused(string code, Result<InPlayPitchCandidate> result)
        {
            Assert.Equal(code, Code(result));
            Assert.Equal(["start"], seen);                                                                // nothing past the first stage ran: no H1 restore, no id, no draw
            seen.Clear();
        }

        void Hook(string s) => seen.Add(s);

        Refused("PitchCandidate.state_stale", Call(hit, state: rebuilt, hook: Hook));
        Refused("PitchCandidate.game_mismatch", Call(hit, state: otherGame.State, rng: Snapshot(1, "game:3c2b3:other"), hook: Hook));
        Refused("PitchCandidate.rng_context_mismatch", Call(hit, rng: Snapshot(1, "game:3c2b3:other"), hook: Hook));
        Refused("PitchCandidate.rng_state_changed", Call(hit, rng: advancedH0, hook: Hook));                                                   // H0 changed
        Refused("PitchCandidate.rng_state_changed", Call(hit, rng: hit.Pending.RngAfterPitchDraw, hook: Hook));                                // H1 is NOT the current authoritative rng
        Refused("PitchPrepare.batter_mismatch", Call(hit, batter: Sheet(hit.State.Fielding.Pitcher), hook: Hook));
        Refused("PitchPrepare.pitcher_mismatch", Call(hit, pitcher: Sheet(hit.State.Batting.CurrentBatterSlot.Player), hook: Hook));
        Refused("PitchCandidate.batter_side_changed", Call(hit, batter: Sheet(hit.State.Batting.CurrentBatterSlot.Player, BattingSide.Left), hook: Hook));
        Refused("PitchCandidate.pitcher_hand_changed", Call(hit, pitcher: Sheet(hit.State.Fielding.Pitcher, throws: ThrowingHand.Left), hook: Hook));
        Refused("PitchCandidate.rules_changed", Call(hit, state: automated.State, hook: Hook));
        Refused("PitchCandidate.tuning_changed", Call(hit, tuning: otherTuning, hook: Hook));
        Refused("PitchCandidate.prepared_missing", AdvancementPlanning.Resolve(null, hit.State, hit.H0, Tuning, b, p, hit.Cursor, Hook, default));
        Refused("PitchCandidate.state_missing", AdvancementPlanning.Resolve(hit.Pending, null, hit.H0, Tuning, b, p, hit.Cursor, Hook, default));
        Refused("PitchCandidate.rng_missing", AdvancementPlanning.Resolve(hit.Pending, hit.State, null, Tuning, b, p, hit.Cursor, Hook, default));
        Refused("PitchCandidate.tuning_missing", AdvancementPlanning.Resolve(hit.Pending, hit.State, hit.H0, null, b, p, hit.Cursor, Hook, default));
        Refused("PitchCandidate.batter_sheet_missing", AdvancementPlanning.Resolve(hit.Pending, hit.State, hit.H0, Tuning, null, p, hit.Cursor, Hook, default));
        Refused("PitchCandidate.pitcher_sheet_missing", AdvancementPlanning.Resolve(hit.Pending, hit.State, hit.H0, Tuning, b, null, hit.Cursor, Hook, default));

        // forged preparations (the way the C2B-1 tests reach a refusal that no real call can): a changed probability model and a draft contract nobody supports
        var prepared = hit.Pending.Plan.Prepared;
        PendingAdvancement Forge(PreparedPitch forged) => new(new BaseInPlayPlan(forged, hit.Pending.Plan.Facts));
        var model = new PreparedPitch(prepared.State, prepared.GameId, prepared.StartRngStateHash, prepared.BatterId, prepared.PitcherId, prepared.BatterDeclaredSide, prepared.PitcherThrows,
            prepared.Draft, prepared.RulesVersion, prepared.RulesHash, "prob-model-other", prepared.TuningVersion, prepared.TuningHash);
        Refused("PitchCandidate.probability_model_changed", Call(hit, pending: Forge(model), hook: Hook));
        var contract = new PreparedPitch(prepared.State, prepared.GameId, prepared.StartRngStateHash, prepared.BatterId, prepared.PitcherId, prepared.BatterDeclaredSide, prepared.PitcherThrows,
            prepared.Draft with { ContractVersion = "pitch-branch-v0" }, prepared.RulesVersion, prepared.RulesHash, prepared.ProbabilityModelVersion, prepared.TuningVersion, prepared.TuningHash);
        Refused("PitchCandidate.draft_contract_unsupported", Call(hit, pending: Forge(contract), hook: Hook));

        // none of the refusals touched the originals, and the untouched inputs still resolve to the same candidate
        Assert.NotNull(rebuilt);
        Assert.Equal(hit.H0.Canonical(), Snapshot(hit.Seed, Gid).Canonical());
        var ok = Call(hit);
        Assert.Equal(hit.Candidate.EventId, ok.Value.EventId);
        Assert.Equal(hit.Candidate.NextRng.Canonical(), ok.Value.NextRng.Canonical());
    }

    [Fact]
    public void The_event_id_boundary_is_checked_after_validation_with_the_same_codes_as_the_candidate_builder()
    {
        var hit = One();
        var seen = new List<string>();

        // the state's receipts hold e000001..: a cursor of 0 would issue e000001 again
        Assert.True(hit.State.AppliedEvents.Count > 0 && hit.State.AppliedEvents[0].EventId == $"{Gid}:e000001");
        Assert.Equal("InPlayCandidate.event_id_in_use", Code(Call(hit, cursor: 0, hook: seen.Add)));
        Assert.Equal(["start", "validated", "restored"], seen);                                           // validation and the H1 restore passed; no id was issued, nothing was drawn
        Assert.Equal("Id.sequence_invalid", Code(Call(hit, cursor: -1)));
        Assert.Equal("Id.sequence_invalid", Code(Call(hit, cursor: GameEventIds.MaxSequence + 1)));
        Assert.Equal("Id.sequence_exhausted", Code(Call(hit, cursor: GameEventIds.MaxSequence)));

        // a cursor far beyond the receipts leaves a gap in the numbering that this layer cannot see: it is accepted (3-C3 compares it with the real cursor)
        var gap = Call(hit, cursor: 500_000);
        Assert.True(gap.IsSuccess);
        Assert.Equal($"{Gid}:e500001", gap.Value.EventId);
    }

    [Fact]
    public void A_pending_with_an_unknown_context_or_an_unusable_base_plan_is_refused_after_validation_without_any_side_effect()
    {
        var hit = One();
        var facts = hit.Pending.Plan.Facts;
        PendingAdvancement WithFacts(InPlayPlanFacts f) => new(new BaseInPlayPlan(hit.Pending.Plan.Prepared, f));
        var before = (Snap(hit.State), hit.H0.Canonical(), hit.Pending.RngAfterPitchDraw.Canonical());

        Assert.Equal("Advancement.context_unknown", Code(Call(hit, pending: WithFacts(facts with { AdvancementKeys = ["double_runner_on_second"] }))));
        Assert.Equal("Advancement.context_duplicated", Code(Call(hit, pending: WithFacts(facts with { AdvancementKeys = ["single_runner_on_first", "single_runner_on_first"] }))));

        // a phantom runner on third in the base plan: the planner composes it, the UNCHANGED reducer refuses the event, and its error is passed through as it is
        var phantom = new InPlayRunnerPlan(999, hit.State.Batting.CurrentBatterSlot.Player, 3, 4, MoveCause.Play);
        var forged = facts with { Moves = facts.Moves.Add(phantom), Runs = facts.Runs + 1 };
        var refused = Call(hit, pending: WithFacts(forged));
        Assert.Equal("Game.move_source_invalid", Code(refused));

        Assert.Equal(before, (Snap(hit.State), hit.H0.Canonical(), hit.Pending.RngAfterPitchDraw.Canonical()));
        Assert.True(Call(hit).IsSuccess);
    }

    // ================================================================ cancellation and retry

    [Theory]
    [InlineData("start", 1)]
    [InlineData("validated", 1)]
    [InlineData("restored", 1)]
    [InlineData("id_issued", 1)]
    [InlineData("drawing", 1)]
    [InlineData("drawing", 2)]
    [InlineData("drawn", 1)]
    [InlineData("composed", 1)]
    [InlineData("event_built", 1)]
    [InlineData("before_reducer", 1)]
    [InlineData("after_reducer", 1)]
    [InlineData("verified", 1)]
    public void Cancelling_at_any_stage_returns_no_candidate_leaves_every_original_untouched_and_a_retry_gives_the_same_result(string stage, int occurrence)
    {
        var hit = One();
        var before = (Snap(hit.State), hit.H0.Canonical(), hit.Pending.RngAfterPitchDraw.Canonical(), hit.State.AppliedEvents.Count);
        using var source = new CancellationTokenSource();
        var seen = 0;
        var stages = new List<string>();
        var (b, p) = Sheets(hit.State);

        Assert.Throws<OperationCanceledException>(() => AdvancementPlanning.Resolve(hit.Pending, hit.State, hit.H0, Tuning, b, p, hit.Cursor, s =>
        {
            stages.Add(s);
            if (s == stage && ++seen == occurrence)
                source.Cancel();
        }, source.Token));

        Assert.Equal(before, (Snap(hit.State), hit.H0.Canonical(), hit.Pending.RngAfterPitchDraw.Canonical(), hit.State.AppliedEvents.Count));
        Assert.Equal(stage, stages[^1]);                                                                  // nothing ran past the cancelled stage

        var retry = AdvancementPlanning.Resolve(hit.Pending, hit.State, hit.H0, Tuning, b, p, hit.Cursor).Value;
        Assert.Equal(hit.Candidate.EventId, retry.EventId);
        Assert.Equal(EventFingerprint.Receipt(hit.Candidate.Event), EventFingerprint.Receipt(retry.Event));
        Assert.Equal(hit.Candidate.NextRng.Canonical(), retry.NextRng.Canonical());                       // the retry draws the identical rolls: there is no hidden re-roll
    }

    [Fact]
    public void A_call_with_an_already_cancelled_token_does_nothing()
    {
        var hit = One();
        var (b, p) = Sheets(hit.State);
        using var source = new CancellationTokenSource();
        source.Cancel();
        var stages = new List<string>();

        Assert.Throws<OperationCanceledException>(() => AdvancementPlanning.Resolve(hit.Pending, hit.State, hit.H0, Tuning, b, p, hit.Cursor, stages.Add, source.Token));

        Assert.Equal(["start"], stages);
    }

    [Fact]
    public void The_stages_run_in_the_documented_order_with_one_drawing_stage_per_decision()
    {
        var hit = One();
        var stages = new List<string>();

        Assert.True(Call(hit, hook: stages.Add).IsSuccess);

        Assert.Equal(["start", "validated", "restored", "id_issued", "drawing", "drawing", "drawn", "composed", "event_built", "before_reducer", "after_reducer", "verified"], stages);
    }

    // ================================================================ P-08 neutral abilities, boundaries

    [Fact]
    public void The_resolver_reads_no_player_ability_and_supplies_no_modifier_so_every_runner_has_the_same_approved_probabilities()
    {
        var root = Root;
        var sources = new[] { "AdvancementTypes.cs", "AdvancementRules.cs", "AdvancementPlanning.cs", "AdvancementEventBuilder.cs" }
            .Select(f => File.ReadAllText(Path.Combine(root, "src", "StarPlayer.Simulation", "Baseball", f))).ToList();
        foreach (var source in sources)
        {
            Assert.DoesNotContain("ModifierInput", source);
            Assert.DoesNotContain("Ratings", source.Replace("PlayerSheet", ""));
            Assert.DoesNotContain(".Speed", source);
            Assert.DoesNotContain(".Arm", source);
            Assert.DoesNotContain("PlayerState", source);
        }

        // and it never changes an approved number: the tuning file is byte-identical to what the policy validation used
        Assert.Equal("5b70dca79ec3a3c815f741b57c4c4490e93c5bcdca19900ad452732c66d73ea5", Tuning.ContentHash);
    }

    [Fact]
    public void The_resolver_never_touches_the_event_json_or_the_fingerprint_contract()
    {
        var hit = One();
        var receipt = EventFingerprint.Receipt(hit.Candidate.Event);
        Assert.Equal(EventFingerprint.VersionV2, receipt.FingerprintVersion);
        Assert.Equal(hit.Candidate.EventId, receipt.EventId);
        Assert.Equal(receipt, hit.Candidate.UnconfirmedStep.State.AppliedEvents.Last());
        // the audit is not part of the receipt: two candidates with different audits but the same event have the same receipt
        Assert.Equal(receipt, EventFingerprint.Receipt(hit.Candidate.Event with { }));
    }


    private static readonly string Root = PitchBranchFixture.Root;
}
