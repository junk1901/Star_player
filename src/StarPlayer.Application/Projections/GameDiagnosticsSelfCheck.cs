using StarPlayer.Application.Diagnostics;

namespace StarPlayer.Application.Projections;

/// <summary>
/// Unattended Phase 2 check run by the diagnostic screen (and by verify.ps1 through the exported game): every embedded scripted
/// game is played to the end through the real reducer, finalized once, reconciled across batting/pitching/fielding/catching
/// records and compared with the independently written expected results.
/// </summary>
public static class GameDiagnosticsSelfCheck
{
    public static DiagnosticReport Run(GameDiagnosticsService service)
    {
        if (!service.IsReady)
            return new DiagnosticReport([new DiagnosticStep("진단 경기 불러오기", false, service.LoadError ?? "알 수 없는 오류")]);

        var games = service.ListGames();
        var steps = new List<DiagnosticStep> { new("진단 경기 불러오기", games.Count > 0, $"{games.Count}경기") };
        foreach (var game in games)
        {
            var finished = service.Select(game.Id);
            if (finished.IsSuccess)
                finished = service.RunToEnd();
            if (!finished.IsSuccess)
            {
                steps.Add(new DiagnosticStep($"경기 {game.Id} 끝까지 진행", false, $"{finished.ErrorCode}: {finished.ErrorMessage}"));
                continue;
            }

            var view = finished.View!;
            var problems = service.Verify();
            steps.Add(new DiagnosticStep($"경기 {game.Id} 진행·정합성·독립 기대값 비교", view.IsOver && problems.Count == 0,
                problems.Count == 0 ? $"{view.ResultText}, 사건 {view.EventsApplied}개, 박스스코어 항목 모두 일치" : $"{view.ResultText}: {string.Join(" / ", problems.Take(3))}"));
        }

        return new DiagnosticReport(steps);
    }
}
