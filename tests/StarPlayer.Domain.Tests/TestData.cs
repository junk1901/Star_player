using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;

namespace StarPlayer.Domain.Tests;

/// <summary>Small builders so each test states only what it is about. Values are fictional.</summary>
internal static class TestData
{
    public static PlayerId P(string id) => PlayerId.Create(id).Value;
    public static ClubId C(string id) => ClubId.Create(id).Value;
    public static SkillValue S(int value) => SkillValue.Create(value).Value;
    public static DateOnly D(string iso) => DateOnly.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    public static HitterRatings Hitter(int v = 50) => new(S(v), S(v), S(v), S(v), S(v), S(v));

    public static PitcherRatings Pitcher(int v = 50) =>
        new(S(v), S(v), S(v), S(v), PitchRepertoire.Create([new PitchRating(PitchType.Fastball, S(v))]).Value);

    public static CatcherRatings Catcher(int tools) => new(S(tools), S(tools), S(tools), S(tools));

    public static PlayerState State(int v = 50) => new(S(v), S(v), S(v), S(v), S(v), S(v));

    public static Player MakePlayer(string id = "p-1", string name = "홍길동", string birth = "2000-05-17") =>
        Player.Create(P(id), name, D(birth), ThrowingHand.Right, BattingSide.Left, BodyMeasurements.Create(180, 80).Value, [], "").Value;

    public static ProficiencyProfile Proficiency(FieldPosition preferred, params (FieldPosition, int)[] positions) =>
        ProficiencyProfile.Create(preferred, positions.Select(p => (p.Item1, S(p.Item2))), []).Value;

    public static PlayerStatus ActiveStatus(string club = "club-a") =>
        PlayerStatus.Create([new OriginRecord(OriginType.HighSchool, null, null, null)], EntryPath.Draft, RegistrationStatus.Registered,
            LeagueAssignment.FirstTeam, [new ClubAffiliation(C(club), D("2020-01-01"), null)], CareerStatus.Active).Value;
}
