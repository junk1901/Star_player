using Godot;
using StarPlayer.Application.Projections;
using StarPlayer.Client.Components;

namespace StarPlayer.Client.UI;

/// <summary>
/// Phase 2 diagnostic game screen (QHD, not final design). Thin presenter: it selects a scripted test game, asks
/// <see cref="GameDiagnosticsService"/> for the next scripted outcome and draws the returned public view (scoreboard, real
/// count, outs, bases, box score). It owns no game rules and offers no manager controls.
/// </summary>
public partial class GameDiagnosticsScreen : Control
{
    [Signal]
    public delegate void BackRequestedEventHandler();

    private ItemList _games = null!;
    private GridContainer _grid = null!;
    private BaseDiamond _bases = null!;
    private Label _status = null!;
    private Label _info = null!;
    private Label _events = null!;
    private Label _box = null!;
    private Label _check = null!;
    private Button _next = null!;
    private GameDiagnosticsService? _service;
    private IReadOnlyList<ScriptedGameListItem> _items = [];

    public GameView? CurrentView { get; private set; }

    public string BoxText => _box.Text;

    public string CheckText => _check.Text;

    public override void _Ready()
    {
        _games = GetNode<ItemList>("%GameList");
        _grid = GetNode<GridContainer>("%ScoreGrid");
        _bases = GetNode<BaseDiamond>("%Bases");
        _status = GetNode<Label>("%StatusLabel");
        _info = GetNode<Label>("%InfoLabel");
        _events = GetNode<Label>("%EventLabel");
        _box = GetNode<Label>("%BoxLabel");
        _check = GetNode<Label>("%CheckLabel");
        _next = GetNode<Button>("%NextButton");
        _games.ItemSelected += index => StartGame((int)index);
        _next.Pressed += () => PressNext();
        GetNode<Button>("%RestartButton").Pressed += () => { if (CurrentView is { } v) Start(v.GameId); };
        GetNode<Button>("%RunButton").Pressed += () => RunToEnd();
        GetNode<Button>("%BackButton").Pressed += () => EmitSignal(SignalName.BackRequested);
    }

    public void Initialize(GameDiagnosticsService service)
    {
        _service = service;
        _games.Clear();
        if (!service.IsReady)
        {
            _status.Text = service.LoadError ?? "진단 경기 서비스가 연결되지 않았습니다.";
            return;
        }

        _items = service.ListGames();
        foreach (var item in _items)
            _games.AddItem($"{item.Title} · {item.EventCount}개 사건");
        _check.Text = GameDiagnosticsSelfCheckText(service);
        if (_items.Count > 0)
        {
            _games.Select(0);
            StartGame(0);
        }
    }

    public IReadOnlyList<string> ListedGameIds => _items.Select(i => i.Id).ToList();

    public void StartGame(int index)
    {
        if (index >= 0 && index < _items.Count)
            Start(_items[index].Id);
    }

    public void Start(string gameId) => Show(_service?.Select(gameId));

    /// <summary>Applies the next scripted outcome exactly as the button does and returns the new view.</summary>
    public GameView? PressNext()
    {
        Show(_service?.Next());
        return CurrentView;
    }

    public GameView? RunToEnd()
    {
        Show(_service?.RunToEnd());
        return CurrentView;
    }

    private void Show(GameViewResult? result)
    {
        if (result is null)
        {
            _status.Text = "진단 경기 서비스가 연결되지 않았습니다.";
            return;
        }

        if (!result.IsSuccess)
        {
            _status.Text = $"오류 [{result.ErrorCode}] {result.ErrorMessage}";
            return;
        }

        var v = CurrentView = result.View!;
        _status.Text = $"{v.Title}\n규칙: {v.RulesLabel} [{v.RulesStatusLabel}]";
        _info.Text = $"{v.InningLabel}\n볼 {v.Balls} · 스트라이크 {v.Strikes} · 아웃 {v.Outs}\n타자 {(v.CurrentBatter.Length == 0 ? "-" : v.CurrentBatter)}\n투수 {(v.CurrentPitcher.Length == 0 ? "-" : v.CurrentPitcher)}\n"
            + $"주자 1루 {Or(v.FirstRunner)} · 2루 {Or(v.SecondRunner)} · 3루 {Or(v.ThirdRunner)}\n상태: {v.StageLabel}";
        _events.Text = $"방금 진행: {Or(v.LastEventText)}\n다음 사건: {Or(v.NextEventText)}\n진행 {v.EventsApplied}/{v.EventsTotal}" + (v.IsOver ? $"\n{v.ResultText}" : "");
        _bases.SetBases(v.FirstOccupied, v.SecondOccupied, v.ThirdOccupied);
        _next.Disabled = v.IsOver;
        _box.Text = GameViewFormatter.Box(v);
        BuildScoreboard(v);
    }

    private static string Or(string text) => text.Length == 0 ? "-" : text;

    private void BuildScoreboard(GameView v)
    {
        foreach (var child in _grid.GetChildren())
            child.QueueFree();
        _grid.Columns = v.InningHeaders.Count + 4;
        AddRow("", v.InningHeaders.Concat(["R", "H", "E"]).ToList(), header: true);
        AddRow("원정", v.AwayLine.Concat([v.AwayRuns.ToString(), v.AwayHits.ToString(), v.AwayErrors.ToString()]).ToList(), header: false);
        AddRow("홈", v.HomeLine.Concat([v.HomeRuns.ToString(), v.HomeHits.ToString(), v.HomeErrors.ToString()]).ToList(), header: false);
    }

    private void AddRow(string team, IReadOnlyList<string> cells, bool header)
    {
        _grid.AddChild(MakeCell(team, 100, header));
        foreach (var cell in cells)
            _grid.AddChild(MakeCell(cell, 56, header));
    }

    private static Label MakeCell(string text, float width, bool header)
    {
        var label = new Label { Text = text, CustomMinimumSize = new Vector2(width, 48), HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", 30);
        if (header)
            label.Modulate = new Color(0.7f, 0.75f, 0.85f);
        return label;
    }

    /// <summary>Runs the unattended self-check text shown in the bottom panel (every embedded game against its independent expected result).</summary>
    private static string GameDiagnosticsSelfCheckText(GameDiagnosticsService service)
    {
        var report = GameDiagnosticsSelfCheck.Run(service);
        var passed = report.Steps.Count(step => step.Passed);
        return $"자체 점검: {(report.Passed ? "통과" : "실패")} ({passed}/{report.Steps.Count})\n"
            + string.Join("\n", report.Steps.Select(step => $"[{(step.Passed ? "통과" : "실패")}] {step.Name} — {step.Detail}"));
    }

    public bool SelfCheckPassed() => _service is not null && GameDiagnosticsSelfCheck.Run(_service).Passed;
}
