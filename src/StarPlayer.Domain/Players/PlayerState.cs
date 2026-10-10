using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Players;

/// <summary>
/// Transient state (C03): condition, fatigue, sharpness, stress, satisfaction and recent form, each 0-100.
/// Never base skill; a short slump changes this, not <see cref="Ratings"/>. Transitions arrive with Phases 2/7.
/// </summary>
public sealed record PlayerState(SkillValue Condition, SkillValue Fatigue, SkillValue Sharpness, SkillValue Stress, SkillValue Satisfaction, SkillValue RecentForm)
{
    public IReadOnlyList<(StateKey Key, SkillValue Value)> Values() =>
        [(StateKey.Condition, Condition), (StateKey.Fatigue, Fatigue), (StateKey.Sharpness, Sharpness), (StateKey.Stress, Stress), (StateKey.Satisfaction, Satisfaction), (StateKey.RecentForm, RecentForm)];
}

public enum StateKey { Condition, Fatigue, Sharpness, Stress, Satisfaction, RecentForm }
