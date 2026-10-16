using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;

namespace StarPlayer.Simulation.Baseball;

/// <summary>
/// The validation of a <see cref="GameExecution"/> (Phase 3-D, owner decisions P3/P4/S2/S3), used at the game start, when a stored game is restored and again by the resolver. A <c>null</c>
/// execution is a legacy game and is valid. A present one must name one of the ACTIVE model/tuning pairs (<c>prob-model-v2</c> / <c>tuning-virtual-proto-v2</c>, or since Phase 3-E
/// <c>prob-model-v3</c> / <c>tuning-virtual-proto-v3</c>; an explicit table, never a "newer than v1" test), a known advancement policy and a
/// complete park snapshot whose three factors lie inside the supported range OF THAT MODEL (read from the model's own slots). A value the C46 domain accepts (up to 3000 permille) but the active model does not support is
/// REFUSED before the game starts: nothing is clamped and nothing is replaced by neutral.
/// </summary>
internal static class GameExecutionRules
{
    /// <summary>Returns the first problem or null. The slots' ranges come from the model itself, so a later model change cannot leave this check behind.</summary>
    public static Error? Validate(GameExecution? execution, GameRuleSnapshot? rules = null)
    {
        if (execution is null)
            return null;
        if (execution.ProbabilityModelVersion is not (ProbabilityModelVersion.V2Text or ProbabilityModelVersion.V3Text or ProbabilityModelVersion.V4Text))
            return Err("Game.execution_model_unsupported", $"경기 실행 계약의 확률 모델은 '{ProbabilityModelVersion.V2Text}', '{ProbabilityModelVersion.V3Text}' 또는 '{ProbabilityModelVersion.V4Text}'이어야 합니다(받은 값: '{execution.ProbabilityModelVersion}').");
        var model = ProbabilityModel.Resolve(ProbabilityModelVersion.Create(execution.ProbabilityModelVersion).Value).Value;
        var tuning = TuningVersion.Create(execution.TuningVersion);
        if (!tuning.IsSuccess || !ProbabilityModel.Pairs(model.Version, tuning.Value))
            return Err("Game.execution_tuning_unsupported", $"경기 실행 계약의 튜닝 버전은 확률 모델 '{model.Version}'의 튜닝 '{ProbabilityModel.TuningFor(model.Version).Value}'이어야 합니다(받은 값: '{execution.TuningVersion}').");
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

        // Phase 3-E: a model whose Framing is active and a game whose rules call pitches by hand (non_automated) read the current catcher's rating from the pinned player input set, so the set
        // must be pinned. Automated play never needs a Framing rating, so it keeps the v2 requirements exactly.
        if (rules is { CalledPitchMode: CalledPitchMode.NonAutomated } && model.FramingActive && execution.PlayerInputsId is null)
            return Err("Game.execution_inputs_required_framing", $"확률 모델 '{model.Version}'에서 수동 판정(non_automated) 경기는 포수의 Framing을 읽을 선수 입력 집합 식별자가 있어야 합니다.");

        // Phase 3-E (Game Calling v4, GC-S1): the model pins the current catcher's GameCalling AND the current pitcher's Control in the game's player input set (content version 3), in EVERY called-pitch mode.
        // Without a pinned set there is no authoritative source for either value, so the game is refused here and nothing is defaulted. (The identity is a hash: its content VERSION is checked wherever the set itself is read.)
        if (model.GameCallingActive && execution.PlayerInputsId is null)
            return Err("Game.execution_inputs_required_game_calling", $"확률 모델 '{model.Version}'은 포수의 Game Calling과 투수의 Control을 읽을 선수 입력 집합(콘텐츠 형식 {PlayerInputSet.ContentV3}) 식별자가 있어야 합니다.");

        foreach (var (slotId, factor, name) in new[] { ("park.home_runs", park.HomeRuns, "홈런"), ("park.hits", park.Hits, "안타(단타)"), ("park.extra_base_hits", park.ExtraBaseHits, "장타") })
        {
            var slot = model.Slot(slotId)!;
            if (factor.Permille < slot.InputMin || factor.Permille > slot.InputMax)
                return Err("Game.park_out_of_model_range", $"구장 {name} 계수 {factor.Permille}‰는 활성 모델이 지원하는 {slot.InputMin}~{slot.InputMax}‰ 밖입니다(도메인 절대 상한 {ParkFactorBounds.AbsoluteMaxPermille}‰와는 별개).");
        }

        return null;
    }

    private static bool IsHash(string? text) => text is { Length: 64 } && text.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static Error Err(string code, string message) => new(code, message);
}
