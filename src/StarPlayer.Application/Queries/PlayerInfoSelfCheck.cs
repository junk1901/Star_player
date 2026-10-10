using StarPlayer.Application.Diagnostics;

namespace StarPlayer.Application.Queries;

/// <summary>
/// Phase 1 self-check run by the diagnostic screen (and, unattended, by <c>verify.ps1</c> through the exported game).
/// It re-proves the visibility contract against the loaded data using only public views and the card text.
/// </summary>
public static class PlayerInfoSelfCheck
{
    public static DiagnosticReport Run(PlayerInfoService service)
    {
        if (!service.IsReady)
            return new DiagnosticReport([new DiagnosticStep("선수 데이터 불러오기", false, service.LoadError ?? "알 수 없는 오류")]);

        var steps = new List<DiagnosticStep>();
        var list = service.ListPlayers();
        var views = list.Select(item => service.GetPlayer(new PlayerQuery(item.PlayerId))).ToList();
        var loaded = views.All(v => v.IsSuccess);
        var all = views.Where(v => v.IsSuccess).Select(v => v.View!).ToList();
        var summary = service.GetSummary();

        steps.Add(new("선수 데이터 불러오기", loaded && list.Count >= 10 && summary is { ClubCount: >= 2 }, $"선수 {list.Count}명 · 구단 {summary?.ClubCount}개 · {summary?.Label}"));

        var own = all.Where(v => v.IsOwnPlayer).ToList();
        var ownLines = own.SelectMany(v => v.BaseAbilities.Concat(v.CurrentState).Concat(v.Proficiencies)).ToList();
        steps.Add(new("내 선수는 정확한 현재 능력치만 표시", own.Count == 1 && ownLines.Count > 0 && ownLines.All(l => l.Exact is not null && l.Grade is null),
            own.Count == 1 ? $"{own[0].DisplayName}: {ownLines.Count}개 항목이 모두 정확한 숫자" : $"내 선수 {own.Count}명"));

        var others = all.Where(v => !v.IsOwnPlayer).ToList();
        var otherLines = others.SelectMany(v => v.BaseAbilities.Concat(v.CurrentState).Concat(v.Proficiencies)).ToList();
        var otherPitches = others.SelectMany(v => v.Pitches).ToList();
        steps.Add(new("다른 선수는 관측 등급·범위·신뢰도만 표시",
            others.Count > 0 && otherLines.All(l => l.Exact is null && l.Grade is not null && l.RangeLow < l.RangeHigh && l.Confidence is not null) && otherPitches.All(p => p.Exact is null),
            $"{others.Count}명 · {otherLines.Count + otherPitches.Count}개 항목에 정확한 숫자 없음"));

        var cards = string.Join("\n", all.Select(PlayerCardFormatter.Format));
        var leaks = HiddenDataGuard.FindLeaks(cards + System.Text.Json.JsonSerializer.Serialize(all));
        steps.Add(new("실제 잠재력·성장 유형 비노출", leaks.Count == 0 && cards.Contains(PlayerCardFormatter.HiddenNotice), leaks.Count == 0 ? "카드 텍스트와 DTO에서 숨은 필드 식별자를 찾지 못함" : $"발견: {string.Join(", ", leaks)}"));

        var sameName = all.GroupBy(v => v.DisplayName).FirstOrDefault(g => g.Count() > 1);
        steps.Add(new("동명이인은 서로 다른 선수 ID로 구분", sameName is not null && sameName.Select(v => v.PlayerId).Distinct().Count() == sameName.Count(),
            sameName is null ? "동명이인 시나리오 없음" : $"{sameName.Key}: {string.Join(", ", sameName.Select(v => v.PlayerId))}"));

        var catchers = all.Where(v => v.BaseAbilities.Any(l => l.Key == "receiving_framing")).ToList();
        steps.Add(new("포수 능력과 C 숙련도는 별도 항목", catchers.Count >= 2 && catchers.All(v => v.Proficiencies.Any(l => l.Key == "position_catcher") && v.BaseAbilities.All(l => l.Key != "position_catcher")),
            $"포수 {catchers.Count}명"));

        var withClub = all.Where(v => v.ClubName is not null).ToList();
        steps.Add(new("소속 구단과 현재 등번호 표시", withClub.Count == all.Count && withClub.All(v => v.CurrentJerseyNumber is not null), $"{withClub.Count(v => v.CurrentJerseyNumber is not null)}명에게 현재 등번호 표시"));
        return new DiagnosticReport(steps);
    }
}
