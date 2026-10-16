using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;

namespace StarPlayer.Simulation.Baseball;

// Phase 3-E (Game Calling v4, GC-S1): the pitcher's authoritative base Control. Under prob-model-v4 the value the game uses is the one pinned in the game's PlayerInputSet (content version 3) for the player who
// is the CURRENT pitcher in the GameState - not a PlayerSheet value that someone hands in later, and not anything written in a prepared pitch or an audit. Like the catcher input it is frozen when the
// pitch is prepared, before the first random number, and re-derived from the real state and the real set whenever something is verified.

/// <summary>
/// The pitcher on the mound for one pitch and his base <c>Control</c> (0-100), frozen at preparation. It exists exactly when the active model pins Control in the player input set
/// (<see cref="Required"/>, <c>prob-model-v4</c>); <c>prob-model-v1/v2/v3</c> keep reading Control from the pitcher's sheet and have no such object. It holds the game id and the set identity so it cannot be
/// moved to another game or set. It is a record of what the pitch used, never a source of truth.
/// </summary>
public sealed class FrozenPitcherInput
{
    internal FrozenPitcherInput(string gameId, string inputSetId, PlayerId pitcher, int control)
    {
        GameId = gameId; InputSetId = inputSetId; Pitcher = pitcher; Control = control;
    }

    public string GameId { get; }

    /// <summary>The identity of the content-version-3 set the rating was read from.</summary>
    public string InputSetId { get; }

    /// <summary>The player on the mound for the fielding team when the pitch was prepared.</summary>
    public PlayerId Pitcher { get; }

    /// <summary>His base <c>Control</c>, 0-100, as pinned in the input set.</summary>
    public int Control { get; }

    /// <summary>Whether the model takes the pitcher's Control from the pinned input set instead of a sheet: exactly when Game Calling is active (<c>prob-model-v4</c>).</summary>
    internal static bool Required(ProbabilityModel? model) => model is { GameCallingActive: true };

    /// <summary>
    /// Reads the CURRENT pitcher of the FIELDING team from <paramref name="state"/> and his Control from <paramref name="set"/> (content version 3). A set of another content version, a pitcher who is not in the
    /// set and a pitcher without pitching ratings are errors (codes <c>{prefix}.pitcher_input_format</c>, <c>pitcher_input_missing</c>, <c>pitcher_ratings_missing</c>, <c>pitcher_rating_invalid</c>); nothing is defaulted to 50.
    /// </summary>
    internal static Result<FrozenPitcherInput> Build(GameState state, PlayerInputSet set, string prefix)
    {
        if (set.ContentVersion != PlayerInputSet.ContentV3)
            return Fail(prefix + ".pitcher_input_format", $"투수 Control을 읽으려면 선수 입력 집합이 콘텐츠 형식 '{PlayerInputSet.ContentV3}'이어야 합니다(받은 형식: '{set.ContentVersion}').");
        var pitcher = state.Fielding.Pitcher;
        if (set.Find(pitcher) is not { } input)
            return Fail(prefix + ".pitcher_input_missing", $"투수 {pitcher}가 선수 입력 집합에 없습니다.");
        if (input.PitcherControl is not { } control)
            return Fail(prefix + ".pitcher_ratings_missing", $"투수 {pitcher}에게 투수 능력치(Control)가 없어 Control 보정을 만들 수 없습니다.");
        if (control is < SkillValue.Min or > SkillValue.Max)
            return Fail(prefix + ".pitcher_rating_invalid", $"투수 {pitcher}의 Control 값이 0~100 범위가 아닙니다.");
        return Result<FrozenPitcherInput>.Success(new FrozenPitcherInput(state.GameId, set.Id, pitcher, control));
    }

    // There is deliberately no "Extend" here: pitcher.control keeps its ONE connection in PitchModifierInputs (a second creation site would be a double application, ModifierWiringPinTests).
    // The resolver proves, before the first draw, that the Control it feeds there equals this frozen value.

    private static Result<FrozenPitcherInput> Fail(string code, string message) => Result<FrozenPitcherInput>.Failure(new Error(code, message));

    public override string ToString() => $"FrozenPitcherInput({Pitcher})";
}
