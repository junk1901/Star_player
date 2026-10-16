using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;

namespace StarPlayer.Simulation.Baseball;

// Phase 3-E Package B (owner decisions E1-E7, Codex PASS_WITH_NOTES): the catcher input of one pitch. Like PitchFieldingInputs it is read from the game state and an immutable
// PlayerInputSet BEFORE the first random number, copied into plain values and frozen. It is resolver-internal: the exact rating of an NPC never reaches a client.
// Game Calling v4 (GC1-GC10): the same object also carries the catcher's GameCalling; the two requirements are separate (see FramingRequired / GameCallingRequired).

/// <summary>
/// The catcher who is behind the plate for one pitch and the base ratings the active model reads from him, frozen when the pitch is prepared: <c>ReceivingFraming</c> (0-100) when the model's Framing is
/// active AND the game's rules call pitches by hand (<see cref="FramingRequired"/>), and <c>GameCalling</c> (0-100) when the model's Game Calling is active (<see cref="GameCallingRequired"/>, every mode).
/// A rating nobody asked for is <c>null</c> - never read, never defaulted. Under <c>prob-model-v1/v2</c> nothing is read; under <c>prob-model-v3</c> and automated calling nothing is read either; under
/// <c>prob-model-v4</c> an automated game reads GameCalling only and a non_automated game reads both. It holds the game id and the identity of the <see cref="PlayerInputSet"/> it came from so it cannot be
/// moved to another game or set, and it keeps no sheet, dictionary or delegate. The catcher is NEVER taken from a stored audit or from the prepared pitch when something is verified: the expected value is
/// always re-derived from the real state and the real set.
/// </summary>
public sealed class FrozenCatcherInput
{
    internal FrozenCatcherInput(string gameId, string inputSetId, PlayerId catcher, int? receivingFraming, int? gameCalling = null)
    {
        GameId = gameId; InputSetId = inputSetId; Catcher = catcher; ReceivingFraming = receivingFraming; GameCalling = gameCalling;
    }

    public string GameId { get; }

    /// <summary>The identity of the set the ratings were read from (content version 2 for <c>prob-model-v3</c>, content version 3 for <c>prob-model-v4</c>).</summary>
    public string InputSetId { get; }

    /// <summary>The player standing at catcher for the fielding team when the pitch was prepared.</summary>
    public PlayerId Catcher { get; }

    /// <summary>His base <c>ReceivingFraming</c>, 0-100, or <c>null</c> when this pitch's model and rules do not use Framing.</summary>
    public int? ReceivingFraming { get; }

    /// <summary>His base <c>GameCalling</c>, 0-100, or <c>null</c> when this pitch's model does not use Game Calling (<c>prob-model-v1/v2/v3</c>).</summary>
    public int? GameCalling { get; }

    /// <summary>
    /// Whether a pitch of this model and these rules needs the catcher's Framing: the model's Framing slot is active (<c>prob-model-v3</c> and later) and the rules declare
    /// <see cref="CalledPitchMode.NonAutomated"/>. An automated or undeclared profile never needs it, so a game without Framing ratings stays valid there.
    /// </summary>
    internal static bool FramingRequired(ProbabilityModel? model, GameRuleSnapshot? rules) =>
        model is { FramingActive: true } && rules is { CalledPitchMode: CalledPitchMode.NonAutomated };

    /// <summary>Whether a pitch of this model needs the catcher's GameCalling: the model's Game Calling slot is active (<c>prob-model-v4</c>), whatever the called-pitch mode. Never a guess from the data.</summary>
    internal static bool GameCallingRequired(ProbabilityModel? model) => model is { GameCallingActive: true };

    /// <summary>Whether a pitch needs a catcher input at all (either requirement). Clear and Borderline pitches are treated alike - the input is required for the pitch, the model's scope decides where it acts.</summary>
    internal static bool Required(ProbabilityModel? model, GameRuleSnapshot? rules) => FramingRequired(model, rules) || GameCallingRequired(model);

    /// <summary>
    /// Reads the CURRENT catcher of the FIELDING team from <paramref name="state"/> and the ratings <paramref name="model"/> and the state's rules require from <paramref name="set"/>. The set must be
    /// the content version the model reads (3 when Game Calling is required, else 2); a missing catcher, a catcher who is not in the set and a catcher without the needed rating are errors (codes
    /// <c>{prefix}.catcher_missing</c>, <c>catcher_input_missing</c>, <c>catcher_ratings_missing</c>, <c>catcher_game_calling_missing</c>, <c>catcher_input_format</c>); nothing is defaulted to 50.
    /// </summary>
    internal static Result<FrozenCatcherInput> Build(GameState state, PlayerInputSet set, ProbabilityModel model, string prefix)
    {
        var framingRequired = FramingRequired(model, state.Rules);
        var gameCallingRequired = GameCallingRequired(model);
        if (!framingRequired && !gameCallingRequired)
            return Fail(prefix + ".catcher_input_unexpected", "이 확률 모델·판정 방식에서는 포수 입력을 읽지 않습니다.");
        var expectedFormat = gameCallingRequired ? PlayerInputSet.ContentV3 : PlayerInputSet.ContentV2;
        if (set.ContentVersion != expectedFormat)
            return Fail(prefix + ".catcher_input_format", $"포수 능력치를 읽으려면 선수 입력 집합이 콘텐츠 형식 '{expectedFormat}'이어야 합니다(받은 형식: '{set.ContentVersion}').");
        if (state.DefenderAt(state.FieldingSide, FieldPosition.Catcher) is not { } catcher)
            return Fail(prefix + ".catcher_missing", "수비 위치 Catcher에 선수가 없어 포수 입력을 고정할 수 없습니다.");
        if (set.Find(catcher) is not { } input)
            return Fail(prefix + ".catcher_input_missing", $"포수 {catcher}가 선수 입력 집합에 없습니다.");

        int? framing = null;
        if (framingRequired)
        {
            if (input.ReceivingFraming is not { } f)
                return Fail(prefix + ".catcher_ratings_missing", $"포수 {catcher}에게 포수 능력치(ReceivingFraming)가 없어 Framing 보정을 만들 수 없습니다.");
            if (f is < SkillValue.Min or > SkillValue.Max)
                return Fail(prefix + ".catcher_rating_invalid", $"포수 {catcher}의 ReceivingFraming 값이 0~100 범위가 아닙니다.");
            framing = f;
        }

        int? gameCalling = null;
        if (gameCallingRequired)
        {
            if (input.GameCalling is not { } g)
                return Fail(prefix + ".catcher_game_calling_missing", $"포수 {catcher}에게 포수 능력치(GameCalling)가 없어 Game Calling 보정을 만들 수 없습니다.");
            if (g is < SkillValue.Min or > SkillValue.Max)
                return Fail(prefix + ".catcher_game_calling_invalid", $"포수 {catcher}의 GameCalling 값이 0~100 범위가 아닙니다.");
            gameCalling = g;
        }

        return Result<FrozenCatcherInput>.Success(new FrozenCatcherInput(state.GameId, set.Id, catcher, framing, gameCalling));
    }

    /// <summary>
    /// <paramref name="inputs"/> (the stage's inputs, or null) extended with the catcher's modifiers: <c>catcher.framing</c> for <c>called_pitch</c> when a Framing rating was frozen, <c>catcher.game_calling</c>
    /// for <c>actual_location</c> when a GameCalling rating was frozen. Every condition of those stages receives it: the MODEL's condition scope decides where it acts, the resolver does not filter.
    /// </summary>
    internal static IReadOnlyList<ModifierInput>? Extend(IReadOnlyList<ModifierInput>? inputs, string stage, FrozenCatcherInput? catcher)
    {
        if (catcher is null)
            return inputs;
        if (stage == StageIds.CalledPitch && catcher.ReceivingFraming is { } framing)
            return new List<ModifierInput>(inputs ?? []) { ModifierInput.Linear("catcher.framing", framing) };
        if (stage == StageIds.ActualLocation && catcher.GameCalling is { } gameCalling)
            return new List<ModifierInput>(inputs ?? []) { ModifierInput.Linear("catcher.game_calling", gameCalling) };
        return inputs;
    }

    private static Result<FrozenCatcherInput> Fail(string code, string message) => Result<FrozenCatcherInput>.Failure(new Error(code, message));

    public override string ToString() => $"FrozenCatcherInput({Catcher})";
}
