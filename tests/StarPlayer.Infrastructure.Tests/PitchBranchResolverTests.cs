using System.Reflection;
using System.Text.Json;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-C2A: the single-pitch probability branch drawn from the real seeded streams. Expected results come from the independent Python oracle
/// (tests/Fixtures/generator/generate_pitch_branch_golden.py), never from the resolver under test.
/// </summary>
public class PitchBranchResolverTests
{
    // ---- the independent golden scenarios (chains of pitches that share one RNG; some start from an already advanced RNG) ----

    public static IEnumerable<object[]> ScenarioIds() => Golden.GetProperty("scenarios").EnumerateArray().Select(s => new object[] { s.GetProperty("id").GetString()! });

    private static JsonElement Scenario(string id) => Golden.GetProperty("scenarios").EnumerateArray().Single(s => s.GetProperty("id").GetString() == id);

    private static (JsonElement Spec, PitchBranchDraft Draft, JsonElement Expected)[] RunScenario(string id)
    {
        var scenario = Scenario(id);
        var context = scenario.GetProperty("context").GetString()!; // the golden context key is also the game id of the state
        var rng = Snapshot(Hex64(scenario.GetProperty("seed")), context,
            scenario.GetProperty("prior_calls").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32()));
        var results = new List<(JsonElement, PitchBranchDraft, JsonElement)>();
        foreach (var pitch in scenario.GetProperty("pitches").EnumerateArray())
        {
            var spec = pitch.GetProperty("spec");
            var take = spec.GetProperty("explicit_take").GetBoolean();
            var draft = Resolved(Request(StateFor(RulesFor(spec.GetProperty("called_pitch_mode").GetString()!), take, context), rng,
                Intent(spec.GetProperty("intent").GetString()!), take, Bats(spec.GetProperty("batter_side").GetString()!), Throws(spec.GetProperty("pitcher_hand").GetString()!)));
            results.Add((spec, draft, pitch.GetProperty("expected")));
            rng = draft.NextRng; // the next pitch continues from this position
        }

        return results.ToArray();
    }

    private static string? Text(JsonElement e, string name) => e.GetProperty(name).ValueKind == JsonValueKind.Null ? null : e.GetProperty(name).GetString();

    [Theory]
    [MemberData(nameof(ScenarioIds))]
    public void Every_golden_scenario_reproduces_the_independent_oracle_exactly(string id)
    {
        var scenario = Scenario(id);
        var results = RunScenario(id);
        foreach (var (_, draft, expected) in results)
        {
            Assert.Equal(Text(expected, "target_zone"), draft.Target.Zone.Value);
            Assert.Equal(Text(expected, "location_zone"), draft.Location.Zone.Value);
            Assert.Equal(Text(expected, "applied_side"), draft.AppliedSide.ToString().ToLowerInvariant());
            Assert.Equal(Text(expected, "boundary"), draft.Location.Boundary.ToString().ToLowerInvariant());
            Assert.Equal(Text(expected, "decision"), draft.BatterDecision);
            Assert.Equal(Text(expected, "called_pitch"), draft.CalledPitch);
            Assert.Equal(Text(expected, "swing_result"), draft.SwingResult);
            Assert.Equal(Text(expected, "batted_ball"), draft.BattedBall);
            Assert.Equal(Text(expected, "defense_result"), draft.DefenseResult);
            Assert.Equal(Kind(Text(expected, "kind")!), draft.Kind);
            Assert.Equal(expected.GetProperty("total_calls").GetInt32(), draft.NextBoundedCalls);

            var steps = expected.GetProperty("steps").EnumerateArray().ToArray();
            Assert.Equal(steps.Length, draft.Steps.Length);
            for (var i = 0; i < steps.Length; i++)
            {
                Assert.Equal(Text(steps[i], "stage"), draft.Steps[i].Stage);
                Assert.Equal(Text(steps[i], "condition"), draft.Steps[i].Condition);
                Assert.Equal(Text(steps[i], "outcome"), draft.Steps[i].Outcome);
                Assert.Equal(Text(steps[i], "stream"), draft.Steps[i].Stream.Canonical);
                Assert.Equal(steps[i].GetProperty("roll").GetUInt32(), draft.Steps[i].Roll);
                Assert.Equal(steps[i].GetProperty("raw_draws").GetInt64(), draft.Steps[i].RawDraws);
            }

            var calls = expected.GetProperty("calls_by_stream").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32());
            var draws = expected.GetProperty("draws_by_stream").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt64());
            Assert.Equal(calls.Keys.OrderBy(k => k, StringComparer.Ordinal), draft.StreamUsage.Select(u => u.Stream.Canonical)); // sorted, only reached streams
            foreach (var use in draft.StreamUsage)
            {
                Assert.Equal(calls[use.Stream.Canonical], use.Calls);
                Assert.Equal(draws[use.Stream.Canonical], use.RawDraws);
            }
        }

        var last = results[^1].Draft;
        Assert.Equal(scenario.GetProperty("final_canonical").GetString(), last.NextRng.Canonical());
        Assert.Equal(scenario.GetProperty("final_state_hash").GetString(), last.NextRng.StateHash);
    }

    [Fact]
    public void The_golden_set_covers_every_branch_kind_both_call_modes_every_defense_result_and_a_rejected_draw()
    {
        var pitches = Golden.GetProperty("scenarios").EnumerateArray().SelectMany(s => s.GetProperty("pitches").EnumerateArray()).ToArray();
        var seen = pitches.Select(p => (Text(p.GetProperty("expected"), "kind"), p.GetProperty("spec").GetProperty("explicit_take").GetBoolean())).ToHashSet();
        foreach (var kind in new[] { "hit_by_pitch", "called_ball", "called_strike" })
        {
            Assert.Contains((kind, false), seen);
            Assert.Contains((kind, true), seen);
        }

        foreach (var kind in new[] { "swinging_strike", "foul", "in_play" })
            Assert.Contains((kind, false), seen);
        Assert.Equal(["double", "home_run", "out", "reached_on_error", "single", "triple"],
            pitches.Select(p => Text(p.GetProperty("expected"), "defense_result")).Where(d => d is not null).Distinct().OrderBy(d => d, StringComparer.Ordinal));
        Assert.Equal(["automated", "non_automated"], pitches.Select(p => Text(p.GetProperty("spec"), "called_pitch_mode")).Distinct().OrderBy(m => m, StringComparer.Ordinal));
        // an explicit Take that met a "swing" decision still ends as a called pitch
        Assert.Contains(pitches, p => p.GetProperty("spec").GetProperty("explicit_take").GetBoolean() && Text(p.GetProperty("expected"), "decision") == "swing");
    }

    [Fact]
    public void A_rejected_raw_output_is_one_decision_call_but_two_raw_draws()
    {
        var results = RunScenario("rejection");
        var draft = Assert.Single(results).Draft;
        Assert.Equal(StageIds.TargetZone, draft.Steps[0].Stage);
        Assert.Equal(2, draft.Steps[0].RawDraws);
        var target = draft.StreamUsage.Single(u => u.Stream == GameRngStreams.Target);
        Assert.Equal(1, target.Calls);
        Assert.Equal(2, target.RawDraws);
        Assert.Equal(draft.Steps.Count(), draft.NextBoundedCalls); // one call per reached decision
        Assert.True(draft.StreamUsage.Sum(u => u.RawDraws) > draft.NextBoundedCalls);
    }

    // ---- stage order, streams and the "not reached = not drawn" rule ----

    private static readonly Dictionary<string, RngStreamId> StreamOfStage = new()
    {
        [StageIds.TargetZone] = GameRngStreams.Target,
        [StageIds.ActualLocation] = GameRngStreams.Location,
        [StageIds.BatterDecision] = GameRngStreams.Pitch,
        [StageIds.CalledPitch] = GameRngStreams.Pitch,
        [StageIds.SwingResult] = GameRngStreams.Contact,
        [StageIds.BattedBall] = GameRngStreams.Contact,
        [StageIds.DefenseResult] = GameRngStreams.Field,
    };

    private static IEnumerable<PitchBranchDraft> AllGoldenDrafts() =>
        Golden.GetProperty("scenarios").EnumerateArray().SelectMany(s => RunScenario(s.GetProperty("id").GetString()!).Select(r => r.Draft));

    [Fact]
    public void Every_stage_draws_from_its_contracted_stream_exactly_once_and_advancement_is_never_drawn()
    {
        foreach (var draft in AllGoldenDrafts())
        {
            Assert.All(draft.Steps, s => Assert.Equal(StreamOfStage[s.Stage], s.Stream));
            Assert.DoesNotContain(draft.Steps, s => s.Stage == StageIds.Advancement);
            Assert.DoesNotContain(draft.StreamUsage, u => u.Stream == GameRngStreams.Runner || u.Stream == GameRngStreams.Ai);
            Assert.Equal(draft.Steps.Select(s => s.Stage).Distinct().Count(), draft.Steps.Length); // no stage twice in one pitch
            Assert.Equal(draft.Steps.Length, draft.NextBoundedCalls);
        }
    }

    [Fact]
    public void Target_comes_first_then_the_actual_location_conditioned_on_it_and_both_before_any_outcome()
    {
        foreach (var draft in AllGoldenDrafts())
        {
            Assert.Equal([StageIds.TargetZone, StageIds.ActualLocation, StageIds.BatterDecision], draft.Steps.Take(3).Select(s => s.Stage));
            Assert.Equal(draft.Intent.ToString().ToLowerInvariant(), draft.Steps[0].Condition);
            Assert.Equal(draft.Steps[0].Outcome, draft.Steps[1].Condition); // the location is drawn GIVEN the target
            Assert.Equal(draft.Steps[1].Outcome, draft.Steps[2].Condition); // the decision is drawn given the actual location
            Assert.Equal(draft.Target.Zone.Value, draft.Steps[0].Outcome);
            Assert.Equal(draft.Location.Zone.Value, draft.Steps[1].Outcome);
        }
    }

    [Fact]
    public void A_stage_that_is_not_reached_draws_nothing()
    {
        foreach (var draft in AllGoldenDrafts())
        {
            var used = draft.StreamUsage.Select(u => u.Stream).ToHashSet();
            switch (draft.Kind)
            {
                case PitchBranchKind.HitByPitch:
                    Assert.Equal(new[] { GameRngStreams.Location, GameRngStreams.Pitch, GameRngStreams.Target }.OrderBy(s => s.Canonical, StringComparer.Ordinal), used.OrderBy(s => s.Canonical, StringComparer.Ordinal));
                    Assert.Equal(3, draft.NextBoundedCalls);
                    break;
                case PitchBranchKind.CalledBall or PitchBranchKind.CalledStrike:
                    Assert.DoesNotContain(GameRngStreams.Contact, used);
                    Assert.DoesNotContain(GameRngStreams.Field, used);
                    Assert.Equal(2, draft.StreamUsage.Single(u => u.Stream == GameRngStreams.Pitch).Calls); // decision + called pitch
                    Assert.Equal(4, draft.NextBoundedCalls);
                    break;
                case PitchBranchKind.SwingingStrike or PitchBranchKind.Foul:
                    Assert.DoesNotContain(GameRngStreams.Field, used);
                    Assert.Equal(1, draft.StreamUsage.Single(u => u.Stream == GameRngStreams.Contact).Calls); // swing result only, no batted ball
                    Assert.Equal(4, draft.NextBoundedCalls);
                    break;
                default:
                    Assert.Equal(2, draft.StreamUsage.Single(u => u.Stream == GameRngStreams.Contact).Calls); // swing result + batted ball
                    Assert.Equal(1, draft.StreamUsage.Single(u => u.Stream == GameRngStreams.Field).Calls);
                    Assert.Equal(6, draft.NextBoundedCalls);
                    break;
            }
        }
    }

    [Fact]
    public void A_stage_with_a_certain_outcome_still_makes_its_contracted_call()
    {
        // automated:in.* is 100% strike and automated:out.* is 100% ball (a baseline row with ppm 1,000,000). The call is made and recorded anyway.
        var drafts = AllGoldenDrafts().Where(d => d.CalledPitch is not null && d.Steps.Any(s => s.Stage == StageIds.CalledPitch && s.Condition.StartsWith("automated:", StringComparison.Ordinal))).ToArray();
        Assert.NotEmpty(drafts);
        foreach (var draft in drafts)
        {
            var step = draft.Steps.Single(s => s.Stage == StageIds.CalledPitch);
            Assert.Contains(Tuning.Baseline(StageIds.CalledPitch, step.Condition).Value.Ppm, p => p == ProbabilityScale.Denominator);
            Assert.Equal(ProbabilityScale.RollBound, 1_000_000u);
            Assert.True(step.Roll < ProbabilityScale.RollBound);
            Assert.True(step.RawDraws >= 1);
        }
    }

    [Fact]
    public void Each_step_carries_the_trace_of_exactly_the_distribution_it_selected_from()
    {
        foreach (var step in AllGoldenDrafts().SelectMany(d => d.Steps))
        {
            Assert.Equal(step.Stage, step.Trace.Stage);
            Assert.Equal(step.Condition, step.Trace.Condition);
            Assert.Contains(step.Outcome, step.Trace.Outcomes);
            Assert.Equal(Tuning.Baseline(step.Stage, step.Condition).Value.Ppm, step.Trace.Final); // neutral inputs, no tendency: the baseline row itself
        }
    }

    [Fact]
    public void The_called_pitch_condition_depends_on_the_declared_mode_and_the_actual_zone()
    {
        foreach (var mode in new[] { "automated", "non_automated" })
        {
            var draft = AllGoldenDrafts().First(d => d.CalledPitch is not null && d.Steps.Any(s => s.Stage == StageIds.CalledPitch && s.Condition.StartsWith(mode + ":", StringComparison.Ordinal)));
            Assert.Equal($"{mode}:{draft.Location.Zone.Value}", draft.Steps.Single(s => s.Stage == StageIds.CalledPitch).Condition);
        }

        // both modes appear for the same rule data through the rule profile, never through a default
        const string modeId = "game:3c2a:mode";
        var auto = Resolved(Request(StartState(Automated, modeId), Snapshot(5, modeId), explicitTake: true));
        var non = Resolved(Request(StartState(NonAutomated, modeId), Snapshot(5, modeId), explicitTake: true));
        Assert.Equal(auto.Location.Zone, non.Location.Zone); // the location draw does not depend on the call mode
        if (auto.CalledPitch is not null)
            Assert.StartsWith("automated:", auto.Steps.Single(s => s.Stage == StageIds.CalledPitch).Condition, StringComparison.Ordinal);
        if (non.CalledPitch is not null)
            Assert.StartsWith("non_automated:", non.Steps.Single(s => s.Stage == StageIds.CalledPitch).Condition, StringComparison.Ordinal);
    }

    // ---- explicit Take ----

    [Fact]
    public void An_explicit_take_never_runs_swing_contact_or_field_and_keeps_the_hit_by_pitch_mass()
    {
        var takes = AllGoldenDrafts().Where(d => d.ExplicitTake).ToArray();
        Assert.NotEmpty(takes);
        foreach (var draft in takes)
        {
            Assert.DoesNotContain(draft.Steps, s => s.Stage is StageIds.SwingResult or StageIds.BattedBall or StageIds.DefenseResult);
            Assert.DoesNotContain(draft.StreamUsage, u => u.Stream == GameRngStreams.Contact || u.Stream == GameRngStreams.Field);
            Assert.Null(draft.SwingResult);
            Assert.Null(draft.BattedBall);
            Assert.Null(draft.DefenseResult);
            Assert.True(draft.Kind is PitchBranchKind.HitByPitch or PitchBranchKind.CalledBall or PitchBranchKind.CalledStrike);
            // the decision is the ordinary batter_decision distribution of the actual zone (hit_by_pitch mass included), not a Take-only table
            var decision = draft.Steps[2];
            Assert.Equal(Tuning.Baseline(StageIds.BatterDecision, draft.Location.Zone.Value).Value.Ppm, decision.Trace.Final);
            Assert.Equal(draft.Kind == PitchBranchKind.HitByPitch, draft.BatterDecision == "hit_by_pitch");
            Assert.Equal(draft.Kind == PitchBranchKind.HitByPitch, draft.CalledPitch is null);
        }

        // a hit-by-pitch is possible only where the baseline gives mass to it
        Assert.Contains(takes, d => d.Kind == PitchBranchKind.HitByPitch);
        Assert.All(takes.Where(d => d.Kind == PitchBranchKind.HitByPitch), d => Assert.True(Tuning.Baseline(StageIds.BatterDecision, d.Location.Zone.Value).Value.Of("hit_by_pitch").Value > 0));
    }

    [Fact]
    public void An_explicit_take_is_only_legal_at_a_start_or_decision_point_and_a_normal_pitch_only_inside_a_sequence()
    {
        const string id = "game:3c2a:legal";
        var rng = Snapshot(11, id);
        Assert.True(PitchBranchResolver.Resolve(Request(StartState(NonAutomated, id), rng, explicitTake: true)).IsSuccess);
        Assert.True(PitchBranchResolver.Resolve(Request(DecisionState(NonAutomated, id), rng, explicitTake: true)).IsSuccess);
        Assert.True(PitchBranchResolver.Resolve(Request(InSequenceState(NonAutomated, id), rng)).IsSuccess);

        Assert.Equal("PitchBranch.take_not_legal", PitchBranchResolver.Resolve(Request(InSequenceState(NonAutomated, id), rng, explicitTake: true)).Error!.Value.Code);
        Assert.Equal("PitchBranch.pitch_not_legal", PitchBranchResolver.Resolve(Request(StartState(NonAutomated, id), rng)).Error!.Value.Code);
        Assert.Equal("PitchBranch.pitch_not_legal", PitchBranchResolver.Resolve(Request(DecisionState(NonAutomated, id), rng)).Error!.Value.Code);
    }

    // ---- switch hitters and the applied side ----

    [Theory]
    [InlineData(BattingSide.Right, ThrowingHand.Right, AppliedBatterSide.Right)]
    [InlineData(BattingSide.Right, ThrowingHand.Left, AppliedBatterSide.Right)]
    [InlineData(BattingSide.Left, ThrowingHand.Right, AppliedBatterSide.Left)]
    [InlineData(BattingSide.Left, ThrowingHand.Left, AppliedBatterSide.Left)]
    [InlineData(BattingSide.Switch, ThrowingHand.Right, AppliedBatterSide.Left)]
    [InlineData(BattingSide.Switch, ThrowingHand.Left, AppliedBatterSide.Right)]
    public void The_applied_side_follows_the_declared_side_and_for_a_switch_hitter_is_opposite_to_the_pitchers_throwing_hand(BattingSide bats, ThrowingHand throws, AppliedBatterSide expected)
    {
        var draft = Resolved(Request(InSequenceState(NonAutomated, "game:3c2a:side"), Snapshot(21, "game:3c2a:side"), bats: bats, throws: throws));
        Assert.Equal(expected, draft.AppliedSide);
        Assert.Equal(expected, draft.Location.Side);
        Assert.True(AppliedBatterSideRules.Validate(bats, draft.AppliedSide).IsSuccess); // the existing rule accepts what the resolver applied
        Assert.Equal(PitchLocation.FromZone(ZoneModelVersion.Pzl1, draft.Location.Zone, expected).Value, draft.Location); // built through the factory
    }

    [Fact]
    public void A_pitching_change_recomputes_the_switch_hitters_side_for_the_next_pitch_and_the_side_consumes_no_randomness()
    {
        const string id = "game:3c2a:change";
        var rng = Snapshot(31, id);
        var vsRight = Resolved(Request(InSequenceState(NonAutomated, id), rng, bats: BattingSide.Switch, throws: ThrowingHand.Right));
        var vsLeft = Resolved(Request(InSequenceState(NonAutomated, id), rng, bats: BattingSide.Switch, throws: ThrowingHand.Left));
        var rightHanded = Resolved(Request(InSequenceState(NonAutomated, id), rng, bats: BattingSide.Right, throws: ThrowingHand.Right));

        Assert.Equal(AppliedBatterSide.Left, vsRight.AppliedSide);
        Assert.Equal(AppliedBatterSide.Right, vsLeft.AppliedSide);
        // identical draws and identical next position whichever side was applied: the side decision uses no RNG
        Assert.Equal(vsRight.NextRng.Canonical(), vsLeft.NextRng.Canonical());
        Assert.Equal(vsRight.NextRng.Canonical(), rightHanded.NextRng.Canonical());
        Assert.Equal(vsRight.Steps.Select(s => (s.Outcome, s.Roll)), vsLeft.Steps.Select(s => (s.Outcome, s.Roll)));

        // same batter, same pitcher: the same policy on the next pitch
        var again = Resolved(Request(InSequenceState(NonAutomated, id), vsRight.NextRng, bats: BattingSide.Switch, throws: ThrowingHand.Right));
        Assert.Equal(AppliedBatterSide.Left, again.AppliedSide);
    }

    [Fact]
    public void The_boundary_class_and_zone_model_of_the_location_are_the_models()
    {
        foreach (var draft in AllGoldenDrafts())
        {
            Assert.Equal(ZoneModelVersion.Pzl1, draft.Location.Model);
            Assert.Equal(ZoneModelVersion.Pzl1, draft.Target.Model);
            Assert.Equal(PitchZoneModel.V1.Classify(draft.Location.Zone).Value, draft.Location.Boundary);
        }
    }

    // ---- versions carried by the draft ----

    [Fact]
    public void The_draft_names_the_contract_model_tuning_and_rules_it_was_drawn_under()
    {
        var rules = NonAutomated;
        var draft = Resolved(Request(InSequenceState(rules, "game:3c2a:versions"), Snapshot(41, "game:3c2a:versions")));
        Assert.Equal("pitch-branch-v1", draft.ContractVersion);
        Assert.Equal("pitch-branch-v1", PitchBranchResolver.ContractVersion);
        Assert.Equal("prob-model-v1", draft.ProbabilityModelVersion);
        Assert.Equal("tuning-virtual-proto-v1", draft.TuningVersion);
        Assert.Equal(TuningProfileTests.GoldenHash, draft.TuningHash);
        Assert.Equal(rules.ProfileId, draft.RulesProfileId);
        Assert.Equal(rules.Version, draft.RulesVersion);
        Assert.Equal(rules.ContentHash, draft.RulesHash);
        Assert.All(draft.Steps, s => Assert.Equal(TuningProfileTests.GoldenHash, s.Trace.TuningHash));
    }

    [Fact]
    public void A_draft_is_not_a_game_event_and_the_resolver_never_touches_the_reducer()
    {
        Assert.False(typeof(GameEvent).IsAssignableFrom(typeof(PitchBranchDraft)));
        Assert.Null(typeof(PitchBranchDraft).GetProperty("Id"));
        var source = File.ReadAllText(Path.Combine(Root, "src", "StarPlayer.Simulation", "Baseball", "PitchBranchResolver.cs"));
        Assert.DoesNotContain("GameReducer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EventFingerprint", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InPlayEvent", source, StringComparison.Ordinal);
    }

    // ---- validation before the first draw ----

    private static (PitchBranchRequest? Request, string Code) Invalid(string name)
    {
        const string id = "game:3c2a:invalid";
        var rng = Snapshot(51, id, new Dictionary<string, int> { ["game/target@1"] = 2, ["game/pitch@1"] = 1 });
        var state = InSequenceState(NonAutomated, id);
        return name switch
        {
            "null request" => (null, "PitchBranch.request_missing"),
            "null state" => (Request(state, rng) with { State = null }, "PitchBranch.state_missing"),
            "finished game" => (Request(state with { Status = GameStatus.Final }, rng), "PitchBranch.game_not_in_progress"),
            "undeclared call mode (gamerules-v1)" => (Request(InSequenceState(Undeclared, id), rng), "PitchBranch.called_pitch_mode_undeclared"),
            "undeclared call mode on a take" => (Request(StartState(Undeclared, id), rng, explicitTake: true), "PitchBranch.called_pitch_mode_undeclared"),
            "null tuning" => (Request(state, rng) with { Tuning = null }, "PitchBranch.tuning_missing"),
            "invalid intent" => (Request(state, rng, intent: (PitchIntent)99), "PitchBranch.intent_invalid"),
            "missing batter side" => (Request(state, rng, bats: null), "PitchBranch.batter_side_missing"),
            "invalid batter side" => (Request(state, rng, bats: (BattingSide)9), "PitchBranch.batter_side_invalid"),
            "missing pitcher hand" => (Request(state, rng, throws: null), "PitchBranch.pitcher_hand_missing"),
            "missing pitcher hand for a switch hitter" => (Request(state, rng, bats: BattingSide.Switch, throws: null), "PitchBranch.pitcher_hand_missing"),
            "invalid pitcher hand" => (Request(state, rng, throws: (ThrowingHand)9), "PitchBranch.pitcher_hand_invalid"),
            "missing rng snapshot" => (Request(state, rng) with { Rng = null }, "Rng.snapshot_missing"),
            "unsupported rng format" => (Request(state, rng with { FormatVersion = "rngset9" }), "Rng.snapshot_format_unsupported"),
            "unsupported rng algorithm" => (Request(state, rng with { AlgorithmVersion = "pcg32-xsh-rr-v9" }), "Rng.algorithm_unsupported"),
            "tampered rng stream" => (Request(state, rng with { Streams = rng.Streams.SetItem(0, rng.Streams[0] with { Draws = rng.Streams[0].Draws + 1 }) }), "Rng.state_mismatch"),
            "take at the wrong stage" => (Request(state, rng, explicitTake: true), "PitchBranch.take_not_legal"),
            "pitch before an approach" => (Request(StartState(NonAutomated, id), rng), "PitchBranch.pitch_not_legal"),
            "rules zone model differs from the tuning" => (Request(state with { Rules = RulesWithZoneModel("pzl-zones-v2") }, rng), "PitchBranch.zone_model_mismatch"),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };
    }

    public static IEnumerable<object[]> InvalidNames() => new[]
    {
        "null request", "null state", "finished game", "undeclared call mode (gamerules-v1)", "undeclared call mode on a take", "null tuning", "invalid intent",
        "missing batter side", "invalid batter side", "missing pitcher hand", "missing pitcher hand for a switch hitter", "invalid pitcher hand", "missing rng snapshot",
        "unsupported rng format", "unsupported rng algorithm", "tampered rng stream", "take at the wrong stage", "pitch before an approach",
        "rules zone model differs from the tuning",
    }.Select(n => new object[] { n });

    private static GameRuleSnapshot RulesWithZoneModel(string text)
    {
        var boxed = (object)default(ZoneModelVersion);
        typeof(ZoneModelVersion).GetProperty(nameof(ZoneModelVersion.Value))!.SetValue(boxed, text);
        var source = NonAutomated;
        return GameRuleSnapshot.Create(source.ProfileId, source.Version, source.Status, source.Provenance, source.OpenItems, source.OutsPerHalfInning, source.BallsForWalk,
            source.StrikesForStrikeout, source.RegulationInnings, source.ExtraInningsLimit, source.TieAllowed, source.DesignatedHitter, source.TwoWayPlayerRule,
            source.StarterWinMinOuts, source.SaveMaxLead, source.SaveMinOuts, source.SaveLongOuts, source.DhMayBeForfeited, source.CountWalkOffRunnersAsLeftOnBase,
            source.FielderToMoundForfeitsDh, source.RulePolicyId, source.CalledPitchMode, (ZoneModelVersion)boxed).Value;
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public void Unsupported_or_invalid_input_fails_with_its_own_code_and_changes_nothing(string name)
    {
        var (request, code) = Invalid(name);
        var before = request?.Rng?.Canonical();
        var result = PitchBranchResolver.Resolve(request);

        Assert.False(result.IsSuccess, name);
        Assert.Equal(code, result.Error!.Value.Code);
        Assert.Equal(before, request?.Rng?.Canonical()); // the caller's position is exactly as it was
    }

    [Fact]
    public void The_mismatched_zone_model_helper_really_builds_a_different_zone_model()
    {
        Assert.Equal("pzl-zones-v2", RulesWithZoneModel("pzl-zones-v2").ZoneModel!.Value.Value);
        Assert.Equal("pzl-zones-v1", Tuning.ZoneModel.Value);
    }

    // ---- the game and the RNG context must be the same game (Codex review of 3-C2A) ----

    public const string MismatchCode = "PitchBranch.game_context_mismatch";

    public static IEnumerable<object[]> MismatchedPairs() => new[]
    {
        new object[] { "game id A with context B", "game:3c2a:a", "game:3c2a:b" },
        new object[] { "game id B with context A", "game:3c2a:b", "game:3c2a:a" },
        new object[] { "only the letter case differs", "game:3c2a:case", "GAME:3C2A:CASE" },
        new object[] { "the review's reproduction (g001 with a probe context)", "g001", "game:3c2a:probe" },
        new object[] { "a trailing difference", "game:3c2a:prefix", "game:3c2a:prefix2" },
    };

    [Theory]
    [MemberData(nameof(MismatchedPairs))]
    public void A_snapshot_of_another_game_is_refused_before_any_draw_and_nothing_of_the_caller_changes(string name, string gameId, string contextKey)
    {
        // a perfectly valid, restorable snapshot that has already been used: only its game differs
        var live = GameRandom.Create(101, contextKey).Value;
        live.Stream(GameRngStreams.Target).NextBounded(ProbabilityScale.RollBound);
        live.Stream(GameRngStreams.Pitch).NextBounded(ProbabilityScale.RollBound);
        var snapshot = live.Snapshot();
        Assert.True(GameRandom.Restore(snapshot).IsSuccess, name);
        var state = InSequenceState(NonAutomated, gameId);
        var stateText = state.ToString();
        var plays = state.Plays;
        var applied = state.AppliedEvents;
        var canonicalBefore = snapshot.Canonical();
        var liveHash = live.Snapshot().StateHash;
        var targetDraws = live.Stream(GameRngStreams.Target).Draws;

        var result = PitchBranchResolver.Resolve(Request(state, snapshot));

        Assert.False(result.IsSuccess, name);
        Assert.Equal(MismatchCode, result.Error!.Value.Code);
        Assert.False(string.IsNullOrWhiteSpace(result.Error!.Value.Message));
        Assert.Equal(canonicalBefore, snapshot.Canonical()); // the caller's snapshot is untouched
        Assert.Equal(liveHash, live.Snapshot().StateHash); // so is the live random it came from
        Assert.Equal(targetDraws, live.Stream(GameRngStreams.Target).Draws);
        Assert.Same(plays, state.Plays);
        Assert.Same(applied, state.AppliedEvents);
        Assert.Equal(stateText, state.ToString());
        Assert.Equal(gameId, state.GameId); // nothing normalized or replaced the id
        // and the same snapshot still works for its own game afterwards
        Assert.True(PitchBranchResolver.Resolve(Request(InSequenceState(NonAutomated, contextKey), snapshot)).IsSuccess, name);
    }

    [Theory]
    [InlineData("game:3c2a:a", "game:3c2a:b")]
    [InlineData("game:3c2a:b", "game:3c2a:a")]
    public void Both_explicit_take_and_normal_pitch_are_refused_on_a_mismatch(string gameId, string contextKey)
    {
        var rng = Snapshot(102, contextKey);
        Assert.Equal(MismatchCode, PitchBranchResolver.Resolve(Request(InSequenceState(NonAutomated, gameId), rng)).Error!.Value.Code);
        Assert.Equal(MismatchCode, PitchBranchResolver.Resolve(Request(StartState(NonAutomated, gameId), rng, explicitTake: true)).Error!.Value.Code);
        Assert.Equal(MismatchCode, PitchBranchResolver.Resolve(Request(DecisionState(Automated, gameId), rng, explicitTake: true)).Error!.Value.Code);
    }

    [Fact]
    public void An_invalid_snapshot_keeps_its_own_error_code_whatever_the_game_id_is()
    {
        var rng = Snapshot(103, "game:3c2a:other", new Dictionary<string, int> { ["game/target@1"] = 2 });
        var state = InSequenceState(NonAutomated, "game:3c2a:mine");

        Assert.Equal("Rng.snapshot_missing", PitchBranchResolver.Resolve(Request(state, rng) with { Rng = null }).Error!.Value.Code);
        Assert.Equal("Rng.snapshot_format_unsupported", PitchBranchResolver.Resolve(Request(state, rng with { FormatVersion = "rngset9" })).Error!.Value.Code);
        Assert.Equal("Rng.algorithm_unsupported", PitchBranchResolver.Resolve(Request(state, rng with { AlgorithmVersion = "pcg32-xsh-rr-v9" })).Error!.Value.Code);
        Assert.Equal("Rng.state_mismatch",
            PitchBranchResolver.Resolve(Request(state, rng with { Streams = rng.Streams.SetItem(0, rng.Streams[0] with { Draws = rng.Streams[0].Draws + 1 }) })).Error!.Value.Code);
        // an empty context key is a broken snapshot, reported by the snapshot check rather than as a different game
        var empty = PitchBranchResolver.Resolve(Request(state, rng with { ContextKey = "" }));
        Assert.False(empty.IsSuccess);
        Assert.StartsWith("Rng.", empty.Error!.Value.Code, StringComparison.Ordinal);
        // an empty or missing game id never matches a real context
        Assert.Equal(MismatchCode, PitchBranchResolver.Resolve(Request(state with { GameId = "" }, Snapshot(103, "game:3c2a:other"))).Error!.Value.Code);
    }

    [Theory]
    [InlineData("game:3c2a:a")]
    [InlineData("game:3c2a:b")]
    public void The_same_game_is_accepted_with_the_same_draws_as_before_the_check_existed(string id)
    {
        var rng = Snapshot(104, id, new Dictionary<string, int> { ["game/target@1"] = 1, ["game/pitch@1"] = 2 });
        var state = InSequenceState(NonAutomated, id);
        var first = Resolved(Request(state, rng));
        var retry = Resolved(Request(state, rng)); // the same snapshot, the same input
        Assert.Equal(Summary(first), Summary(retry));
        Assert.Equal(id, first.NextRng.ContextKey); // the next position still belongs to this game

        // a restored snapshot continues exactly like the draft's own next position
        var restored = GameRandom.Restore(first.NextRng).Value.Snapshot();
        var continued = Resolved(Request(InSequenceState(NonAutomated, id), restored));
        var chained = Resolved(Request(InSequenceState(NonAutomated, id), first.NextRng));
        Assert.Equal(Summary(chained), Summary(continued));
    }

    [Fact]
    public void The_mismatch_error_code_belongs_to_the_resolver_and_is_new()
    {
        Assert.StartsWith("PitchBranch.", MismatchCode, StringComparison.Ordinal);
        var source = File.ReadAllText(Path.Combine(Root, "src", "StarPlayer.Simulation", "Baseball", "PitchBranchResolver.cs"));
        Assert.Equal(1, source.Split('"' + MismatchCode + '"').Length - 1); // defined once
        var others = Directory.EnumerateFiles(Path.Combine(Root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith("PitchBranchResolver.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(others, f => File.ReadAllText(f).Contains(MismatchCode, StringComparison.Ordinal));
    }

    // ---- no side effects on the caller ----

    [Fact]
    public void A_successful_draft_leaves_the_callers_state_and_live_random_untouched_and_the_same_input_gives_the_same_draft()
    {
        const string id = "game:3c2a:live";
        var live = GameRandom.Create(61, id).Value;
        live.Stream(GameRngStreams.Target).NextBounded(ProbabilityScale.RollBound);
        live.Stream(GameRngStreams.Pitch).NextBounded(ProbabilityScale.RollBound);
        var snapshot = live.Snapshot();
        var hashBefore = snapshot.StateHash;
        var targetDrawsBefore = live.Stream(GameRngStreams.Target).Draws;
        var state = InSequenceState(NonAutomated, id);
        var stateText = state.ToString();
        var plays = state.Plays;
        var applied = state.AppliedEvents;

        var first = Resolved(Request(state, snapshot));
        var second = Resolved(Request(state, snapshot));

        Assert.Equal(hashBefore, live.Snapshot().StateHash);
        Assert.Equal(targetDrawsBefore, live.Stream(GameRngStreams.Target).Draws);
        Assert.Equal(hashBefore, snapshot.StateHash);
        Assert.NotEqual(hashBefore, first.NextRng.StateHash); // the draft carries the NEXT position; the caller adopts it only if it accepts the draft
        Assert.Same(plays, state.Plays);
        Assert.Same(applied, state.AppliedEvents);
        Assert.Equal(stateText, state.ToString());
        Assert.Equal(Summary(first), Summary(second));
    }

    [Fact]
    public void The_pitch_continues_from_the_given_position_and_does_not_reseed()
    {
        const string id = "game:3c2a:continue";
        var fresh = Snapshot(71, id);
        var advanced = Snapshot(71, id, new Dictionary<string, int> { ["game/target@1"] = 3 });
        var a = Resolved(Request(InSequenceState(NonAutomated, id), fresh));
        var b = Resolved(Request(InSequenceState(NonAutomated, id), advanced));
        var chained = Resolved(Request(InSequenceState(NonAutomated, id), a.NextRng));

        Assert.NotEqual(a.Steps[0].Roll, b.Steps[0].Roll); // three target calls were already used, so the roll differs
        Assert.NotEqual(Summary(a), Summary(chained)); // a following pitch does not repeat the previous one
        // advancing the target stream by three calls and then resolving equals the golden-style expectation: the first roll is the stream's fourth call
        var probe = GameRandom.Create(71, "game:3c2a:continue").Value.Stream(GameRngStreams.Target);
        _ = new[] { probe.NextBounded(ProbabilityScale.RollBound), probe.NextBounded(ProbabilityScale.RollBound), probe.NextBounded(ProbabilityScale.RollBound) };
        Assert.Equal(probe.NextBounded(ProbabilityScale.RollBound), b.Steps[0].Roll);
    }

    [Fact]
    public void A_cancelled_request_throws_before_any_work_and_changes_nothing()
    {
        const string id = "game:3c2a:cancel";
        var live = GameRandom.Create(81, id).Value;
        live.Stream(GameRngStreams.Pitch).NextBounded(ProbabilityScale.RollBound);
        var snapshot = live.Snapshot();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.Throws<OperationCanceledException>(() => PitchBranchResolver.Resolve(Request(InSequenceState(NonAutomated, id), snapshot), cancelled.Token));

        Assert.Equal(snapshot.StateHash, live.Snapshot().StateHash);
        Assert.True(PitchBranchResolver.Resolve(Request(InSequenceState(NonAutomated, id), snapshot)).IsSuccess); // and it works afterwards
    }

    // ---- boundary and compatibility ----

    [Fact]
    public void The_draft_types_are_resolver_internal_and_not_part_of_any_application_or_infrastructure_signature()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        Type[] hidden = [typeof(PitchBranchDraft), typeof(PitchBranchStep), typeof(PitchBranchRequest), typeof(PitchBranchStreamUse)];
        var assemblies = new[] { typeof(StarPlayer.Application.Queries.PlayerInfoService).Assembly, typeof(StarPlayer.Infrastructure.Fixtures.PlayerInfoComposition).Assembly };
        foreach (var type in assemblies.SelectMany(a => a.GetTypes()))
        {
            foreach (var field in type.GetFields(all))
                Assert.DoesNotContain(field.FieldType, hidden);
            foreach (var property in type.GetProperties(all))
                Assert.DoesNotContain(property.PropertyType, hidden);
            foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
            {
                foreach (var parameter in method.GetParameters())
                    Assert.DoesNotContain(parameter.ParameterType, hidden);
                if (method is MethodInfo info)
                    Assert.DoesNotContain(info.ReturnType, hidden);
            }
        }
    }

    [Fact]
    public void The_probability_tuning_and_rule_identities_are_unchanged_by_the_resolver()
    {
        Assert.Equal(TuningProfileTests.GoldenHash, Tuning.ContentHash);
        var before = (NonAutomated.ContentHash, Automated.ContentHash);
        _ = Resolved(Request(InSequenceState(NonAutomated, "game:3c2a:identity"), Snapshot(91, "game:3c2a:identity")));
        Assert.Equal(before, (NonAutomated.ContentHash, Automated.ContentHash));
        Assert.Equal(TuningProfileTests.GoldenHash, Tuning.ContentHash);
    }
}
