using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;

namespace StarPlayer.Simulation.Baseball;

// Phase 3-E Package B (owner decisions E1-E7, Codex PASS_WITH_NOTES): the catcher input of one pitch. Like PitchFieldingInputs it is read from the game state and an immutable
// PlayerInputSet BEFORE the first random number, copied into plain values and frozen. It is resolver-internal: the exact rating of an NPC never reaches a client.

/// <summary>
/// The catcher who is behind the plate for one pitch and his base <c>ReceivingFraming</c> (0-100), frozen when the pitch is prepared. It exists only when the active model
/// has an active Framing slot AND the game's rules call pitches by hand (<see cref="Required"/>); under <c>prob-model-v1/v2</c> and under automated calling nothing is read and nothing is
/// forced. It holds the game id and the identity of the <see cref="PlayerInputSet"/> it came from so it cannot be moved to another game or set, and it keeps no sheet, dictionary or delegate.
/// The catcher is NEVER taken from a stored audit or from the prepared pitch when something is verified: the expected value is always re-derived from the real state and the real set.
/// </summary>
public sealed class FrozenCatcherInput
{
    internal FrozenCatcherInput(string gameId, string inputSetId, PlayerId catcher, int receivingFraming)
    {
        GameId = gameId; InputSetId = inputSetId; Catcher = catcher; ReceivingFraming = receivingFraming;
    }

    public string GameId { get; }

    /// <summary>The identity of the content-version-2 set the rating was read from.</summary>
    public string InputSetId { get; }

    /// <summary>The player standing at catcher for the fielding team when the pitch was prepared.</summary>
    public PlayerId Catcher { get; }

    /// <summary>His base <c>ReceivingFraming</c>, 0-100.</summary>
    public int ReceivingFraming { get; }

    /// <summary>
    /// Whether a pitch of this model and these rules needs a catcher input: the model's Framing slot is active (<c>prob-model-v3</c>) and the rules declare
    /// <see cref="CalledPitchMode.NonAutomated"/>. An automated or undeclared profile never needs one, so a game without Framing ratings stays valid there. Clear and Borderline pitches are treated
    /// alike - the input is required for the pitch, the model's scope decides where it acts.
    /// </summary>
    internal static bool Required(ProbabilityModel? model, GameRuleSnapshot? rules) =>
        model is { FramingActive: true } && rules is { CalledPitchMode: CalledPitchMode.NonAutomated };

    /// <summary>
    /// Reads the CURRENT catcher of the FIELDING team from <paramref name="state"/> and his rating from <paramref name="set"/>. The set must be content version 2; a missing catcher, a catcher who is not in the
    /// set and a catcher without catcher ratings are errors (codes <c>{prefix}.catcher_missing</c>, <c>catcher_input_missing</c>, <c>catcher_ratings_missing</c>, <c>catcher_input_format</c>); nothing is defaulted to 50.
    /// </summary>
    internal static Result<FrozenCatcherInput> Build(GameState state, PlayerInputSet set, string prefix)
    {
        if (set.ContentVersion != PlayerInputSet.ContentV2)
            return Fail(prefix + ".catcher_input_format", $"포수 Framing을 읽으려면 선수 입력 집합이 콘텐츠 형식 '{PlayerInputSet.ContentV2}'이어야 합니다(받은 형식: '{set.ContentVersion}').");
        if (state.DefenderAt(state.FieldingSide, FieldPosition.Catcher) is not { } catcher)
            return Fail(prefix + ".catcher_missing", "수비 위치 Catcher에 선수가 없어 포수 입력을 고정할 수 없습니다.");
        if (set.Find(catcher) is not { } input)
            return Fail(prefix + ".catcher_input_missing", $"포수 {catcher}가 선수 입력 집합에 없습니다.");
        if (input.ReceivingFraming is not { } framing)
            return Fail(prefix + ".catcher_ratings_missing", $"포수 {catcher}에게 포수 능력치(ReceivingFraming)가 없어 Framing 보정을 만들 수 없습니다.");
        if (framing is < SkillValue.Min or > SkillValue.Max)
            return Fail(prefix + ".catcher_rating_invalid", $"포수 {catcher}의 ReceivingFraming 값이 0~100 범위가 아닙니다.");
        return Result<FrozenCatcherInput>.Success(new FrozenCatcherInput(state.GameId, set.Id, catcher, framing));
    }

    /// <summary>
    /// <paramref name="inputs"/> (the stage's inputs, or null) extended, for <c>called_pitch</c> only and only when a catcher input exists, with <c>catcher.framing</c>. Every called-pitch condition
    /// receives it: the MODEL's condition scope decides where it acts, the resolver does not filter.
    /// </summary>
    internal static IReadOnlyList<ModifierInput>? Extend(IReadOnlyList<ModifierInput>? inputs, string stage, FrozenCatcherInput? catcher)
    {
        if (stage != StageIds.CalledPitch || catcher is null)
            return inputs;
        var list = new List<ModifierInput>(inputs ?? []) { ModifierInput.Linear("catcher.framing", catcher.ReceivingFraming) };
        return list;
    }

    private static Result<FrozenCatcherInput> Fail(string code, string message) => Result<FrozenCatcherInput>.Failure(new Error(code, message));

    public override string ToString() => $"FrozenCatcherInput({Catcher})";
}
