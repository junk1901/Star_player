using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Tests.AdvancementPolicy;
using StarPlayer.Simulation.Tests.InPlayFeasibility;
using Seq = StarPlayer.Simulation.Tests.AdvancementPolicy.AdvModel.SeqMode;

namespace StarPlayer.Simulation.Tests.Advancement;

/// <summary>
/// Phase 3-C2B-3, stage A: the PURE extra-base planner (contexts, sequential conditional composition, rules, event builder). No random number is involved: the drawn result of
/// every decision is an explicit input, so all 5,562 raw action vectors of the 1,314 approved pending cases can be fed in. The expected values come from the independent TEST-ONLY
/// judge of Phase 3-C2B-3-P (<see cref="AdvJudge"/>, written from the rules, not from the reducer) and from the unchanged production <see cref="GameReducer"/>; the production
/// code under test never produces an expected value.
/// </summary>
public class AdvancementPlannerTests
{
    private static readonly AdvancementOutcome[] AllDrawn = [AdvancementOutcome.Extra, AdvancementOutcome.Hold, AdvancementOutcome.Out];

    private static AdvancementOutcome ToOutcome(AdvAction a) => a switch
    {
        AdvAction.Extra => AdvancementOutcome.Extra, AdvAction.Hold => AdvancementOutcome.Hold, AdvAction.Out => AdvancementOutcome.Out, _ => AdvancementOutcome.Void,
    };

    private static AdvAction ToAction(AdvancementOutcome o) => o switch
    {
        AdvancementOutcome.Extra => AdvAction.Extra, AdvancementOutcome.Hold => AdvAction.Hold, AdvancementOutcome.Out => AdvAction.Out, _ => AdvAction.Void,
    };

    private static ImmutableArray<AdvancementContext> Contexts(PendingCase c)
    {
        var made = AdvancementPlanner.Contexts(c.Facts);
        Assert.True(made.IsSuccess, made.Error?.Code);
        return made.Value;
    }

    /// <summary>A raw vector as drawn outcomes aligned to the planner's processing order.</summary>
    private static AdvancementOutcome[] Drawn(PendingCase c, IReadOnlyDictionary<int, AdvAction> raw) =>
        Contexts(c).Select(x => ToOutcome(raw[x.StartBase])).ToArray();

    private static AdvancementComposition Compose(PendingCase c, IReadOnlyList<AdvancementOutcome> drawn)
    {
        var made = AdvancementPlanner.Compose(c.Facts, c.State, drawn);
        Assert.True(made.IsSuccess, made.Error?.Code + " " + made.Error?.Message);
        return made.Value;
    }

    /// <summary>The applied outcomes of a composition keyed by start base (what the test-only judge takes).</summary>
    private static Dictionary<int, AdvAction> Applied(AdvancementComposition comp) => comp.Steps.ToDictionary(s => s.Context.StartBase, s => ToAction(s.Applied));

    private static (InPlayEvent Event, Result<GameStep> Applied) Reduce(PendingCase c, AdvancementComposition comp)
    {
        var evt = AdvancementEventBuilder.Build(c.Facts, comp, "p1", FeasibilityHarness.Location);
        return (evt, GameReducer.Apply(c.State, evt));
    }

    private static PendingCase Find(Func<PendingCase, bool> where) => AdvCases.All.First(where);

    private static bool IsSingle(PendingCase c) => c.Facts.Official.Result == InPlayResult.Single;

    // ================================================================ P-01: contexts and the lead-runner-first draw order

    [Fact]
    public void Contexts_are_in_lead_runner_first_order_even_though_the_stored_keys_are_trailing_runner_first()
    {
        var composite = AdvCases.All.Where(c => c.K == 2).ToList();
        Assert.Equal(270, composite.Count);
        foreach (var c in composite)
        {
            Assert.Equal(["single_runner_on_first", "single_runner_on_second"], c.Facts.AdvancementKeys.ToArray());   // the C2B-2 storage order is not changed
            var contexts = Contexts(c);
            Assert.Equal(["single_runner_on_second", "single_runner_on_first"], contexts.Select(x => x.Key).ToArray());
            Assert.Equal([2, 1], contexts.Select(x => x.StartBase).ToArray());
            Assert.Equal([1, 2], contexts.Select(x => x.Order).ToArray());
        }
    }

    [Fact]
    public void Every_declared_context_appears_exactly_once_and_the_order_is_the_same_on_every_call()
    {
        foreach (var c in AdvCases.All)
        {
            var first = Contexts(c);
            var second = Contexts(c);
            Assert.Equal(c.Facts.AdvancementKeys.Length, first.Length);
            Assert.Equal(c.Facts.AdvancementKeys.OrderBy(k => k, StringComparer.Ordinal), first.Select(x => x.Key).OrderBy(k => k, StringComparer.Ordinal));
            Assert.Equal(first.Length, first.Select(x => x.RunnerKey).Distinct().Count());
            Assert.Equal(first.Select(x => (x.Key, x.RunnerKey, x.StartBase, x.PlanTo, x.TargetTo)), second.Select(x => (x.Key, x.RunnerKey, x.StartBase, x.PlanTo, x.TargetTo)));
            Assert.Equal(first.OrderByDescending(x => x.StartBase).Select(x => x.Key), first.Select(x => x.Key));
        }
    }

    [Fact]
    public void A_context_describes_the_decision_the_independent_model_derives_from_the_same_base_plan()
    {
        foreach (var c in AdvCases.All)
        {
            var contexts = Contexts(c);
            foreach (var x in contexts)
            {
                var model = c.Decisions.Single(d => d.Key == x.Key);
                Assert.Equal((model.RunnerKey, model.Runner, model.StartBase, model.PlanTo, model.TargetTo), (x.RunnerKey, x.Runner, x.StartBase, x.PlanTo, x.TargetTo));
                Assert.Equal(model.Retriever, x.Retriever);
                Assert.Equal(model.BaseFielder, x.BaseFielder);
                Assert.Equal(c.Facts.Moves.Single(m => m.From == x.StartBase).To, x.PlanTo);   // the base plan already put him there; the attempt is one base further
                Assert.Equal(x.PlanTo + 1, x.TargetTo);
            }
        }
    }

    [Fact]
    public void An_unknown_context_key_or_a_missing_runner_is_a_failure_not_a_guess()
    {
        var c = Find(x => x.K == 1);
        var unknown = c.Facts with { AdvancementKeys = ["double_runner_on_second"] };
        Assert.Equal("Advancement.context_unknown", AdvancementPlanner.Contexts(unknown).Error!.Value.Code);
        var duplicated = c.Facts with { AdvancementKeys = [c.Facts.AdvancementKeys[0], c.Facts.AdvancementKeys[0]] };
        Assert.Equal("Advancement.context_duplicated", AdvancementPlanner.Contexts(duplicated).Error!.Value.Code);
        var noRunner = c.Facts with { Moves = c.Facts.Moves.RemoveAll(m => m.From == Contexts(c)[0].StartBase) };
        Assert.Equal("Advancement.runner_missing", AdvancementPlanner.Contexts(noRunner).Error!.Value.Code);
    }

    // ================================================================ P-02 / P-03 / P-04: composition over all 5,562 raw vectors

    [Fact]
    public void The_rules_pre_check_flags_exactly_the_840_vectors_the_independent_judge_calls_illegal_and_none_of_the_4722_legal_ones()
    {
        int total = 0, legal = 0, illegal = 0, illegalReducerAccepted = 0;
        foreach (var c in AdvCases.All)
            foreach (var (label, raw) in AdvCases.RawVectors(c))
            {
                total++;
                var judged = AdvJudge.Judge(c.State, c.Facts, c.Decisions, raw, Seq.Lead);
                var violations = AdvancementRules.Check(c.Facts, c.State, Contexts(c), Drawn(c, raw));
                Assert.True(judged.Legal == violations.IsEmpty, $"{c.Key} {label}: judge {string.Join(",", judged.Violations)} rules {string.Join(",", violations)}");
                if (judged.Legal)
                {
                    legal++;
                    continue;
                }

                illegal++;
                // "the reducer accepted it" is NOT "it is legal": count the vectors the rules refuse although the unchanged reducer takes them
                if (AdvReducer.Reduce(c.State, AdvModel.BuildEvent(c.Facts, c.Decisions, raw, Seq.Lead)).Ok)
                    illegalReducerAccepted++;
            }

        Assert.Equal(5562, total);
        Assert.Equal(4722, legal);
        Assert.Equal(840, illegal);
        Assert.Equal(480, illegalReducerAccepted);
    }

    [Fact]
    public void A_legal_raw_vector_is_composed_unchanged_and_an_illegal_one_is_always_changed_into_a_legal_play()
    {
        int unchanged = 0, repaired = 0, hold = 0, voided = 0;
        foreach (var c in AdvCases.All)
            foreach (var (label, raw) in AdvCases.RawVectors(c))
            {
                var judgedRaw = AdvJudge.Judge(c.State, c.Facts, c.Decisions, raw, Seq.Lead);
                var drawn = Drawn(c, raw);
                var comp = Compose(c, drawn);

                Assert.Equal(drawn.Length, comp.Steps.Length);
                Assert.Equal(drawn, comp.Steps.Select(s => s.Drawn));                                     // the raw draw is recorded as drawn, never rewritten
                // a draw MOVES a runner when it is Extra or Out; a drawn Hold moves nobody
                var movedDifferently = comp.Steps.Any(s => s.Drawn != AdvancementOutcome.Hold && s.Applied != s.Drawn);
                if (judgedRaw.Legal)
                {
                    Assert.False(movedDifferently, $"{c.Key} {label}: a legal draw must be applied as drawn");
                    // the only difference a legal vector can show is a drawn Hold after the play was over: that is Void (P-02), kept apart from a Hold
                    Assert.All(comp.Steps.Where(s => s.Applied != s.Drawn), s => Assert.Equal((AdvancementOutcome.Hold, AdvancementOutcome.Void, AdvancementReason.PlayOver), (s.Drawn, s.Applied, s.Reason)));
                    Assert.All(comp.Steps.Where(s => s.Applied == s.Drawn), s => Assert.Equal(AdvancementReason.None, s.Reason));
                    unchanged++;
                }
                else
                {
                    Assert.True(movedDifferently, $"{c.Key} {label}: an illegal draw must not be applied as drawn");
                    repaired++;
                }

                foreach (var s in comp.Steps.Where(s => s.Applied != s.Drawn))
                {
                    if (s.Applied == AdvancementOutcome.Hold) { Assert.Equal(AdvancementReason.OccupiedBase, s.Reason); hold++; }
                    else { Assert.Equal(AdvancementOutcome.Void, s.Applied); Assert.Equal(AdvancementReason.PlayOver, s.Reason); voided++; }
                }

                // whatever was composed is a legal play by the independent judge and by the rules, and the unchanged reducer takes it and agrees on every counted fact
                Assert.True(AdvancementRules.Check(c.Facts, c.State, Contexts(c), comp.Steps.Select(s => s.Applied).ToArray()).IsEmpty, $"{c.Key} {label}");
                var judged = AdvJudge.Judge(c.State, c.Facts, c.Decisions, Applied(comp), Seq.Lead);
                Assert.True(judged.Legal, $"{c.Key} {label}: {string.Join(",", judged.Violations)}");
                var (evt, applied) = Reduce(c, comp);
                var row = AdvReducer.Reduce(c.State, evt);
                Assert.Equal("LEGAL_ACCEPTED", AdvReducer.Classify(judged, row));
                Assert.Equal(judged.CountedRuns, comp.Runs);
                Assert.True(comp.OutsAfter <= c.State.Rules.OutsPerHalfInning, $"{c.Key} {label}: a composed play never has a fourth out");
                Assert.Equal(judged.OutsAfter, comp.OutsAfter);
                Assert.Equal(judged.HalfOver, comp.HalfOver);
                Assert.Equal(judged.WalkOffReached, comp.WalkOffReached);
                Assert.Equal(judged.WinnerKey, comp.Winner?.RunnerKey ?? -1);
                Assert.Null(AdvancementEventBuilder.Verify(c.Facts, c.State, comp, evt, applied, out _));
            }

        Assert.Equal(4722, unchanged);
        Assert.Equal(840, repaired);
        Assert.True(hold > 0 && voided > 0);                                                              // both kinds of repair really occur
    }

    [Fact]
    public void Hold_and_Void_are_different_facts_blocked_base_versus_play_over()
    {
        // first AND second occupied, two outs, an official single: the lead runner (second) is processed first
        var c = Find(x => x.K == 2 && !x.WalkOffRoot && x.Occupancy == 3 && x.Outs == 2 && IsSingle(x));

        // lead runner thrown out at home = the third out; the trailing runner's drawn extra base no longer applies: VOID because the play is over
        var over = Compose(c, [AdvancementOutcome.Out, AdvancementOutcome.Extra]);
        Assert.Equal([AdvancementOutcome.Out, AdvancementOutcome.Void], over.Steps.Select(s => s.Applied).ToArray());
        Assert.Equal([AdvancementReason.None, AdvancementReason.PlayOver], over.Steps.Select(s => s.Reason).ToArray());
        Assert.True(over.HalfOver);

        // lead runner holds on third: the trailing runner's base is occupied: HOLD (attributed) because of the occupied base, the play goes on
        var blocked = Compose(c, [AdvancementOutcome.Hold, AdvancementOutcome.Extra]);
        Assert.Equal([AdvancementOutcome.Hold, AdvancementOutcome.Hold], blocked.Steps.Select(s => s.Applied).ToArray());
        Assert.Equal([AdvancementReason.None, AdvancementReason.OccupiedBase], blocked.Steps.Select(s => s.Reason).ToArray());
        Assert.False(blocked.HalfOver);

        // the same drawn Out on an occupied base is impossible too (P-02 R1 couples the attempt): Hold, never a thrown-out runner on a base a teammate stands on
        var blockedOut = Compose(c, [AdvancementOutcome.Hold, AdvancementOutcome.Out]);
        Assert.Equal(AdvancementOutcome.Hold, blockedOut.Steps[1].Applied);
        Assert.Equal(AdvancementReason.OccupiedBase, blockedOut.Steps[1].Reason);
        Assert.Equal(0, blockedOut.Moves.Count(m => m.IsOut));
        // a drawn Hold on an occupied base is a plain Hold: nothing to attribute
        Assert.Equal(AdvancementReason.None, Compose(c, [AdvancementOutcome.Hold, AdvancementOutcome.Hold]).Steps[1].Reason);
    }

    [Fact]
    public void The_lead_runners_result_is_applied_before_the_trailing_runner_is_judged_so_a_vacated_base_can_be_attempted()
    {
        var c = Find(x => x.K == 2 && !x.WalkOffRoot && x.Occupancy == 3 && x.Outs == 0 && IsSingle(x));

        // lead runner scores: third is free again, so the trailing runner may now take it or be thrown out there
        var extra = Compose(c, [AdvancementOutcome.Extra, AdvancementOutcome.Extra]);
        Assert.Equal([AdvancementOutcome.Extra, AdvancementOutcome.Extra], extra.Steps.Select(s => s.Applied).ToArray());
        Assert.Equal(1, extra.Runs);
        var thrown = Compose(c, [AdvancementOutcome.Extra, AdvancementOutcome.Out]);
        Assert.Equal([AdvancementOutcome.Extra, AdvancementOutcome.Out], thrown.Steps.Select(s => s.Applied).ToArray());
        Assert.Equal((1, 1), (thrown.Runs, thrown.OutsAfter));

        // every move is described once, no base holds two runners and nobody passed another
        foreach (var comp in new[] { extra, thrown })
        {
            Assert.Equal(c.State.Bases.Count, comp.Moves.Length);
            Assert.Equal(comp.Moves.Length, comp.Moves.Select(m => m.RunnerKey).Distinct().Count());
            var landing = comp.Moves.Where(m => m.To is >= 1 and <= 3).Select(m => m.To).ToList();
            Assert.Equal(landing.Count, landing.Distinct().Count());
        }
    }

    // ================================================================ P-05: explicit sequence, lead runner first

    [Fact]
    public void Every_out_and_run_of_a_play_carries_an_explicit_sequence_lead_runner_first_and_a_play_without_any_carries_none()
    {
        foreach (var c in AdvCases.All)
            foreach (var (_, raw) in AdvCases.RawVectors(c))
            {
                var comp = Compose(c, Drawn(c, raw));
                var terminals = comp.Moves.Where(m => m.IsRun || m.IsOut).OrderByDescending(m => m.From).ToList();
                if (terminals.Count == 0)
                {
                    Assert.All(comp.Moves, m => Assert.Null(m.Sequence));
                    continue;
                }

                Assert.Equal(Enumerable.Range(1, terminals.Count), terminals.Select(m => m.Sequence!.Value));
                Assert.All(comp.Moves.Where(m => !m.IsRun && !m.IsOut), m => Assert.Null(m.Sequence));
            }
    }

    [Fact]
    public void The_318_legal_vectors_with_a_run_and_a_third_out_count_their_run_only_because_of_the_lead_first_sequence()
    {
        int sensitive = 0;
        foreach (var c in AdvCases.All)
            foreach (var (label, raw) in AdvCases.RawVectors(c))
            {
                var judged = AdvJudge.Judge(c.State, c.Facts, c.Decisions, raw, Seq.Lead);
                if (!judged.Legal || !judged.HalfOver || judged.CountedRuns == 0)
                    continue;
                sensitive++;

                var comp = Compose(c, Drawn(c, raw));
                var (evt, applied) = Reduce(c, comp);
                Assert.True(applied.IsSuccess, $"{c.Key} {label}");
                Assert.Equal(judged.CountedRuns, applied.Value.NewPlays.Single().Runs.Count);              // the production sequence: the runs before the third out count
                Assert.Equal(judged.CountedRuns, comp.Runs);
                Assert.Null(AdvancementEventBuilder.Verify(c.Facts, c.State, comp, evt, applied, out _));

                // the same facts with the sequence reversed (trailing runner's out first): every one of those runs is void. The Sequence decides the official record.
                var reversed = AdvModel.BuildEvent(c.Facts, c.Decisions, Applied(comp), Seq.Trail);
                var other = GameReducer.Apply(c.State, reversed);
                Assert.True(other.IsSuccess, $"{c.Key} {label}");
                Assert.Empty(other.Value.NewPlays.Single().Runs);
                // the verification refuses that reducer result for this composition, and it is the counted runs that it names
                var refusal = AdvancementEventBuilder.Verify(c.Facts, c.State, comp, reversed, other, out var none)!.Value;
                Assert.Equal("Advancement.official_record_invalid", refusal.Code);
                Assert.Contains("counted runs", refusal.Message);
                Assert.Null(none);
            }

        Assert.Equal(318, sensitive);
    }

    [Fact]
    public void A_third_out_voids_the_runs_when_it_is_a_force_out_or_the_batter_runner_before_first_and_otherwise_only_the_runs_made_before_it()
    {
        Terminal Run(int key, int sequence) => new(key, true, false, sequence, OutClassification.NonForce, false);
        Terminal Out(int key, int sequence, OutClassification cls) => new(key, false, true, sequence, cls, false);

        // no third out: every run counts
        Assert.Equal([1, 2], AdvancementRules.CountedRuns(0, 3, [Run(1, 1), Run(2, 2)]).OrderBy(k => k));
        // a non-force third out: only the runs made BEFORE it (the lead runner's run, the trailing runner's tag-out afterwards)
        Assert.Equal([1], AdvancementRules.CountedRuns(2, 3, [Run(1, 1), Out(2, 2, OutClassification.NonForce)]).OrderBy(k => k));
        // the same facts in the other order: the run is after the third out
        Assert.Empty(AdvancementRules.CountedRuns(2, 3, [Out(2, 1, OutClassification.NonForce), Run(1, 2)]));
        // a force out as the third out voids every run, whatever the order
        Assert.Empty(AdvancementRules.CountedRuns(2, 3, [Run(1, 1), Out(2, 2, OutClassification.Force)]));
        // the batter-runner retired before reaching first as the third out voids every run
        Assert.Empty(AdvancementRules.CountedRuns(2, 3, [Run(1, 1), Out(9, 2, OutClassification.BatterRunner)]));
        // two outs on the play, the second one is the third out; a run between them counts, one after does not
        Assert.Equal([1], AdvancementRules.CountedRuns(1, 3, [Out(8, 1, OutClassification.NonForce), Run(1, 2), Out(2, 3, OutClassification.NonForce), Run(3, 4)]).OrderBy(k => k));
    }

    [Fact]
    public void The_counted_run_rule_agrees_with_the_unchanged_reducer_for_a_force_third_out_and_a_batter_runner_third_out()
    {
        // two outs, first and third occupied. Hand-built events (the four approved contexts never make these outs): the reducer's verdict is the expected value of the rule.
        var s = InPlayStates.Make(InPlayStates.Root.GeneralTop, 0, 5, 2);
        var first = s.Bases.First!;
        var third = s.Bases.Third!;
        var credits = System.Collections.Immutable.ImmutableList.Create(new PositionCredit(FieldPosition.Shortstop, CreditKind.Assist), new PositionCredit(FieldPosition.SecondBase, CreditKind.Putout));

        // a force out at second as the third out: the lead runner's run (made first) does NOT count, whatever the order
        var forceOut = new InPlayEvent("f1", BattedBallType.Ground, InPlayResult.FieldersChoice, false,
            System.Collections.Immutable.ImmutableList.Create(
                new RunnerMove(3, 4, MoveCause.Play, Sequence: 1),
                new RunnerMove(1, 0, MoveCause.Play, OutKind.Force, credits, 2, 2, OutMethod.Tag)), Location: FeasibilityHarness.Location);
        var forced = GameReducer.Apply(s, forceOut);
        Assert.True(forced.IsSuccess, forced.Error?.Code);
        Assert.Empty(forced.Value.NewPlays.Single().Runs);
        Assert.Contains(forced.Value.NewPlays.Single().Moves, m => m.RunnerKey == third.RunnerKey && m.RunVoided);
        Assert.Empty(AdvancementRules.CountedRuns(2, 3, [new Terminal(third.RunnerKey, true, false, 1, OutClassification.NonForce, false), new Terminal(first.RunnerKey, false, true, 2, OutClassification.Force, false)]));

        // the batter-runner caught for the third out (a fly ball): the runner's run after it does not count
        var caught = new InPlayEvent("f2", BattedBallType.Fly, InPlayResult.Out, false,
            System.Collections.Immutable.ImmutableList.Create(new RunnerMove(3, 4, MoveCause.Play, Sequence: 1), new RunnerMove(1, 1, MoveCause.Play)),
            System.Collections.Immutable.ImmutableList.Create(new PositionCredit(FieldPosition.LeftField, CreditKind.Putout)), Location: FeasibilityHarness.Location);
        var batterOut = GameReducer.Apply(s, caught);
        Assert.True(batterOut.IsSuccess, batterOut.Error?.Code);
        Assert.Empty(batterOut.Value.NewPlays.Single().Runs);
        Assert.Empty(AdvancementRules.CountedRuns(2, 3, [new Terminal(99, false, true, 0, OutClassification.BatterRunner, false), new Terminal(third.RunnerKey, true, false, 1, OutClassification.NonForce, false)]));
    }

    // ================================================================ rules, one by one

    [Fact]
    public void The_rules_check_names_each_forbidden_situation()
    {
        var c = Find(x => x.K == 2 && !x.WalkOffRoot && x.Occupancy == 3 && x.Outs == 2 && IsSingle(x));
        var contexts = Contexts(c);
        string[] Codes(params AdvancementOutcome[] a) => AdvancementRules.Check(c.Facts, c.State, contexts, a).ToArray();

        Assert.Empty(Codes(AdvancementOutcome.Hold, AdvancementOutcome.Hold));
        Assert.Contains(AdvancementRules.AttemptIntoOccupiedBase, Codes(AdvancementOutcome.Hold, AdvancementOutcome.Extra));       // 3 is occupied
        Assert.Contains(AdvancementRules.AttemptIntoOccupiedBase, Codes(AdvancementOutcome.Hold, AdvancementOutcome.Out));         // being thrown out at an occupied base
        Assert.Contains(AdvancementRules.MoveAfterPlayEnded, Codes(AdvancementOutcome.Out, AdvancementOutcome.Extra));             // third out already made
        Assert.Empty(Codes(AdvancementOutcome.Out, AdvancementOutcome.Hold));
        Assert.Empty(Codes(AdvancementOutcome.Out, AdvancementOutcome.Void));                                                       // Void is a Hold-like no-move
        Assert.Contains(AdvancementRules.BaseOccupiedTwice, Codes(AdvancementOutcome.Hold, AdvancementOutcome.Extra));             // the end position check names the shared base as well

        // four outs: two outs before the play, so the lead runner's out is the third and the trailing runner's out would be a fourth: a move after the end AND too many outs
        var fourth = Codes(AdvancementOutcome.Out, AdvancementOutcome.Out);
        Assert.Contains(AdvancementRules.MoveAfterPlayEnded, fourth);
        Assert.Contains(AdvancementRules.TooManyOuts, fourth);
    }

    [Fact]
    public void The_rules_catch_a_passed_runner_a_duplicate_action_a_missing_source_and_a_wrong_target_in_hand_built_contexts()
    {
        var c = Find(x => x.K == 2 && !x.WalkOffRoot && x.Occupancy == 3 && x.Outs == 0 && IsSingle(x));
        var real = Contexts(c);
        string[] Codes(IReadOnlyList<AdvancementContext> ctx, params AdvancementOutcome[] a) => AdvancementRules.Check(c.Facts, c.State, ctx, a).ToArray();

        // a runner the base plan does not hold on the stated base
        var ghost = real[0] with { RunnerKey = real[0].RunnerKey + 99 };
        Assert.Contains(AdvancementRules.SourceRunnerMissing, Codes([ghost, real[1]], AdvancementOutcome.Hold, AdvancementOutcome.Hold));
        // an attempt that is not exactly one base further
        var far = real[1] with { TargetTo = 4 };
        Assert.Contains(AdvancementRules.TargetInvalid, Codes([real[0], far], AdvancementOutcome.Hold, AdvancementOutcome.Hold));
        // the same runner decided twice
        Assert.Contains(AdvancementRules.DuplicateRunnerAction, Codes([real[0], real[0] with { Order = 2 }], AdvancementOutcome.Hold, AdvancementOutcome.Hold));
        // a count of drawn results that is not the count of contexts
        Assert.Contains(AdvancementRules.ActionCountMismatch, Codes(real, AdvancementOutcome.Hold));
        // overtaking: the trailing runner (started on first) goes home while the lead runner (started on second) stays on third (a hand-built, inconsistent base plan)
        var passing = c.Facts with { Moves = [.. c.Facts.Moves.Select(m => m.From == 1 ? m with { To = 3 } : m)] };
        var trailingHome = real[1] with { PlanTo = 3, TargetTo = 4 };
        Assert.Contains(AdvancementRules.RunnerPassedRunner, AdvancementRules.Check(passing, c.State, [trailingHome], [AdvancementOutcome.Extra]).ToArray());
        Assert.Contains(AdvancementRules.BaseOccupiedTwice, AdvancementRules.Check(passing, c.State, [trailingHome], [AdvancementOutcome.Hold]).ToArray());
    }

    [Fact]
    public void A_walk_off_ends_the_play_at_the_winning_run_and_the_trailing_decision_is_void_while_a_failed_attempt_keeps_the_game_going()
    {
        // bottom of the 9th, tied, first and second occupied, an official single: one run wins the game, the lead runner (second) is the extra-base candidate
        var c = Find(x => x.WalkOffRoot && x.Diff == 0 && x.K == 2 && x.Outs == 0 && x.Occupancy == 3 && IsSingle(x));
        Assert.Equal(1, c.Facts.RunsNeeded);
        Assert.Equal(0, c.Facts.Runs);

        var wins = Compose(c, [AdvancementOutcome.Extra, AdvancementOutcome.Extra]);
        Assert.True(wins.WalkOffReached);
        Assert.Equal(AdvancementOutcome.Void, wins.Steps[1].Applied);                                      // after the winning run nothing else applies
        Assert.Equal(AdvancementReason.PlayOver, wins.Steps[1].Reason);
        Assert.Equal(AdvancementOutcome.Extra, wins.Steps[1].Drawn);                                       // ... but the drawn result is still recorded
        Assert.Equal(InPlayScorerKind.ExistingRunner, wins.Winner!.Kind);
        Assert.Equal(c.Decisions.Single(d => d.StartBase == 2).RunnerKey, wins.Winner.RunnerKey);
        Assert.Equal(2, wins.Winner.StartingBase);
        var (evt, applied) = Reduce(c, wins);
        Assert.True(applied.IsSuccess);
        Assert.Equal((GameStatus.Final, GameOutcome.HomeWin), (applied.Value.State.Status, applied.Value.State.Outcome));
        Assert.Null(AdvancementEventBuilder.Verify(c.Facts, c.State, wins, evt, applied, out var record));
        Assert.True(record!.GameEnded);
        Assert.Equal(BatterResult.Single, record.Result);                                                  // the credited base does not change: the winner advanced as far as the hit

        // the lead runner is held or thrown out: the game goes on, the trailing runner is judged normally
        var holds = Compose(c, [AdvancementOutcome.Hold, AdvancementOutcome.Hold]);
        Assert.False(holds.WalkOffReached);
        Assert.Null(holds.Winner);
        var out1 = Compose(c, [AdvancementOutcome.Out, AdvancementOutcome.Extra]);
        Assert.False(out1.WalkOffReached);
        Assert.Equal(AdvancementOutcome.Extra, out1.Steps[1].Applied);                                      // third base is free again
    }

    [Fact]
    public void A_walk_off_winner_is_the_needth_run_in_sequence_order_for_every_walk_off_vector()
    {
        int walkOffs = 0;
        foreach (var c in AdvCases.All.Where(x => x.WalkOffRoot))
            foreach (var (label, raw) in AdvCases.RawVectors(c))
            {
                var comp = Compose(c, Drawn(c, raw));
                var judged = AdvJudge.Judge(c.State, c.Facts, c.Decisions, Applied(comp), Seq.Lead);
                Assert.Equal(judged.WalkOffReached, comp.WalkOffReached);
                if (!comp.WalkOffReached)
                {
                    Assert.Null(comp.Winner);
                    continue;
                }

                walkOffs++;
                var runs = comp.Moves.Where(m => m.IsRun).OrderBy(m => m.Sequence).ToList();
                Assert.Equal(runs[c.Facts.RunsNeeded - 1].RunnerKey, comp.Winner!.RunnerKey);
                Assert.Equal(runs[c.Facts.RunsNeeded - 1].From, comp.Winner.StartingBase);
                Assert.Equal(judged.WinnerKey, comp.Winner.RunnerKey);
                Assert.Equal(c.Facts.RunsNeeded, runs.Count);                                               // no run after the winning run on a non-home-run play
                // walkoff-credit-v1: the winner advanced at least as far as the hit gave, so the credited base cannot shrink and no record change is needed (OBR 9.06(f) basis)
                var bases = InPlayOfficialCredit.BasesOf(c.Facts.Official.Result);
                Assert.Equal(bases, Math.Min(bases, 4 - comp.Winner.StartingBase));
            }

        Assert.True(walkOffs > 0);
    }

    [Fact]
    public void A_run_after_the_winning_run_is_flagged_even_though_the_four_contexts_cannot_make_one()
    {
        // bottom of the 9th, away ahead by one, bases loaded, nobody out, an official single: the base plan scores the runner from third (1 run), two are needed
        var c = Find(x => x.WalkOffRoot && x.Diff == 1 && x.K == 2 && x.Occupancy == 7 && x.Outs == 0 && IsSingle(x));
        Assert.Equal((1, 2), (c.Facts.Runs, c.Facts.RunsNeeded));
        Assert.Empty(AdvancementRules.Check(c.Facts, c.State, Contexts(c), [AdvancementOutcome.Extra, AdvancementOutcome.Hold]));   // the second run wins: legal

        // had one run been enough, the lead runner's extra base would be a run after the winning run, and a move after the play was over
        var oneEnough = c.Facts with { RunsNeeded = 1 };
        var codes = AdvancementRules.Check(oneEnough, c.State, Contexts(c), [AdvancementOutcome.Extra, AdvancementOutcome.Hold]).ToArray();
        Assert.Contains(AdvancementRules.RunAfterWinningRun, codes);
        Assert.Contains(AdvancementRules.MoveAfterPlayEnded, codes);
    }

    [Fact]
    public void A_scoring_play_that_also_makes_the_third_out_in_a_walk_off_context_counts_the_run_made_first_and_does_not_end_the_game_unless_the_runs_win_it()
    {
        // bottom of the 9th, away ahead by one, bases loaded, two out, an official single: the runner from third scores in the base plan, the lead extra-base candidate is the runner from second
        var c = Find(x => x.WalkOffRoot && x.Diff == 1 && x.K == 2 && x.Occupancy == 7 && x.Outs == 2 && IsSingle(x));
        Assert.Equal((1, 2), (c.Facts.Runs, c.Facts.RunsNeeded));

        // thrown out at home for the third out: the base plan's run (made first, a non-force out follows) counts but ties the game only; the inning ends, the game does not
        var out3 = Compose(c, [AdvancementOutcome.Out, AdvancementOutcome.Extra]);
        Assert.True(out3.HalfOver);
        Assert.False(out3.WalkOffReached);
        Assert.Null(out3.Winner);
        Assert.Equal(AdvancementOutcome.Void, out3.Steps[1].Applied);
        var (evt, applied) = Reduce(c, out3);
        Assert.True(applied.IsSuccess);
        Assert.Single(applied.Value.NewPlays.Single().Runs);
        Assert.Equal(GameStatus.InProgress, applied.Value.State.Status);                                    // tied after the 9th: an extra inning follows
        Assert.Null(AdvancementEventBuilder.Verify(c.Facts, c.State, out3, evt, applied, out var record));
        Assert.False(record!.GameEnded);
        Assert.True(record.HalfOver);

        // scoring instead: the second run wins, the trailing decision is void and the game ends with no out against it
        var wins = Compose(c, [AdvancementOutcome.Extra, AdvancementOutcome.Out]);
        Assert.True(wins.WalkOffReached);
        Assert.Equal(AdvancementOutcome.Void, wins.Steps[1].Applied);
        Assert.Equal(2, wins.Runs);
        var (evt2, applied2) = Reduce(c, wins);
        Assert.Equal(GameOutcome.HomeWin, applied2.Value.State.Outcome);
        Assert.Null(AdvancementEventBuilder.Verify(c.Facts, c.State, wins, evt2, applied2, out _));
    }

    // ================================================================ the event the builder makes

    [Fact]
    public void The_built_event_keeps_the_real_in_play_result_and_carries_the_out_details_of_a_thrown_out_runner()
    {
        var c = Find(x => x.K == 1 && !x.WalkOffRoot && x.Outs == 0 && x.Occupancy == 2 && IsSingle(x));
        var comp = Compose(c, [AdvancementOutcome.Out]);
        var evt = AdvancementEventBuilder.Build(c.Facts, comp, "p1", FeasibilityHarness.Location);

        Assert.Equal(InPlayResult.Single, evt.Result);                                                      // the batting result is never rewritten
        Assert.Equal(FeasibilityHarness.Location, evt.Location);                                            // the confirmed pitch location is kept
        var move = evt.MoveList.Single();
        Assert.Equal((2, 0, OutKind.Tag, 4, OutMethod.Tag, 1), (move.From, move.To, move.OutKind, move.OutAt, move.Method, move.Sequence));
        Assert.Equal(2, move.CreditList.Count);
        Assert.Equal(1, move.CreditList.Count(x => x.Kind == CreditKind.Putout));
        Assert.Equal(FieldPosition.Catcher, move.CreditList.Single(x => x.Kind == CreditKind.Putout).Position);
        Assert.Equal(c.Decisions.Single().Retriever, move.CreditList.Single(x => x.Kind == CreditKind.Assist).Position);
    }

    [Fact]
    public void A_fly_out_with_the_third_base_runner_scoring_makes_the_reducer_derive_a_sacrifice_fly_from_an_Out_event()
    {
        var c = Find(x => x.Facts.AdvancementKeys.SequenceEqual(["fly_out_runner_on_third"]) && !x.WalkOffRoot && x.Outs == 0 && x.Occupancy == 4);
        Assert.Equal(InPlayResult.Out, c.Facts.Official.Result);

        var scores = Compose(c, [AdvancementOutcome.Extra]);
        var (evt, applied) = Reduce(c, scores);
        Assert.Equal(InPlayResult.Out, evt.Result);                                                         // there is no InPlayResult.SacrificeFly: the event input stays Out
        Assert.Equal(BattedBallType.Fly, evt.Batted);
        Assert.True(applied.IsSuccess);
        var play = applied.Value.NewPlays.Single();
        Assert.Equal(BatterResult.SacrificeFly, play.Result);                                               // ... and the reducer DERIVES the official record
        Assert.Single(play.Runs);
        Assert.Null(AdvancementEventBuilder.Verify(c.Facts, c.State, scores, evt, applied, out var record));
        Assert.Equal((BatterResult.SacrificeFly, 1, 1, false), (record!.Result, record.Runs, record.Rbi, record.AtBat));   // no at-bat, one RBI

        // the runner is held or thrown out: an ordinary fly out; the extra out means the play is no sacrifice fly even though the batter is also out
        var held = Compose(c, [AdvancementOutcome.Hold]);
        Assert.Equal(BatterResult.Out, GameReducer.Apply(c.State, AdvancementEventBuilder.Build(c.Facts, held, "p1", FeasibilityHarness.Location)).Value.NewPlays.Single().Result);
        var thrown = Compose(c, [AdvancementOutcome.Out]);
        var (evt3, applied3) = Reduce(c, thrown);
        Assert.Equal(BatterResult.Out, applied3.Value.NewPlays.Single().Result);
        Assert.Equal(2, thrown.OutsAfter);
        Assert.Null(AdvancementEventBuilder.Verify(c.Facts, c.State, thrown, evt3, applied3, out var record3));
        Assert.Equal((BatterResult.Out, 0, true), (record3!.Result, record3.Runs, record3.AtBat));
    }

    [Fact]
    public void The_verification_refuses_a_composition_that_does_not_match_what_the_reducer_made_of_the_event()
    {
        var c = Find(x => x.K == 1 && !x.WalkOffRoot && x.Outs == 0 && x.Occupancy == 2 && IsSingle(x));
        var scores = Compose(c, [AdvancementOutcome.Extra]);
        var (evt, applied) = Reduce(c, scores);
        Assert.Null(AdvancementEventBuilder.Verify(c.Facts, c.State, scores, evt, applied, out _));

        // a composition that claims a different run count than the reducer's official record
        var lying = scores with { Runs = scores.Runs + 1 };
        Assert.Equal("Advancement.official_record_invalid", AdvancementEventBuilder.Verify(c.Facts, c.State, lying, evt, applied, out _)!.Value.Code);
        // a reducer refusal is passed through, and a duplicate or a missing receipt is never a candidate
        var refused = GameReducer.Apply(c.State, evt with { Moves = [] });
        Assert.False(refused.IsSuccess);
        Assert.Equal(refused.Error!.Value.Code, AdvancementEventBuilder.Verify(c.Facts, c.State, scores, evt, refused, out _)!.Value.Code);
        var again = GameReducer.Apply(applied.Value.State, evt);
        Assert.Equal("InPlayCandidate.event_duplicate", AdvancementEventBuilder.Verify(c.Facts, c.State, scores, evt, again, out _)!.Value.Code);
    }

    [Fact]
    public void Composition_is_deterministic_and_changes_neither_the_facts_nor_the_state()
    {
        var c = Find(x => x.K == 2 && !x.WalkOffRoot && x.Occupancy == 3 && x.Outs == 1 && IsSingle(x));
        var before = FeasibilityHarness.Digest(c.State);
        var movesBefore = c.Facts.Moves;
        var keysBefore = c.Facts.AdvancementKeys;

        foreach (var a in AllDrawn)
            foreach (var b in AllDrawn)
            {
                var first = Compose(c, [a, b]);
                var second = Compose(c, [a, b]);
                Assert.Equal(first.Steps.ToArray(), second.Steps.ToArray());
                Assert.Equal(first.Moves.ToArray(), second.Moves.ToArray());
                Assert.Equal((first.OutsAfter, first.Runs, first.HalfOver, first.WalkOffReached), (second.OutsAfter, second.Runs, second.HalfOver, second.WalkOffReached));
            }

        Assert.Equal(before, FeasibilityHarness.Digest(c.State));
        Assert.Equal(movesBefore, c.Facts.Moves);
        Assert.Equal(keysBefore, c.Facts.AdvancementKeys);
    }

    [Fact]
    public void Composition_refuses_a_Void_draw_and_a_wrong_number_of_draws()
    {
        var c = Find(x => x.K == 2 && !x.WalkOffRoot && x.Occupancy == 3 && x.Outs == 1 && IsSingle(x));
        Assert.Equal("Advancement.drawn_invalid", AdvancementPlanner.Compose(c.Facts, c.State, [AdvancementOutcome.Void, AdvancementOutcome.Hold]).Error!.Value.Code);
        Assert.Equal("Advancement.drawn_invalid", AdvancementPlanner.Compose(c.Facts, c.State, [AdvancementOutcome.Hold]).Error!.Value.Code);
        Assert.Equal("Advancement.drawn_invalid", AdvancementPlanner.Compose(c.Facts, c.State, [AdvancementOutcome.Hold, AdvancementOutcome.Hold, AdvancementOutcome.Hold]).Error!.Value.Code);
        Assert.Equal("Advancement.state_missing", AdvancementPlanner.Compose(c.Facts, null!, [AdvancementOutcome.Hold, AdvancementOutcome.Hold]).Error!.Value.Code);
    }
}
