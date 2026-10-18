using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.SimHarness;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.SimHarness.Tests;

/// <summary>
/// F-C2-C: the 48-cell strategy-dominance check of ADR-008 FC-D4 (proposal §6.1-§6.7). Small synthetic samples for the statistics and a small real run of all 48 cells (two games each) for the
/// evidence, integrity and replay paths. Nothing here is an official run or official data.
/// </summary>
public sealed class DominanceTests
{
    private static readonly ulong[] Seeds = [11, 12];

    /// <summary>All 48 cells, two games each, run once and shared (deterministic; no official seed).</summary>
    private static readonly Lazy<IReadOnlyList<RunResult>> Baseline = new(() => DominanceMatrix.Cells.Select(c => DominanceMatrix.Run(c, Seeds)).ToList());

    private static RunResult Of(string cellId) => Baseline.Value.Single(r => r.Config.CompetitionKey == DominanceMatrix.Cells.Single(c => c.Id == cellId).CompetitionKey);

    private static IReadOnlyList<RunResult> Replace(string cellId, RunResult? run) =>
        Baseline.Value.Select(r => r == Of(cellId) ? run : r).Where(r => r is not null).Select(r => r!).ToList();

    /// <summary>A run with consistently recomputed config / result hashes (so only the evaluator's own rules, not a hash check, can refuse what the test changed).</summary>
    private static RunResult Rebuild(RunResult r, IReadOnlyList<GameSpec> specs, IReadOnlyList<GameRunResult> games)
    {
        var aggregate = new MetricCounters();
        foreach (var g in games.Where(g => g.Completed))
            aggregate.Add(g.Counters);
        var config = r.Config.ConfigHash(specs);
        return r with { Specs = specs, Games = games, Aggregate = aggregate, ConfigHash = config, RunResultHash = Hashing.Sha256(RunResultText.V1Text(config, games.Select(g => g.ResultHash), aggregate.Canonical())) };
    }

    // ================================================================ the 48 cells

    [Fact]
    public void The_matrix_is_exactly_the_48_cells_of_proposal_6_6_row_by_row()
    {
        // independent oracle: the approved table itself (ADR-008 FC-D4.1 "as tabulated in proposal §6.6")
        var text = File.ReadAllText(Path.Combine(H.Root, "docs", "reports", "PHASE_3F_FC1_POLICY_PROPOSAL.md"));
        var rows = Regex.Matches(text, @"^\| (\d+) \| `(DOM-[A-Z]{2}-[A-Z]{2})` \| (\w+) \| (\w+) \| (same|opposite) \| (\w+) \| `([^`]+)` \| `([^`]+)` \| `([^`]+)` \| `([^`]+)` \| B-col (\w+) \| P-col (\w+) \|", RegexOptions.Multiline)
            .Select(m => m.Groups.Values.Skip(1).Select(g => g.Value).ToArray()).ToList();
        Assert.Equal(48, rows.Count);
        var cells = DominanceMatrix.Cells.Select(c => new[]
        {
            c.Number.ToString(), c.Id, c.Batter.ToString(), c.Pitcher.ToString(), c.Hand == MatrixHand.Same ? "same" : "opposite", c.Mode.ToString(), c.CohortId, c.RulesProfileId, c.PolicyId,
            c.CompetitionKey, c.BatterColumn, c.PitcherColumn,
        }).ToList();
        Assert.Equal(rows.Select(r => string.Join('|', r)), cells.Select(c => string.Join('|', c)));

        // no duplicate id / key / policy-per-condition / axis combination, and every combination of the approved axes present
        Assert.Equal(48, DominanceMatrix.Cells.Select(c => c.Id).Distinct().Count());
        Assert.Equal(48, DominanceMatrix.Cells.Select(c => c.CompetitionKey).Distinct().Count());
        Assert.Equal(48, DominanceMatrix.Cells.Select(c => (c.Batter, c.Pitcher, c.Hand, c.Mode)).Distinct().Count());
        Assert.Equal(4 * 3 * 2 * 2, Enum.GetValues<BatterStrategy>().Length * Enum.GetValues<PitcherStrategy>().Length * Enum.GetValues<MatrixHand>().Length * 2);
        Assert.Equal(12, DominanceMatrix.Cells.Select(c => c.BatterColumn).Distinct().Count());     // proposal §6.3: 12 batter-side columns of 4 cells
        Assert.Equal(16, DominanceMatrix.Cells.Select(c => c.PitcherColumn).Distinct().Count());    // and 16 pitcher-side columns of 3 cells
        Assert.All(DominanceMatrix.Cells.GroupBy(c => c.BatterColumn), g => Assert.Equal(4, g.Count()));
        Assert.All(DominanceMatrix.Cells.GroupBy(c => c.PitcherColumn), g => Assert.Equal(3, g.Count()));
        Assert.All(DominanceMatrix.Cells, c => Assert.True(SeedFamilies.IsOfficialCompetitionKey(c.CompetitionKey) && c.CompetitionKey.StartsWith("fc3-dom-", StringComparison.Ordinal)));
        Assert.Same(DominanceMatrix.Cells, DominanceMatrix.Cells);                                  // one generated, read-only list
        Assert.IsAssignableFrom<IReadOnlyList<DominanceCell>>(DominanceMatrix.Cells);
    }

    [Fact]
    public void The_twelve_policies_are_registered_and_a_cell_config_is_built_only_from_the_registries_and_the_fixed_profile()
    {
        Assert.Equal(12, DominancePolicy.All.Select(p => p.Id).Distinct().Count());
        foreach (var p in DominancePolicy.All)
        {
            Assert.Same(p, H.Ok(PolicyRegistry.Resolve(p.Id)));
            Assert.Same(p, DominancePolicy.Of(p.Batter, p.Pitcher));
        }

        Assert.Equal("dom-takefirst-careful-v1", DominancePolicy.Of(BatterStrategy.TakeFirst, PitcherStrategy.Careful).Id);
        foreach (var cell in DominanceMatrix.Cells.Where((_, i) => i % 7 == 0))
        {
            var c = H.Ok(DominanceMatrix.Config(cell, "x"));
            Assert.Equal((cell.CompetitionKey, cell.CohortId, cell.RulesProfileId, cell.PolicyId, cell.Mode), (c.CompetitionKey, c.Cohort.CohortId, c.Rules.ProfileId, c.Policy.Id, c.Rules.CalledPitchMode));
            Assert.Equal((OfficialProfile.TuningHash, OfficialProfile.AdvancementPolicyVersion, OfficialProfile.ParkText, OfficialProfile.EventBudget, OfficialProfile.RulesHashes[cell.RulesProfileId]),
                (c.Execution.TuningHash, c.AdvancementPolicyVersion, OfficialProfile.ParkTextOf(c.Park), c.EventBudget, c.Rules.ContentHash));
            Assert.Equal(cell.Stratum, c.Cohort.StratumOf(1));
        }
    }

    [Fact]
    public void A_seed_manifest_binds_to_exactly_one_cell_and_keeps_the_full_execution_contract()
    {
        var cell = DominanceMatrix.Cells[37];
        SeedManifest M(string family = "fc3-dom", string? key = null, string? cohort = null, string? policy = null, string? rules = null, long? pa = null) =>
            new(family, key ?? cell.CompetitionKey, cohort ?? cell.CohortId, policy ?? cell.PolicyId, rules ?? cell.RulesProfileId, 1, 2, pa, [], new string('a', 64));
        Assert.Same(cell, H.Ok(DominanceMatrix.CellOf(M())));
        Assert.Equal("Dominance.family_invalid", DominanceMatrix.CellOf(M(family: "fc3-cal")).Error!.Value.Code);
        Assert.Equal("Dominance.cell_unknown", DominanceMatrix.CellOf(M(key: "fc3-dom-x")).Error!.Value.Code);
        Assert.Equal("Dominance.cell_binding_mismatch", DominanceMatrix.CellOf(M(cohort: "neutral-v1")).Error!.Value.Code);           // ON cell on the same-hand cohort
        Assert.Equal("Dominance.cell_binding_mismatch", DominanceMatrix.CellOf(M(policy: DominanceMatrix.Cells[36].PolicyId)).Error!.Value.Code);   // another cell's strategy pair
        Assert.Equal("Dominance.cell_binding_mismatch", DominanceMatrix.CellOf(M(rules: HarnessData.AutomatedProfile)).Error!.Value.Code);
        Assert.Equal("Dominance.stop_rule_not_allowed", DominanceMatrix.CellOf(M(pa: 100_000)).Error!.Value.Code);                         // ADR-009 SR-D5: no calibration stop for fc3-dom
        Assert.False(StopPlan.FamilyIsStopAware(DominanceMatrix.Family));
    }

    // ================================================================ A-1: policies are state-free and a half-inning carries nothing over

    private static GameState Initial(DominanceCell cell)
    {
        var config = H.Ok(DominanceMatrix.Config(cell, "a1"));
        var (away, home) = config.Cohort.SetupFor(1);
        return H.Ok(GameStarter.Begin(new GameStart(config.Spec(1, 5).GameId, config.Rules, away, home, config.Execution), 5)).Current.State;
    }

    [Fact]
    public void A1_every_matrix_policy_depends_only_on_the_count_and_holds_no_state()
    {
        var start = Initial(DominanceMatrix.Cells[0]);
        var variants = new[]
        {
            start, start with { Inning = 7, Half = InningHalf.Bottom }, start with { AwayRuns = 9, HomeRuns = 0 }, start with { HomeRuns = 12 }, start with { Outs = 2 },
            start with { Inning = 12, Half = InningHalf.Top, AwayRuns = 3, HomeRuns = 3, Outs = 1 },
        };
        foreach (var p in DominancePolicy.All)
        {
            foreach (var count in new[] { PitchCountState.Zero, new PitchCountState(1, 0), new PitchCountState(0, 2), new PitchCountState(3, 2) })
            {
                var decisions = variants.Select(s => (p.AtPlateAppearance(s with { Count = count }), p.IntentFor(s with { Count = count }))).Distinct().ToList();
                Assert.Single(decisions);                                                      // no score, inning, half or out dependence
                var (decision, intent) = decisions[0];
                Assert.Equal(p.Pitcher switch { PitcherStrategy.Attack => PitchIntent.Attack, PitcherStrategy.Careful => PitchIntent.Careful, _ => PitchIntent.Balanced }, intent);
                var expected = p.Batter switch
                {
                    BatterStrategy.TakeFirst => count == PitchCountState.Zero ? PaDecision.TakeOne : PaDecision.Choose(ApproachKind.Normal),
                    BatterStrategy.Patient => PaDecision.Choose(ApproachKind.Patient),
                    BatterStrategy.Aggressive => PaDecision.Choose(ApproachKind.Aggressive),
                    _ => PaDecision.Choose(ApproachKind.Normal),
                };
                Assert.Equal(expected, decision);
            }
        }

        // no mutable state: every instance field is read-only, so a decision can never depend on an earlier one
        Assert.All(typeof(DominancePolicy).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), f => Assert.True(f.IsInitOnly, f.Name));
        Assert.Empty(typeof(DominancePolicy).GetConstructors(BindingFlags.Instance | BindingFlags.Public));
    }

    /// <summary>Records every decision point the executor asks the wrapped matrix policy about.</summary>
    private sealed class Recording(IGamePolicy inner) : IGamePolicy
    {
        public List<GameState> PaStates { get; } = [];

        public string Id => inner.Id;

        public PaDecision AtPlateAppearance(GameState state)
        {
            PaStates.Add(state);
            return inner.AtPlateAppearance(state);
        }

        public PitchIntent IntentFor(GameState state) => inner.IntentFor(state);
    }

    [Fact]
    public void A1_every_half_inning_of_innings_1_to_8_starts_empty_and_takefirst_takes_only_at_0_0()
    {
        foreach (var cell in new[] { DominanceMatrix.Cells.Single(c => c.Id == "DOM-SA-TK"), DominanceMatrix.Cells.Single(c => c.Id == "DOM-ON-PC") })
        {
            var recording = new Recording(H.Ok(PolicyRegistry.Resolve(cell.PolicyId)));
            var baseConfig = H.Ok(DominanceMatrix.Config(cell, "a1"));
            var config = H.Ok(RunConfig.Create("a1", "test", baseConfig.Cohort, baseConfig.Rules, baseConfig.Tuning, baseConfig.Park, baseConfig.AdvancementPolicyVersion, baseConfig.EventBudget, recording,
                baseConfig.CompetitionKey));
            var game = GameExecutor.Play(config, config.Spec(1, 7));
            Assert.True(game.Completed, game.Failure?.Code);
            var firsts = recording.PaStates.Where(s => s.Inning <= RegulationRuns.Innings).GroupBy(s => (s.Inning, s.Half)).Select(g => g.First()).ToList();
            Assert.Equal(16, firsts.Count);
            Assert.All(firsts, s => Assert.Equal((0, 0, PitchCountState.Zero), (s.Outs, s.Bases.Count, s.Count)));   // nothing carried over: outs, bases and count reset (GameReducer.NextHalf)
            Assert.Empty(recording.PaStates.SelectMany(s => s.Substitutions));                                        // and no substitution ever happened
            if (cell.Batter == BatterStrategy.TakeFirst)
            {
                var takes = recording.PaStates.Count(s => s.Count == PitchCountState.Zero);
                Assert.Equal(takes, (int)game.Counters.Takes);                                                        // exactly one take per 0-0 decision point, none elsewhere
                Assert.Equal(game.Counters.PlateAppearances, takes);                                                  // each PA reaches 0-0 exactly once
            }
        }
    }

    // ================================================================ the observation (innings 1-8 half-inning runs)

    [Fact]
    public void The_regulation_observation_reads_16_half_innings_and_refuses_a_short_or_inconsistent_line_score()
    {
        int[] away = [0, 1, 0, 0, 2, 0, 0, 0, 1], home = [1, 0, 0, 3, 0, 0, 0, 0];
        var plays = new Dictionary<(int, InningHalf), int>();
        for (var i = 0; i < away.Length; i++) if (away[i] > 0) plays[(i + 1, InningHalf.Top)] = away[i];
        for (var i = 0; i < home.Length; i++) if (home[i] > 0) plays[(i + 1, InningHalf.Bottom)] = home[i];
        var ok = RegulationRuns.Compare(away, home, 4, 4, plays);
        Assert.True(ok.Ok);
        Assert.Equal(new[] { 0, 1, 1, 0, 0, 0, 0, 3, 2, 0, 0, 0, 0, 0, 0, 0 }, ok.HalfInnings);
        Assert.Equal(7, ok.Total);                                                             // the ninth-inning run is not in the observation

        Assert.Equal("Dominance.regulation_incomplete", RegulationRuns.Compare(away, home[..7], 4, 4, plays).ErrorCode);
        Assert.Equal("Dominance.line_score_total_mismatch", RegulationRuns.Compare(away, home, 5, 4, plays).ErrorCode);
        var wrong = new Dictionary<(int, InningHalf), int>(plays) { [(4, InningHalf.Bottom)] = 2 };
        Assert.Equal("Dominance.line_score_mismatch", RegulationRuns.Compare(away, home, 4, 4, wrong).ErrorCode);
        Assert.False(ok.SameAs(RegulationRuns.Compare(away, home, 4, 4, wrong)));

        foreach (var g in Baseline.Value.SelectMany(r => r.Games))                             // every real game: 16 values, cross-check passed, never more than the final score
        {
            Assert.True(g.Regulation is { Ok: true }, g.Regulation?.ErrorCode);
            Assert.Equal(16, g.Regulation!.HalfInnings.Length);
            Assert.True(g.Regulation.Total <= g.AwayRuns + g.HomeRuns);
        }
    }

    // ================================================================ statistics (pure, synthetic)

    [Fact]
    public void The_normal_quantiles_match_an_independent_reference()
    {
        // Python statistics.NormalDist().inv_cdf(1 - tail), recorded once
        foreach (var (tail, z) in new[] { (0.05, 1.6448536269514715), (0.025, 1.9599639845400536), (0.05 / 32, 2.9551668474978343), (0.05 / 36, 2.99131611518378) })
            Assert.InRange(DominanceStatistics.UpperQuantile(tail), z - 1e-8, z + 1e-8);
        Assert.Equal(DominanceStatistics.UpperQuantile(0.05), DominanceStatistics.ZAlpha);
        Assert.InRange(DominanceStatistics.UpperQuantile(0.99), -2.3263478740408 - 1e-8, -2.3263478740408 + 1e-8);   // lower region (symmetry)
        Assert.Throws<ArgumentOutOfRangeException>(() => DominanceStatistics.UpperQuantile(0));
        Assert.Equal((0.05, 0.05, 0.8), (DominanceStatistics.Delta, DominanceStatistics.Alpha, DominanceStatistics.TargetPower));   // ADR-008 FC-D4.2
    }

    [Fact]
    public void Superiority_is_strict_at_the_one_sided_bound_and_holm_is_inclusive_at_its_threshold()
    {
        var z = DominanceStatistics.ZAlpha;
        Assert.Equal(0, DominanceStatistics.LowerBound(z, 1));
        Assert.False(DominanceStatistics.Superior(z, 1));                                       // LCB exactly 0: not superior
        Assert.True(DominanceStatistics.Superior(Math.BitIncrement(z), 1));

        Assert.Equal(1.0, DominanceStatistics.NonSuperiorityZ(0.03, 0.02), 12);
        Assert.Equal(0, DominanceStatistics.NonSuperiorityZ(DominanceStatistics.Delta, 0.01));  // D = δ exactly: Z = 0, never a rejection
        Assert.Throws<ArgumentOutOfRangeException>(() => DominanceStatistics.NonSuperiorityZ(0.01, 0));   // SE 0: the Welch z is undefined, not infinitely precise

        double c2 = DominanceStatistics.UpperQuantile(0.025), c1 = DominanceStatistics.UpperQuantile(0.05);
        var both = DominanceStatistics.Holm([c1, c2]);                                          // largest first at α/2, then α/1; both exactly on their thresholds
        Assert.Equal(new (double?, bool)[] { (c1, true), (c2, true) }, both);
        var stop = DominanceStatistics.Holm([Math.BitDecrement(c2), 10]);                       // 10 rejected at α/2, then 1.9599… ≥ z(α/1) = 1.6448…: rejected too
        Assert.True(stop[1].Rejected && stop[0].Rejected);
        var halted = DominanceStatistics.Holm([Math.BitDecrement(c2), 0.5, 3]);                 // m = 3: 3 ≥ z(α/3) = 2.128; 1.9599… < z(α/2): stop, the 0.5 is never tested
        Assert.True(halted[2].Rejected);
        Assert.False(halted[0].Rejected);
        Assert.Equal(c2, halted[0].Critical);
        Assert.Equal((null, false), halted[1]);
        Assert.Equal(DominanceStatistics.Holm([2, 2, 2]), DominanceStatistics.Holm([2, 2, 2]));
    }

    [Fact]
    public void The_cell_sample_is_exact_game_cluster_arithmetic()
    {
        var s = CellSample.Of([1, 3]);
        Assert.Equal((2, 4L, 10L), (s.Games, s.TotalRuns, s.SumOfSquares));
        Assert.Equal(0.125, s.Mean);                                                            // 4 / (16·2)
        Assert.Equal(0.0078125, s.Variance);                                                    // ((1/16 − 1/8)² + (3/16 − 1/8)²) / 1
        Assert.Null(CellSample.Of([5]).Variance);
        Assert.Equal(0, CellSample.Of([4, 4, 4]).Variance);
        Assert.Throws<ArgumentOutOfRangeException>(() => CellSample.Of([-1]));
    }

    /// <summary>48 synthetic samples: <paramref name="runs"/> gives each cell's per-game innings-1-8 runs.</summary>
    private static Dictionary<string, CellSample> Samples(Func<DominanceCell, IEnumerable<int>> runs) => DominanceMatrix.Cells.ToDictionary(c => c.Id, c => CellSample.Of(runs(c)));

    private static IEnumerable<int> Alternating(int n, int low, int high, int bumped = 0) => Enumerable.Range(0, n).Select(i => (i % 2 == 0 ? low : high) + (i < bumped ? 1 : 0));

    private static (SideResult B, SideResult P, DominanceGate Gate) Judge(Dictionary<string, CellSample> samples)
    {
        var (b, p) = DominanceStatistics.Evaluate(samples);
        return (b, p, DominanceEvaluator.GateOf(b, p).Gate);
    }

    [Fact]
    public void Equal_cells_with_enough_precision_support_no_dominance_on_both_sides_and_pass()
    {
        var (b, p, gate) = Judge(Samples(_ => Alternating(200, 8, 9)));
        Assert.Equal((SideVerdict.NoDominanceSupported, SideVerdict.NoDominanceSupported, DominanceGate.Pass), (b.Verdict, p.Verdict, gate));
        Assert.Equal(new[] { 36, 36, 36, 36 }, b.Strategies.Select(s => s.Witnesses.Count));    // |W_σ| = 3 rivals × 12 columns
        Assert.Equal(new[] { 32, 32, 32 }, p.Strategies.Select(s => s.Witnesses.Count));        // 2 rivals × 16 columns
        Assert.All(b.Strategies.Concat(p.Strategies), s => Assert.Equal((false, true, "NOT_DOMINANT"), (s.Dominant, s.NotDominant, s.Label)));
        Assert.All(b.Strategies.Concat(p.Strategies).SelectMany(s => s.Witnesses), w => Assert.Equal(0, w.Difference));
    }

    [Fact]
    public void A_strategy_better_in_every_matchup_is_detected_and_fails_the_gate_on_either_side()
    {
        var (b, p, gate) = Judge(Samples(c => Alternating(200, 8, 9).Select(r => c.Batter == BatterStrategy.Patient ? r + 16 : r)));   // +1 run per half-inning
        Assert.Equal((SideVerdict.DominanceDetected, SideVerdict.NoDominanceSupported, DominanceGate.Fail), (b.Verdict, p.Verdict, gate));
        Assert.Equal(new[] { "Patient" }, b.Strategies.Where(s => s.Dominant).Select(s => s.Strategy));
        Assert.All(b.Strategies.Single(s => s.Strategy == "Patient").Witnesses, w => Assert.Equal(1.0, w.Difference, 12));
        Assert.Contains("Patient", b.Reason);

        // pitcher side: fewer runs allowed is better (D_P = R̄(τ) − R̄(σ))
        var (b2, p2, gate2) = Judge(Samples(c => Alternating(200, 8, 9).Select(r => c.Pitcher == PitcherStrategy.Careful ? r : r + 16)));
        Assert.Equal((SideVerdict.NoDominanceSupported, SideVerdict.DominanceDetected, DominanceGate.Fail), (b2.Verdict, p2.Verdict, gate2));
        Assert.Equal(new[] { "Careful" }, p2.Strategies.Where(s => s.Dominant).Select(s => s.Strategy));
    }

    [Fact]
    public void Dominance_needs_every_matchup_and_takes_precedence_over_a_sub_delta_margin()
    {
        // better everywhere except one column (pitcher Attack, opposite hand, NonAutomated): IUT fails, and that column proves "not dominant"
        var (b, _, gate) = Judge(Samples(c => Alternating(200, 8, 9).Select(r => c.Batter == BatterStrategy.Patient && c.BatterColumn != "KON" ? r + 16 : r)));
        var patient = b.Strategies.Single(s => s.Strategy == "Patient");
        Assert.Equal((false, true), (patient.Dominant, patient.NotDominant));
        Assert.Equal(SideVerdict.NoDominanceSupported, b.Verdict);
        Assert.Equal(DominanceGate.Pass, gate);

        // significantly better everywhere, but by about 0.03 < δ runs per half-inning: both conditions hold and dominance wins (FC-D4.3, proposal §6.4-3)
        var (b2, _, gate2) = Judge(Samples(c => c.Batter == BatterStrategy.Patient ? Alternating(200, 8, 9, bumped: 96) : Alternating(200, 8, 9)));
        var close = b2.Strategies.Single(s => s.Strategy == "Patient");
        Assert.All(close.Witnesses, w => Assert.InRange(w.Difference, 0.0299, 0.0301));
        Assert.Equal((true, true, "DOMINANT"), (close.Dominant, close.NotDominant, close.Label));
        Assert.Equal((SideVerdict.DominanceDetected, DominanceGate.Fail), (b2.Verdict, gate2));
    }

    [Fact]
    public void Too_little_precision_is_inconclusive_never_a_pass_and_the_statistics_are_deterministic()
    {
        var samples = Samples(_ => [0, 32]);
        var (b, p, gate) = Judge(samples);
        Assert.Equal((SideVerdict.Inconclusive, SideVerdict.Inconclusive, DominanceGate.Incomplete), (b.Verdict, p.Verdict, gate));
        Assert.All(b.Strategies.Concat(p.Strategies), s => Assert.Equal("UNDETERMINED", s.Label));

        var again = DominanceStatistics.Evaluate(samples);
        Assert.Equal(b.Strategies.SelectMany(s => s.Witnesses), again.Batter.Strategies.SelectMany(s => s.Witnesses));
        Assert.Equal(p.Strategies.SelectMany(s => s.Witnesses), again.Pitcher.Strategies.SelectMany(s => s.Witnesses));
        Assert.Throws<ArgumentException>(() => DominanceStatistics.Evaluate(Samples(_ => [3])));                   // below two games the statistics refuse to run

        // two constant cells compared with each other: no defined Welch z, so nothing is concluded (both sides NOT_EVALUATED, never a PASS from a zero variance)
        var constant = Judge(Samples(c => c.Id is "DOM-SA-NB" or "DOM-SA-PB" ? new[] { 4, 4 } : new[] { 3, 6 }));
        Assert.Equal((SideVerdict.NotEvaluated, SideVerdict.NotEvaluated, DominanceGate.NotEvaluated), (constant.B.Verdict, constant.P.Verdict, constant.Gate));
        Assert.Contains("DOM-SA-NB/DOM-SA-PB", constant.B.Reason);
        var lone = Judge(Samples(c => c.Id is "DOM-SA-NB" or "DOM-ON-TC" ? new[] { 4, 4 } : new[] { 3, 6 }));                       // constant cells never compared with each other: defined
        Assert.NotEqual(SideVerdict.NotEvaluated, lone.B.Verdict);
        Assert.Throws<ArgumentException>(() => DominanceStatistics.Evaluate(new Dictionary<string, CellSample>()));

        var notEvaluated = new SideResult(StrategySide.Pitcher, SideVerdict.NotEvaluated, "", []);
        Assert.Equal(DominanceGate.NotEvaluated, DominanceEvaluator.GateOf(b with { Verdict = SideVerdict.NoDominanceSupported }, notEvaluated).Gate);
        Assert.Equal(DominanceGate.Fail, DominanceEvaluator.GateOf(b with { Verdict = SideVerdict.DominanceDetected }, notEvaluated).Gate);
    }

    // ================================================================ evaluator over real cell runs (two games per cell)

    [Fact]
    public void All_48_real_cells_are_evaluable_but_no_gate_is_decided_before_the_per_cell_sample_is_frozen()
    {
        var e = DominanceEvaluator.Evaluate(Baseline.Value);
        Assert.All(e.Cells, c => Assert.Equal(CellState.Ok, c.State));
        Assert.Equal(DominanceMatrix.Cells.Select(c => c.Id), e.Cells.Select(c => c.Cell.Id));
        Assert.Empty(e.SetProblems);
        Assert.All(e.Cells, c => Assert.Equal(2, c.Sample!.Games));
        Assert.Null(e.FrozenGamesPerCell);
        Assert.Equal(DominanceGate.NotEvaluated, e.Gate);                                       // FC-D4.7: the per-cell sample size is a Final Execution Freeze value, still unfrozen
        Assert.Contains("FC-D4.7", e.GateReason);
        Assert.All(e.Cells, c => Assert.Equal(Of(c.Cell.Id).RunResultHash, c.RunResultHash));

        // with a (test-only) frozen size the same evidence gets exactly the gate its side verdicts imply — a sample this small is no evidence of balance, which is why the size is frozen
        var frozen = DominanceEvaluator.Evaluate(Baseline.Value, 2);
        Assert.Equal(frozen.Batter.Verdict == SideVerdict.NotEvaluated ? DominanceGate.NotEvaluated : DominanceEvaluator.GateOf(frozen.Batter, frozen.Pitcher).Gate, frozen.Gate);
        Assert.Equal((e.Batter.Verdict, e.Pitcher.Verdict), (frozen.Batter.Verdict, frozen.Pitcher.Verdict));
        Assert.All(Baseline.Value.SelectMany(r => r.Games), g => Assert.True(g.Completed, g.Failure?.Code));   // every strategy, TakeFirst included, plays legal complete games

        var shuffled = Baseline.Value.Reverse().Where((_, i) => i % 2 == 0).Concat(Baseline.Value.Reverse().Where((_, i) => i % 2 == 1)).ToList();
        Assert.Equal(e.Canonical(), DominanceEvaluator.Evaluate(shuffled).Canonical());
        Assert.Equal(e.Canonical(), DominanceEvaluator.Evaluate(Baseline.Value).Canonical());
    }

    [Fact]
    public void A_missing_duplicated_foreign_or_null_run_leaves_the_matrix_not_evaluated()
    {
        var missing = DominanceEvaluator.Evaluate(Replace("DOM-OA-TC", null));
        Assert.Equal((DominanceGate.NotEvaluated, SideVerdict.NotEvaluated, SideVerdict.NotEvaluated), (missing.Gate, missing.Batter.Verdict, missing.Pitcher.Verdict));
        Assert.Equal(CellState.Missing, missing.Cells.Single(c => c.Cell.Id == "DOM-OA-TC").State);
        Assert.Contains("DOM-OA-TC=Missing", missing.GateReason);

        var duplicated = DominanceEvaluator.Evaluate(Baseline.Value.Append(Of("DOM-SN-AK")));
        Assert.Equal(DominanceGate.NotEvaluated, duplicated.Gate);
        Assert.Equal(CellState.IntegrityViolation, duplicated.Cells.Single(c => c.Cell.Id == "DOM-SN-AK").State);

        var foreign = H.Ok(RunConfig.Create("f", "smoke", NeutralCohort.Create(), H.Rules(true), H.Tuning, H.Park(), "advancement-policy-v2", 3000, competitionKey: "fc3-dom-x"));
        var withForeign = DominanceEvaluator.Evaluate(Baseline.Value.Append(SimRunner.Run(foreign, [1])));
        Assert.Equal(DominanceGate.NotEvaluated, withForeign.Gate);
        Assert.Contains(withForeign.SetProblems, p => p.Contains("fc3-dom-x"));

        Assert.Equal(DominanceGate.NotEvaluated, DominanceEvaluator.Evaluate(Baseline.Value.Append(null)).Gate);
        Assert.Equal(DominanceGate.NotEvaluated, DominanceEvaluator.Evaluate([]).Gate);
    }

    [Fact]
    public void A_run_that_is_not_its_cells_evidence_is_an_integrity_violation()
    {
        var cell = DominanceMatrix.Cells.Single(c => c.Id == "DOM-SA-NB");
        var original = Of(cell.Id);
        CellState StateWith(RunResult run) => DominanceEvaluator.Evaluate(Replace(cell.Id, run)).Cells.Single(c => c.Cell.Id == cell.Id).State;
        RunResult RunWith(IGamePolicy? policy = null, CohortInputs? cohort = null, bool automated = true, int budget = 3000) =>
            SimRunner.Run(H.Ok(RunConfig.Create("i", "t", cohort ?? NeutralCohort.Create(), H.Rules(automated), H.Tuning, H.Park(), "advancement-policy-v2", budget, policy ?? DominancePolicy.Of(BatterStrategy.Normal, PitcherStrategy.Balanced),
                cell.CompetitionKey)), Seeds);

        Assert.Equal(CellState.Ok, StateWith(RunWith()));                                       // the same inputs built by hand are the same evidence
        Assert.Equal(CellState.IntegrityViolation, StateWith(RunWith(policy: DominancePolicy.Of(BatterStrategy.Patient, PitcherStrategy.Balanced))));   // another cell's strategy under this key
        Assert.Equal(CellState.IntegrityViolation, StateWith(RunWith(policy: new NormalBalancedPolicy())));                                               // same behaviour, not a matrix policy
        Assert.Equal(CellState.IntegrityViolation, StateWith(RunWith(cohort: NeutralCohort.CreateOpposite())));                                           // wrong hand
        Assert.Equal(CellState.IntegrityViolation, StateWith(RunWith(automated: false)));                                                                 // wrong mode
        Assert.Equal(CellState.IntegrityViolation, StateWith(RunWith(budget: 2999)));                                                                     // not the fixed profile
        var stronger = H.Variant(NeutralCohort.Id, s => s.Ratings.Pitcher is null ? null : H.WithRatings(s, null, NeutralCohort.Pitcher(55, 50, 50), null), kind: CohortKind.Neutral);
        Assert.NotEqual(NeutralCohort.Create().InputsHash, stronger.InputsHash);
        Assert.Equal(CellState.IntegrityViolation, StateWith(RunWith(cohort: stronger)));                                                                  // same cohort id, other inputs

        var reseeded = original with { Specs = [original.Specs[0] with { Seed = 99 }, original.Specs[1]] };                                               // seed changed, hashes not
        Assert.Equal(CellState.IntegrityViolation, StateWith(reseeded));
        Assert.Equal(CellState.IntegrityViolation, StateWith(original with { RunResultHash = new string('0', 64) }));
        Assert.Equal(CellState.IntegrityViolation, StateWith(original with { Games = [original.Games[1], original.Games[0]] }));                           // reordered results
        Assert.Equal(CellState.IntegrityViolation, StateWith(original with { Aggregate = new MetricCounters() }));

        // ADR-009 SR-D5: a stop-aware run is never fc3-dom evidence, even when it ran the same games
        var config = H.Ok(DominanceMatrix.Config(cell, "stop"));
        var stopAware = SimRunner.RunStopAware(config, new StopPlan("fc2-dry", 2, 1, original.Specs, "x.json", new string('a', 64)));
        Assert.NotNull(stopAware.Stop);
        var refused = DominanceEvaluator.Evaluate(Replace(cell.Id, stopAware)).Cells.Single(c => c.Cell.Id == cell.Id);
        Assert.Equal(CellState.IntegrityViolation, refused.State);
        Assert.Contains("SR-D5", refused.Reason);                                               // refused as stop-aware, before any hash comparison
    }

    [Fact]
    public void A_failed_game_or_metrics_failure_fails_the_gate_and_is_never_hidden()
    {
        var cell = DominanceMatrix.Cells.Single(c => c.Id == "DOM-ON-PK");
        var original = Of(cell.Id);
        var config = H.Ok(DominanceMatrix.Config(cell, "fail"));
        var shortBudget = H.Ok(RunConfig.Create("fail", "t", config.Cohort, config.Rules, config.Tuning, config.Park, config.AdvancementPolicyVersion, 10, config.Policy, config.CompetitionKey));
        var failedGame = GameExecutor.Play(shortBudget, original.Specs[1]);
        Assert.Equal(GameRunStatus.Failed, failedGame.Status);
        var withFailure = Rebuild(original, original.Specs, [original.Games[0], failedGame]);

        var e = DominanceEvaluator.Evaluate(Replace(cell.Id, withFailure));
        var c = e.Cells.Single(x => x.Cell.Id == cell.Id);
        Assert.Equal((CellState.EngineFailure, 1), (c.State, c.FailedGames));
        Assert.Equal((DominanceGate.Fail, SideVerdict.NotEvaluated), (e.Gate, e.Batter.Verdict));
        Assert.Contains("DOM-ON-PK", e.GateReason);
        Assert.Contains("Harness.event_budget_exceeded", e.GateReason);

        // a missing cell elsewhere cannot turn the known failure into NOT_EVALUATED, and is still named
        var both = DominanceEvaluator.Evaluate(Replace(cell.Id, withFailure).Where(r => r.Config.CompetitionKey != "fc3-dom-sa-nb").ToList());
        Assert.Equal(DominanceGate.Fail, both.Gate);
        Assert.Contains("DOM-SA-NB=Missing", both.GateReason);

        // a completed game whose innings-1-8 observation failed its cross-check is a metrics failure (FC-D9.1), not a dropped game
        var broken = original.Games[0] with { Regulation = new RegulationRuns(ImmutableArray<int>.Empty, "Dominance.line_score_mismatch") };
        var m = DominanceEvaluator.Evaluate(Replace(cell.Id, original with { Games = [broken, original.Games[1]] }));
        Assert.Equal((CellState.MetricsFailure, DominanceGate.Fail), (m.Cells.Single(x => x.Cell.Id == cell.Id).State, m.Gate));
        Assert.Contains("Dominance.line_score_mismatch", m.GateReason);
        var none = DominanceEvaluator.Evaluate(Replace(cell.Id, original with { Games = [original.Games[0] with { Regulation = null }, original.Games[1]] }));
        Assert.Equal(DominanceGate.Fail, none.Gate);
    }

    // ================================================================ FC2C-01: a failed metric computation of a trusted run (FC-D9.1)

    private static RunResult WithMetricsError(RunResult r, string code = "Metrics.counts_inconsistent") => r with { Metrics = Result<MetricsReport>.Failure(new Error(code, "test")) };

    [Fact]
    public void A_trusted_cell_run_whose_metric_computation_failed_is_a_metrics_failure_and_fails_the_gate()
    {
        var cell = DominanceMatrix.Cells.Single(c => c.Id == "DOM-ON-PK");
        var original = Of(cell.Id);
        Assert.True(original.MetricsComputed);
        var broken = WithMetricsError(original);
        Assert.False(broken.MetricsComputed);
        Assert.Equal((original.ConfigHash, original.RunResultHash), (broken.ConfigHash, broken.RunResultHash));   // not a forged hash: the run is as trustworthy as before, only its metric computation failed

        foreach (int? frozen in new int?[] { null, 2 })
        {
            var e = DominanceEvaluator.Evaluate(Replace(cell.Id, broken), frozen);
            var c = e.Cells.Single(x => x.Cell.Id == cell.Id);
            Assert.Equal((CellState.MetricsFailure, 0, 1, (CellSample?)null), (c.State, c.FailedGames, c.MetricsFailures, c.Sample));
            Assert.Contains("Metrics.counts_inconsistent", c.Reason);                         // the error code is kept in the cell's reason and in the gate's
            Assert.Contains("Metrics.counts_inconsistent", e.GateReason);
            Assert.Contains(cell.Id, e.GateReason);
            Assert.Equal((DominanceGate.Fail, SideVerdict.NotEvaluated, SideVerdict.NotEvaluated), (e.Gate, e.Batter.Verdict, e.Pitcher.Verdict));
            Assert.Equal(47, e.Cells.Count(x => x.State == CellState.Ok));                    // one cell cannot hide in the other 47 healthy ones
        }

        var other = DominanceEvaluator.Evaluate(Replace(cell.Id, WithMetricsError(original, "Metrics.target_missing")));
        Assert.Contains("Metrics.target_missing", other.Cells.Single(x => x.Cell.Id == cell.Id).Reason);
        Assert.NotEqual(other.Canonical(), DominanceEvaluator.Evaluate(Replace(cell.Id, broken)).Canonical());   // the code is part of the compared evaluation text

        // the replay re-executes the cell from its definition: a recorded failure the re-execution does not reproduce is not accepted as a replay
        Assert.Equal("DomReplay.evaluation_mismatch", DominanceReplay.Verify([broken], DominanceEvaluator.Evaluate([broken]))!.Value.Code);
    }

    [Fact]
    public void A_metric_computation_failure_takes_precedence_over_an_unfrozen_or_short_sample_and_a_missing_cell()
    {
        var cell = DominanceMatrix.Cells.Single(c => c.Id == "DOM-OA-TC");
        var broken = WithMetricsError(Of(cell.Id));

        // unfrozen per-cell size (the normal state today): FAIL, not the NOT_EVALUATED of an unfrozen sample
        var unfrozen = DominanceEvaluator.Evaluate(Replace(cell.Id, broken), null);
        Assert.Equal(DominanceGate.Fail, unfrozen.Gate);
        Assert.DoesNotContain("FC-D4.7", unfrozen.GateReason);

        // a frozen size above what was run: every other cell is InsufficientSample, the failure still wins
        var shortOfFrozen = DominanceEvaluator.Evaluate(Replace(cell.Id, broken), 3);
        Assert.Equal(DominanceGate.Fail, shortOfFrozen.Gate);
        Assert.Equal(CellState.MetricsFailure, shortOfFrozen.Cells.Single(x => x.Cell.Id == cell.Id).State);

        // the failing cell itself too short to estimate a variance: still a metrics failure, not InsufficientSample
        var single = WithMetricsError(Rebuild(Of(cell.Id), [Of(cell.Id).Specs[0]], [Of(cell.Id).Games[0]]));
        var tiny = DominanceEvaluator.Evaluate(Replace(cell.Id, single));
        Assert.Equal((CellState.MetricsFailure, DominanceGate.Fail), (tiny.Cells.Single(x => x.Cell.Id == cell.Id).State, tiny.Gate));

        // a missing cell elsewhere cannot turn it into NOT_EVALUATED, and is still named
        var missing = DominanceEvaluator.Evaluate(Replace(cell.Id, broken).Where(r => r.Config.CompetitionKey != "fc3-dom-sa-nb").ToList());
        Assert.Equal(DominanceGate.Fail, missing.Gate);
        Assert.Contains("DOM-SA-NB=Missing", missing.GateReason);
        Assert.Contains("Metrics.counts_inconsistent", missing.GateReason);
    }

    [Fact]
    public void A_working_metric_computation_with_not_evaluated_diagnostics_is_not_a_metrics_failure()
    {
        Assert.All(Baseline.Value, r => Assert.True(r.MetricsComputed));
        Assert.All(Baseline.Value, r => Assert.Contains(r.Metrics.Value.Metrics, m => m.Status == MetricStatus.NotEvaluated));   // the unapproved policy leaves official verdicts NOT_EVALUATED
        var e = DominanceEvaluator.Evaluate(Baseline.Value, 2);
        Assert.All(e.Cells, c => Assert.Equal((CellState.Ok, 0, "평가 가능"), (c.State, c.MetricsFailures, c.Reason)));
        Assert.DoesNotContain("Metrics.", e.GateReason);
        Assert.NotEqual(DominanceGate.Fail, DominanceEvaluator.Evaluate(Baseline.Value).Gate);        // unfrozen: NOT_EVALUATED by FC-D4.7 alone
    }

    [Fact]
    public void A_metric_computation_failure_does_not_displace_game_failures_integrity_or_incomplete_cells()
    {
        var cell = DominanceMatrix.Cells.Single(c => c.Id == "DOM-SN-AK");
        var original = Of(cell.Id);

        // a failed game and a failed metric computation together: the engine failure is the state, both are counted and named
        var config = H.Ok(DominanceMatrix.Config(cell, "both"));
        var shortBudget = H.Ok(RunConfig.Create("both", "t", config.Cohort, config.Rules, config.Tuning, config.Park, config.AdvancementPolicyVersion, 10, config.Policy, config.CompetitionKey));
        var withFailure = WithMetricsError(Rebuild(original, original.Specs, [original.Games[0], GameExecutor.Play(shortBudget, original.Specs[1])]));
        var both = DominanceEvaluator.Evaluate(Replace(cell.Id, withFailure)).Cells.Single(x => x.Cell.Id == cell.Id);
        Assert.Equal((CellState.EngineFailure, 1, 1), (both.State, both.FailedGames, both.MetricsFailures));
        Assert.Contains("Harness.event_budget_exceeded", both.Reason);
        Assert.Contains("Metrics.counts_inconsistent", both.Reason);

        // an untrusted run stays an integrity violation: its metric state is not evidence, and it does not turn the gate into FAIL
        var untrusted = DominanceEvaluator.Evaluate(Replace(cell.Id, WithMetricsError(original with { RunResultHash = new string('0', 64) })));
        Assert.Equal((CellState.IntegrityViolation, 0, DominanceGate.NotEvaluated), (untrusted.Cells.Single(x => x.Cell.Id == cell.Id).State, untrusted.Cells.Single(x => x.Cell.Id == cell.Id).MetricsFailures, untrusted.Gate));

        // an incomplete run keeps its Incomplete state; the known failure is still not hidden (FC-D9.1)
        var truncated = WithMetricsError(Rebuild(original, original.Specs, [original.Games[0]]));
        var incomplete = DominanceEvaluator.Evaluate(Replace(cell.Id, truncated));
        Assert.Equal((CellState.Incomplete, 1, DominanceGate.Fail), (incomplete.Cells.Single(x => x.Cell.Id == cell.Id).State, incomplete.Cells.Single(x => x.Cell.Id == cell.Id).MetricsFailures, incomplete.Gate));

        // a wrong-seed / foreign cell run is still refused as before, metric state or not
        var reseeded = WithMetricsError(original with { Specs = [original.Specs[0] with { Seed = 99 }, original.Specs[1]] });
        Assert.Equal(CellState.IntegrityViolation, DominanceEvaluator.Evaluate(Replace(cell.Id, reseeded)).Cells.Single(x => x.Cell.Id == cell.Id).State);
    }

    // ================================================================ FC2C-02: the approved catalogue cannot be changed through its public surface

    [Fact]
    public void The_cell_catalogue_and_its_key_lookup_cannot_be_changed_by_a_caller()
    {
        var ids = DominanceMatrix.Cells.Select(c => c.Id).ToArray();
        var keys = DominanceMatrix.Cells.Select(c => c.CompetitionKey).ToArray();
        var first = DominanceMatrix.Cells[0];
        var second = DominanceMatrix.Cells[1];

        Assert.False(DominanceMatrix.Cells is List<DominanceCell> or DominanceCell[]);
        Assert.False(DominanceMatrix.ByCompetitionKey is Dictionary<string, DominanceCell>);

        // every mutating interface a cast could reach refuses; none of these may succeed
        var list = Assert.IsAssignableFrom<IList<DominanceCell>>(DominanceMatrix.Cells);
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list[0] = second);
        Assert.Throws<NotSupportedException>(() => list.Add(second));
        Assert.Throws<NotSupportedException>(() => list.Insert(0, second));
        Assert.Throws<NotSupportedException>(() => list.RemoveAt(0));
        Assert.Throws<NotSupportedException>(() => list.Remove(first));
        Assert.Throws<NotSupportedException>(() => list.Clear());
        var legacy = (System.Collections.IList)DominanceMatrix.Cells;
        Assert.True(legacy.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => legacy[0] = second);

        if (DominanceMatrix.ByCompetitionKey is IDictionary<string, DominanceCell> map)
        {
            Assert.True(map.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => map["fc3-dom-extra"] = first);
            Assert.Throws<NotSupportedException>(() => map.Add("fc3-dom-extra2", first));
            Assert.Throws<NotSupportedException>(() => map[first.CompetitionKey] = second);
            Assert.Throws<NotSupportedException>(() => map.Remove(first.CompetitionKey));
            Assert.Throws<NotSupportedException>(() => map.Clear());
        }

        // after every attempt the catalogue is exactly the approved 48 cells, in order, and the lookup resolves to the very same definitions
        Assert.Equal(ids, DominanceMatrix.Cells.Select(c => c.Id));
        Assert.Equal(48, DominanceMatrix.Cells.Count);
        Assert.Equal(Enumerable.Range(1, 48), DominanceMatrix.Cells.Select(c => c.Number));
        Assert.Equal(48, DominanceMatrix.ByCompetitionKey.Count);
        Assert.Equal(keys.Order(StringComparer.Ordinal), DominanceMatrix.ByCompetitionKey.Keys.Order(StringComparer.Ordinal));
        Assert.All(DominanceMatrix.Cells, c => Assert.Same(c, DominanceMatrix.ByCompetitionKey[c.CompetitionKey]));
        Assert.Same(first, DominanceMatrix.Cells[0]);
        Assert.False(DominanceMatrix.ByCompetitionKey.ContainsKey("fc3-dom-extra"));
        Assert.Equal(ids, DominanceMatrix.Cells.Select(c => c.Id));                              // deterministic: the same order on every read
    }

    [Fact]
    public void Incomplete_or_too_small_cells_are_not_evaluated_or_insufficient_never_pass()
    {
        // fewer results than the plan (a run that did not execute its whole seed list): not a full execution
        var cell = DominanceMatrix.Cells.Single(c => c.Id == "DOM-SN-TB");
        var original = Of(cell.Id);
        var truncated = Rebuild(original, original.Specs, [original.Games[0]]);
        var incomplete = DominanceEvaluator.Evaluate(Replace(cell.Id, truncated));
        Assert.Equal((CellState.Incomplete, DominanceGate.NotEvaluated), (incomplete.Cells.Single(c => c.Cell.Id == cell.Id).State, incomplete.Gate));

        // every cell planned and run with one game: the game-level variance cannot be estimated
        var single = Baseline.Value.Select(r => Rebuild(r, [r.Specs[0]], [r.Games[0]])).ToList();
        var insufficient = DominanceEvaluator.Evaluate(single);
        Assert.All(insufficient.Cells, c => Assert.Equal(CellState.InsufficientSample, c.State));
        Assert.Equal((DominanceGate.InsufficientSample, SideVerdict.NotEvaluated), (insufficient.Gate, insufficient.Batter.Verdict));

        // a frozen size above what was run: every cell is short of it (FC-D9.4); a frozen size below the plan: not the frozen plan
        var shortOfFrozen = DominanceEvaluator.Evaluate(Baseline.Value, 3);
        Assert.All(shortOfFrozen.Cells, c => Assert.Equal(CellState.InsufficientSample, c.State));
        Assert.Equal(DominanceGate.InsufficientSample, shortOfFrozen.Gate);
        var overFrozen = DominanceEvaluator.Evaluate(Baseline.Value, 1);
        Assert.Equal(DominanceGate.NotEvaluated, overFrozen.Gate);
        Assert.Contains(overFrozen.SetProblems, p => p.Contains("동결된 셀당 경기 수 1"));

        // one cell planned with a different size than the others (proposal §6.6: the same n for every cell)
        var uneven = DominanceEvaluator.Evaluate(Replace(cell.Id, Rebuild(original, [original.Specs[0]], [original.Games[0]])));
        Assert.Equal(DominanceGate.NotEvaluated, uneven.Gate);
        Assert.Contains(uneven.SetProblems, p => p.Contains("계획 경기 수"));
    }

    // ================================================================ replay

    [Fact]
    public void Replay_reexecutes_all_48_cells_from_their_definitions_and_matches()
    {
        var evaluation = DominanceEvaluator.Evaluate(Baseline.Value);
        Assert.Null(DominanceReplay.Verify(Baseline.Value, evaluation));
    }

    [Fact]
    public void Replay_detects_tampered_games_observations_seeds_and_verdicts()
    {
        var cell = DominanceMatrix.Cells.Single(c => c.Id == "DOM-OA-AC");
        var original = Of(cell.Id);
        var one = new[] { original };
        var recorded = DominanceEvaluator.Evaluate(one);
        Assert.Null(DominanceReplay.Verify(one, recorded));                                      // a partial set replays to its own (NOT_EVALUATED) verdict

        // a consistently re-hashed forged failure, an observation edit (outside every hash), a reseeded plan, a forged result hash, an edited verdict, a foreign run
        var config = H.Ok(DominanceMatrix.Config(cell, "x"));
        var shortBudget = H.Ok(RunConfig.Create("x", "t", config.Cohort, config.Rules, config.Tuning, config.Park, config.AdvancementPolicyVersion, 10, config.Policy, config.CompetitionKey));
        var forged = Rebuild(original, original.Specs, [GameExecutor.Play(shortBudget, original.Specs[0]), original.Games[1]]);
        Assert.Equal("DomReplay.result_games_mismatch", DominanceReplay.Verify([forged], DominanceEvaluator.Evaluate([forged]))!.Value.Code);

        var edited = original with { Games = [original.Games[0] with { Regulation = new RegulationRuns([.. Enumerable.Repeat(0, 16)], null) }, original.Games[1]] };
        Assert.Equal(original.RunResultHash, edited.RunResultHash);
        Assert.Equal("DomReplay.result_games_mismatch", DominanceReplay.Verify([edited], DominanceEvaluator.Evaluate([edited]))!.Value.Code);

        var reseeded = original with { Specs = [original.Specs[0] with { Seed = 77 }, original.Specs[1]] };
        Assert.Equal("DomReplay.config_hash_mismatch", DominanceReplay.Verify([reseeded], recorded)!.Value.Code);
        var renamed = original with { Specs = [original.Specs[0] with { GameId = original.Specs[1].GameId }, original.Specs[1]] };
        Assert.Equal("DomReplay.game_id_mismatch", DominanceReplay.Verify([renamed], recorded)!.Value.Code);
        Assert.Equal("DomReplay.run_result_hash_mismatch", DominanceReplay.Verify([original with { RunResultHash = new string('f', 64) }], recorded)!.Value.Code);
        Assert.Equal("DomReplay.evaluation_mismatch", DominanceReplay.Verify(one, recorded with { Gate = DominanceGate.Pass })!.Value.Code);
        Assert.Equal("DomReplay.evaluation_mismatch", DominanceReplay.Verify(one, DominanceEvaluator.Evaluate(Baseline.Value))!.Value.Code);
        Assert.Equal("DomReplay.evaluation_mismatch", DominanceReplay.Verify(one, DominanceEvaluator.Evaluate(one, 2))!.Value.Code);   // a record cannot bring its own frozen size
        Assert.Null(DominanceReplay.Verify(one, DominanceEvaluator.Evaluate(one, 2), 2));

        var foreign = SimRunner.Run(H.Ok(RunConfig.Create("f", "smoke", NeutralCohort.Create(), H.Rules(true), H.Tuning, H.Park(), "advancement-policy-v2", 3000, competitionKey: "fc3-dom-x")), [1]);
        Assert.Equal("DomReplay.cell_unknown", DominanceReplay.Verify([foreign], recorded)!.Value.Code);
    }

    [Fact]
    public void Repeat_runs_are_identical_and_official_execution_stays_disabled()
    {
        var cell = DominanceMatrix.Cells.Single(c => c.Id == "DOM-ON-TC");
        var again = DominanceMatrix.Run(cell, Seeds, "another-run-id");
        var first = Of(cell.Id);
        Assert.Equal((first.ConfigHash, first.RunResultHash), (again.ConfigHash, again.RunResultHash));          // the run id is not an input
        Assert.True(first.Games.Zip(again.Games).All(p => p.First.Regulation!.SameAs(p.Second.Regulation)));
        Assert.NotEqual(first.ConfigHash, Of("DOM-OA-TC").ConfigHash);                                          // another cell is another run (key, cohort, rules → GameId / RNG context)

        Assert.False(OfficialApprovals.Current.ExecutionPathReady);
        Assert.Empty(SeedManifestPins.Official);
        Assert.Null(OfficialApprovals.Current.GitCommitSha);
    }
}
