using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;

namespace StarPlayer.Application.Projections;

/// <summary>Korean player-facing text for the game diagnostic screen (English enum names are technical notation only).</summary>
internal static class GameText
{
    public static string Side(TeamSide side) => side == TeamSide.Away ? "원정" : "홈";

    public static string Position(FieldPosition position) => position switch
    {
        FieldPosition.Pitcher => "투수", FieldPosition.Catcher => "포수", FieldPosition.FirstBase => "1루수", FieldPosition.SecondBase => "2루수",
        FieldPosition.ThirdBase => "3루수", FieldPosition.Shortstop => "유격수", FieldPosition.LeftField => "좌익수", FieldPosition.CenterField => "중견수",
        FieldPosition.RightField => "우익수", FieldPosition.DesignatedHitter => "지명타자", _ => throw new ArgumentOutOfRangeException(nameof(position)),
    };

    public static string Status(GameRulesStatus status) => status switch
    {
        GameRulesStatus.KboOrientedUnverified => "KBO 지향 기준(미인증)",
        GameRulesStatus.FictionalTest => "가상 테스트 규칙",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static string Rules(GameRuleSnapshot rules) =>
        $"{rules.ProfileId} · 지명타자 {(rules.DesignatedHitter ? "있음" : "없음")} · 연장 {(rules.ExtraInningsLimit is { } limit ? $"{limit}회까지, 이후 무승부" : "한도 없음")}";

    public static string Stage(PaStage stage) => stage switch
    {
        PaStage.Start => "타석 시작 (접근 방식 선택 또는 Take)",
        PaStage.Decision => "Take 후 결정 대기 (볼카운트 유지)",
        PaStage.InSequence => "투구 진행 중",
        _ => throw new ArgumentOutOfRangeException(nameof(stage)),
    };

    public static string Action(GameActionKind kind) => kind switch
    {
        GameActionKind.Approach => "접근 방식 선택", GameActionKind.Take => "Take (투구 하나만 지켜봄)", GameActionKind.Pitch => "투구 결과",
        GameActionKind.InPlay => "타구 결과", GameActionKind.RunnerPlay => "주자 플레이", GameActionKind.Substitution => "선수 교체",
        GameActionKind.Finalize => "경기 확정", _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static string Decision(DecisionKind kind) => kind switch { DecisionKind.Win => "승", DecisionKind.Loss => "패", DecisionKind.Save => "세", _ => "" };

    private static string Pitch(PitchOutcome outcome) => outcome switch
    {
        PitchOutcome.Ball => "볼", PitchOutcome.CalledStrike => "스트라이크(루킹)", PitchOutcome.SwingingStrike => "스트라이크(헛스윙)", PitchOutcome.Foul => "파울",
        PitchOutcome.FoulBunt => "번트 파울", PitchOutcome.HitByPitch => "몸에 맞는 공", _ => "",
    };

    private static string Result(InPlayResult result, BattedBallType batted) => result switch
    {
        InPlayResult.Single => "안타", InPlayResult.Double => "2루타", InPlayResult.Triple => "3루타", InPlayResult.HomeRun => "홈런",
        InPlayResult.FieldersChoice => "야수 선택", InPlayResult.ReachedOnError => "실책 출루",
        _ => batted switch { BattedBallType.Ground => "땅볼 아웃", BattedBallType.Fly => "뜬공 아웃", BattedBallType.Line => "직선타 아웃", BattedBallType.Pop => "팝플라이 아웃", _ => "번트 아웃" },
    };

    /// <summary>One Korean line describing a scripted event, for the "last/next outcome" display.</summary>
    public static string Describe(GameEvent e) => e switch
    {
        ApproachEvent a => $"타석 접근 방식 선택: {a.Approach switch { ApproachKind.Patient => "신중하게", ApproachKind.Aggressive => "공격적으로", _ => "보통" }}",
        PitchEvent p => "투구: " + PitchText(p.Spec),
        TakeEvent t => "Take(투구 하나만 지켜봄): " + PitchText(t.Spec),
        InPlayEvent i => "타구: " + Result(i.Result, i.Batted) + (i.Sacrifice ? " (희생번트)" : ""),
        RunnerPlayEvent r => "주자 플레이: " + r.Kind switch { RunnerPlayKind.Steal => "도루 시도", RunnerPlayKind.Pickoff => "견제", RunnerPlayKind.Balk => "보크", _ => "기타" },
        SubstitutionEvent s => $"{Side(s.Team)} 선수 교체: " + s.Kind switch
        {
            SubstitutionKind.PinchHit => $"대타 {s.Player}", SubstitutionKind.PinchRun => $"대주자 {s.Player}", SubstitutionKind.DefensiveSub => $"수비 교체 {s.Player}",
            SubstitutionKind.PositionSwap => "수비 위치 맞교환", SubstitutionKind.DhToField => "지명타자가 수비로 이동(지명타자 소멸)",
            SubstitutionKind.NameDesignatedHitter => "지명타자 지정 요청", _ => $"투수 교체 {s.Player}",
        },
        _ => "알 수 없는 사건",
    };

    private static string PitchText(PitchSpec spec) =>
        Pitch(spec.Outcome) + (spec.Mishap == PitchMishap.WildPitch ? " + 폭투" : spec.Mishap == PitchMishap.PassedBall ? " + 포일" : "")
        + (spec.DroppedThirdStrike == PitchMishap.None ? "" : " + 낫아웃");
}
