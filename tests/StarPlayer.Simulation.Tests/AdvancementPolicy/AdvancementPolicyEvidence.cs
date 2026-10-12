using System.Globalization;
using System.Text;
using System.Text.Json;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Simulation.Randomness;
using Seq = StarPlayer.Simulation.Tests.AdvancementPolicy.AdvModel.SeqMode;

namespace StarPlayer.Simulation.Tests.AdvancementPolicy;

/// <summary>
/// Phase 3-C2B-3-P evidence. The numbers of the report are produced here from the real planner facts, the real reducer and the real RNG streams. The files are written only
/// when the environment variable <c>STAR_ADV_OUT</c> names a directory; without it the test still computes everything and checks the totals, so it cannot silently rot.
/// </summary>
public class AdvancementPolicyEvidence
{
    private static string Inv(object? v) => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";

    private static string Letters(AdvAction a) => a switch { AdvAction.Extra => "E", AdvAction.Hold => "H", AdvAction.Out => "O", _ => "V" };

    [Fact]
    public void Evidence_is_computed_and_written_on_request()
    {
        var all = AdvCases.All;
        var csv = new StringBuilder();
        csv.AppendLine("case_key,root,diff,occupancy,outs,combo,context_keys,k,vector_lead_order,judge_legal,judge_violations,judge_runs,judge_outs_after,judge_walkoff,reducer_ok,reducer_code,reducer_runs,reducer_voided,reducer_outs_after,reducer_game_over,reducer_result,reducer_rbi,reducer_lob,class");
        var classes = new Dictionary<string, int>(StringComparer.Ordinal);
        var rawCount = 0;
        foreach (var c in all)
            foreach (var (label, actions) in AdvCases.RawVectors(c))
            {
                var j = AdvJudge.Judge(c.State, c.Facts, c.Decisions, actions, Seq.Lead);
                var r = AdvReducer.Reduce(c.State, AdvModel.BuildEvent(c.Facts, c.Decisions, actions, Seq.Lead));
                var cls = AdvReducer.Classify(j, r) + (j.Legal ? "" : "|" + string.Join("+", j.Violations));
                classes[cls] = classes.GetValueOrDefault(cls) + 1;
                rawCount++;
                csv.AppendLine(string.Join(",", c.Key.Replace('|', ':'), c.WalkOffRoot ? "walkoff" : "ordinary", c.Diff, c.Occupancy, c.Outs, c.Combo.Key, string.Join("+", c.Facts.AdvancementKeys), c.K,
                    label, j.Legal, string.Join("+", j.Violations), j.CountedRuns, j.OutsAfter, j.WalkOffReached, r.Ok, r.Code, r.Runs, r.Voided, r.OutsAfter, r.GameOver, r.Result, r.Rbi, r.Lob, cls));
            }

        // order sensitivity of the SAME facts (P-05): lead-first vs trailing-first explicit order vs no explicit order
        int orderDiffer = 0, orderRequired = 0, trailVoidsRun = 0;
        foreach (var c in all)
            foreach (var (_, actions) in AdvCases.RawVectors(c))
            {
                var j = AdvJudge.Judge(c.State, c.Facts, c.Decisions, actions, Seq.Lead);
                if (!j.Legal) continue;
                var lead = AdvReducer.Reduce(c.State, AdvModel.BuildEvent(c.Facts, c.Decisions, actions, Seq.Lead));
                var trail = AdvReducer.Reduce(c.State, AdvModel.BuildEvent(c.Facts, c.Decisions, actions, Seq.Trail));
                var implicitOrder = AdvReducer.Reduce(c.State, AdvModel.BuildEvent(c.Facts, c.Decisions, actions, Seq.Implicit));
                if (!implicitOrder.Ok && implicitOrder.Code == "Game.play_order_required") orderRequired++;
                if (trail.Ok && (trail.Runs != lead.Runs || trail.GameOver != lead.GameOver)) { orderDiffer++; if (trail.Voided > lead.Voided) trailVoidsRun++; }
            }

        // exact policy audit and marginals
        var policies = new List<object>();
        foreach (var spec in PolicySpec.All)
        {
            var perKey = new Dictionary<string, decimal[]>(StringComparer.Ordinal);
            var perKeyCases = new Dictionary<string, int>(StringComparer.Ordinal);
            var perKeyK2 = new Dictionary<string, decimal[]>(StringComparer.Ordinal);
            var perKeyK2Cases = new Dictionary<string, int>(StringComparer.Ordinal);
            int reachable = 0, illegal = 0, refused = 0;
            decimal massAll = 0, massK2 = 0;
            foreach (var c in all)
            {
                var exact = AdvModel.Exact(spec, c.Decisions, c.StartWork);
                var scale = (decimal)Math.Pow(1_000_000, c.K);
                foreach (var d in c.Decisions)
                {
                    var marginal = new decimal[4];
                    foreach (var (key, weight) in exact)
                    {
                        var action = AdvCases.FromKey(c, key)[d.StartBase];
                        marginal[(int)action] += weight / scale;
                    }

                    void Add(Dictionary<string, decimal[]> sums, Dictionary<string, int> n)
                    {
                        if (!sums.TryGetValue(d.Key, out var s)) sums[d.Key] = s = new decimal[4];
                        for (var i = 0; i < 4; i++) s[i] += marginal[i];
                        n[d.Key] = n.GetValueOrDefault(d.Key) + 1;
                    }

                    Add(perKey, perKeyCases);
                    if (c.K == 2) Add(perKeyK2, perKeyK2Cases);
                }

                foreach (var (key, _) in exact)
                {
                    reachable++;
                    var actions = AdvCases.FromKey(c, key);
                    var j = AdvJudge.Judge(c.State, c.Facts, c.Decisions, actions, Seq.Lead);
                    var r = AdvReducer.Reduce(c.State, AdvModel.BuildEvent(c.Facts, c.Decisions, actions, Seq.Lead));
                    if (!j.Legal) { illegal++; massAll += exact[key] / scale; if (c.K == 2) massK2 += exact[key] / scale; }
                    if (!r.Ok) refused++;
                }
            }

            object Rows(Dictionary<string, decimal[]> sums, Dictionary<string, int> n) =>
                sums.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => new
                {
                    cases = n[x.Key], extra_ppm = Math.Round(x.Value[0] / n[x.Key] * 1_000_000m, 1), hold_ppm = Math.Round(x.Value[1] / n[x.Key] * 1_000_000m, 1),
                    out_ppm = Math.Round(x.Value[2] / n[x.Key] * 1_000_000m, 1), void_ppm = Math.Round(x.Value[3] / n[x.Key] * 1_000_000m, 1),
                });

            policies.Add(new { policy = spec.Name, reachable_vectors = reachable, illegal_reachable = illegal, reducer_refused = refused, illegal_probability_ppm_mean_all_cases = Math.Round(massAll / all.Count * 1_000_000m, 1), illegal_probability_ppm_mean_two_decision_cases = Math.Round(massK2 / all.Count(x => x.K == 2) * 1_000_000m, 1), marginals_all_cases = Rows(perKey, perKeyCases), marginals_two_decision_cases = Rows(perKeyK2, perKeyK2Cases) });
        }

        // RNG consumption on the real streams, 300 seeds per case class
        var consumption = new List<object>();
        foreach (var spec in PolicySpec.All)
        {
            var calls = new List<int>();
            var extraRaw = 0L;
            var sample = all.Where(x => x.K == 2).Take(60).Concat(all.Where(x => x.K == 1).Take(60)).ToList();
            for (var ci = 0; ci < sample.Count; ci++)
                for (ulong s = 1; s <= 100; s++)
                {
                    var c = sample[ci];
                    var seed = (ulong)ci * 1000 + s;                                       // a different seed for every (case, repetition): 12,000 independent runs
                    var random = GameRandom.Create(seed, "game:3c2b3p").Value;
                    var stream = random.Stream(GameRngStreams.Runner);
                    var ordered = AdvModel.InOrder(c.Decisions, spec.Order);
                    var before = stream.Draws;
                    var resolved = AdvModel.Resolve(spec, ordered, c.StartWork, () => stream.NextBounded(ProbabilityScale.RollBound));
                    calls.Add(resolved.Calls);
                    extraRaw += stream.Draws - before - resolved.Calls;
                }

            consumption.Add(new { policy = spec.Name, runs = calls.Count, min_calls = calls.Min(), max_calls = calls.Max(), mean_calls = Math.Round(calls.Average(), 4), distinct_call_counts = calls.Distinct().Count(), raw_draws_beyond_calls = extraRaw });
        }

        Assert.Equal(5562, rawCount);
        Assert.Equal(12, policies.Count);
        Assert.Equal(12, consumption.Count);
        Assert.True(orderRequired > 0 && orderDiffer > 0 && trailVoidsRun > 0);

        var dir = Environment.GetEnvironmentVariable("STAR_ADV_OUT");
        if (string.IsNullOrWhiteSpace(dir))
            return;
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "PHASE_3C2B3P_CASE_VECTORS.csv"), csv.ToString(), new UTF8Encoding(false));
        var summary = new
        {
            format = "phase3c2b3p-policy-evidence-v1",
            evidence_class = "STATIC_POLICY_CHECK + TEST_HARNESS_CHECK + ACTUAL_REDUCER_CHECK (see test names); no 3-C2B-3 engine exists",
            case_structure = all.GroupBy(c => string.Join("+", c.Facts.AdvancementKeys) + (c.WalkOffRoot ? "|walkoff" : "|ordinary")).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count()),
            third_out_possible_cases = all.Count(c => c.Outs + (c.Facts.Official.Result == InPlayResult.Out ? 1 : 0) + 1 >= 3),
            pending_cases = all.Count, ordinary = all.Count(c => !c.WalkOffRoot), walkoff = all.Count(c => c.WalkOffRoot), composite = all.Count(c => c.Facts.Composite),
            one_decision = all.Count(c => c.K == 1), two_decisions = all.Count(c => c.K == 2), raw_vectors = rawCount,
            raw_vector_classes = classes.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Value),
            order_sensitivity = new { rule_legal_vectors_that_need_explicit_order = orderRequired, rule_legal_vectors_whose_runs_or_game_end_change_with_trailing_first_order = orderDiffer, of_which_a_run_is_voided_only_in_trailing_order = trailVoidsRun },
            policies, rng_consumption = consumption,
        };
        File.WriteAllText(Path.Combine(dir, "PHASE_3C2B3P_POLICY_SUMMARY.json"), JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
    }
}
