using StarPlayer.Application.Ports;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.Application.Projections;

/// <summary>
/// Phase 2 diagnostic runner: steps a scripted game through the real <see cref="GameReducer"/> one outcome at a time and returns
/// public views of the committed state. The script is data; the reducer only checks and applies each event, so what the screen
/// shows is exactly what the engine accepted. No manager controls: the only command is "next scripted outcome".
/// It has no public constructor; Infrastructure's composition builds it from the embedded games.
/// </summary>
public sealed class GameDiagnosticsService
{
    private readonly IReadOnlyList<ScriptedGame> _games = [];
    private ScriptedGame? _game;
    private GameState? _state;
    private int _next;
    private string _last = "";

    public string? LoadError { get; }

    public bool IsReady => LoadError is null;

    internal GameDiagnosticsService(IScriptedGameSource source)
    {
        try
        {
            var loaded = source.Load();
            if (!loaded.IsSuccess)
            {
                LoadError = $"진단 경기를 불러오지 못했습니다: {loaded.Error!.Value.Message} ({loaded.Error.Value.Code})";
                return;
            }

            _games = loaded.Value;
        }
        catch (Exception exception)
        {
            LoadError = $"진단 경기를 불러오지 못했습니다: {exception.Message}";
        }
    }

    public IReadOnlyList<ScriptedGameListItem> ListGames() =>
        _games.Select(g => new ScriptedGameListItem(g.Id, g.Title, GameText.Rules(g.Rules), g.Events.Count)).ToList();

    /// <summary>Starts (or restarts) a scripted game from its first event.</summary>
    public GameViewResult Select(string? gameId)
    {
        var game = _games.FirstOrDefault(g => g.Id == gameId);
        if (game is null)
            return GameViewResult.Failure("Game.not_found", "해당 진단 경기를 찾을 수 없습니다.");
        var start = GameReducer.Start(new GameStart(game.Id, game.Rules, game.Away, game.Home));
        if (!start.IsSuccess)
            return GameViewResult.Failure(start.Error!.Value.Code, start.Error.Value.Message);
        (_game, _state, _next, _last) = (game, start.Value, 0, "경기 시작 전");
        return GameViewResult.Success(Build());
    }

    public GameViewResult Current() =>
        _game is null ? GameViewResult.Failure("Game.not_selected", "먼저 경기를 선택하세요.") : GameViewResult.Success(Build());

    /// <summary>Applies the next scripted event. A rejected event reports the reducer's error and leaves the game exactly where it was.</summary>
    public GameViewResult Next()
    {
        if (_game is null || _state is null)
            return GameViewResult.Failure("Game.not_selected", "먼저 경기를 선택하세요.");
        if (_state.Status == GameStatus.Final)
            return GameViewResult.Success(Build());
        if (_next >= _game.Events.Count)
            return GameViewResult.Failure("Game.script_ended", "스크립트가 끝났지만 경기가 끝나지 않았습니다.");

        var e = _game.Events[_next];
        var applied = GameReducer.Apply(_state, e);
        if (!applied.IsSuccess)
            return GameViewResult.Failure(applied.Error!.Value.Code, $"{GameText.Describe(e)} — {applied.Error.Value.Message}");

        _state = applied.Value.State;
        _next++;
        _last = GameText.Describe(e);
        if (_state.Status == GameStatus.Final && GameReducer.Finalize(_state) is { IsSuccess: true } finalized)
            _state = finalized.Value.State; // the one-time finalization happens here; the box score is then read from it
        return GameViewResult.Success(Build());
    }

    /// <summary>Runs every remaining event (used by the unattended self-check).</summary>
    public GameViewResult RunToEnd()
    {
        var result = Current();
        while (result.IsSuccess && !result.View!.IsOver)
        {
            result = Next();
            if (!result.IsSuccess)
                return result;
        }

        return result;
    }

    /// <summary>The finished game's reconciliation problems and comparison with the script's independent expected results (empty when everything agrees).</summary>
    internal IReadOnlyList<string> Verify()
    {
        if (_game is null || _state?.Final is not { } final)
            return ["경기가 아직 확정되지 않았습니다."];
        var problems = BoxScoreReconciler.Check(final.BoxScore).ToList();
        if (_game.Expected is { } expected)
            problems.AddRange(ExpectedComparison.Compare(final, expected));
        else
            problems.Add("독립 기대값이 없는 경기입니다.");
        return problems;
    }

    private GameView Build()
    {
        var s = _state!;
        var g = _game!;
        var over = s.Status == GameStatus.Final;
        var box = s.Final?.BoxScore;
        int Sum(TeamSide team, StatKey key) => s.Stats.Where(x => x.Team == team && x.Key == key).Sum(x => x.Amount);
        var innings = Math.Max(g.Rules.RegulationInnings, s.AwayLine.Count);
        string Cell(IReadOnlyList<int> line, int i, bool home) => i < line.Count ? line[i].ToString() : home && over && i < s.AwayLine.Count ? "X" : "";
        var legal = GameReducer.LegalActions(s);
        var nextText = !over && _next < g.Events.Count ? GameText.Describe(g.Events[_next]) : "";
        var batterSlot = s.Batting.CurrentBatterSlot;
        var resultText = !over ? "" : s.Outcome switch
        {
            GameOutcome.AwayWin => $"원정 승리 {s.AwayRuns}:{s.HomeRuns}",
            GameOutcome.HomeWin => $"홈 승리 {s.HomeRuns}:{s.AwayRuns}",
            _ => $"무승부 {s.AwayRuns}:{s.HomeRuns}",
        };

        return new GameView(
            g.Id, g.Title, GameText.Rules(g.Rules), GameText.Status(g.Rules.Status),
            over ? "경기 종료" : $"{s.Inning}회 {(s.Half == InningHalf.Top ? "초" : "말")} ({GameText.Side(s.BattingSide)} 공격)",
            s.Count.Balls, s.Count.Strikes, over ? s.Outs : s.Outs,
            s.Bases.First is not null, s.Bases.Second is not null, s.Bases.Third is not null,
            s.Bases.First?.Player.Value ?? "", s.Bases.Second?.Player.Value ?? "", s.Bases.Third?.Player.Value ?? "",
            s.AwayRuns, s.HomeRuns, Sum(TeamSide.Away, StatKey.BatH), Sum(TeamSide.Home, StatKey.BatH), Sum(TeamSide.Away, StatKey.FldE), Sum(TeamSide.Home, StatKey.FldE),
            Enumerable.Range(1, innings).Select(i => i.ToString()).ToList(),
            Enumerable.Range(0, innings).Select(i => Cell(s.AwayLine, i, false)).ToList(),
            Enumerable.Range(0, innings).Select(i => Cell(s.HomeLine, i, true)).ToList(),
            over ? "" : $"{batterSlot.Order}번 {batterSlot.Player} ({GameText.Position(batterSlot.Position)})",
            over ? "" : $"{s.Fielding.Pitcher}",
            over ? "경기 종료" : GameText.Stage(s.Stage),
            legal.Actions.Select(GameText.Action).ToList(),
            _last, nextText, _next, g.Events.Count, over, resultText, box is null ? null : BoxScoreProjection.From(box));
    }
}
