using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;

namespace StarPlayer.Simulation.Baseball;

// Phase 3-D (ability modifier wiring, ADR-006 item 8 / ADR-007 "Decisions (3-D ability modifiers)"): turns the CURRENT base ratings of the two players and the
// strategy/count/handedness context of one pitch into the ModifierInputs of the nine live slots. It draws nothing, owns no probability and changes no rating.

/// <summary>
/// The six base ratings (0-100) the pitch resolver reads. Decision D-ABILITY-1 (temporary, Phase 3 only): the effective ability IS the base rating in the current
/// <see cref="PlayerSheet"/>; condition, fatigue and the other <see cref="PlayerState"/> values are not applied, and no <c>EvaluateEffectiveAbility</c> formula is invented.
/// This record is the replaceable boundary: the Phase 7 effective-ability calculation will fill it instead of <see cref="From"/>.
/// </summary>
public sealed record PitchAbilities(int BatterContact, int BatterPower, int BatterEye, int PitcherControl, int PitcherStuff, int PitcherVelocity)
{
    /// <summary>Reads the current base ratings of the two sheets. A missing hitting or pitching rating set is an error: nothing is defaulted to 50.</summary>
    public static Result<PitchAbilities> From(PlayerSheet? batter, PlayerSheet? pitcher)
    {
        if (batter?.Ratings.Hitter is not { } hit)
            return Result<PitchAbilities>.Failure(new Error("PitchAbilities.batter_hitting_missing", "타석의 선수에게 타자 능력치가 없어 투구 보정을 만들 수 없습니다."));
        if (pitcher?.Ratings.Pitcher is not { } pit)
            return Result<PitchAbilities>.Failure(new Error("PitchAbilities.pitcher_pitching_missing", "마운드의 선수에게 투수 능력치가 없어 투구 보정을 만들 수 없습니다."));
        return Result<PitchAbilities>.Success(new PitchAbilities(
            hit.Contact.Value, hit.Power.Value, hit.Eye.Value, pit.Control.Value, pit.Stuff.Value, pit.Velocity.Value));
    }

    internal bool InRange() =>
        new[] { BatterContact, BatterPower, BatterEye, PitcherControl, PitcherStuff, PitcherVelocity }.All(v => v is >= SkillValue.Min and <= SkillValue.Max);
}

/// <summary>
/// Which <see cref="ModifierInput"/>s a stage receives. Exactly nine slots are connected: <c>pitcher.control</c> (actual_location); <c>batter.eye</c>,
/// <c>strategy.approach</c>, <c>situation.count_pressure</c> (batter_decision); <c>pitcher.stuff</c>, <c>batter.contact</c>, <c>matchup.handedness</c> (swing_result);
/// <c>pitcher.velocity</c>, <c>batter.power</c> (batted_ball). Every other stage and slot (fielding, runner, arm, park, catcher, battery) gets nothing, so each ability feeds one
/// slot in one stage and nothing is applied twice.
/// </summary>
internal static class PitchModifierInputs
{
    public static IReadOnlyList<ModifierInput> For(
        string stage, PitchAbilities abilities, PitchCountState count, ApproachKind? approach, AppliedBatterSide batterSide, ThrowingHand pitcherHand) => stage switch
    {
        StageIds.ActualLocation => ForActualLocation(abilities.PitcherControl),
        StageIds.BatterDecision =>
        [
            ModifierInput.Linear("batter.eye", abilities.BatterEye),
            ModifierInput.Choice("strategy.approach", Approach(approach)),
            ModifierInput.Choice("situation.count_pressure", CountPressure(count)),
        ],
        StageIds.SwingResult =>
        [
            ModifierInput.Linear("pitcher.stuff", abilities.PitcherStuff),
            ModifierInput.Linear("batter.contact", abilities.BatterContact),
            ModifierInput.Choice("matchup.handedness", Handedness(batterSide, pitcherHand)),
        ],
        StageIds.BattedBall =>
        [
            ModifierInput.Linear("pitcher.velocity", abilities.PitcherVelocity),
            ModifierInput.Linear("batter.power", abilities.BatterPower),
        ],
        _ => [],
    };

    /// <summary>
    /// The one place <c>pitcher.control</c> is connected: the <c>actual_location</c> inputs for a given base Control. The resolver reaches it through <see cref="For"/>; the commit evidence (Game Calling v4) calls it with the
    /// Control it re-derived from the pinned player input set, so the expected distribution is built by the very same code and the slot still has a single connection.
    /// </summary>
    internal static IReadOnlyList<ModifierInput> ForActualLocation(int pitcherControl) => [ModifierInput.Linear("pitcher.control", pitcherControl)];

    /// <summary>The chosen plate-appearance approach; before one is chosen (start of a plate appearance, or after an explicit Take) the pitch is neutral.</summary>
    public static string Approach(ApproachKind? approach) => approach switch
    {
        ApproachKind.Patient => "patient",
        ApproachKind.Aggressive => "aggressive",
        _ => "neutral", // Normal, or no approach chosen
    };

    /// <summary>
    /// Decision D-ABILITY-2 (temporary conflict rule, not a final balance decision): one state at most. 3 balls with fewer than 2 strikes is <c>three_balls</c>; 2 strikes with
    /// fewer than 3 balls is <c>two_strikes</c>; the full count 3-2 and every other count are <c>neutral</c>. The count is the one that exists before the pitch.
    /// </summary>
    public static string CountPressure(PitchCountState count) => (count.Balls, count.Strikes) switch
    {
        (3, < 2) => "three_balls",
        ( < 3, 2) => "two_strikes",
        _ => "neutral",
    };

    /// <summary>A batter standing on the same side as the pitcher throws is a same-handed matchup (favours the pitcher); the opposite side favours the batter. Uses the applied side.</summary>
    public static string Handedness(AppliedBatterSide side, ThrowingHand hand) =>
        (side == AppliedBatterSide.Right) == (hand == ThrowingHand.Right) ? "same_hand" : "opposite_hand";
}
