using System.Text;

namespace StarPlayer.Application.Projections;

/// <summary>Plain Korean text for the diagnostic screen and its unattended dump, built only from <see cref="GameView"/>.</summary>
public static class GameViewFormatter
{
    public static string Summary(GameView v)
    {
        var text = new StringBuilder();
        text.AppendLine($"{v.Title}");
        text.AppendLine($"규칙: {v.RulesLabel} [{v.RulesStatusLabel}]");
        text.AppendLine($"{v.InningLabel} · 볼 {v.Balls} 스트라이크 {v.Strikes} 아웃 {v.Outs}");
        text.AppendLine($"점수 원정 {v.AwayRuns} : 홈 {v.HomeRuns} · 안타 {v.AwayHits}-{v.HomeHits} · 실책 {v.AwayErrors}-{v.HomeErrors}");
        text.AppendLine($"주자 1루 {Or(v.FirstRunner)} / 2루 {Or(v.SecondRunner)} / 3루 {Or(v.ThirdRunner)}");
        text.AppendLine($"타자 {Or(v.CurrentBatter)} · 투수 {Or(v.CurrentPitcher)}");
        text.AppendLine($"상태: {v.StageLabel}");
        text.AppendLine($"가능한 행동: {string.Join(", ", v.LegalActions)}");
        text.AppendLine($"방금 진행: {Or(v.LastEventText)}");
        text.AppendLine($"다음 사건: {Or(v.NextEventText)}");
        text.AppendLine($"진행 {v.EventsApplied}/{v.EventsTotal}" + (v.IsOver ? $" · {v.ResultText}" : ""));
        return text.ToString().ReplaceLineEndings("\n");
    }

    public static string LineScore(GameView v)
    {
        var text = new StringBuilder();
        text.AppendLine("      " + string.Join(" ", v.InningHeaders.Select(h => h.PadLeft(2))) + "   R  H  E");
        text.AppendLine("원정  " + string.Join(" ", v.AwayLine.Select(c => c.PadLeft(2))) + $" {v.AwayRuns,3}{v.AwayHits,3}{v.AwayErrors,3}");
        text.AppendLine("홈    " + string.Join(" ", v.HomeLine.Select(c => c.PadLeft(2))) + $" {v.HomeRuns,3}{v.HomeHits,3}{v.HomeErrors,3}");
        return text.ToString().ReplaceLineEndings("\n");
    }

    public static string Box(GameView v)
    {
        if (v.BoxScore is not { } box)
            return "경기가 끝나면 박스스코어가 표시됩니다.";
        var text = new StringBuilder();
        text.AppendLine("■ 박스스코어 (확정된 공식 기록)");
        text.AppendLine(box.AwayTotals);
        text.AppendLine(box.HomeTotals);
        text.AppendLine("투수 결정: " + (box.Decisions.Count == 0 ? "없음(무승부)" : string.Join(" · ", box.Decisions)));
        text.AppendLine();
        text.AppendLine("[타자]  팀 선수 타석 타수 득점 안타 타점 볼넷 삼진 홈런 도루");
        foreach (var b in box.Batters)
            text.AppendLine($"  {b.Team} {b.Player}  {b.PlateAppearances} {b.AtBats} {b.Runs} {b.Hits} {b.RunsBattedIn} {b.Walks} {b.Strikeouts} {b.HomeRuns} {b.StolenBases}");
        text.AppendLine();
        text.AppendLine("[투수]  팀 선수 이닝 피안타 실점 자책 볼넷 삼진 피홈런 폭투 투구수 결정");
        foreach (var p in box.Pitchers)
            text.AppendLine($"  {p.Team} {p.Player}  {p.InningsPitched} {p.Hits} {p.Runs} {p.EarnedRuns} {p.Walks} {p.Strikeouts} {p.HomeRuns} {p.WildPitches} {p.Pitches} {p.Decision}");
        text.AppendLine();
        text.AppendLine("[포수 실제 출전]  팀 선수 받은 공 아웃 도루허용 도루저지 포일");
        foreach (var c in box.Catchers)
            text.AppendLine($"  {c.Team} {c.Player}  {c.PitchesReceived} {c.OutsWhileCatching} {c.StolenBasesAllowed} {c.CaughtStealing} {c.PassedBalls}");
        text.AppendLine();
        text.AppendLine("[배터리 조합]  팀 포수/투수 투구 아웃 상대타자");
        foreach (var b in box.Batteries)
            text.AppendLine($"  {b.Team} {b.Catcher}/{b.Pitcher}  {b.Pitches} {b.Outs} {b.BattersFaced}");
        return text.ToString().ReplaceLineEndings("\n");
    }

    private static string Or(string text) => text.Length == 0 ? "-" : text;
}
