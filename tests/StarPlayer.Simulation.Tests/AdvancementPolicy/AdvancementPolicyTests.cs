using System.Reflection;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;
using StarPlayer.Simulation.Tests.InPlayFeasibility;
using Root = StarPlayer.Simulation.Tests.InPlayFeasibility.InPlayStates.Root;
using Seq = StarPlayer.Simulation.Tests.AdvancementPolicy.AdvModel.SeqMode;

namespace StarPlayer.Simulation.Tests.AdvancementPolicy;

/// <summary>
/// Phase 3-C2B-3-P: policy validation for the future extra-base (<c>advancement</c>) step. EVERYTHING here is test-only. Test names carry the evidence class:
/// <c>Static_</c> = STATIC_POLICY_CHECK (rules and combinations judged by <see cref="AdvJudge"/>), <c>Harness_</c> = TEST_HARNESS_CHECK (candidate policies of
/// <see cref="AdvModel"/>), <c>Reducer_</c> = ACTUAL_REDUCER_CHECK (events built here, applied to the unchanged production <see cref="GameReducer"/>). None of them is an
/// integration test of a 3-C2B-3 engine, because none exists.
/// </summary>
public class AdvancementPolicyTests
{
    private const long Den2 = 1_000_000L * 1_000_000L;

    private static PendingCase Case(string key) => AdvCases.All.Single(c => c.Key == key);

    private static RedRow Apply(PendingCase c, string vector, Seq mode = Seq.Lead)
    {
        var actions = AdvCases.FromKey(c, vector);
        return AdvReducer.Reduce(c.State, AdvModel.BuildEvent(c.Facts, c.Decisions, actions, mode));
    }

    private static Judged Judge(PendingCase c, string vector, Seq mode = Seq.Lead) =>
        AdvJudge.Judge(c.State, c.Facts, c.Decisions, AdvCases.FromKey(c, vector), mode);

    // =============================================================== A. what is pending (STATIC)

    [Fact]
    public void Static_the_1314_pending_cases_are_the_C2B2_baseline_and_split_by_decision_count_as_derived_by_hand()
    {
        var all = AdvCases.All;

        Assert.Equal(1314, all.Count);
        Assert.Equal(300, all.Count(c => !c.WalkOffRoot));
        Assert.Equal(1014, all.Count(c => c.WalkOffRoot));
        Assert.Equal(864, all.Count(c => c.Facts.Composite));
        Assert.Equal(210, all.Count(c => !c.WalkOffRoot && c.Facts.Composite));
        Assert.Equal(654, all.Count(c => c.WalkOffRoot && c.Facts.Composite));
        // by hand: two open decisions exist only for an official single with first AND second occupied (occupancy 3, or 7 when the lead run does not already end the game):
        // ordinary 10 single combinations x 2 occupancies x 3 out counts = 60; walk-off 10 x 3 x (4 diffs for occupancy 3 + 3 diffs for occupancy 7) = 210
        Assert.Equal(60, all.Count(c => !c.WalkOffRoot && c.K == 2));
        Assert.Equal(210, all.Count(c => c.WalkOffRoot && c.K == 2));
        Assert.Equal(1044, all.Count(c => c.K == 1));
        Assert.DoesNotContain(all, c => c.K is 0 or > 2);
        Assert.All(all.Where(c => c.K == 2), c => Assert.True(c.Facts.AdvancementKeys.SequenceEqual(["single_runner_on_first", "single_runner_on_second"])));
    }

    [Fact]
    public void Static_composite_is_not_the_same_as_two_decisions_864_composite_cases_hold_only_270_with_two_open_decisions()
    {
        var composite = AdvCases.All.Where(c => c.Facts.Composite).ToList();

        Assert.Equal(864, composite.Count);
        Assert.Equal(270, composite.Count(c => c.K == 2));                      // the only genuine interaction
        Assert.Equal(594, composite.Count(c => c.K == 1));                      // a bystander runner exists but his base play is already complete (he scores, holds or is not a decision)
        Assert.All(composite.Where(c => c.K == 1), c => Assert.Single(c.Decisions));
    }

    [Fact]
    public void Static_the_production_context_key_order_is_trailing_runner_first_which_is_method_B_of_the_task()
    {
        var two = AdvCases.All.First(c => c.K == 2);

        Assert.Equal("single_runner_on_first", two.Decisions[0].Key);            // runner on first (trailing) is listed before runner on second (lead)
        Assert.True(two.Decisions[0].StartBase < two.Decisions[1].StartBase);
        Assert.Equal(AdvModel.InOrder(two.Decisions, DrawOrder.TrailFirst).Select(d => d.Key), two.Decisions.Select(d => d.Key));
    }

    [Fact]
    public void Static_every_decision_attempts_exactly_one_base_beyond_the_base_plan_and_only_a_trailing_runner_can_be_blocked()
    {
        foreach (var c in AdvCases.All)
            foreach (var d in c.Decisions)
            {
                Assert.InRange(d.StartBase, 1, 3);                                    // the batter-runner never has an open decision in the approved contexts
                Assert.Equal(d.PlanTo + 1, d.TargetTo);
                Assert.InRange(d.TargetTo, 2, 4);
                var blockable = d.TargetTo < 4;                                       // only an attempt at second or third can run into an occupied base; home is never blocked
                Assert.Equal(d.Key == "single_runner_on_first", blockable);
                Assert.Equal(1_000_000, d.Ppm.Sum());
            }
    }

    [Fact]
    public void Static_the_approved_probabilities_are_the_four_rows_of_the_tuning_json_and_are_not_changed_by_any_policy()
    {
        Assert.Equal([590000, 380000, 30000], AdvData.Ppm("single_runner_on_second"));
        Assert.Equal([270000, 700000, 30000], AdvData.Ppm("single_runner_on_first"));
        Assert.Equal([560000, 400000, 40000], AdvData.Ppm("double_runner_on_first"));
        Assert.Equal([700000, 280000, 20000], AdvData.Ppm("fly_out_runner_on_third"));
        Assert.Equal(4, AdvData.Keys.Count);
    }

    // =============================================================== B. all raw action vectors against the real reducer (REDUCER + STATIC)

    private sealed record Raw(PendingCase Case, string Label, Judged Judged, RedRow Red, string Class);

    private static readonly Lazy<List<Raw>> RawVectors = new(() =>
    {
        var rows = new List<Raw>();
        foreach (var c in AdvCases.All)
            foreach (var (label, actions) in AdvCases.RawVectors(c))
            {
                var red = AdvReducer.Reduce(c.State, AdvModel.BuildEvent(c.Facts, c.Decisions, actions, Seq.Lead));
                var j = AdvJudge.Judge(c.State, c.Facts, c.Decisions, actions, Seq.Lead);
                rows.Add(new Raw(c, label, j, red, AdvReducer.Classify(j, red)));
            }

        return rows;
    });

    [Fact]
    public void Reducer_every_rule_legal_raw_vector_is_accepted_with_exactly_the_record_the_independent_judge_expects()
    {
        var rows = RawVectors.Value;

        Assert.Equal(1044 * 3 + 270 * 9, rows.Count);                             // 5,562 = every {extra, hold, out}^k of every pending case
        Assert.DoesNotContain(rows, r => r.Class.StartsWith("LEGAL_MISMATCH", StringComparison.Ordinal));
        Assert.Equal(4722, rows.Count(r => r.Class == "LEGAL_ACCEPTED"));
        Assert.All(rows.Where(r => r.Case.K == 1), r => Assert.Equal("LEGAL_ACCEPTED", r.Class));      // a single open decision can never produce an illegal play
    }

    [Fact]
    public void Reducer_acceptance_is_not_baseball_legality_480_illegal_vectors_are_accepted_and_only_360_are_refused()
    {
        var illegal = RawVectors.Value.Where(r => !r.Judged.Legal).ToList();

        Assert.Equal(840, illegal.Count);                                          // 270 + 210 + 270 + 90
        Assert.Equal(480, illegal.Count(r => r.Red.Ok));                           // 270 runner thrown out at an occupied base + 210 moves after the third out / the winning run
        Assert.Equal(270, illegal.Count(r => r.Red.Code == "Game.base_occupied_twice"));
        Assert.Equal(90, illegal.Count(r => r.Red.Code == "Game.too_many_outs"));
        Assert.All(illegal, r => Assert.Equal(2, r.Case.K));
        // by hand: second AND first occupied, the lead runner holds (38%): the trailing runner's extra base is refused as base_occupied_twice (270 rows), his "out" is accepted (270 rows)
        Assert.Equal(270, illegal.Count(r => r.Judged.Violations.Contains(AdvJudge.AttemptIntoOccupied) && r.Red.Ok));
    }

    [Fact]
    public void Reducer_a_runner_tagged_out_on_a_base_a_teammate_occupies_is_accepted_as_a_legal_event()
    {
        var c = Case("g|0|3|0|line_weak/single");

        var red = Apply(c, "O/H");                                                  // trailing runner "out at third", lead runner holds on third

        Assert.True(red.Ok);                                                        // the reducer cannot know that third base was occupied: the policy must keep it out
        Assert.False(Judge(c, "O/H").Legal);
        Assert.Contains(AdvJudge.AttemptIntoOccupied, Judge(c, "O/H").Violations);
        Assert.Contains(AdvJudge.AttemptIntoOccupied, Judge(c, "E/H").Violations);        // an extra base onto an occupied third is named as such, not only as two runners on one base
        Assert.Equal("Game.base_occupied_twice", Apply(c, "E/H").Code);
    }

    [Fact]
    public void Reducer_a_move_after_the_third_out_is_accepted_and_two_outs_after_two_are_refused()
    {
        var c = Case("g|0|3|2|line_weak/single");                                   // two outs, first and second occupied

        Assert.True(Apply(c, "E/O").Ok);                                            // lead runner out at home = third out, then the trailing runner "takes third": accepted, but impossible
        Assert.Contains(AdvJudge.MoveAfterTerminal, Judge(c, "E/O").Violations);
        Assert.Equal("Game.too_many_outs", Apply(c, "O/O").Code);
    }

    // =============================================================== C. composition and D3 (HARNESS, with the reducer as witness)

    private static IReadOnlyList<PendingCase> Rows(Func<PendingCase, bool>? where = null) => AdvCases.All.Where(c => where?.Invoke(c) ?? true).ToList();

    private sealed record PolicyAudit(string Policy, int Reachable, int IllegalReachable, int ReducerRefused, int LegalMismatch, double IllegalMassPpm);

    private static PolicyAudit Audit(PolicySpec spec)
    {
        int reachable = 0, illegal = 0, refused = 0, mismatch = 0;
        decimal mass = 0;
        foreach (var c in AdvCases.All)
        {
            var exact = AdvModel.Exact(spec, c.Decisions, c.StartWork);
            var scale = (decimal)Math.Pow(1_000_000, c.K);
            foreach (var (key, weight) in exact)
            {
                reachable++;
                var actions = AdvCases.FromKey(c, key);
                var j = AdvJudge.Judge(c.State, c.Facts, c.Decisions, actions, Seq.Lead);
                var red = AdvReducer.Reduce(c.State, AdvModel.BuildEvent(c.Facts, c.Decisions, actions, Seq.Lead));
                if (!j.Legal) { illegal++; mass += weight / scale; }
                if (!red.Ok) refused++;
                if (AdvReducer.Classify(j, red).StartsWith("LEGAL_MISMATCH", StringComparison.Ordinal)) mismatch++;
            }
        }

        return new PolicyAudit(spec.Name, reachable, illegal, refused, mismatch, (double)(mass / AdvCases.All.Count * 1_000_000m));
    }

    private static readonly Lazy<Dictionary<string, PolicyAudit>> Audits = new(() => PolicySpec.All.ToDictionary(s => s.Name, Audit));

    [Fact]
    public void Harness_independent_per_context_draws_without_repair_reach_illegal_plays()
    {
        var a = Audits.Value["IND-L"];

        Assert.True(a.IllegalReachable > 0);
        Assert.True(a.ReducerRefused > 0);                                          // some of them the reducer happens to refuse...
        Assert.True(a.IllegalReachable > a.ReducerRefused);                         // ...and some it accepts
    }

    [Theory]
    [InlineData("A-L-coupled-fixed")]
    [InlineData("B-L-coupled-fixed")]
    [InlineData("A-L-coupled-skip")]
    [InlineData("B-L-coupled-skip")]
    [InlineData("C-L-redraw-coupled")]
    [InlineData("B-T-post-fixed")]
    [InlineData("A-T-pre-fixed")]
    [InlineData("B-T-pre-fixed")]
    public void Harness_and_reducer_a_coupled_policy_reaches_only_legal_plays_the_reducer_accepts_with_the_judged_record(string policy)
    {
        var a = Audits.Value[policy];

        Assert.True(a.Reachable > 1314);
        Assert.Equal(0, a.IllegalReachable);
        Assert.Equal(0, a.ReducerRefused);
        Assert.Equal(0, a.LegalMismatch);
    }

    [Theory]
    [InlineData("A-L-extra-only-fixed")]
    [InlineData("B-L-extra-only-fixed")]
    [InlineData("C-L-redraw-extra-only")]
    public void Harness_excluding_only_the_extra_base_still_lets_a_runner_be_thrown_out_on_an_occupied_base(string policy)
    {
        var a = Audits.Value[policy];

        Assert.True(a.IllegalReachable > 0);
        Assert.Equal(0, a.ReducerRefused);                                          // the reducer cannot see it: the guard must be in the policy
    }

    [Fact]
    public void Harness_with_attempt_coupling_policies_A_and_B_give_the_same_exact_distribution_in_all_1314_cases()
    {
        foreach (var c in AdvCases.All)
        {
            var a = AdvModel.Exact(PolicySpec.ALead, c.Decisions, c.StartWork).OrderBy(x => x.Key).ToList();
            var b = AdvModel.Exact(PolicySpec.BLead, c.Decisions, c.StartWork).OrderBy(x => x.Key).ToList();
            Assert.True(a.SequenceEqual(b), c.Key);
        }
    }

    [Fact]
    public void Harness_post_hoc_repair_in_the_production_key_order_equals_lead_first_sequential_drawing_in_all_1314_cases()
    {
        foreach (var c in AdvCases.All)
        {
            var post = AdvModel.Exact(PolicySpec.BTrailPost, c.Decisions, c.StartWork).OrderBy(x => x.Key).ToList();
            var lead = AdvModel.Exact(PolicySpec.BLead, c.Decisions, c.StartWork).OrderBy(x => x.Key).ToList();
            Assert.True(post.SequenceEqual(lead), c.Key);
        }
    }

    [Fact]
    public void Harness_sequential_feasibility_in_the_production_key_order_makes_the_trailing_runners_extra_base_impossible()
    {
        var c = Case("g|0|3|0|line_weak/single");

        var trail = AdvModel.Exact(PolicySpec.BTrailPre, c.Decisions, c.StartWork);

        Assert.DoesNotContain(trail.Keys, k => k.StartsWith("E/", StringComparison.Ordinal));  // the lead runner still stands on third when the trailing runner is decided
        var lead = AdvModel.Exact(PolicySpec.BLead, c.Decisions, c.StartWork);
        Assert.Contains(lead.Keys, k => k.StartsWith("E/", StringComparison.Ordinal));
    }

    [Fact]
    public void Harness_exact_joint_distribution_of_first_and_second_occupied_with_nobody_out_matches_the_hand_derivation()
    {
        var c = Case("g|0|3|0|line_weak/single");                                   // keys are "<runner on first>/<runner on second>"

        var lead = AdvModel.Exact(PolicySpec.BLead, c.Decisions, c.StartWork);

        // lead runner (second base): extra 59%, hold 38%, out 3%. Trailing runner only if the lead runner does not stay on third: extra 27%, hold 70%, out 3%.
        Assert.Equal(590_000L * 270_000, lead["E/E"]);
        Assert.Equal(590_000L * 700_000, lead["H/E"]);
        Assert.Equal(590_000L * 30_000, lead["O/E"]);
        Assert.Equal(380_000L * 1_000_000, lead["H/H"]);                            // a lead runner who holds forces the trailing runner to hold (one roll, no choice)
        Assert.Equal(30_000L * 270_000, lead["E/O"]);
        Assert.Equal(30_000L * 700_000, lead["H/O"]);
        Assert.Equal(30_000L * 30_000, lead["O/O"]);
        Assert.Equal(Den2, lead.Values.Sum());
        Assert.DoesNotContain("E/H", lead.Keys);
        Assert.DoesNotContain("O/H", lead.Keys);

        var independent = AdvModel.Exact(PolicySpec.IndLead, c.Decisions, c.StartWork);
        Assert.Equal(380_000L * 270_000, independent["E/H"]);                       // the naive draw: 10.26% "extra base onto an occupied third"
        Assert.Equal(380_000L * 30_000, independent["O/H"]);                        // and 1.14% "thrown out on an occupied third"
        Assert.Equal(114_000_000_000L, independent["E/H"] + independent["O/H"]);   // 11.4 % of the draws are not plays at all
    }

    [Fact]
    public void Harness_blocked_runner_is_thrown_out_conditional_distribution_A_versus_B_when_only_the_extra_base_is_removed()
    {
        // the two readings of D3 differ only when SOME outcomes survive: remove "extra" from (27 %, 70 %, 3 %)
        var renormalized = ProbabilityScale.Normalize([0L, 700_000L, 30_000L]).Value;

        Assert.Equal([0, 958_904, 41_096], renormalized);                           // A: out rises from 3.00 % to 4.11 % (x1.37), hold falls from 97 % to 95.89 %
        // B: the removed 27 % goes entirely to hold: hold 97 %, out stays 3.00 %
        var c = Case("g|0|3|0|line_weak/single");
        var a = AdvModel.Exact(PolicySpec.ALeadX, c.Decisions, c.StartWork);
        var b = AdvModel.Exact(PolicySpec.BLeadX, c.Decisions, c.StartWork);
        long trailingOutA = a.Where(k => k.Key.StartsWith("O/H", StringComparison.Ordinal)).Sum(k => k.Value);
        long trailingOutB = b.Where(k => k.Key.StartsWith("O/H", StringComparison.Ordinal)).Sum(k => k.Value);
        Assert.Equal(380_000L * 41_096, trailingOutA);
        Assert.Equal(380_000L * 30_000, trailingOutB);
        Assert.NotEqual(trailingOutA, trailingOutB);
    }

    [Fact]
    public void Harness_when_the_play_is_over_the_remaining_contexts_are_void_and_never_moved()
    {
        var c = Case("g|0|3|2|line_weak/single");                                   // two outs: the lead runner's out is the third

        var lead = AdvModel.Exact(PolicySpec.BLead, c.Decisions, c.StartWork);

        Assert.Equal(30_000L * 1_000_000, lead["V/O"]);                             // the trailing runner is Void after the third out, not "extra"
        Assert.DoesNotContain(lead.Keys, k => k is "E/O" or "O/O");
        // a Void context is frozen where the base plan left him
        var red = Apply(c, "H/O");
        Assert.True(red.Ok);
        Assert.True(red.HalfOver);
        Assert.Equal(3, red.OutsAfter);
    }

    // =============================================================== D. RNG consumption and determinism (HARNESS on the real streams)

    private static (int Calls, long Draws, string Actions, string Hash) Run(PolicySpec spec, PendingCase c, ulong seed)
    {
        var random = GameRandom.Create(seed, "game:3c2b3p").Value;
        var stream = random.Stream(GameRngStreams.Runner);
        var ordered = AdvModel.InOrder(c.Decisions, spec.Order);
        var before = stream.Draws;
        var resolved = AdvModel.Resolve(spec, ordered, c.StartWork, () => stream.NextBounded(ProbabilityScale.RollBound));
        var key = string.Join("/", ordered.Select((d, i) => (d.StartBase, a: resolved.Actions[i])).OrderBy(x => x.StartBase).Select(x => x.a switch { AdvAction.Extra => "E", AdvAction.Hold => "H", AdvAction.Out => "O", _ => "V" }));
        return (resolved.Calls, stream.Draws - before, key, random.Snapshot().StateHash);
    }

    private static string KeyOfRun(PolicySpec spec, PendingCase c, ulong seed) => Run(spec, c, seed).Actions;

    [Fact]
    public void Harness_resolver_outputs_stay_inside_the_exact_reachable_set_and_coupled_policies_only_ever_play_legal_accepted_events()
    {
        var all = AdvCases.All;
        foreach (var spec in PolicySpec.All)
        {
            var coupled = spec.Coupling == Coupling.AttemptCoupled && spec.Kind != PolicyKind.Ind && spec.Order == DrawOrder.LeadFirst || spec.Repair == Repair.Post || spec == PolicySpec.ATrailPre || spec == PolicySpec.BTrailPre;
            var checkedKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i];
                var reachable = AdvModel.Exact(spec, c.Decisions, c.StartWork).Keys.ToHashSet(StringComparer.Ordinal);
                for (ulong s = 1; s <= 12; s++)
                {
                    var key = KeyOfRun(spec, c, (ulong)i * 100 + s);
                    Assert.Contains(key, reachable);
                    if (coupled && checkedKeys.Add(c.Key + "#" + key))
                    {
                        var actions = AdvCases.FromKey(c, key);
                        var j = AdvJudge.Judge(c.State, c.Facts, c.Decisions, actions, Seq.Lead);
                        var red = AdvReducer.Reduce(c.State, AdvModel.BuildEvent(c.Facts, c.Decisions, actions, Seq.Lead));
                        Assert.True(j.Legal, spec.Name + " " + c.Key + " " + key);
                        Assert.True(red.Ok, spec.Name + " " + c.Key + " " + key + " " + red.Code);
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData("A-L-coupled-fixed")]
    [InlineData("B-L-coupled-fixed")]
    [InlineData("C-L-redraw-coupled")]
    [InlineData("B-T-post-fixed")]
    [InlineData("IND-L")]
    public void Harness_resolver_frequencies_on_the_real_stream_match_the_exact_distribution_within_five_sigma(string policy)
    {
        var spec = PolicySpec.All.Single(s => s.Name == policy);
        var c = Case("g|0|3|0|line_weak/single");
        const int n = 40_000;
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (ulong seed = 1; seed <= n; seed++)
        {
            var key = KeyOfRun(spec, c, seed);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        var exact = AdvModel.Exact(spec, c.Decisions, c.StartWork);
        Assert.Subset(exact.Keys.ToHashSet(), counts.Keys.ToHashSet());
        foreach (var (key, weight) in exact)
        {
            var p = (double)weight / Den2;
            var sigma = Math.Sqrt(p * (1 - p) / n);
            Assert.InRange((double)counts.GetValueOrDefault(key) / n, p - 5 * sigma - 1.0 / n, p + 5 * sigma + 1.0 / n);
        }
    }

    [Fact]
    public void Harness_fixed_consumption_uses_exactly_one_roll_per_declared_context_whatever_the_outcome()
    {
        foreach (var name in new[] { "IND-L", "A-L-coupled-fixed", "B-L-coupled-fixed", "B-T-post-fixed" })
        {
            var spec = PolicySpec.All.Single(s => s.Name == name);
            foreach (var c in new[] { Case("g|0|3|0|line_weak/single"), Case("g|0|3|2|line_weak/single"), Case("w|1|7|1|line_weak/single"), Case("g|0|1|0|line_weak/single") })
                for (ulong seed = 1; seed <= 400; seed++)
                    Assert.Equal(c.K, Run(spec, c, seed).Calls);
        }
    }

    [Fact]
    public void Harness_skip_consumption_makes_the_final_rng_position_depend_on_the_outcome_and_redraw_can_exceed_the_context_count()
    {
        var c = Case("g|0|3|0|line_weak/single");

        var skip = Enumerable.Range(1, 2000).Select(s => Run(PolicySpec.BLeadSkip, c, (ulong)s)).ToList();
        var redraw = Enumerable.Range(1, 2000).Select(s => Run(PolicySpec.CLeadX, c, (ulong)s)).ToList();

        Assert.Contains(skip, r => r.Calls == 1);                                   // a lead-runner hold forces the trailing runner: no roll
        Assert.Contains(skip, r => r.Calls == 2);
        Assert.True(skip.Select(r => r.Calls).Distinct().Count() > 1);
        Assert.True(redraw.Max(r => r.Calls) > c.K);
        Assert.Contains(redraw, r => r.Calls > 2);
    }

    [Fact]
    public void Harness_the_same_seed_always_gives_the_same_actions_calls_and_final_position()
    {
        foreach (var spec in PolicySpec.All)
            foreach (var c in new[] { Case("g|0|3|0|line_weak/single"), Case("w|2|7|2|line_hard/single"), Case("g|0|4|1|fly_weak/out") })
                for (ulong seed = 1; seed <= 50; seed++)
                    Assert.Equal(Run(spec, c, seed), Run(spec, c, seed));
    }

    [Fact]
    public void Harness_rejection_sampling_may_use_extra_raw_draws_but_the_number_of_calls_stays_fixed_and_replays_are_identical()
    {
        var c = Case("g|0|3|0|line_weak/single");
        ulong found = 0;
        for (ulong seed = 1; seed < 200_000 && found == 0; seed++)
            if (Run(PolicySpec.BLead, c, seed) is { } r && r.Draws > r.Calls)
                found = seed;

        Assert.NotEqual(0UL, found);                                                // a roll was rejected (about 0.0225 % of the rolls): more raw outputs than calls
        var first = Run(PolicySpec.BLead, c, found);
        Assert.Equal(c.K, first.Calls);
        Assert.True(first.Draws > first.Calls);
        Assert.Equal(first, Run(PolicySpec.BLead, c, found));                       // determinism does not depend on the draw count being equal to the call count
    }

    [Fact]
    public void Harness_the_advancement_draw_touches_only_the_runner_stream_and_never_moves_the_start_snapshot()
    {
        var c = Case("g|0|3|0|line_weak/single");
        var h1 = GameRandom.Create(7, "game:3c2b3p").Value;
        h1.Stream(GameRngStreams.Contact).NextBounded(ProbabilityScale.RollBound);   // stand-in for the C2A draw
        var before = h1.Snapshot();
        var work = GameRandom.Restore(before).Value;                                // the future draw works on a private copy restored from H1

        var stream = work.Stream(GameRngStreams.Runner);
        AdvModel.Resolve(PolicySpec.BLead, AdvModel.InOrder(c.Decisions, DrawOrder.LeadFirst), c.StartWork, () => stream.NextBounded(ProbabilityScale.RollBound));
        var after = work.Snapshot();

        Assert.Equal(before.StateHash, h1.Snapshot().StateHash);                    // H1 is untouched
        Assert.NotEqual(before.StateHash, after.StateHash);                         // H2 differs
        Assert.Equal(before.Streams.Length + 1, after.Streams.Length);              // exactly one stream was added: game/runner@1
        Assert.Contains(after.Streams, s => s.Stream.Canonical == GameRngStreams.Runner.Canonical && s.Draws >= c.K);
        foreach (var s in before.Streams)
            Assert.Contains(after.Streams, t => t.Stream.Canonical == s.Stream.Canonical && t.Draws == s.Draws && t.State == s.State);
    }

    [Fact]
    public void Harness_draw_order_changes_which_roll_belongs_to_which_runner_but_not_the_set_of_possible_plays()
    {
        var c = Case("g|0|3|0|line_weak/single");
        var differing = 0;
        for (ulong seed = 1; seed <= 2000; seed++)
            if (Run(PolicySpec.BLead, c, seed).Actions != Run(PolicySpec.BTrailPost, c, seed).Actions)
                differing++;

        Assert.True(differing > 0);                                                 // same seed, different order: a different play for some seeds (a legitimate consequence of the order)
        Assert.True(differing < 2000);                                              // ...and the same play for many
    }

    // =============================================================== E. third out and the order of runs (REDUCER)

    [Fact]
    public void Reducer_nobody_out_first_and_second_extra_outs_are_ordinary_tag_outs_with_the_expected_record()
    {
        var one = Case("g|0|1|0|line_weak/single");
        var red = Apply(one, "O");                                                  // runner on first thrown out trying for third

        Assert.True(red.Ok);
        Assert.Equal((1, 0, false), (red.OutsAfter, red.Runs, red.HalfOver));
        var play = red.Step!.NewPlays.Single();
        var runner = play.Moves.Single(m => m.From == 1);
        Assert.Equal((0, OutKind.Tag, OutClassification.NonForce, 3), (runner.To, runner.OutKind, runner.Classification, runner.OutAt));
        Assert.Contains(play.Credits, c => c.Position == FieldPosition.ThirdBase && c.Kind == CreditKind.Putout);
        Assert.Contains(play.Credits, c => c.Position == FieldPosition.CenterField && c.Kind == CreditKind.Assist);

        var second = Case("g|0|2|1|line_weak/single");
        var red2 = Apply(second, "O");                                              // runner on second thrown out at home, one out before
        Assert.Equal((2, 0, false), (red2.OutsAfter, red2.Runs, red2.HalfOver));
        Assert.Contains(red2.Step!.NewPlays.Single().Credits, c => c.Position == FieldPosition.Catcher && c.Kind == CreditKind.Putout);
    }

    [Fact]
    public void Reducer_two_outs_the_runner_thrown_out_at_home_is_the_third_out_and_the_batter_runner_is_left_on_base()
    {
        var c = Case("g|0|2|2|line_weak/single");

        var red = Apply(c, "O");

        Assert.True(red.Ok);
        Assert.Equal((3, 0, true), (red.OutsAfter, red.Runs, red.HalfOver));
        Assert.Equal(1, red.Lob);                                                   // the batter-runner who singled stays on first
        Assert.Equal((BatterResult?)BatterResult.Single, red.Result);                              // the hit stands: no force out removed it
    }

    [Fact]
    public void Reducer_a_fly_out_with_a_runner_out_at_home_counts_both_outs_and_a_runner_scoring_makes_it_a_sacrifice_fly()
    {
        var zero = Case("g|0|4|0|fly_weak/out");                                    // runner on third, nobody out, fly ball caught
        var out0 = Apply(zero, "O");
        Assert.Equal((2, 0), (out0.OutsAfter, out0.Runs));                          // the batter AND the runner: two outs on one play
        Assert.Equal((BatterResult?)BatterResult.Out, out0.Result);

        var one = Case("g|0|4|1|fly_weak/out");
        var out1 = Apply(one, "O");
        Assert.Equal((3, 0, true), (out1.OutsAfter, out1.Runs, out1.HalfOver));

        var scored = Apply(one, "E");
        Assert.Equal((2, 1), (scored.OutsAfter, scored.Runs));
        Assert.Equal((BatterResult?)BatterResult.SacrificeFly, scored.Result);                      // an advancement result CHANGES the batting result label (Out -> SacrificeFly)
        Assert.Equal(1, scored.Rbi);
        Assert.Equal((BatterResult?)BatterResult.Out, Apply(one, "H").Result);
    }

    [Fact]
    public void Reducer_loaded_bases_two_runners_thrown_out_with_the_lead_runs_before_both_outs()
    {
        var zero = Case("g|0|7|0|line_weak/single");
        var red0 = Apply(zero, "O/O");                                              // keys: <first>/<second>; runner on second out at home, runner on first out at third

        Assert.True(red0.Ok);
        Assert.Equal((2, 1), (red0.OutsAfter, red0.Runs));                   // the run of the runner from third scored first and counts

        var one = Case("g|0|7|1|line_weak/single");
        var red1 = Apply(one, "O/O");
        Assert.Equal((3, 1, true, 0), (red1.OutsAfter, red1.Runs, red1.HalfOver, red1.Voided));   // third out is a non-force tag out made after the run: the run counts
        Assert.True(Judge(one, "O/O").Legal);
    }

    [Fact]
    public void Reducer_a_run_made_before_a_non_force_third_out_counts_and_after_it_is_voided_and_the_order_must_be_explicit()
    {
        var c = Case("g|0|7|2|line_weak/single");                                   // two outs, loaded, single: the runner from third scores in the base plan

        var before = Apply(c, "H/O", Seq.Lead);                                      // run (seq 1), then the runner from second is out at home (seq 2)
        var after = Apply(c, "H/O", Seq.Trail);                                      // out first, then the run
        var implicitOrder = Apply(c, "H/O", Seq.Implicit);

        Assert.Equal((true, 3, 1, 0), (before.Ok, before.OutsAfter, before.Runs, before.Voided));
        Assert.Equal((true, 3, 0, 1), (after.Ok, after.OutsAfter, after.Runs, after.Voided));
        Assert.False(implicitOrder.Ok);
        Assert.Equal("Game.play_order_required", implicitOrder.Code);               // a third out together with a run needs an explicit order: a policy decision, not a default
        Assert.Equal(1, Judge(c, "H/O", Seq.Lead).CountedRuns);
        Assert.Equal(0, Judge(c, "H/O", Seq.Trail).CountedRuns);
        Assert.Equal(after.Lob, before.Lob + 1);                                    // the voided run is counted as left on base
    }

    [Fact]
    public void Reducer_a_force_out_or_a_batter_runner_out_as_the_third_out_voids_every_run_whatever_the_order()
    {
        // not produced by the advancement draw (a third-out play holds every runner in the approved base plan); this is the reducer's own contract, for the record
        var loaded = InPlayStates.Make(Root.GeneralTop, 0, 7, 2);
        var runners = loaded.Bases.Occupied().ToDictionary(o => o.Base, o => o.Runner);
        var groundOut = ImmutableMoves(
            new RunnerMove(3, 4, MoveCause.Play, null, null, 1), new RunnerMove(2, 2), new RunnerMove(1, 1));
        var evt = new InPlayEvent("p1", BattedBallType.Ground, InPlayResult.Out, false, groundOut,
            System.Collections.Immutable.ImmutableList.Create(new PositionCredit(FieldPosition.Shortstop, CreditKind.Assist), new PositionCredit(FieldPosition.FirstBase, CreditKind.Putout)),
            null, 2, FeasibilityHarness.Location);

        var red = AdvReducer.Reduce(loaded, evt);

        Assert.True(red.Ok, red.Code);
        Assert.Equal((3, 0, 1), (red.OutsAfter, red.Runs, red.Voided));             // batter out at first (third out): the run from third does not count, although it was sequenced first
        Assert.Equal(3, runners.Count);

        // third out is a force out on another base: runner from first forced out at second, the runner from third scores
        var fc = new InPlayEvent("p1", BattedBallType.Ground, InPlayResult.FieldersChoice, false,
            ImmutableMoves(
                new RunnerMove(3, 4, MoveCause.Forced, null, null, 2), new RunnerMove(2, 3, MoveCause.Forced),
                new RunnerMove(1, 0, MoveCause.Play, OutKind.Force, System.Collections.Immutable.ImmutableList.Create(new PositionCredit(FieldPosition.SecondBase, CreditKind.Putout)), 1, 2)),
            null, null, null, FeasibilityHarness.Location);
        var redFc = AdvReducer.Reduce(loaded, fc);
        Assert.True(redFc.Ok, redFc.Code);
        Assert.Equal((3, 0, 1), (redFc.OutsAfter, redFc.Runs, redFc.Voided));
    }

    private static System.Collections.Immutable.ImmutableList<RunnerMove> ImmutableMoves(params RunnerMove[] moves) => System.Collections.Immutable.ImmutableList.CreateRange(moves);

    [Fact]
    public void Reducer_the_same_runner_cannot_move_twice_and_no_play_can_exceed_three_outs()
    {
        var c = Case("g|0|3|1|line_weak/single");
        var duplicate = AdvModel.BuildEvent(c.Facts, c.Decisions, AdvCases.FromKey(c, "H/H"), Seq.Lead);
        duplicate = duplicate with { Moves = duplicate.MoveList.Add(duplicate.MoveList[0]) };

        Assert.Equal("Game.runner_moved_twice", AdvReducer.Reduce(c.State, duplicate).Code);
        var tooMany = Case("g|0|3|2|line_weak/single");
        Assert.Equal("Game.too_many_outs", Apply(tooMany, "O/O").Code);
        // one-out play with both runners out is exactly three outs and legal
        Assert.True(Apply(Case("g|0|3|1|line_weak/single"), "O/O").Ok);
    }

    // =============================================================== F. extra-base walk-offs (REDUCER, hand-derived)

    private static int WinnerKey(PendingCase c, string vector) => Judge(c, vector).WinnerKey;

    private static int KeyAtBase(PendingCase c, int @base) => c.State.Bases.At(@base)!.RunnerKey;

    [Fact]
    public void Reducer_walkoff_1_tied_ninth_runner_on_second_single_extra_base_wins_the_game_and_the_base_plan_winner_is_missing()
    {
        var c = Case("w|0|2|0|line_weak/single");

        Assert.Null(c.Facts.WinningRunner);                                          // the C2B-2 base plan has no winner: its WinningRunner can NOT be reused
        var red = Apply(c, "E");

        Assert.Equal((true, (GameOutcome?)GameOutcome.HomeWin, 1, 1, (BatterResult?)BatterResult.Single), (red.GameOver, red.Outcome, red.Runs, red.Rbi, red.Result));
        Assert.Equal(KeyAtBase(c, 2), WinnerKey(c, "E"));
        Assert.Equal(1, red.Lob);                                                    // the batter-runner stays on first (profile rule: walk-off runners count as left on base)
        Assert.False(Apply(c, "H").GameOver);
        Assert.False(Apply(c, "O").GameOver);
    }

    [Fact]
    public void Reducer_walkoff_2_tied_ninth_runner_on_first_double_extra_base_scores_from_first_and_keeps_the_double()
    {
        var c = Case("w|0|1|0|line_weak/double");

        var red = Apply(c, "E");

        Assert.Equal((true, (GameOutcome?)GameOutcome.HomeWin, 1, (BatterResult?)BatterResult.Double), (red.GameOver, red.Outcome, red.Runs, red.Result));
        Assert.Equal(KeyAtBase(c, 1), WinnerKey(c, "E"));
        Assert.Equal(InPlayResult.Double, c.Facts.Official.Result);
        Assert.Equal(2, c.Facts.Official.BatterDestination);                         // credit = min(2 bases, 4 - 1 = 3 bases the winner advanced) = 2: unchanged
    }

    [Fact]
    public void Reducer_walkoff_3_one_behind_second_and_third_single_the_second_base_runner_scoring_takes_the_lead()
    {
        var c = Case("w|1|6|0|line_weak/single");                                   // away leads by 1; the runner from third ties it in the base plan

        Assert.Equal(1, c.Facts.Runs);
        var win = Apply(c, "E");
        Assert.Equal((true, (GameOutcome?)GameOutcome.HomeWin, 2, 2), (win.GameOver, win.Outcome, win.Runs, win.Rbi));
        Assert.Equal(KeyAtBase(c, 2), WinnerKey(c, "E"));                             // the second counted run, after the runner from third
        var hold = Apply(c, "H");
        Assert.Equal((false, 1), (hold.GameOver, hold.Runs));
        var thrownOut = Apply(c, "O");
        Assert.Equal((false, 1, 1), (thrownOut.GameOver, thrownOut.Runs, thrownOut.OutsAfter));
    }

    [Fact]
    public void Reducer_walkoff_4_two_behind_bases_loaded_double_the_runner_from_first_scoring_is_the_third_run_and_the_winner()
    {
        var c = Case("w|2|7|0|line_hard/double");                                   // a double scores two (third, second); three are needed

        Assert.Equal(2, c.Facts.Runs);
        Assert.False(c.Facts.WalkOffEndsGame);
        var win = Apply(c, "E");
        Assert.Equal((true, (GameOutcome?)GameOutcome.HomeWin, 3, 3, (BatterResult?)BatterResult.Double), (win.GameOver, win.Outcome, win.Runs, win.Rbi, win.Result));
        Assert.Equal(KeyAtBase(c, 1), WinnerKey(c, "E"));
        Assert.False(Apply(c, "H").GameOver);
    }

    [Fact]
    public void Reducer_walkoff_5_two_outs_a_runner_out_at_home_and_the_winning_run_in_one_play_depend_on_who_is_decided_first()
    {
        var c = Case("w|1|6|2|line_weak/single");                                   // down 1, runners on second and third, two outs

        var win = Apply(c, "E");
        Assert.Equal((true, 2, 2), (win.GameOver, win.Runs, win.OutsAfter));
        var out3 = Apply(c, "O", Seq.Lead);                                         // run from third (seq 1), out at home (seq 2, the third out): the run counts, the game is tied and goes on
        Assert.Equal((false, 1, 3, 0), (out3.GameOver, out3.Runs, out3.OutsAfter, out3.Voided));
        var reversed = Apply(c, "O", Seq.Trail);                                    // the same facts in the other order: the run is voided and the visitors win 1-0
        Assert.Equal((true, (GameOutcome?)GameOutcome.AwayWin, 0, 3, 1), (reversed.GameOver, reversed.Outcome, reversed.Runs, reversed.OutsAfter, reversed.Voided));
    }

    [Fact]
    public void Reducer_walkoff_6_failed_extra_bases_never_end_the_game()
    {
        foreach (var key in new[] { "w|0|2|0|line_weak/single", "w|0|2|1|line_weak/single", "w|0|1|0|line_weak/double", "w|0|4|0|fly_weak/out" })
        {
            var c = Case(key);
            foreach (var v in new[] { "H", "O" })
            {
                var red = Apply(c, v);
                Assert.True(red.Ok, key + v);
                Assert.False(red.GameOver, key + v);
            }
        }

        Assert.True(Apply(Case("w|0|4|0|fly_weak/out"), "E").GameOver);              // a sacrifice-fly walk-off needs the extra base
    }

    [Fact]
    public void Static_walkoff_7_no_extra_base_changes_the_credited_base_of_a_hit_in_any_pending_vector()
    {
        var changed = 0;
        var reachedWalkOff = 0;
        foreach (var r in RawVectors.Value.Where(r => r.Judged.Legal && r.Judged.WalkOffReached))
        {
            reachedWalkOff++;
            var f = r.Case.Facts;
            var k = InPlayOfficialCredit.BasesOf(f.Drawn.Result);
            if (k < 2)
                continue;                                                           // only a drawn double or triple can be credited with fewer bases (9.06(f))
            var winner = r.Case.Facts.Moves.Single(m => m.RunnerKey == r.Judged.WinnerKey);
            // the winner is an existing runner here; credit = min(drawn bases, 4 - his starting base)
            if (Math.Min(k, 4 - winner.From) != k) changed++;
        }

        Assert.True(reachedWalkOff > 0);
        Assert.Equal(0, changed);                                                   // true of the four approved contexts; a future context must re-run this rule after the advancement
    }

    [Fact]
    public void Static_walkoff_8_every_extra_base_winner_is_an_existing_runner_and_never_the_batter_runner()
    {
        var winners = RawVectors.Value.Where(r => r.Judged.Legal && r.Judged.WalkOffReached).ToList();

        Assert.NotEmpty(winners);
        Assert.All(winners, r => Assert.Contains(r.Case.Facts.Moves, m => m.RunnerKey == r.Judged.WinnerKey));
        Assert.All(winners, r => Assert.Null(r.Case.Facts.WinningRunner));            // the base plan never knew the winner: the winner must be recomputed from the final play
    }

    [Fact]
    public void Reducer_advancement_changes_the_official_record_so_it_must_be_recomputed_from_the_reducers_final_play()
    {
        var sf = Case("g|0|4|0|fly_weak/out");

        var baseline = Apply(sf, "H");
        var extra = Apply(sf, "E");

        Assert.Equal(((BatterResult?)BatterResult.Out, 0), (baseline.Result, baseline.Rbi));
        Assert.Equal(((BatterResult?)BatterResult.SacrificeFly, 1), (extra.Result, extra.Rbi));
        var abBaseline = baseline.Step!.Delta.Entries.Where(e => e.Key == StatKey.BatAB).Sum(e => e.Amount);
        var abExtra = extra.Step!.Delta.Entries.Where(e => e.Key == StatKey.BatAB).Sum(e => e.Amount);
        Assert.Equal((1, 0), (abBaseline, abExtra));                                // the at-bat disappears: the base plan's batting line is not final
    }

    // =============================================================== G. data supply (STATIC reflection)

    [Fact]
    public void Static_missing_input_the_game_state_holds_runners_and_defenders_as_ids_only_with_no_speed_or_arm()
    {
        static bool Carries(Type t, params string[] names) => t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(p => names.Any(n => p.Name.Contains(n, StringComparison.OrdinalIgnoreCase)));

        Assert.False(Carries(typeof(BaseRunner), "Speed", "Rating", "Skill", "Fatigue"));
        Assert.False(Carries(typeof(TeamGameState), "Speed", "Arm", "Rating", "Skill", "Fatigue", "Sheet"));
        Assert.False(Carries(typeof(GameState), "Speed", "Arm", "Rating", "Skill", "Fatigue", "Sheet"));
        Assert.True(typeof(BaseRunner).GetProperty("Player")!.PropertyType == typeof(PlayerId));
        Assert.True(typeof(HitterRatings).GetProperty("Speed") is not null && typeof(HitterRatings).GetProperty("Arm") is not null);   // the values exist, on PlayerSheet only
    }

    [Fact]
    public void Static_missing_input_the_planning_entry_points_receive_only_the_batter_and_pitcher_sheets()
    {
        foreach (var name in new[] { nameof(InPlayPitchPlanning.PlanBase), nameof(InPlayPitchPlanning.Build) })
        {
            var method = typeof(InPlayPitchPlanning).GetMethods().First(m => m.Name == name && m.IsPublic);
            Assert.Equal(2, method.GetParameters().Count(p => p.ParameterType == typeof(PlayerSheet)));
        }

        var prepare = typeof(PitchPreparation).GetMethod(nameof(PitchPreparation.Prepare))!;
        Assert.Equal(2, prepare.GetParameters().Count(p => p.ParameterType == typeof(PlayerSheet)));
    }

    [Fact]
    public void Static_missing_input_the_model_declares_the_speed_and_arm_slots_but_the_pitch_resolver_supplies_no_advancement_modifier()
    {
        var model = ProbabilityModel.V1;

        Assert.Equal("advancement", model.Slot("runner.speed")!.Stage);
        Assert.Equal("advancement", model.Slot("defense.arm")!.Stage);
        Assert.Equal(0, model.Slots.Count(s => s.Stage == "advancement" && s.Id.Contains("fatigue", StringComparison.OrdinalIgnoreCase)));   // no condition or fatigue slot exists
        var baseball = Path.Combine(FeasibilityData.Root, "src", "StarPlayer.Simulation", "Baseball");
        var resolver = File.ReadAllText(Path.Combine(baseball, "PitchBranchResolver.cs"));
        // Phase 3-D: the resolver no longer draws every stage neutral; it hands each stage the inputs PitchModifierInputs builds, and it never builds a ModifierInput itself
        Assert.DoesNotContain("ModifierInput", resolver.Replace("PitchModifierInputs", ""));
        Assert.Contains("PitchModifierInputs.For(stage, abilities,", resolver);
        Assert.Contains("StageProbability.Compute(tuning, stage, condition, inputs, tendency)", resolver);
        // the only place a ModifierInput is created connects exactly the nine approved slots; no advancement, defence-fielding, runner, arm, park, catcher or battery slot
        var inputs = File.ReadAllText(Path.Combine(baseball, "PitchModifierInputs.cs"));
        var slots = System.Text.RegularExpressions.Regex.Matches(inputs, @"ModifierInput\.(?:Linear|Choice)\(""([a-z_.]+)""").Select(m => m.Groups[1].Value).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[]
        {
            "batter.contact", "batter.eye", "batter.power", "matchup.handedness", "pitcher.control", "pitcher.stuff", "pitcher.velocity", "situation.count_pressure", "strategy.approach",
        }, slots);
        foreach (var banned in new[] { "runner.", "defense.", "park.", "catcher.", "battery." })
            Assert.DoesNotContain(banned, inputs);
    }

    // =============================================================== H. isolation: the production state is untouched

    [Fact]
    public void Reducer_building_and_applying_every_vector_leaves_the_state_the_event_was_applied_to_unchanged()
    {
        foreach (var c in AdvCases.All.Take(200))
        {
            var before = FeasibilityHarness.Digest(c.State);
            foreach (var (_, actions) in AdvCases.RawVectors(c))
                AdvReducer.Reduce(c.State, AdvModel.BuildEvent(c.Facts, c.Decisions, actions, Seq.Lead));
            Assert.Equal(before, FeasibilityHarness.Digest(c.State));
        }
    }
}
