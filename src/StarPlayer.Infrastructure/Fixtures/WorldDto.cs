namespace StarPlayer.Infrastructure.Fixtures;

// JSON schema v1 for the Phase 1 fictional pack (documented in docs/data-model.md). Plain data only: every value is
// validated again by the Domain factories when mapped, so a hand-edited file cannot create an illegal object.
// Property names are snake_case through the serializer options. Unknown properties are rejected (typos must not pass).
// Format versions are separate strict DTOs: WorldDto is v1 and never gains a field; WorldV2Dto (Phase 3-D) adds the optional hidden batter-tendency collection. A v1 file that
// carries the v2 field is therefore an unmapped member and is rejected by the unchanged shared options.

internal class WorldDto
{
    public required int FormatVersion { get; set; }
    public required string Label { get; set; }
    public required DateOnly AsOfDate { get; set; }
    public required string ControlledPlayerId { get; set; }
    public required RulesDto Rules { get; set; }
    public required BoundsDto ParkFactorBounds { get; set; }
    public required JerseyRulesDto Jersey { get; set; }
    public required List<ClubDto> Clubs { get; set; }
    public required List<StadiumDto> Stadiums { get; set; }
    public required List<ParkFactorProfileDto> ParkFactorProfiles { get; set; }
    public required List<StadiumUsageDto> StadiumUsage { get; set; }
    public required List<PlayerDto> Players { get; set; }
    public required List<DevelopmentDto> DevelopmentProfiles { get; set; }
    public required List<JerseyAssignmentDto> JerseyAssignments { get; set; }
    public required List<BatteryPairDto> BatteryPairs { get; set; }
}

/// <summary>
/// JSON schema v2: everything of v1 plus the OPTIONAL <c>batter_zone_tendencies</c> list (hidden, sparse, source order). Omitted and an empty array are different file shapes and both
/// are kept; an explicit JSON null is refused by the reader. The attribute (not the shared options) omits the member when the world had none.
/// </summary>
internal sealed class WorldV2Dto : WorldDto
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<BatterZoneTendencyDto?>? BatterZoneTendencies { get; set; }

    public WorldV2Dto()
    {
    }

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public WorldV2Dto(WorldDto v1)
    {
        FormatVersion = 2; Label = v1.Label; AsOfDate = v1.AsOfDate; ControlledPlayerId = v1.ControlledPlayerId; Rules = v1.Rules; ParkFactorBounds = v1.ParkFactorBounds;
        Jersey = v1.Jersey; Clubs = v1.Clubs; Stadiums = v1.Stadiums; ParkFactorProfiles = v1.ParkFactorProfiles; StadiumUsage = v1.StadiumUsage; Players = v1.Players;
        DevelopmentProfiles = v1.DevelopmentProfiles; JerseyAssignments = v1.JerseyAssignments; BatteryPairs = v1.BatteryPairs;
    }
}

/// <summary>One hidden batter tendency row of a v2 pack. Every field is required; <c>values</c> are the nine inner-cell integers in row-major order.</summary>
internal sealed class BatterZoneTendencyDto
{
    public required string PlayerId { get; set; }
    public required string ZoneModel { get; set; }
    public required string ReferenceId { get; set; }
    public required List<int> Values { get; set; }
}

internal sealed class RulesDto
{
    public required List<RuleAreaDefinitionDto> Catalog { get; set; }
    public required RuleProfileDto Profile { get; set; }
}

internal sealed class RuleAreaDefinitionDto
{
    public required string Area { get; set; }
    public required string Version { get; set; }
    public required DateOnly EffectiveFrom { get; set; }
    public required DateOnly? EffectiveTo { get; set; }
    public required string Provenance { get; set; }
    public required List<SimplificationDto> Simplifications { get; set; }
}

internal sealed class SimplificationDto
{
    public required string Id { get; set; }
    public required string Description { get; set; }
}

internal sealed class RuleProfileDto
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required DateOnly EffectiveOn { get; set; }
    public required List<RuleAreaRefDto> Areas { get; set; }
}

internal sealed class RuleAreaRefDto
{
    public required string Area { get; set; }
    public required string Version { get; set; }
}

internal sealed class BoundsDto
{
    public required int MinPermille { get; set; }
    public required int MaxPermille { get; set; }
}

internal sealed class JerseyRulesDto
{
    public required int Min { get; set; }
    public required int Max { get; set; }
    public required List<RetiredNumberDto> Retired { get; set; }
}

internal sealed class RetiredNumberDto
{
    public required string ClubId { get; set; }
    public required int Number { get; set; }
    public required DateOnly EffectiveFrom { get; set; }
}

internal sealed class ClubDto
{
    public required string ClubId { get; set; }
    public required string Name { get; set; }
    public required string ShortName { get; set; }
}

internal sealed class StadiumDto
{
    public required string StadiumId { get; set; }
    public required string DisplayName { get; set; }
    public required string City { get; set; }
    public required int? Capacity { get; set; }
}

internal sealed class ParkFactorProfileDto
{
    public required string ProfileId { get; set; }
    public required string StadiumId { get; set; }
    public required int Version { get; set; }
    public required DateOnly EffectiveFrom { get; set; }
    public required int HomeRunPermille { get; set; }
    public required int HitPermille { get; set; }
    public required int ExtraBaseHitPermille { get; set; }
}

internal sealed class StadiumUsageDto
{
    public required string StadiumId { get; set; }
    public required string ClubId { get; set; }
    public required DateOnly From { get; set; }
    public required DateOnly? To { get; set; }
}

internal sealed class PlayerDto
{
    public required string PlayerId { get; set; }
    public required string DisplayName { get; set; }
    public required DateOnly BirthDate { get; set; }
    public required string Throws { get; set; }
    public required string Bats { get; set; }
    public required int HeightCm { get; set; }
    public required int WeightKg { get; set; }
    public required List<string> Personality { get; set; }
    public required string Biography { get; set; }
    public required RatingsDto Ratings { get; set; }
    public required StateDto State { get; set; }
    public required ProficiencyDto Proficiency { get; set; }
    public required StatusDto Status { get; set; }
}

internal sealed class RatingsDto
{
    public required HitterDto? Hitter { get; set; }
    public required PitcherDto? Pitcher { get; set; }
    public required CatcherDto? Catcher { get; set; }
}

internal sealed class HitterDto
{
    public required int Contact { get; set; }
    public required int Power { get; set; }
    public required int Eye { get; set; }
    public required int Speed { get; set; }
    public required int Fielding { get; set; }
    public required int Arm { get; set; }
}

internal sealed class PitcherDto
{
    public required int Stuff { get; set; }
    public required int Control { get; set; }
    public required int Velocity { get; set; }
    public required int Endurance { get; set; }
    public required List<PitchDto> Pitches { get; set; }
}

internal sealed class PitchDto
{
    public required string Type { get; set; }
    public required int Grade { get; set; }
}

internal sealed class CatcherDto
{
    public required int ReceivingFraming { get; set; }
    public required int Blocking { get; set; }
    public required int PopTime { get; set; }
    public required int GameCalling { get; set; }
}

internal sealed class StateDto
{
    public required int Condition { get; set; }
    public required int Fatigue { get; set; }
    public required int Sharpness { get; set; }
    public required int Stress { get; set; }
    public required int Satisfaction { get; set; }
    public required int RecentForm { get; set; }
}

internal sealed class ProficiencyDto
{
    public required string PreferredPosition { get; set; }
    public required List<PositionProficiencyDto> Positions { get; set; }
    public required List<RoleProficiencyDto> Roles { get; set; }
}

internal sealed class PositionProficiencyDto
{
    public required string Position { get; set; }
    public required int Value { get; set; }
}

internal sealed class RoleProficiencyDto
{
    public required string Role { get; set; }
    public required int Value { get; set; }
}

internal sealed class StatusDto
{
    public required List<OriginDto> Origins { get; set; }
    public required string? EntryPath { get; set; }
    public required string Registration { get; set; }
    public required string? League { get; set; }
    public required List<AffiliationDto> Affiliations { get; set; }
    public required string Career { get; set; }
}

internal sealed class OriginDto
{
    public required string Type { get; set; }
    public required string? OriginTeamId { get; set; }
    public required DateOnly? From { get; set; }
    public required DateOnly? To { get; set; }
}

internal sealed class AffiliationDto
{
    public required string ClubId { get; set; }
    public required DateOnly From { get; set; }
    public required DateOnly? To { get; set; }
}

/// <summary>Hidden development truth. Lives in its own top-level list, never inside a player record.</summary>
internal sealed class DevelopmentDto
{
    public required string PlayerId { get; set; }
    public required int TruePotential { get; set; }
    public required string GrowthType { get; set; }
    public required int PrimeYears { get; set; }
    public required int DeclineOnsetAge { get; set; }
    public required List<SkillCeilingDto> SkillCeilings { get; set; }
}

internal sealed class SkillCeilingDto
{
    public required string Skill { get; set; }
    public required int Ceiling { get; set; }
}

internal sealed class JerseyAssignmentDto
{
    public required string PlayerId { get; set; }
    public required string ClubId { get; set; }
    public required int Number { get; set; }
    public required DateOnly EffectiveFrom { get; set; }
    public required DateOnly? EffectiveTo { get; set; }
    public required string Reason { get; set; }
}

internal sealed class BatteryPairDto
{
    public required string CatcherId { get; set; }
    public required string PitcherId { get; set; }
    public required int SharedAppearances { get; set; }
    public required DateOnly? LastBatteryDate { get; set; }
}
