using StarPlayer.Domain.Players;

namespace StarPlayer.Application.Queries;

internal enum ObservationQuality { Standard, Detailed }

internal readonly record struct ObservedValue(string Grade, int Low, int High, string Confidence);

/// <summary>
/// Phase 1 placeholder observation model (<see cref="Version"/>), used for every non-self value. It is a pure function of
/// the current value and the observation quality: no RNG, no clock. The result is a coarse bucket whose range always
/// contains the real value and is never zero-width (the top bucket is 90-100 / 95-100), so an observer learns a band,
/// not the number. Real scouting evaluators, disagreement and wrong estimates arrive with Phase 12.
/// </summary>
internal static class ObservationPolicy
{
    public const string Version = "observation-v0";

    public static ObservedValue Observe(SkillValue value, ObservationQuality quality)
    {
        var width = quality == ObservationQuality.Detailed ? 5 : 10;
        var bucket = Math.Min(value.Value / width, SkillValue.Max / width - 1);
        var low = bucket * width;
        var high = low + width >= SkillValue.Max ? SkillValue.Max : low + width - 1;
        return new ObservedValue(Grade((low + high) / 2), low, high, quality == ObservationQuality.Detailed ? "높음" : "보통");
    }

    private static string Grade(int midpoint) => midpoint switch
    {
        >= 90 => "S",
        >= 80 => "A",
        >= 70 => "B",
        >= 60 => "C",
        >= 50 => "D",
        >= 40 => "E",
        _ => "F",
    };
}
