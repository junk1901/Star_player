using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;

namespace StarPlayer.Simulation.Baseball;

// Phase 3-D v2 Part C (owner decisions R2, R3, R4, R5; AdvancementPolicy.VersionV2): the two ability inputs of one `advancement` decision. The runner is the current occupant of the start
// base, the thrower is the current fielder at the Retriever position (the player the throw-out credits with the assist). Both are read from the immutable player input set; a missing
// player or rating is an error, never a default of 50. The inputs go into ONE StageProbability.Compute call, so the row is multiplied and normalised once.

/// <summary>The inputs of one decision and the audit of where they came from.</summary>
internal sealed record AdvancementInputs(IReadOnlyList<ModifierInput> Modifiers, AdvancementAbilityAudit Audit);

internal static class AdvancementSlots
{
    public static Result<AdvancementInputs> For(AdvancementContext context, GameState original, PlayerInputSet set)
    {
        static Result<AdvancementInputs> Fail(string code, string message) => Result<AdvancementInputs>.Failure(new Error(code, message));

        if (set.Find(context.Runner) is not { } runner)
            return Fail("Advancement.runner_input_missing", $"'{context.Key}' 맥락의 주자 {context.Runner}가 선수 입력 집합에 없습니다.");
        if (runner.Speed is not { } speed)
            return Fail("Advancement.runner_speed_missing", $"'{context.Key}' 맥락의 주자 {context.Runner}에게 주루(Speed) 능력치가 없습니다.");
        if (original.DefenderAt(original.FieldingSide, context.Retriever) is not { } thrower)
            return Fail("Advancement.retriever_missing", $"'{context.Key}' 맥락의 송구 위치 {context.Retriever}에 수비수가 없습니다.");
        if (set.Find(thrower) is not { } fielder)
            return Fail("Advancement.retriever_input_missing", $"'{context.Key}' 맥락의 송구 수비수 {thrower}가 선수 입력 집합에 없습니다.");
        if (fielder.Arm is not { } arm)
            return Fail("Advancement.retriever_arm_missing", $"'{context.Key}' 맥락의 송구 수비수 {thrower}에게 송구(Arm) 능력치가 없습니다.");

        return Result<AdvancementInputs>.Success(new AdvancementInputs(Inputs(speed, arm), new AdvancementAbilityAudit(context.Runner, speed, context.Retriever, thrower, arm)));
    }

    /// <summary>The two inputs of one decision, in the model's slot order. The only place they are built: planning and the commit-time re-derivation both use it.</summary>
    public static IReadOnlyList<ModifierInput> Inputs(int speed, int arm) => [ModifierInput.Linear("runner.speed", speed), ModifierInput.Linear("defense.arm", arm)];
}
