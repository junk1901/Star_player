using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Baseball.Game;

namespace StarPlayer.Application.Projections;

/// <summary>Turns the Domain box score into the public view and checks that its separate records reconcile with each other.</summary>
internal static class BoxScoreProjection
{
    public static BoxScoreView From(BoxScore box)
    {
        var batters = box.Players.Where(p => p.Get(StatKey.BatPA) > 0 || p.Get(StatKey.RunR) > 0 || p.Get(StatKey.RunSB) > 0)
            .Select(p => new BatterLineView(GameText.Side(p.Team), p.Player.Value, p.Get(StatKey.BatPA), p.Get(StatKey.BatAB), p.Get(StatKey.RunR), p.Get(StatKey.BatH),
                p.Get(StatKey.BatRBI), p.Get(StatKey.BatBB), p.Get(StatKey.BatSO), p.Get(StatKey.BatHR), p.Get(StatKey.RunSB))).ToList();
        var pitchers = box.Players.Where(p => p.Get(StatKey.PitchBF) > 0 || p.Get(StatKey.PitchOuts) > 0 || p.Get(StatKey.PitchPitches) > 0)
            .Select(p => new PitcherLineView(GameText.Side(p.Team), p.Player.Value, InningsOuts.FromOuts(p.Get(StatKey.PitchOuts)).Value.ToString(), p.Get(StatKey.PitchH),
                p.Get(StatKey.PitchR), p.Get(StatKey.PitchER), p.Get(StatKey.PitchBB), p.Get(StatKey.PitchK), p.Get(StatKey.PitchHR), p.Get(StatKey.PitchWP), p.Get(StatKey.PitchPitches),
                string.Concat(box.Decisions.Where(d => d.Pitcher == p.Player).Select(d => GameText.Decision(d.Kind))))).ToList();
        var catchers = box.CatcherExposures.Select(c => new CatcherLineView(GameText.Side(c.Team), c.Catcher.Value, c.PitchesReceived, c.OutsWhileCatching, c.StolenBasesAllowed, c.CaughtStealing, c.PassedBalls)).ToList();
        var batteries = box.BatteryExposures.Select(b => new BatteryLineView(GameText.Side(b.Team), b.Catcher.Value, b.Pitcher.Value, b.Pitches, b.Outs, b.BattersFaced)).ToList();
        return new BoxScoreView(batters, pitchers, catchers, batteries, Totals("원정", box.Away), Totals("홈", box.Home),
            box.Decisions.Select(d => $"{GameText.Side(d.Team)} {GameText.Decision(d.Kind)}: {d.Pitcher}").ToList());
    }

    private static string Totals(string label, TeamTotals t) => $"{label} 득점 {t.Runs} · 안타 {t.Hits} · 실책 {t.Errors} · 잔루 {t.LeftOnBase}";
}

/// <summary>
/// Cross-checks the independent records of one finished game: batting vs pitching vs fielding vs catching vs line score.
/// An empty result means every identity holds. It never changes a number; a failure names the identity that broke.
/// </summary>
internal static class BoxScoreReconciler
{
    public static IReadOnlyList<string> Check(BoxScore box)
    {
        var problems = new List<string>();
        int Sum(TeamSide team, StatKey key) => box.Players.Where(p => p.Team == team).Sum(p => p.Get(key));
        void Same(string name, int left, int right)
        {
            if (left != right)
                problems.Add($"{name}: {left} ≠ {right}");
        }

        foreach (var bat in new[] { TeamSide.Away, TeamSide.Home })
        {
            var fld = bat == TeamSide.Away ? TeamSide.Home : TeamSide.Away;
            var label = GameText.Side(bat);
            var line = bat == TeamSide.Away ? box.AwayLine : box.HomeLine;
            var totals = bat == TeamSide.Away ? box.Away : box.Home;
            Same($"{label} 득점: 팀 합계와 이닝별 합", totals.Runs, line.Sum());
            Same($"{label} 득점: 팀 합계와 타자 득점 합", totals.Runs, Sum(bat, StatKey.RunR));
            Same($"{label} 득점: 팀 합계와 상대 투수 실점 합", totals.Runs, Sum(fld, StatKey.PitchR));
            Same($"{label} 안타: 팀 합계와 타자 안타 합", totals.Hits, Sum(bat, StatKey.BatH));
            Same($"{label} 안타: 타자 안타와 상대 투수 피안타", Sum(bat, StatKey.BatH), Sum(fld, StatKey.PitchH));
            Same($"{label} 안타: 안타 합과 1·2·3루타·홈런 합", Sum(bat, StatKey.BatH), Sum(bat, StatKey.Bat1B) + Sum(bat, StatKey.Bat2B) + Sum(bat, StatKey.Bat3B) + Sum(bat, StatKey.BatHR));
            Same($"{label} 타석: 타석 수와 상대 투수 상대 타자 수", Sum(bat, StatKey.BatPA), Sum(fld, StatKey.PitchBF));
            Same($"{label} 타석: 타석 수와 타수·볼넷·사구·희생타 합", Sum(bat, StatKey.BatPA),
                Sum(bat, StatKey.BatAB) + Sum(bat, StatKey.BatBB) + Sum(bat, StatKey.BatHBP) + Sum(bat, StatKey.BatSF) + Sum(bat, StatKey.BatSH));
            // OBR 9.03(c): a box score proves when at-bats + walks + hit batters + sacrifices = runs + left on base + the opponent's putouts
            Same($"{label} 박스 증명(OBR 9.03(c)): 타수·볼넷·사구·희생 합과 득점·잔루·상대 풋아웃 합",
                Sum(bat, StatKey.BatAB) + Sum(bat, StatKey.BatBB) + Sum(bat, StatKey.BatHBP) + Sum(bat, StatKey.BatSF) + Sum(bat, StatKey.BatSH),
                totals.Runs + totals.LeftOnBase + Sum(fld, StatKey.FldPO));
            Same($"{label} 볼넷: 타자와 상대 투수", Sum(bat, StatKey.BatBB), Sum(fld, StatKey.PitchBB));
            Same($"{label} 삼진: 타자와 상대 투수", Sum(bat, StatKey.BatSO), Sum(fld, StatKey.PitchK));
            Same($"{label} 사구: 타자와 상대 투수", Sum(bat, StatKey.BatHBP), Sum(fld, StatKey.PitchHBP));
            Same($"{label} 홈런: 타자와 상대 투수", Sum(bat, StatKey.BatHR), Sum(fld, StatKey.PitchHR));
            Same($"{label} 도루: 주자 도루와 상대 포수 허용", Sum(bat, StatKey.RunSB), Sum(fld, StatKey.CatSBA));
            Same($"{label} 공격 때 상대 아웃: 투수 아웃과 수비 자살 합", Sum(fld, StatKey.PitchOuts), Sum(fld, StatKey.FldPO));
            Same($"{label} 공격 때 상대 아웃: 투수 아웃과 포수 기준 아웃", Sum(fld, StatKey.PitchOuts), Sum(fld, StatKey.CatOuts));
            Same($"{label} 공격 때 상대 투구: 투수 투구와 포수 받은 공", Sum(fld, StatKey.PitchPitches), Sum(fld, StatKey.CatPitches));
            Same($"{label} 공격 때 상대 투구: 투수 투구와 배터리 투구", Sum(fld, StatKey.PitchPitches), Sum(fld, StatKey.BatteryPitches));
            Same($"{label} 공격 때 상대 타자 수: 투수와 배터리", Sum(fld, StatKey.PitchBF), Sum(fld, StatKey.BatteryBF));
            Same($"{label} 공격 때 상대 아웃: 투수 아웃과 배터리 아웃", Sum(fld, StatKey.PitchOuts), Sum(fld, StatKey.BatteryOuts));
            if (Sum(fld, StatKey.PitchER) > Sum(fld, StatKey.PitchR))
                problems.Add($"{label} 득점: 자책점이 실점보다 많음");
            if (Sum(bat, StatKey.BatRBI) > totals.Runs)
                problems.Add($"{label} 타점이 득점보다 많음");
            if (box.CatcherExposures.Where(c => c.Team == fld).Sum(c => c.PitchesReceived) != Sum(fld, StatKey.PitchPitches))
                problems.Add($"{label} 공격 때 상대 포수 노출 합이 투구 수와 다름");
            if (box.BatteryExposures.Where(b => b.Team == fld).Any(b => b.SharedAppearances != 1))
                problems.Add($"{label} 공격 때 상대 배터리 노출이 한 경기에 한 번이 아님");
        }

        var duplicate = box.Appearances.GroupBy(a => a.Player).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            problems.Add($"선수 {duplicate.Key}의 경기 참가가 두 번 이상 기록됨");
        return problems;
    }
}
