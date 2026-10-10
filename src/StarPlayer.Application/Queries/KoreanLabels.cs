using StarPlayer.Domain.Players;

namespace StarPlayer.Application.Queries;

/// <summary>
/// Korean player-facing names for Domain vocabulary (English enum names are technical notation only). An enum member
/// without a label throws; a test walks every member of every enum so the gap shows up before a screen does.
/// </summary>
internal static class KoreanLabels
{
    public static string Skill(SkillKey key) => key switch
    {
        SkillKey.Contact => "컨택",
        SkillKey.Power => "파워",
        SkillKey.Eye => "선구안",
        SkillKey.Speed => "주루",
        SkillKey.Fielding => "수비",
        SkillKey.Arm => "송구",
        SkillKey.Stuff => "구위",
        SkillKey.Control => "제구",
        SkillKey.Velocity => "구속",
        SkillKey.Endurance => "체력",
        SkillKey.ReceivingFraming => "포구·프레이밍",
        SkillKey.Blocking => "블로킹",
        SkillKey.PopTime => "팝타임",
        SkillKey.GameCalling => "투수 리드",
        _ => throw new ArgumentOutOfRangeException(nameof(key)),
    };

    public static string SkillGroup(SkillKey key) => key switch
    {
        SkillKey.Contact or SkillKey.Power or SkillKey.Eye or SkillKey.Speed or SkillKey.Fielding or SkillKey.Arm => "타자 능력",
        SkillKey.Stuff or SkillKey.Control or SkillKey.Velocity or SkillKey.Endurance => "투수 능력",
        _ => "포수 능력",
    };

    public static string Pitch(PitchType type) => type switch
    {
        PitchType.Fastball => "패스트볼",
        PitchType.Slider => "슬라이더",
        PitchType.Curve => "커브",
        PitchType.Changeup => "체인지업",
        PitchType.Forkball => "포크",
        PitchType.Sinker => "싱커",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static string State(StateKey key) => key switch
    {
        StateKey.Condition => "컨디션",
        StateKey.Fatigue => "피로도",
        StateKey.Sharpness => "경기 감각",
        StateKey.Stress => "스트레스",
        StateKey.Satisfaction => "만족도",
        StateKey.RecentForm => "최근 흐름",
        _ => throw new ArgumentOutOfRangeException(nameof(key)),
    };

    public static string Position(FieldPosition position) => position switch
    {
        FieldPosition.Pitcher => "투수",
        FieldPosition.Catcher => "포수",
        FieldPosition.FirstBase => "1루수",
        FieldPosition.SecondBase => "2루수",
        FieldPosition.ThirdBase => "3루수",
        FieldPosition.Shortstop => "유격수",
        FieldPosition.LeftField => "좌익수",
        FieldPosition.CenterField => "중견수",
        FieldPosition.RightField => "우익수",
        FieldPosition.DesignatedHitter => "지명타자",
        _ => throw new ArgumentOutOfRangeException(nameof(position)),
    };

    public static string Role(PlayerRole role) => role switch
    {
        PlayerRole.Batter => "타자 역할",
        PlayerRole.StartingPitcher => "선발 투수 역할",
        PlayerRole.ReliefPitcher => "구원 투수 역할",
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    public static string Kind(PlayerKind kind) => kind switch
    {
        PlayerKind.Hitter => "타자",
        PlayerKind.Pitcher => "투수",
        PlayerKind.TwoWay => "투타 겸업",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static string Handedness(ThrowingHand throws, BattingSide bats) =>
        (throws switch { ThrowingHand.Right => "우투", ThrowingHand.Left => "좌투", _ => throw new ArgumentOutOfRangeException(nameof(throws)) })
        + (bats switch { BattingSide.Right => "우타", BattingSide.Left => "좌타", BattingSide.Switch => "양타", _ => throw new ArgumentOutOfRangeException(nameof(bats)) });

    public static string Personality(PersonalityTrait trait) => trait switch
    {
        PersonalityTrait.Diligent => "성실함",
        PersonalityTrait.Ambitious => "야심가",
        PersonalityTrait.Competitive => "승부욕 강함",
        PersonalityTrait.Sociable => "사교적",
        PersonalityTrait.Introverted => "내향적",
        PersonalityTrait.Flashy => "화려함 추구",
        PersonalityTrait.Frugal => "절약형",
        PersonalityTrait.BaseballObsessed => "야구광",
        PersonalityTrait.FreeSpirited => "자유분방함",
        _ => throw new ArgumentOutOfRangeException(nameof(trait)),
    };

    public static string Origin(OriginType type) => type switch
    {
        OriginType.HighSchool => "고교",
        OriginType.University => "대학",
        OriginType.Overseas => "해외파",
        OriginType.OtherProspect => "기타 프로 진출 대상",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static string Entry(EntryPath? path) => path switch
    {
        null => "해당 없음",
        EntryPath.Draft => "드래프트 지명",
        EntryPath.DevelopmentContract => "육성선수 계약",
        EntryPath.Other => "기타 진입",
        _ => throw new ArgumentOutOfRangeException(nameof(path)),
    };

    public static string Registration(RegistrationStatus status) => status switch
    {
        RegistrationStatus.Amateur => "아마추어",
        RegistrationStatus.DraftCandidate => "드래프트 후보",
        RegistrationStatus.Development => "육성선수",
        RegistrationStatus.Registered => "정식선수",
        RegistrationStatus.Released => "방출",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static string League(LeagueAssignment? league) => league switch
    {
        null => "소속 없음",
        LeagueAssignment.FirstTeam => "1군",
        LeagueAssignment.SecondTeam => "2군",
        _ => throw new ArgumentOutOfRangeException(nameof(league)),
    };

    public static string Career(CareerStatus status) => status switch
    {
        CareerStatus.Active => "현역",
        CareerStatus.Retired => "은퇴",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };
}
