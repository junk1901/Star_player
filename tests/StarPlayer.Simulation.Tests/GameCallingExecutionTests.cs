using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Simulation.Tests.Script;
using Policy = StarPlayer.Simulation.Baseball.AdvancementPolicy;

namespace StarPlayer.Simulation.Tests;

/// <summary>
/// Phase 3-E (Game Calling v4, GC-A): the game execution contract of <c>prob-model-v4</c>. The pair is explicit (v4/v4), the park range is the model's own, and - because Game Calling and the pitcher's Control
/// are read from the pinned player input set - a v4 game without a pinned set is refused when it starts, in every called-pitch mode. v2 and v3 contracts are unchanged.
/// </summary>
public class GameCallingExecutionTests
{
    private const string V4 = "prob-model-v4";
    private const string T4 = "tuning-virtual-proto-v4";

    private static string Code(Result<GameState> r) => r.Error!.Value.Code;

    private static Result<GameState> StartWith(GameExecution execution, GameRuleSnapshot? rules = null) =>
        GameReducer.Start(new GameStart("g1", rules ?? TestRules.Dh, Team("a", true), Team("h", true), execution));

    [Fact]
    public void The_v4_pair_is_accepted_with_a_pinned_input_set_whatever_the_advancement_policy()
    {
        foreach (var policy in new[] { Policy.Version, Policy.VersionV2 })
            Assert.True(StartWith(Ex.Exec(model: V4, tuning: T4, policy: policy, inputs: Ex.Hash)).IsSuccess, policy);
    }

    [Fact]
    public void A_v4_game_without_a_pinned_input_set_is_refused_in_every_mode_and_nothing_is_defaulted()
    {
        Assert.Equal("Game.execution_inputs_required_game_calling", Code(StartWith(Ex.Exec(model: V4, tuning: T4))));
        Assert.Equal("Game.execution_inputs_required_game_calling", Code(StartWith(Ex.Exec(model: V4, tuning: T4, policy: Policy.Version, inputs: null))));
        // and the rules-aware framing requirement of v3 is unchanged: v3 automated needs no set, v4 does
        Assert.True(StartWith(Ex.Exec(model: "prob-model-v3", tuning: "tuning-virtual-proto-v3")).IsSuccess);
        Assert.True(StartWith(Ex.Exec()).IsSuccess);                                  // v2 as before
    }

    [Fact]
    public void Only_the_diagonal_pair_of_v4_is_accepted_and_every_neighbour_is_refused_with_the_existing_codes()
    {
        string[] models = ["prob-model-v2", "prob-model-v3", V4];
        string[] tunings = ["tuning-virtual-proto-v1", "tuning-virtual-proto-v2", "tuning-virtual-proto-v3", T4];
        foreach (var model in models)
            foreach (var tuning in tunings)
            {
                var pair = (model, tuning);
                var diagonal = pair is ("prob-model-v2", "tuning-virtual-proto-v2") or ("prob-model-v3", "tuning-virtual-proto-v3") or (V4, T4);
                var started = StartWith(Ex.Exec(model: model, tuning: tuning, inputs: Ex.Hash));
                Assert.Equal(diagonal, started.IsSuccess);
                if (!diagonal)
                    Assert.Equal("Game.execution_tuning_unsupported", Code(started));
            }

        Assert.Equal("Game.execution_model_unsupported", Code(StartWith(Ex.Exec(model: "prob-model-v5", tuning: "tuning-virtual-proto-v5", inputs: Ex.Hash))));
        Assert.Equal("Game.execution_model_unsupported", Code(StartWith(Ex.Exec(model: "prob-model-v1", tuning: "tuning-virtual-proto-v1", inputs: Ex.Hash))));
        Assert.Equal("Game.execution_model_unsupported", Code(StartWith(Ex.Exec(model: "PROB-MODEL-V4", tuning: T4, inputs: Ex.Hash))));
    }

    [Theory]
    [InlineData(799, 1000, 1000)]
    [InlineData(1000, 1251, 1000)]
    [InlineData(1000, 1000, 3000)]
    public void The_park_range_of_v4_is_the_models_own_and_the_same_as_v2_and_v3(int hr, int hits, int xbh) =>
        Assert.Equal("Game.park_out_of_model_range", Code(StartWith(Ex.Exec(Ex.Park(hr, hits, xbh), model: V4, tuning: T4, inputs: Ex.Hash))));

    [Theory]
    [InlineData(800, 800, 800)]
    [InlineData(1250, 1250, 1250)]
    public void The_edges_of_the_park_range_are_accepted_under_v4(int hr, int hits, int xbh) =>
        Assert.True(StartWith(Ex.Exec(Ex.Park(hr, hits, xbh), model: V4, tuning: T4, inputs: Ex.Hash)).IsSuccess);

    [Fact]
    public void The_pinned_set_identifier_must_still_be_a_lower_case_sha256_and_the_other_pins_keep_their_codes()
    {
        Assert.Equal("Game.execution_inputs_id_invalid", Code(StartWith(Ex.Exec(model: V4, tuning: T4, inputs: "not-a-hash"))));
        Assert.Equal("Game.execution_tuning_hash_invalid", Code(StartWith(Ex.Exec(model: V4, tuning: T4, hash: "ABC", inputs: Ex.Hash))));
        Assert.Equal("Game.execution_policy_unsupported", Code(StartWith(Ex.Exec(model: V4, tuning: T4, policy: "advancement-policy-v3", inputs: Ex.Hash))));
        Assert.Equal("Game.park_snapshot_missing", Code(StartWith(Ex.Exec(model: V4, tuning: T4, inputs: Ex.Hash) with { Park = null! })));
    }
}
