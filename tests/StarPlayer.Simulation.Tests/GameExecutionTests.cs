using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;
using StarPlayer.Simulation.Tests.Authority;
using static StarPlayer.Simulation.Tests.Script;
using Policy = StarPlayer.Simulation.Baseball.AdvancementPolicy;

namespace StarPlayer.Simulation.Tests;

/// <summary>Builders for the Phase 3-D game execution contract. The park snapshot is made by the real C46 factory under the widest domain bounds (so a value the ACTIVE model refuses can be built on purpose).</summary>
internal static class Ex
{
    public static readonly string Hash = new('a', 64);

    public static ParkFactorSnapshot Park(int homeRuns = 1000, int hits = 1000, int extraBaseHits = 1000, string stadium = "stadium-ex", string profile = "pf-ex", int version = 1)
    {
        var made = ParkFactorProfile.Create(ParkFactorProfileId.Create(profile).Value, StadiumId.Create(stadium).Value, version, new DateOnly(2030, 1, 1), homeRuns, hits, extraBaseHits,
            ParkFactorBounds.Create(1, ParkFactorBounds.AbsoluteMaxPermille).Value);
        Assert.True(made.IsSuccess, made.Error?.Message);
        return made.Value.ToSnapshot();
    }

    public static GameExecution Exec(ParkFactorSnapshot? park = null, string model = "prob-model-v2", string tuning = "tuning-virtual-proto-v2", string? hash = null,
        string policy = Policy.Version, string? inputs = null) =>
        new(model, tuning, hash ?? Hash, policy, park ?? Park(), inputs);

    public static Result<GameState> Start(GameExecution? execution) =>
        GameReducer.Start(new GameStart("g1", TestRules.Dh, Team("a", true), Team("h", true), execution));
}

/// <summary>
/// Phase 3-D (owner decisions P3, S2, S3): the game execution contract is validated when the game starts, preserved by every state transition, copied to the finalized game and
/// re-validated when a stored game is restored. A legacy game (no contract) records no park and stays valid; an active-model game needs an explicit snapshot even for a neutral stadium.
/// </summary>
public class GameExecutionTests
{
    private static readonly string Hash64 = Ex.Hash;

    // ---------------------------------------------------------------- start

    [Fact]
    public void A_legacy_game_has_no_execution_and_finalizes_without_one()
    {
        var s = new Script().HomeRun().Outs(3).ThreeUp(17).Finish();

        Assert.Null(new Script().State.Execution);
        Assert.Null(s.State.Execution);
        Assert.Null(s.State.Final!.Execution);
    }

    [Fact]
    public void A_valid_execution_is_kept_exactly_by_the_started_state()
    {
        var execution = Ex.Exec(Ex.Park(1100, 1020, 1050));

        var state = Ex.Start(execution).Value;

        Assert.Same(execution, state.Execution);
        Assert.Equal((1100, 1020, 1050), (state.Execution!.Park.HomeRuns.Permille, state.Execution.Park.Hits.Permille, state.Execution.Park.ExtraBaseHits.Permille));
    }

    [Fact]
    public void A_neutral_stadium_still_carries_an_explicit_snapshot_so_recorded_neutral_differs_from_not_recorded()
    {
        var recorded = Ex.Start(Ex.Exec(Ex.Park())).Value;
        var legacy = Ex.Start(null).Value;

        Assert.NotNull(recorded.Execution);
        Assert.Equal((1000, 1000, 1000), (recorded.Execution!.Park.HomeRuns.Permille, recorded.Execution.Park.Hits.Permille, recorded.Execution.Park.ExtraBaseHits.Permille));
        Assert.Null(legacy.Execution);
        Assert.NotEqual(recorded, legacy);
    }

    [Theory]
    [InlineData(800, 800, 800)]
    [InlineData(1000, 1000, 1000)]
    [InlineData(1250, 1250, 1250)]
    [InlineData(800, 1250, 1000)]
    public void Park_values_on_the_edges_of_the_active_range_are_accepted(int hr, int hits, int xbh) =>
        Assert.True(Ex.Start(Ex.Exec(Ex.Park(hr, hits, xbh))).IsSuccess);

    [Theory]
    [InlineData(799, 1000, 1000)]
    [InlineData(1000, 799, 1000)]
    [InlineData(1000, 1000, 799)]
    [InlineData(1251, 1000, 1000)]
    [InlineData(1000, 1251, 1000)]
    [InlineData(1000, 1000, 1251)]
    [InlineData(3000, 1000, 1000)]
    [InlineData(1000, 1, 1000)]
    public void A_value_the_domain_accepts_but_the_active_model_does_not_support_is_refused_before_the_game_starts(int hr, int hits, int xbh)
    {
        var snapshot = Ex.Park(hr, hits, xbh);                   // C46 accepts these (its absolute bound is 3000 permille)

        var started = Ex.Start(Ex.Exec(snapshot));

        Assert.False(started.IsSuccess);
        Assert.Equal("Game.park_out_of_model_range", started.Error!.Value.Code);
    }

    [Fact]
    public void A_default_or_forged_snapshot_is_refused_because_a_record_is_not_a_validated_input()
    {
        Assert.Equal("Game.park_snapshot_invalid", Ex.Start(Ex.Exec(new ParkFactorSnapshot(default, default, 0, default, default, default))).Error!.Value.Code);
        Assert.Equal("Game.park_snapshot_invalid", Ex.Start(Ex.Exec(Ex.Park() with { Version = 0 })).Error!.Value.Code);
        Assert.Equal("Game.park_snapshot_invalid", Ex.Start(Ex.Exec(Ex.Park() with { Stadium = default })).Error!.Value.Code);
        Assert.Equal("Game.park_snapshot_invalid", Ex.Start(Ex.Exec(Ex.Park() with { Profile = default })).Error!.Value.Code);
        Assert.Equal("Game.park_snapshot_missing", Ex.Start(Ex.Exec() with { Park = null! }).Error!.Value.Code);
    }

    [Fact]
    public void The_pinned_versions_hash_policy_and_inputs_identifier_are_validated()
    {
        Assert.Equal("Game.execution_model_unsupported", Ex.Start(Ex.Exec(model: "prob-model-v1")).Error!.Value.Code);
        Assert.Equal("Game.execution_model_unsupported", Ex.Start(Ex.Exec(model: "prob-model-v4")).Error!.Value.Code);
        Assert.Equal("Game.execution_tuning_unsupported", Ex.Start(Ex.Exec(tuning: "tuning-virtual-proto-v1")).Error!.Value.Code);
        Assert.Equal("Game.execution_tuning_hash_invalid", Ex.Start(Ex.Exec(hash: "ABC")).Error!.Value.Code);
        Assert.Equal("Game.execution_tuning_hash_invalid", Ex.Start(Ex.Exec(hash: new string('A', 64))).Error!.Value.Code);        // lower-case hex only
        Assert.Equal("Game.execution_policy_unsupported", Ex.Start(Ex.Exec(policy: "advancement-policy-v3")).Error!.Value.Code);
        Assert.Equal("Game.execution_inputs_id_invalid", Ex.Start(Ex.Exec(inputs: "not-a-hash")).Error!.Value.Code);
        Assert.True(Ex.Start(Ex.Exec(policy: Policy.Version, inputs: Hash64)).IsSuccess);
    }

    [Fact]
    public void Advancement_policy_v2_needs_the_identity_of_the_player_inputs_and_v1_does_not()
    {
        Assert.Equal("Game.execution_inputs_required", Ex.Start(Ex.Exec(policy: Policy.VersionV2)).Error!.Value.Code);
        Assert.True(Ex.Start(Ex.Exec(policy: Policy.VersionV2, inputs: Hash64)).IsSuccess);
        Assert.True(Ex.Start(Ex.Exec(policy: Policy.Version)).IsSuccess);
    }

    [Fact]
    public void Starting_a_game_with_a_bad_execution_changes_nothing_else_about_the_other_start_checks()
    {
        // an execution problem does not mask or replace the existing start errors: the id and the rules are checked first
        Assert.Equal("Game.start_invalid", GameReducer.Start(new GameStart(" ", TestRules.Dh, Team("a", true), Team("h", true), Ex.Exec(hash: "x"))).Error!.Value.Code);
        Assert.Equal("Game.rules_missing", GameReducer.Start(new GameStart("g", null!, Team("a", true), Team("h", true), Ex.Exec(hash: "x"))).Error!.Value.Code);
    }

    // ---------------------------------------------------------------- preservation

    [Fact]
    public void The_execution_survives_every_kind_of_transition_unchanged()
    {
        var execution = Ex.Exec(Ex.Park(1080, 1020, 1050), policy: Policy.VersionV2, inputs: Hash64);
        var s = new Script(execution);

        s.Approach().Strike().Ball().Foul();                              // pitches
        Assert.Same(execution, s.State.Execution);
        s.InPlay(BattedBallType.Line, InPlayResult.Single);               // a hit
        s.Runner(RunnerPlayKind.Steal, M(1, 2, MoveCause.Steal));         // a runner play
        s.Sub(SubstitutionKind.PinchHit, TeamSide.Away, "ab1");           // a substitution
        s.Strikeout().Strikeout().Strikeout();                            // three outs and a half-inning change
        Assert.Same(execution, s.State.Execution);
        Assert.Equal(Ex.Park(1080, 1020, 1050), s.State.Execution!.Park);
    }

    [Fact]
    public void The_finalized_game_carries_the_same_execution_and_finalizing_again_changes_nothing()
    {
        var execution = Ex.Exec(Ex.Park(1250, 800, 1000), inputs: Hash64);
        var s = new Script(execution).HomeRun().Outs(3).ThreeUp(17).Finish();

        Assert.Same(execution, s.State.Execution);
        Assert.Same(execution, s.State.Final!.Execution);
        var again = GameReducer.Finalize(s.State).Value;
        Assert.False(again.WasNew);
        Assert.Same(execution, again.Game.Execution);
    }

    [Fact]
    public void Two_executions_that_differ_in_any_member_are_different_values()
    {
        var baseline = Ex.Exec(Ex.Park(1100, 1020, 1050), inputs: Hash64);

        Assert.Equal(baseline, Ex.Exec(Ex.Park(1100, 1020, 1050), inputs: Hash64));
        Assert.NotEqual(baseline, Ex.Exec(Ex.Park(1101, 1020, 1050), inputs: Hash64));
        Assert.NotEqual(baseline, Ex.Exec(Ex.Park(1100, 1021, 1050), inputs: Hash64));
        Assert.NotEqual(baseline, Ex.Exec(Ex.Park(1100, 1020, 1051), inputs: Hash64));
        Assert.NotEqual(baseline, Ex.Exec(Ex.Park(1100, 1020, 1050, stadium: "stadium-other"), inputs: Hash64));
        Assert.NotEqual(baseline, Ex.Exec(Ex.Park(1100, 1020, 1050, profile: "pf-other"), inputs: Hash64));
        Assert.NotEqual(baseline, Ex.Exec(Ex.Park(1100, 1020, 1050, version: 2), inputs: Hash64));
        Assert.NotEqual(baseline, Ex.Exec(Ex.Park(1100, 1020, 1050), hash: new string('b', 64), inputs: Hash64));
        Assert.NotEqual(baseline, Ex.Exec(Ex.Park(1100, 1020, 1050), inputs: new string('c', 64)));
        Assert.NotEqual(baseline, Ex.Exec(Ex.Park(1100, 1020, 1050), policy: Policy.VersionV2, inputs: Hash64));
    }

    // ---------------------------------------------------------------- the authoritative aggregate and its commit

    private static AuthoritativeGame Aggregate(GameExecution? execution)
    {
        var state = GameReducer.Start(new GameStart(Auth.GameId, TestRules.Dh, Team("a", true), Team("h", true), execution)).Value;
        return Auth.Ok(AuthoritativeGame.StartNew(state, Auth.Rng()));
    }

    [Fact]
    public void A_committed_event_publishes_a_state_that_still_holds_the_same_execution()
    {
        var execution = Ex.Exec(Ex.Park(1100, 1020, 1050));
        var cursor = Auth.Cursor(Aggregate(execution));

        var record = Auth.Commit(cursor, Auth.Approach(1));

        Assert.Equal(1, record.EventSequence);
        Assert.Same(execution, cursor.Current.State.Execution);
    }

    [Fact]
    public void A_restored_game_re_validates_its_execution_and_never_trusts_it()
    {
        var execution = Ex.Exec(Ex.Park(1100, 1020, 1050));
        var state = Aggregate(execution).State;

        Assert.True(AuthoritativeGame.Restore(state, Auth.Rng(), 0, 0).IsSuccess);
        Assert.Equal("Game.park_out_of_model_range", Auth.Code(AuthoritativeGame.Restore(state with { Execution = Ex.Exec(Ex.Park(799)) }, Auth.Rng(), 0, 0)));
        Assert.Equal("Game.park_snapshot_invalid", Auth.Code(AuthoritativeGame.Restore(state with { Execution = Ex.Exec(Ex.Park() with { Version = 0 }) }, Auth.Rng(), 0, 0)));
        Assert.Equal("Game.execution_tuning_hash_invalid", Auth.Code(AuthoritativeGame.Restore(state with { Execution = execution with { TuningHash = "short" } }, Auth.Rng(), 0, 0)));
    }

    [Fact]
    public void A_finished_game_whose_final_record_holds_another_execution_is_not_complete()
    {
        var execution = Ex.Exec(Ex.Park(1100, 1020, 1050));
        var ended = new Script(execution).HomeRun().Outs(3).ThreeUp(17).State;
        var finalized = GameReducer.Finalize(ended).Value.State;
        var other = Ex.Exec(Ex.Park(1101, 1020, 1050));

        Assert.True(AuthoritativeGame.IsComplete(finalized));
        Assert.False(AuthoritativeGame.IsComplete(finalized with { Final = finalized.Final! with { Execution = other } }));
        Assert.False(AuthoritativeGame.IsComplete(finalized with { Final = finalized.Final! with { Execution = null } }));      // "recorded" does not silently become "not recorded"
        Assert.False(AuthoritativeGame.IsComplete(finalized with { Execution = other }));
    }
}
