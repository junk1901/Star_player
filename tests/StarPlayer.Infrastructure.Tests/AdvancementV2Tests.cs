using System.Collections.Immutable;
using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;
using static StarPlayer.Infrastructure.Tests.Pc;
using Policy = StarPlayer.Simulation.Baseball.AdvancementPolicy;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-D v2 Part C (owner decisions R2-R5, S1-S3): <c>advancement-policy-v2</c> hands each of the four declared contexts the CURRENT runner's Speed and the Retriever's Arm in ONE
/// computation. Every case is a REAL pending advancement from the real resolver on the real seeded streams; expected rows come from the independent Python oracle, expected RNG
/// positions from the existing <see cref="GameRandom"/> primitives. v1 (the neutral policy) is shown to be exactly what it was.
/// </summary>
public class AdvancementV2Tests
{
    private static readonly string[] Keys = ["single_runner_on_second", "single_runner_on_first", "double_runner_on_first", "fly_out_runner_on_third"];

    private static int Occupancy(string key) => key switch { "single_runner_on_second" => 2, "fly_out_runner_on_third" => 4, _ => 1 };

    private static PlayerId Runner(GameState state, int @base) => state.Bases.At(@base)!.Player;

    /// <summary>A v2-policy game and its pinned player input set: the situation is built twice (the set depends on who the runners and fielders are, the game pins the set's identity).</summary>
    private static (Driver D, PlayerInputSet Set) V2Game(int occupancy, int outs, Func<GameState, Func<PlayerId, (int Fielding, int Speed, int Arm)?>> ratings, string policy = Policy.VersionV2,
        bool approach = true, Action<Driver, GameState>? before = null, ParkFactorSnapshot? park = null)
    {
        var probe = Gx.Situation(null, occupancy, outs, false);
        var set = Gx.Set(ratings(probe.State));
        var d = Gx.Situation(Gx.Exec(park ?? Gx.Park(), policy, set.Id), occupancy, outs, false);
        before?.Invoke(d, probe.State);
        return (approach ? d.Approach() : d, set);
    }

    private static (AuthoritativeGame Game, PendingAdvancement Pending, InPlayPitchCandidate Candidate) Resolved(Driver d, PlayerInputSet set, string key, TuningProfile? tuning = null)
    {
        var (g, _, pending) = Gx.FindPending(d, tuning ?? Gx.Tuning2, set, p => p.ContextKeys.Contains(key));
        return (g, pending, Ok(Gx.Resolve(g, pending, tuning ?? Gx.Tuning2, set)));
    }

    // ---------------------------------------------------------------- the policy is the game's

    [Fact]
    public void Policy_v1_is_the_neutral_policy_and_gives_the_approved_row_even_when_a_set_with_extreme_ratings_exists()
    {
        foreach (var key in Keys)
        {
            // legacy game on the v1 tuning, no execution at all
            var legacy = Gx.Situation(null, Occupancy(key), 0, true);
            var (g, _, pending) = Gx.FindPending(legacy, Gx.Tuning1, null, p => p.ContextKeys.Contains(key));
            var candidate = Ok(Gx.Resolve(g, pending, Gx.Tuning1));
            var detail = candidate.Audit.AdvancementDetail!;
            Assert.Equal(Policy.Version, detail.PolicyVersion);
            Assert.Null(detail.PlayerInputsId);
            Assert.All(detail.Decisions, d => Assert.Null(d.Abilities));
            Assert.Equal(Gx.OracleAdvancement(key, null, null), detail.Decisions.Single(d => d.ContextKey == key).Ppm.ToArray());

            // a v2-model game that explicitly pins policy v1 and holds extreme ratings: still the neutral row
            var (d1, set) = V2Game(Occupancy(key), 0, _ => _ => (50, 0, 100), Policy.Version);
            var (g1, _, c1) = Resolved(d1, set, key);
            Assert.Equal(Policy.Version, c1.Audit.AdvancementDetail!.PolicyVersion);
            Assert.All(c1.Audit.AdvancementDetail.Decisions, x => Assert.Null(x.Abilities));
            Assert.Equal(Gx.OracleAdvancement(key, null, null), c1.Audit.AdvancementDetail.Decisions.Single(x => x.ContextKey == key).Ppm.ToArray());
            Assert.NotNull(g1);
        }
    }

    [Fact]
    public void A_v2_game_names_policy_v2_in_its_audit_and_every_decision_records_who_produced_its_row()
    {
        foreach (var key in Keys)
        {
            var (d, set) = V2Game(Occupancy(key), 0, state => id => id == Runner(state, key == "single_runner_on_second" ? 2 : key == "fly_out_runner_on_third" ? 3 : 1) ? (50, 80, 50) : (50, 50, 66));
            var (g, _, candidate) = Resolved(d, set, key);
            var detail = candidate.Audit.AdvancementDetail!;
            var decision = detail.Decisions.Single(x => x.ContextKey == key);
            var startBase = key == "single_runner_on_second" ? 2 : key == "fly_out_runner_on_third" ? 3 : 1;

            Assert.Equal(Policy.VersionV2, detail.PolicyVersion);
            Assert.Equal(set.Id, detail.PlayerInputsId);
            Assert.Equal(g.State.Execution!.AdvancementPolicyVersion, detail.PolicyVersion);
            Assert.Equal(Runner(g.State, startBase), decision.Abilities!.Runner);
            Assert.Equal(80, decision.Abilities.RunnerSpeed);
            Assert.Equal(g.State.DefenderAt(g.State.FieldingSide, decision.Abilities.Retriever), decision.Abilities.RetrieverPlayer);
            Assert.Equal(66, decision.Abilities.RetrieverArm);
            Assert.Equal(Gx.OracleAdvancement(key, 80, 66), decision.Ppm.ToArray());
        }
    }

    // ---------------------------------------------------------------- Speed and Arm values against the oracle

    [Fact]
    public void Speed_and_arm_zero_fifty_and_one_hundred_give_exactly_the_oracle_row_for_all_four_contexts()
    {
        (int Speed, int Arm)[] combinations = [(0, 0), (50, 50), (100, 100), (0, 100), (100, 0), (25, 75), (75, 25), (0, 50), (50, 100)];
        foreach (var key in Keys)
            foreach (var (speed, arm) in combinations)
            {
                var (d, set) = V2Game(Occupancy(key), 0, _ => _ => (50, speed, arm));
                var (_, _, candidate) = Resolved(d, set, key);
                var decision = candidate.Audit.AdvancementDetail!.Decisions.Single(x => x.ContextKey == key);

                Assert.Equal(Gx.OracleAdvancement(key, speed, arm), decision.Ppm.ToArray());
                Assert.Equal(1_000_000, decision.Ppm.Sum());
                Assert.Equal((speed, arm), (decision.Abilities!.RunnerSpeed, decision.Abilities.RetrieverArm));
            }
    }

    [Fact]
    public void Speed_acts_on_advance_extra_and_arm_on_out_on_bases_each_in_its_own_direction()
    {
        var key = "single_runner_on_second";
        int[] Row(int speed, int arm) => Gx.OracleAdvancement(key, speed, arm);

        Assert.True(Row(100, 50)[0] > Row(50, 50)[0] && Row(0, 50)[0] < Row(50, 50)[0]);          // speed moves advance_extra
        Assert.True(Row(50, 100)[2] > Row(50, 50)[2] && Row(50, 0)[2] < Row(50, 50)[2]);          // arm moves out_on_bases
        Assert.True(Row(50, 100)[0] <= Row(50, 50)[0]);                                           // arm lowers the others only by normalisation: it never raises advance_extra
        Assert.True(Row(100, 50)[2] <= Row(50, 50)[2]);
    }

    [Fact]
    public void Both_inputs_go_into_one_computation_and_one_normalisation_not_two_sequential_ones()
    {
        var key = "single_runner_on_second";
        var joint = Gx.OracleAdvancement(key, 0, 100);
        var sequential = Gx.OracleAdvancement(key, 0, 100, sequential: true);
        Assert.NotEqual(joint, sequential);                                                       // the two procedures really differ (at rounding scale) for this input

        var (d, set) = V2Game(2, 0, _ => _ => (50, 0, 100));
        var (_, _, candidate) = Resolved(d, set, key);
        var ppm = candidate.Audit.AdvancementDetail!.Decisions.Single(x => x.ContextKey == key).Ppm.ToArray();

        Assert.Equal(joint, ppm);
        Assert.NotEqual(sequential, ppm);
        // and the trace of the single computation: both slots listed, each exactly once, in the model's order
        var traced = StageProbability.Compute(Gx.Tuning2, StageIds.Advancement, key, [ModifierInput.Linear("runner.speed", 0), ModifierInput.Linear("defense.arm", 100)]).Value.Trace;
        Assert.Equal(["runner.speed", "defense.arm"], traced.Modifiers.Select(m => m.Slot));
        Assert.Equal(joint, traced.Final.ToArray());
    }

    [Fact]
    public void Neutral_ratings_under_v2_are_the_v1_row_and_the_same_seed_gives_the_same_event_and_end_position()
    {
        foreach (var key in Keys)
        {
            var (d2, set) = V2Game(Occupancy(key), 0, _ => _ => (50, 50, 50));
            var (g2, _, c2) = Resolved(d2, set, key);
            var legacy = Gx.Situation(null, Occupancy(key), 0, true);
            // the same seed, the same base state, the legacy game and v1 policy
            var g1 = Aggregate(legacy, SeedOf(g2));
            var p1 = Gx.Prepare(g1, Gx.Tuning1);
            var built = Ok(Gx.BuildInPlay(g1, p1, Gx.Tuning1));
            var c1 = Ok(Gx.Resolve(g1, Assert.IsType<PendingAdvancement>(built), Gx.Tuning1));

            Assert.Equal(c1.NextRng.StateHash, c2.NextRng.StateHash);
            Assert.Equal(EventFingerprint.Receipt(c1.Event), EventFingerprint.Receipt(c2.Event));
            Assert.Equal(c1.Audit.AdvancementDetail!.Decisions.Select(x => (x.ContextKey, x.Roll, x.Drawn, x.Applied, x.Ppm.ToArray().Sum())),
                c2.Audit.AdvancementDetail!.Decisions.Select(x => (x.ContextKey, x.Roll, x.Drawn, x.Applied, x.Ppm.ToArray().Sum())));
            Assert.Equal(c1.Audit.AdvancementDetail.Decisions.Select(x => x.Ppm.ToArray()), c2.Audit.AdvancementDetail.Decisions.Select(x => x.Ppm.ToArray()));
        }
    }

    /// <summary>The seed a game's RNG was created from (read from the snapshot header).</summary>
    private static ulong SeedOf(AuthoritativeGame g) => g.Rng.MasterSeed;

    // ---------------------------------------------------------------- runner identity: the slot is not the person

    [Fact]
    public void A_pinch_runner_is_the_runner_whose_speed_is_read_while_the_runner_key_stays_the_slots()
    {
        var probe = Gx.Situation(null, 1, 0, false);
        var original = Runner(probe.State, 1);
        var bench = probe.State.Away.Bench[0];
        var key = probe.State.Bases.First!.RunnerKey;
        var (d, set) = V2Game(1, 0, _ => id => id == original ? (50, 90, 50) : id == bench ? (50, 10, 50) : (50, 50, 50), approach: false,
            before: (driver, _) => driver.Substitute(SubstitutionKind.PinchRun, TeamSide.Away, bench, @base: 1));
        d.Approach();

        Assert.Equal(bench, d.State.Bases.First!.Player);
        Assert.Equal(key, d.State.Bases.First.RunnerKey);                       // the runner slot keeps its key
        Assert.NotEqual(original, d.State.Bases.First.Player);
        var (_, _, candidate) = Resolved(d, set, "single_runner_on_first");
        var decision = candidate.Audit.AdvancementDetail!.Decisions.Single(x => x.ContextKey == "single_runner_on_first");

        Assert.Equal(key, decision.RunnerKey);
        Assert.Equal(bench, decision.Runner);
        Assert.Equal(bench, decision.Abilities!.Runner);
        Assert.Equal(10, decision.Abilities.RunnerSpeed);                       // the pinch runner's own rating, not the replaced player's 90
        Assert.Equal(Gx.OracleAdvancement("single_runner_on_first", 10, 50), decision.Ppm.ToArray());
    }

    [Fact]
    public void Two_runners_each_get_their_own_speed_in_lead_runner_first_order_and_draw_once_each_from_h1_to_h2()
    {
        var probe = Gx.Situation(null, 3, 0, false);
        var onFirst = Runner(probe.State, 1);
        var onSecond = Runner(probe.State, 2);
        var (d, set) = V2Game(3, 0, _ => id => id == onFirst ? (50, 20, 50) : id == onSecond ? (50, 85, 50) : (50, 50, 40));
        var (g, prepared, pending) = Gx.FindPending(d, Gx.Tuning2, set, p => p.ContextKeys.Length == 2);
        var candidate = Ok(Gx.Resolve(g, pending, Gx.Tuning2, set));
        var detail = candidate.Audit.AdvancementDetail!;

        Assert.Equal(2, detail.LogicalDraws);
        Assert.Equal(["single_runner_on_second", "single_runner_on_first"], detail.Decisions.Select(x => x.ContextKey));      // the lead runner first (P-01)
        Assert.Equal([onSecond, onFirst], detail.Decisions.Select(x => x.Abilities!.Runner));
        Assert.Equal([85, 20], detail.Decisions.Select(x => x.Abilities!.RunnerSpeed));
        Assert.Equal(Gx.OracleAdvancement("single_runner_on_second", 85, 40), detail.Decisions[0].Ppm.ToArray());
        Assert.Equal(Gx.OracleAdvancement("single_runner_on_first", 20, 40), detail.Decisions[1].Ppm.ToArray());

        // H1 -> H2: exactly two logical rolls on game/runner@1 from the draft's own position, derived here with the RNG primitives
        var random = GameRandom.Restore(prepared.Draft.NextRng).Value;
        var stream = random.Stream(GameRngStreams.Runner);
        var rolls = new[] { stream.NextBounded(ProbabilityScale.RollBound), stream.NextBounded(ProbabilityScale.RollBound) };
        Assert.Equal(rolls, detail.Decisions.Select(x => x.Roll));
        Assert.Equal(random.Snapshot().StateHash, candidate.NextRng.StateHash);
        Assert.Equal(prepared.Draft.NextRng.StateHash, detail.AfterPitchRngStateHash);
    }

    // ---------------------------------------------------------------- the Retriever throws, the BaseFielder does not

    [Fact]
    public void Only_the_retrievers_arm_is_read_and_the_receiving_fielders_arm_is_ignored()
    {
        var key = "fly_out_runner_on_third";
        var probe = Gx.Situation(null, 4, 0, false);
        var cf = Gx.Defender(probe.State, FieldPosition.CenterField);
        var catcher = Gx.Defender(probe.State, FieldPosition.Catcher);

        // the centre fielder (the putout fielder of a caught fly ball, the Retriever) weak, everybody else - the catcher who receives the throw at home included - strongest
        var (d, set) = V2Game(4, 0, _ => id => id == cf ? (50, 50, 20) : (50, 50, 100));
        var (_, _, candidate) = Resolved(d, set, key);
        var decision = candidate.Audit.AdvancementDetail!.Decisions.Single(x => x.ContextKey == key);
        Assert.Equal(FieldPosition.CenterField, decision.Abilities!.Retriever);
        Assert.Equal(cf, decision.Abilities.RetrieverPlayer);
        Assert.Equal(20, decision.Abilities.RetrieverArm);
        Assert.Equal(Gx.OracleAdvancement(key, 50, 20), decision.Ppm.ToArray());

        // and the reverse: a strong Retriever, a catcher with arm zero
        var (d2, set2) = V2Game(4, 0, _ => id => id == cf ? (50, 50, 100) : id == catcher ? (50, 50, 0) : (50, 50, 50));
        var (_, _, candidate2) = Resolved(d2, set2, key);
        var decision2 = candidate2.Audit.AdvancementDetail!.Decisions.Single(x => x.ContextKey == key);
        Assert.Equal(Gx.OracleAdvancement(key, 50, 100), decision2.Ppm.ToArray());
        Assert.NotEqual(catcher, decision2.Abilities!.RetrieverPlayer);
    }

    [Fact]
    public void A_defensive_substitution_at_the_retriever_position_changes_the_thrower_and_stales_the_old_pending()
    {
        var key = "fly_out_runner_on_third";
        var probe = Gx.Situation(null, 4, 0, false);
        var cf = Gx.Defender(probe.State, FieldPosition.CenterField);
        var bench = probe.State.Home.Bench[0];
        var slot = probe.State.Home.Lineup.Single(s => s.Position == FieldPosition.CenterField).Order;
        var set = Gx.Set(id => id == cf ? (50, 50, 90) : id == bench ? (50, 50, 15) : null);

        // before the substitution
        var before = Gx.Situation(Gx.Exec(Gx.Park(), Policy.VersionV2, set.Id), 4, 0, true);
        var (gOld, _, pendingOld) = Gx.FindPending(before, Gx.Tuning2, set, p => p.ContextKeys.Contains(key));
        Assert.Equal(90, Ok(Gx.Resolve(gOld, pendingOld, Gx.Tuning2, set)).Audit.AdvancementDetail!.Decisions.Single().Abilities!.RetrieverArm);

        // the same game, but the home team replaced its centre fielder between plate appearances
        var after = Gx.Situation(Gx.Exec(Gx.Park(), Policy.VersionV2, set.Id), 4, 0, false);
        after.Substitute(SubstitutionKind.DefensiveSub, TeamSide.Home, bench, slot: slot).Approach();
        Assert.Equal(bench, Gx.Defender(after.State, FieldPosition.CenterField));
        var (gNew, _, pendingNew) = Gx.FindPending(after, Gx.Tuning2, set, p => p.ContextKeys.Contains(key));
        var decision = Ok(Gx.Resolve(gNew, pendingNew, Gx.Tuning2, set)).Audit.AdvancementDetail!.Decisions.Single();

        Assert.Equal(bench, decision.Abilities!.RetrieverPlayer);
        Assert.Equal(15, decision.Abilities.RetrieverArm);
        Assert.Equal(Gx.OracleAdvancement(key, 50, 15), decision.Ppm.ToArray());
        // the pending prepared before the substitution cannot be resolved against the new state
        Assert.Equal("PitchCandidate.fielder_changed", Code(AdvancementPlanning.Resolve(pendingOld, gNew.State, gNew.Rng, Gx.Tuning2, Pc.Sheets(gNew.State).Batter, Pc.Sheets(gNew.State).Pitcher, gNew.LastEventSequence, default, set)));
    }

    // ---------------------------------------------------------------- one logical draw per context, Hold and Void included

    [Fact]
    public void A_void_or_a_held_decision_still_consumed_its_one_logical_draw_and_kept_its_recorded_inputs()
    {
        var (d, set) = V2Game(3, 2, _ => _ => (50, 0, 100));
        var seenVoid = false;
        var seenHold = false;
        var composite = 0;
        foreach (var (g, _, pending) in Gx.Pendings(d, Gx.Tuning2, set))
        {
            if (pending.ContextKeys.Length != 2)
                continue;
            composite++;
            var detail = Ok(Gx.Resolve(g, pending, Gx.Tuning2, set)).Audit.AdvancementDetail!;
            Assert.Equal(pending.ContextKeys.Length, detail.LogicalDraws);                      // a draw per DECLARED context, whatever the applied result
            Assert.Equal(detail.LogicalDraws, detail.Decisions.Length);
            Assert.All(detail.Decisions, x => { Assert.NotNull(x.Abilities); Assert.True(x.RawDraws >= 1); Assert.True(x.Roll < ProbabilityScale.RollBound); Assert.Equal(1_000_000, x.Ppm.Sum()); });
            seenVoid |= detail.Decisions.Any(x => x.Applied == AdvancementOutcome.Void);
            seenHold |= detail.Decisions.Any(x => x.Reason == AdvancementReason.OccupiedBase);
            if ((seenVoid && seenHold) || composite >= 3000)
                break;
        }

        Assert.True(seenVoid, $"no Void decision turned up in {composite} composite cases");
        Assert.True(seenHold, $"no occupied-base Hold decision turned up in {composite} composite cases");
    }

    // ---------------------------------------------------------------- the throw-out credits the Arm player

    [Fact]
    public void A_thrown_out_runner_credits_the_assist_to_the_arm_player_and_the_putout_to_the_receiving_fielder()
    {
        var (d, set) = V2Game(2, 0, _ => _ => (50, 0, 100));
        foreach (var (g, _, pending) in Gx.Pendings(d, Gx.Tuning2, set))
        {
            if (!pending.ContextKeys.Contains("single_runner_on_second"))
                continue;
            var candidate = Ok(Gx.Resolve(g, pending, Gx.Tuning2, set));
            var decision = candidate.Audit.AdvancementDetail!.Decisions.Single(x => x.ContextKey == "single_runner_on_second");
            if (decision.Applied != AdvancementOutcome.Out)
                continue;

            var play = candidate.UnconfirmedStep.NewPlays.Single();
            var assist = play.Credits.Single(c => c.Kind == CreditKind.Assist);
            var putout = play.Credits.Single(c => c.Kind == CreditKind.Putout);
            Assert.Equal((decision.Abilities!.Retriever, decision.Abilities.RetrieverPlayer), (assist.Position, assist.Player));      // the Arm player is the assist player
            Assert.Equal(FieldPosition.Catcher, putout.Position);                                                                 // the receiver at home: never an Arm input
            Assert.Equal(g.State.DefenderAt(g.State.FieldingSide, FieldPosition.Catcher), putout.Player);
            Assert.NotEqual(assist.Player, putout.Player);
            var move = candidate.Event.Moves!.Single(m => m.From == 2);
            Assert.Equal([new PositionCredit(decision.Abilities.Retriever, CreditKind.Assist), new PositionCredit(FieldPosition.Catcher, CreditKind.Putout)], move.Credits!.ToArray());
            // and the commit gate accepts this candidate: the assist-to-Arm link is part of what it re-derives
            Assert.True(Gx.Stage(g, candidate, Gx.Tuning2, set).IsSuccess);
            return;
        }

        throw new InvalidOperationException("no throw-out found");
    }

    // ---------------------------------------------------------------- stale inputs and forged audits

    private static (AuthoritativeGame G, PendingAdvancement P, InPlayPitchCandidate C, PlayerInputSet Set) AResolved()
    {
        var (d, set) = V2Game(2, 0, _ => _ => (50, 70, 60));
        var (g, pending, candidate) = Resolved(d, set, "single_runner_on_second");
        return (g, pending, candidate, set);
    }

    [Fact]
    public void The_matching_set_commits_and_the_published_state_keeps_the_execution()
    {
        var (g, _, candidate, set) = AResolved();

        var staged = Ok(Gx.Stage(g, candidate, Gx.Tuning2, set));
        var published = Ok(new GameCursor(g).Publish(staged));

        Assert.Equal(g.LastEventSequence + 1, published.EventSequence);
        Assert.Same(g.State.Execution, staged.Next.State.Execution);
        Assert.Equal(candidate.NextRng.StateHash, staged.Next.Rng.StateHash);                 // H2 is what is adopted
    }

    [Fact]
    public void Another_set_or_none_at_resolution_or_at_commit_is_a_stale_preparation_and_changes_nothing()
    {
        var (g, pending, candidate, set) = AResolved();
        var changed = Gx.Set(_ => (50, 71, 60));
        var cursor = new GameCursor(g);

        Assert.Equal("PitchCandidate.player_inputs_changed", Code(Gx.Resolve(g, pending, Gx.Tuning2, changed)));
        Assert.Equal("PitchCandidate.player_inputs_changed", Code(Gx.Resolve(g, pending, Gx.Tuning2)));
        Assert.Equal("PitchCandidate.player_inputs_changed", Code(Gx.Stage(g, candidate, Gx.Tuning2, changed)));
        Assert.Equal("PitchCandidate.player_inputs_changed", Code(Gx.Stage(g, candidate, Gx.Tuning2)));
        Assert.Same(g, cursor.Current);
        Assert.True(Gx.Stage(g, candidate, Gx.Tuning2, set).IsSuccess);
    }

    [Fact]
    public void A_forged_audit_cannot_be_committed_policy_inputs_ratings_players_rows_and_results_are_all_checked()
    {
        var (g, _, candidate, set) = AResolved();
        var detail = candidate.Audit.AdvancementDetail!;
        var decision = detail.Decisions.Single();
        var abilities = decision.Abilities!;
        var other = PlayerId.Create("p-other").Value;

        InPlayPitchCandidate Forge(Func<AdvancementAudit, AdvancementAudit> edit) => candidate.WithAudit(candidate.Audit with { AdvancementDetail = edit(detail) });
        AdvancementAudit WithDecision(AdvancementAudit a, Func<AdvancementDecisionAudit, AdvancementDecisionAudit> edit) => a with { Decisions = a.Decisions.SetItem(0, edit(a.Decisions[0])) };

        var forgeries = new Dictionary<string, InPlayPitchCandidate>
        {
            ["policy v1 on a v2 game"] = Forge(a => a with { PolicyVersion = Policy.Version }),
            ["inputs id"] = Forge(a => a with { PlayerInputsId = new string('d', 64) }),
            ["no abilities"] = Forge(a => WithDecision(a, x => x with { Abilities = null })),
            ["runner speed"] = Forge(a => WithDecision(a, x => x with { Abilities = abilities with { RunnerSpeed = abilities.RunnerSpeed + 1 } })),
            ["runner player"] = Forge(a => WithDecision(a, x => x with { Abilities = abilities with { Runner = other } })),
            ["arm"] = Forge(a => WithDecision(a, x => x with { Abilities = abilities with { RetrieverArm = abilities.RetrieverArm + 1 } })),
            ["arm player"] = Forge(a => WithDecision(a, x => x with { Abilities = abilities with { RetrieverPlayer = other } })),
            ["retriever position"] = Forge(a => WithDecision(a, x => x with { Abilities = abilities with { Retriever = abilities.Retriever == FieldPosition.LeftField ? FieldPosition.RightField : FieldPosition.LeftField } })),
            ["row"] = Forge(a => WithDecision(a, x => x with { Ppm = [x.Ppm[0] + 1, x.Ppm[1] - 1, x.Ppm[2]] })),
            ["roll"] = Forge(a => WithDecision(a, x => x with { Roll = (x.Roll + 500_000) % 1_000_000 })),
        };

        Assert.True(Gx.Stage(g, candidate, Gx.Tuning2, set).IsSuccess);
        foreach (var (name, forged) in forgeries)
            Assert.True(Code(Gx.Stage(g, forged, Gx.Tuning2, set)) is "Commit.candidate_link_mismatch" or "Commit.rng_evidence_mismatch", name);
        foreach (var name in new[] { "policy v1 on a v2 game", "inputs id", "no abilities", "runner speed", "arm", "arm player", "row", "roll" })
            Assert.Equal("Commit.candidate_link_mismatch", Code(Gx.Stage(g, forgeries[name], Gx.Tuning2, set)));
    }

    // ---------------------------------------------------------------- errors, never fifty (R5)

    [Fact]
    public void A_runner_without_a_speed_rating_or_missing_from_the_set_is_a_precise_error()
    {
        var probe = Gx.Situation(null, 2, 0, false);
        var runner = Runner(probe.State, 2);
        var noRating = Gx.Set(_ => (50, 50, 50), id => id == runner);                           // a pure pitcher on second base: no hitter ratings
        var missing = Pc.Ok(PlayerInputSet.Create(BasePlayers.Where(p => p != runner).Select(p => Gx.Sheet(p))));

        foreach (var (set, code) in new[] { (noRating, "Advancement.runner_speed_missing"), (missing, "Advancement.runner_input_missing") })
        {
            var d = Gx.Situation(Gx.Exec(Gx.Park(), Policy.VersionV2, set.Id), 2, 0, true);
            var (g, _, pending) = Gx.FindPending(d, Gx.Tuning2, set, p => p.ContextKeys.Contains("single_runner_on_second"));
            Assert.Equal(code, Code(Gx.Resolve(g, pending, Gx.Tuning2, set)));
        }
    }

    [Fact]
    public void A_retriever_without_an_arm_rating_or_missing_from_the_set_is_a_precise_error()
    {
        var probe = Gx.Situation(null, 2, 0, false);
        var lf = Gx.Defender(probe.State, FieldPosition.LeftField);
        var rf = Gx.Defender(probe.State, FieldPosition.RightField);
        var noArm = Gx.Set(_ => (50, 50, 50), id => id == lf || id == rf);                      // left and right field are not among the three frozen fielders, so preparation still succeeds
        var absent = Pc.Ok(PlayerInputSet.Create(BasePlayers.Where(p => p != lf && p != rf).Select(p => Gx.Sheet(p))));

        foreach (var (set, code) in new[] { (noArm, "Advancement.retriever_arm_missing"), (absent, "Advancement.retriever_input_missing") })
        {
            var d = Gx.Situation(Gx.Exec(Gx.Park(), Policy.VersionV2, set.Id), 2, 0, true);
            var (g, _, pending) = Gx.FindPending(d, Gx.Tuning2, set, p => AdvancementPlanner.Contexts(p.Plan.Facts).Value.Any(c => c.Retriever is FieldPosition.LeftField or FieldPosition.RightField));
            Assert.Equal(code, Code(Gx.Resolve(g, pending, Gx.Tuning2, set)));
        }
    }

    [Fact]
    public void The_same_seed_and_inputs_resolve_to_the_same_candidate_every_time()
    {
        var (d, set) = V2Game(3, 0, _ => _ => (50, 35, 65));
        var (g, _, pending) = Gx.FindPending(d, Gx.Tuning2, set, p => p.ContextKeys.Length == 2);

        var first = Ok(Gx.Resolve(g, pending, Gx.Tuning2, set));
        var second = Ok(Gx.Resolve(g, pending, Gx.Tuning2, set));

        Assert.Equal(EventFingerprint.Receipt(first.Event), EventFingerprint.Receipt(second.Event));
        Assert.Equal(first.NextRng.StateHash, second.NextRng.StateHash);
        Assert.Equal(first.Audit.AdvancementDetail!.Decisions.Select(x => (x.Roll, x.Drawn, x.Applied)), second.Audit.AdvancementDetail!.Decisions.Select(x => (x.Roll, x.Drawn, x.Applied)));
    }
}
