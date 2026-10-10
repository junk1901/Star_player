using System.Globalization;
using StarPlayer.Application.Sessions;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Domain.Teams;

namespace StarPlayer.Application.Queries;

/// <summary>Per-player facts the policy needs that are not part of <see cref="PlayerSheet"/>: club, current number and relation to the viewer.</summary>
internal sealed record PlayerProjectionContext(DateOnly AsOf, Club? Club, JerseyNumberAssignment? Jersey, bool IsViewerClubmate);

/// <summary>
/// The only place that turns a <see cref="PlayerSheet"/> into a <see cref="PlayerPublicView"/>.
/// <list type="bullet">
/// <item>The viewer's own player: exact current values.</item>
/// <item>Anyone else: grade + range + confidence from <see cref="ObservationPolicy"/>; the exact number never leaves.</item>
/// <item>Hidden development data (potential, growth type, prime, decline, ceilings) is not in <see cref="PlayerSheet"/>, so it cannot be projected for anyone, including in cheat mode.</item>
/// </list>
/// The decision uses only the session's <see cref="ViewerContext"/> and the target id; nothing in the request can change it.
/// </summary>
internal static class PlayerVisibilityPolicy
{
    public static PlayerPublicView Project(PlayerSheet sheet, ViewerContext viewer, PlayerProjectionContext context)
    {
        var self = viewer.IsSelf(sheet.Id);
        var quality = context.IsViewerClubmate ? ObservationQuality.Detailed : ObservationQuality.Standard;
        var player = sheet.Player;

        var abilities = sheet.Ratings.Skills()
            .Select(s => Line(SnakeCase.From(s.Key.ToString()), KoreanLabels.Skill(s.Key), KoreanLabels.SkillGroup(s.Key), s.Value, self, quality)).ToList();
        var pitches = (sheet.Ratings.Pitcher?.Repertoire.Pitches ?? [])
            .Select(p =>
            {
                var l = Line(SnakeCase.From(p.Type.ToString()), KoreanLabels.Pitch(p.Type), "구종", p.Grade, self, quality);
                return new PitchLine(l.Key, l.Label, l.Exact, l.Grade, l.RangeLow, l.RangeHigh, l.Confidence, l.Display);
            }).ToList();
        var state = sheet.State.Values()
            .Select(s => Line(SnakeCase.From(s.Key.ToString()), KoreanLabels.State(s.Key), "현재 상태", s.Value, self, quality)).ToList();

        var proficiencies = new List<AbilityLine>();
        var shown = sheet.Proficiency.Positions.Select(p => p.Position).ToHashSet();
        if (sheet.Proficiency.PreferredPosition is not (FieldPosition.Pitcher or FieldPosition.DesignatedHitter))
            shown.Add(sheet.Proficiency.PreferredPosition);
        foreach (var position in shown.OrderBy(p => p))
            proficiencies.Add(Line("position_" + SnakeCase.From(position.ToString()), KoreanLabels.Position(position), "수비 포지션 숙련도", sheet.Proficiency.PositionProficiency(position), self, quality));
        foreach (var (role, value) in sheet.Proficiency.Roles)
            proficiencies.Add(Line("role_" + SnakeCase.From(role.ToString()), KoreanLabels.Role(role), "역할 숙련도", value, self, quality));

        var status = sheet.Status;
        return new PlayerPublicView(
            PlayerId: sheet.Id.Value,
            DisplayName: player.DisplayName,
            IsOwnPlayer: self,
            ViewKind: self ? "내 선수 · 정확한 현재 능력치" : "다른 선수 · 관측 등급과 범위",
            Age: player.AgeOn(context.AsOf).Value,
            BirthDate: player.BirthDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Kind: KoreanLabels.Kind(sheet.Kind),
            Handedness: KoreanLabels.Handedness(player.Throws, player.Bats),
            HeightCm: player.Body.HeightCm,
            WeightKg: player.Body.WeightKg,
            Personality: player.Personality.Select(KoreanLabels.Personality).ToList(),
            Biography: player.Biography,
            ClubId: context.Club?.Id.Value,
            ClubName: context.Club?.Name,
            CurrentJerseyNumber: context.Jersey?.Number.ToString(),
            JerseySince: context.Jersey?.EffectiveFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Origin: string.Join(" → ", status.Origins.Select(o => KoreanLabels.Origin(o.Type))),
            EntryPath: KoreanLabels.Entry(status.EntryPath),
            Registration: KoreanLabels.Registration(status.Registration),
            League: KoreanLabels.League(status.League),
            CareerStatus: KoreanLabels.Career(status.Career),
            PreferredPosition: KoreanLabels.Position(sheet.Proficiency.PreferredPosition),
            BaseAbilities: abilities,
            Pitches: pitches,
            CurrentState: state,
            Proficiencies: proficiencies);
    }

    private static AbilityLine Line(string key, string label, string group, SkillValue value, bool exact, ObservationQuality quality)
    {
        if (exact)
            return new AbilityLine(key, label, group, value.Value, null, null, null, null, value.Value.ToString(CultureInfo.InvariantCulture));
        var seen = ObservationPolicy.Observe(value, quality);
        return new AbilityLine(key, label, group, null, seen.Grade, seen.Low, seen.High, seen.Confidence, $"{seen.Grade} ({seen.Low}~{seen.High}) · 신뢰도 {seen.Confidence}");
    }
}
