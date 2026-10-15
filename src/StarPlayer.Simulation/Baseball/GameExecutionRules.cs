using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;

namespace StarPlayer.Simulation.Baseball;

/// <summary>
/// The validation of a <see cref="GameExecution"/> (Phase 3-D, owner decisions P3/P4/S2/S3), used at the game start, when a stored game is restored and again by the resolver. A <c>null</c>
/// execution is a legacy game and is valid. A present one must name the ACTIVE model and tuning (<c>prob-model-v2</c> / <c>tuning-virtual-proto-v2</c>), a known advancement policy and a
/// complete park snapshot whose three factors lie inside the model's own supported range. A value the C46 domain accepts (up to 3000 permille) but the active model does not support is
/// REFUSED before the game starts: nothing is clamped and nothing is replaced by neutral.
/// </summary>
internal static class GameExecutionRules
{
    /// <summary>Returns the first problem or null. The slots' ranges come from the model itself, so a later model change cannot leave this check behind.</summary>
    public static Error? Validate(GameExecution? execution)
    {
        if (execution is null)
            return null;
        if (execution.ProbabilityModelVersion != ProbabilityModelVersion.V2Text)
            return Err("Game.execution_model_unsupported", $"경기 실행 계약의 확률 모델은 '{ProbabilityModelVersion.V2Text}'이어야 합니다(받은 값: '{execution.ProbabilityModelVersion}').");
        if (execution.TuningVersion != TuningVersion.VirtualProtoV2Text)
            return Err("Game.execution_tuning_unsupported", $"경기 실행 계약의 튜닝 버전은 '{TuningVersion.VirtualProtoV2Text}'이어야 합니다(받은 값: '{execution.TuningVersion}').");
        if (!IsHash(execution.TuningHash))
            return Err("Game.execution_tuning_hash_invalid", "경기 실행 계약의 튜닝 내용 해시가 소문자 16진수 64자가 아닙니다.");
        if (execution.AdvancementPolicyVersion != AdvancementPolicy.Version && execution.AdvancementPolicyVersion != AdvancementPolicy.VersionV2)
            return Err("Game.execution_policy_unsupported", $"지원하지 않는 추가 진루 정책 버전입니다: '{execution.AdvancementPolicyVersion}'");
        if (execution.PlayerInputsId is not null && !IsHash(execution.PlayerInputsId))
            return Err("Game.execution_inputs_id_invalid", "선수 입력 집합 식별자가 소문자 16진수 64자가 아닙니다.");
        if (execution.AdvancementPolicyVersion == AdvancementPolicy.VersionV2 && execution.PlayerInputsId is null)
            return Err("Game.execution_inputs_required", $"추가 진루 정책 '{AdvancementPolicy.VersionV2}'은 선수 입력 집합 식별자가 있어야 합니다(주자 Speed·송구 Arm을 읽기 위함).");

        if (execution.Park is not { } park)
            return Err("Game.park_snapshot_missing", "활성 확률 모델의 경기는 중립 구장이라도 명시적인 구장 스냅샷이 있어야 합니다.");
        if (park.Stadium == default || string.IsNullOrWhiteSpace(park.Stadium.Value) || park.Profile == default || string.IsNullOrWhiteSpace(park.Profile.Value) || park.Version < 1)
            return Err("Game.park_snapshot_invalid", "구장 스냅샷의 구장 ID·프로필 ID·버전이 올바르지 않습니다.");

        var v2 = ProbabilityModel.V2;
        foreach (var (slotId, factor, name) in new[] { ("park.home_runs", park.HomeRuns, "홈런"), ("park.hits", park.Hits, "안타(단타)"), ("park.extra_base_hits", park.ExtraBaseHits, "장타") })
        {
            var slot = v2.Slot(slotId)!;
            if (factor.Permille < slot.InputMin || factor.Permille > slot.InputMax)
                return Err("Game.park_out_of_model_range", $"구장 {name} 계수 {factor.Permille}‰는 활성 모델이 지원하는 {slot.InputMin}~{slot.InputMax}‰ 밖입니다(도메인 절대 상한 {ParkFactorBounds.AbsoluteMaxPermille}‰와는 별개).");
        }

        return null;
    }

    private static bool IsHash(string? text) => text is { Length: 64 } && text.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static Error Err(string code, string message) => new(code, message);
}
