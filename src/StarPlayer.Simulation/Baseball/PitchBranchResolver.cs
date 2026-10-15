using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.Simulation.Baseball;

/// <summary>
/// Phase 3-C2A (ADR-007): draws ONE pitch's probability branch from the real seeded streams and returns a <see cref="PitchBranchDraft"/>. It reuses
/// <see cref="StageProbability.Compute"/> and <see cref="Distribution.Select"/>; it adds no probability, no stream algorithm and no rule.
/// <para>
/// <b>Order and streams</b> (every decision is exactly one <c>NextBounded(1,000,000)</c> call; a stage that is not reached is not drawn, a stage whose distribution is
/// 0%/100% is still drawn): <c>target_zone</c> on <c>game/target@1</c> (condition: the intent) → <c>actual_location</c> on <c>game/location@1</c> (condition: the target zone) →
/// <c>batter_decision</c> on <c>game/pitch@1</c> (condition: the actual zone) → <c>called_pitch</c> on <c>game/pitch@1</c> if the batter took or an explicit Take was not a hit by
/// pitch, else <c>swing_result</c> on <c>game/contact@1</c> → <c>batted_ball</c> on <c>game/contact@1</c> (in play) → <c>defense_result</c> on <c>game/field@1</c>. <c>advancement</c> is NOT drawn
/// here (3-C2B).
/// </para>
/// <para>
/// <b>Tendency (Phase 3-D-1).</b> <c>swing_result</c> and <c>batted_ball</c> receive a <see cref="TendencyContext"/> (the actual zone and the batter's hidden vector, neutral when none is
/// given) so the probability model applies its window guard and its bounded shift. It only changes the distribution a roll is read from: no stage is added, no extra draw is made and no roll changes.
/// </para>
/// <para>
/// <b>Ability modifiers (Phase 3-D).</b> When the request carries <see cref="PitchAbilities"/>, each stage receives the modifier inputs <see cref="PitchModifierInputs"/> builds for
/// it (nine live slots; see there). Like the tendency this only changes the distribution a roll is read from.
/// </para>
/// <para>
/// <b>Explicit Take</b> keeps the reducer's contract (ball, called strike, hit by pitch): the batter decision is drawn once exactly as in a normal pitch; a hit by pitch ends the
/// branch, any other decision (take or swing) is treated as watching and draws the called pitch, and swing/contact/field are never drawn.
/// </para>
/// <para>
/// <b>No side effects.</b> The caller's snapshot is only read; the work runs on a private <see cref="GameRandom"/> restored from it and is discarded on any failure or
/// cancellation. The caller adopts <see cref="PitchBranchDraft.NextRng"/> only when it accepts the draft. Everything is validated before the first draw.
/// </para>
/// </summary>
public static class PitchBranchResolver
{
    public const string ContractVersion = "pitch-branch-v1";

    private const string HitByPitch = "hit_by_pitch";
    private const string Take = "take";

    public static Result<PitchBranchDraft> Resolve(PitchBranchRequest? request, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();

        var checkedInput = Validate(request);
        if (!checkedInput.IsSuccess)
            return Result<PitchBranchDraft>.Failure(checkedInput.Error!.Value);
        var (state, tuning, intentKey, side) = checkedInput.Value;
        var rules = state.Rules;
        var tendencyVector = request!.BatterTendency ?? BatterZoneTendency.Create(new int[BatterZoneTendency.ZoneCount], tuning.Reference).Value; // all zeros is always average-neutral

        var restored = GameRandom.Restore(request!.Rng);
        if (!restored.IsSuccess)
            return Result<PitchBranchDraft>.Failure(restored.Error!.Value);
        // the stream position must belong to THIS game: a snapshot is valid for one context only, and the game id is that context (exact, ordinal, never normalized)
        if (!string.Equals(state.GameId, restored.Value.ContextKey, StringComparison.Ordinal))
            return Result<PitchBranchDraft>.Failure(new Error("PitchBranch.game_context_mismatch",
                $"경기 상태의 경기 ID('{state.GameId}')와 난수 스냅샷의 경기 맥락('{restored.Value.ContextKey}')이 서로 다른 경기를 가리켜 투구를 진행할 수 없습니다."));
        var random = restored.Value; // private working copy: the caller's snapshot is immutable and nothing below touches anything else

        var steps = new List<PitchBranchStep>();
        var calls = new Dictionary<RngStreamId, (int Calls, long Draws)>();

        Result<string> Decide(string stage, string condition, RngStreamId streamId, TendencyContext? tendency = null)
        {
            cancellation.ThrowIfCancellationRequested();
            var inputs = request.Abilities is { } abilities
                ? PitchModifierInputs.For(stage, abilities, state.Count, state.Approach, side, request.PitcherThrows!.Value)
                : null;
            var computed = StageProbability.Compute(tuning, stage, condition, inputs, tendency);
            if (!computed.IsSuccess)
                return Result<string>.Failure(computed.Error!.Value);
            var stream = random.Stream(streamId);
            var before = stream.Draws;
            var roll = stream.NextBounded(ProbabilityScale.RollBound); // exactly one call per decision; rejection sampling may use more raw draws
            var used = stream.Draws - before;
            var chosen = computed.Value.Distribution.Select(roll);
            if (!chosen.IsSuccess)
                return Result<string>.Failure(chosen.Error!.Value);
            steps.Add(new PitchBranchStep(stage, condition, chosen.Value, streamId, roll, used, computed.Value.Trace));
            calls[streamId] = calls.TryGetValue(streamId, out var seen) ? (seen.Calls + 1, seen.Draws + used) : (1, used);
            return chosen;
        }

        // 1-2: what the pitcher aimed at, then where the pitch actually went (the location is fixed before any outcome is drawn)
        var targetText = Decide(StageIds.TargetZone, intentKey, GameRngStreams.Target);
        if (!targetText.IsSuccess)
            return Result<PitchBranchDraft>.Failure(targetText.Error!.Value);
        var targetZone = PitchZoneId.Create(targetText.Value);
        if (!targetZone.IsSuccess)
            return Result<PitchBranchDraft>.Failure(targetZone.Error!.Value);
        var target = TargetZone.Create(tuning.ZoneModel, targetZone.Value);
        if (!target.IsSuccess)
            return Result<PitchBranchDraft>.Failure(target.Error!.Value);

        var locationText = Decide(StageIds.ActualLocation, ConditionKeys.Zone(targetZone.Value), GameRngStreams.Location);
        if (!locationText.IsSuccess)
            return Result<PitchBranchDraft>.Failure(locationText.Error!.Value);
        var actualZone = PitchZoneId.Create(locationText.Value);
        if (!actualZone.IsSuccess)
            return Result<PitchBranchDraft>.Failure(actualZone.Error!.Value);
        var location = PitchLocation.FromZone(tuning.ZoneModel, actualZone.Value, side);
        if (!location.IsSuccess)
            return Result<PitchBranchDraft>.Failure(location.Error!.Value);

        // 3: the batter's response to the actual location
        var decision = Decide(StageIds.BatterDecision, ConditionKeys.Zone(actualZone.Value), GameRngStreams.Pitch);
        if (!decision.IsSuccess)
            return Result<PitchBranchDraft>.Failure(decision.Error!.Value);

        string? called = null, swing = null, batted = null, defense = null;
        PitchBranchKind kind;
        if (decision.Value == HitByPitch)
            kind = PitchBranchKind.HitByPitch;
        else if (decision.Value == Take || request.ExplicitTake)
        {
            var key = ConditionKeys.CalledPitch(rules.CalledPitchMode, actualZone.Value);
            if (!key.IsSuccess)
                return Result<PitchBranchDraft>.Failure(key.Error!.Value);
            var call = Decide(StageIds.CalledPitch, key.Value, GameRngStreams.Pitch);
            if (!call.IsSuccess)
                return Result<PitchBranchDraft>.Failure(call.Error!.Value);
            called = call.Value;
            kind = called == "ball" ? PitchBranchKind.CalledBall : PitchBranchKind.CalledStrike;
        }
        else
        {
            var tendency = new TendencyContext(actualZone.Value, tendencyVector);
            var result = Decide(StageIds.SwingResult, ConditionKeys.Zone(actualZone.Value), GameRngStreams.Contact, tendency);
            if (!result.IsSuccess)
                return Result<PitchBranchDraft>.Failure(result.Error!.Value);
            swing = result.Value;
            kind = swing switch { "miss" => PitchBranchKind.SwingingStrike, "foul" => PitchBranchKind.Foul, _ => PitchBranchKind.InPlay };
            if (kind == PitchBranchKind.InPlay)
            {
                var group = ConditionKeys.BattedBallGroup(actualZone.Value);
                if (!group.IsSuccess)
                    return Result<PitchBranchDraft>.Failure(group.Error!.Value);
                var ball = Decide(StageIds.BattedBall, group.Value, GameRngStreams.Contact, tendency);
                if (!ball.IsSuccess)
                    return Result<PitchBranchDraft>.Failure(ball.Error!.Value);
                batted = ball.Value;
                var fielded = Decide(StageIds.DefenseResult, batted, GameRngStreams.Field);
                if (!fielded.IsSuccess)
                    return Result<PitchBranchDraft>.Failure(fielded.Error!.Value);
                defense = fielded.Value;
            }
        }

        cancellation.ThrowIfCancellationRequested();
        return Result<PitchBranchDraft>.Success(new PitchBranchDraft(
            ContractVersion, request.Intent, request.ExplicitTake, target.Value, location.Value, kind, decision.Value, called, swing, batted, defense,
            steps.ToImmutableArray(), steps.Count,
            calls.OrderBy(c => c.Key.Canonical, StringComparer.Ordinal).Select(c => new PitchBranchStreamUse(c.Key, c.Value.Calls, c.Value.Draws)).ToImmutableArray(),
            random.Snapshot(), tuning.Model.Version.Value, tuning.Tuning.Value, tuning.ContentHash, rules.ProfileId, rules.Version, rules.ContentHash));
    }

    /// <summary>Everything that can be refused is refused here, before the first random number is drawn.</summary>
    private static Result<(GameState State, TuningProfile Tuning, string Intent, AppliedBatterSide Side)> Validate(PitchBranchRequest? request)
    {
        static Result<(GameState, TuningProfile, string, AppliedBatterSide)> Fail(string code, string message) =>
            Result<(GameState, TuningProfile, string, AppliedBatterSide)>.Failure(new Error(code, message));

        if (request is null)
            return Fail("PitchBranch.request_missing", "투구 분기 요청이 없습니다.");
        if (request.State is null)
            return Fail("PitchBranch.state_missing", "경기 상태가 없어 투구를 진행할 수 없습니다.");
        if (request.Tuning is null)
            return Fail("PitchBranch.tuning_missing", "튜닝 프로필이 없습니다.");
        var state = request.State;
        var tuning = request.Tuning;
        var rules = state.Rules;

        if (state.Status != GameStatus.InProgress)
            return Fail("PitchBranch.game_not_in_progress", "끝난 경기에서는 투구를 진행할 수 없습니다.");
        if (request.ExplicitTake && state.Stage == PaStage.InSequence)
            return Fail("PitchBranch.take_not_legal", "접근 방식이 정해진 타석에서는 Take를 쓸 수 없습니다.");
        if (!request.ExplicitTake && state.Stage != PaStage.InSequence)
            return Fail("PitchBranch.pitch_not_legal", "접근 방식을 먼저 정해야 투구를 진행할 수 있습니다.");

        // the rules must declare how pitches are called; a profile that predates the field is never read as Automated or NonAutomated
        if (rules is null || !rules.CalledPitchModeDeclared)
            return Fail("PitchBranch.called_pitch_mode_undeclared", "규칙 프로필이 호출 판정 방식을 선언하지 않아 신규 투구를 만들 수 없습니다.");
        if (rules.ZoneModel is not { } ruleZones || tuning.ZoneModel.Value is null || ruleZones.Value != tuning.ZoneModel.Value)
            return Fail("PitchBranch.zone_model_mismatch", "규칙의 구역 모델과 튜닝의 구역 모델이 일치하지 않습니다.");
        if (!PitchZoneModel.Resolve(ruleZones).IsSuccess)
            return Fail("PitchBranch.zone_model_unsupported", $"지원하지 않는 구역 모델입니다: '{ruleZones}'");
        if (!ProbabilityModel.Resolve(tuning.Model.Version).IsSuccess)
            return Fail("PitchBranch.probability_model_unsupported", $"지원하지 않는 확률 모델 버전입니다: '{tuning.Model.Version}'");
        if (!TuningVersion.Create(tuning.Tuning.Value).IsSuccess)
            return Fail("PitchBranch.tuning_version_unsupported", $"지원하지 않는 튜닝 버전입니다: '{tuning.Tuning}'");
        if (tuning.ContentHash is not { Length: 64 } hash || !hash.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f')))
            return Fail("PitchBranch.tuning_hash_invalid", "튜닝 내용 해시가 올바르지 않습니다.");

        if (!Enum.IsDefined(request.Intent))
            return Fail("PitchBranch.intent_invalid", "투수 의도 값이 올바르지 않습니다.");
        var intent = request.Intent switch { PitchIntent.Balanced => "balanced", PitchIntent.Attack => "attack", _ => "careful" };

        if (request.Abilities is { } abilities && !abilities.InRange())
            return Fail("PitchBranch.ability_out_of_range", "투구 보정에 쓰는 기본 능력치는 0~100 범위여야 합니다.");
        if (request.BatterDeclaredSide is not { } declared)
            return Fail("PitchBranch.batter_side_missing", "타자의 선언 타석 방향이 없습니다.");
        if (!Enum.IsDefined(declared))
            return Fail("PitchBranch.batter_side_invalid", "타자의 선언 타석 방향 값이 올바르지 않습니다.");
        if (request.PitcherThrows is not { } hand)
            return Fail("PitchBranch.pitcher_hand_missing", "투수의 투구 손이 없어 타석 방향을 정할 수 없습니다.");
        if (!Enum.IsDefined(hand))
            return Fail("PitchBranch.pitcher_hand_invalid", "투수의 투구 손 값이 올바르지 않습니다.");

        // right/left batters have one side; a switch hitter stands opposite the pitcher's throwing hand. No randomness is used.
        var applied = declared switch
        {
            BattingSide.Right => AppliedBatterSide.Right,
            BattingSide.Left => AppliedBatterSide.Left,
            _ => hand == ThrowingHand.Right ? AppliedBatterSide.Left : AppliedBatterSide.Right,
        };
        var allowed = AppliedBatterSideRules.Validate(declared, applied);
        if (!allowed.IsSuccess)
            return Result<(GameState, TuningProfile, string, AppliedBatterSide)>.Failure(allowed.Error!.Value);

        return Result<(GameState, TuningProfile, string, AppliedBatterSide)>.Success((state, tuning, intent, applied));
    }
}
