using StarPlayer.Domain.Players;

namespace StarPlayer.Application.Queries;

/// <summary>
/// Last-line detector for hidden-development identifiers in text that is about to be shown or serialized (UI dumps,
/// JSON payloads). It is a guard, not the mechanism: exclusion is structural (<see cref="PlayerSheet"/> and
/// <see cref="PlayerPublicView"/> have no such members). Matching ignores case and underscores, so
/// <c>true_potential</c>, <c>TruePotential</c> and <c>truepotential</c> are the same token.
/// </summary>
public static class HiddenDataGuard
{
    private static readonly string[] Tokens =
    [
        nameof(DevelopmentProfile.TruePotential),
        nameof(DevelopmentProfile.GrowthType),
        nameof(DevelopmentProfile.PrimeYears),
        nameof(DevelopmentProfile.DeclineOnsetAge),
        nameof(DevelopmentProfile.SkillCeilings),
        nameof(DevelopmentProfile),
        "Potential",
        "Ceiling",
    ];

    public static IReadOnlyList<string> FindLeaks(string? text)
    {
        var haystack = Normalize(text ?? "");
        return Tokens.Where(token => haystack.Contains(Normalize(token), StringComparison.Ordinal)).ToList();
    }

    private static string Normalize(string value) => value.Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
}
