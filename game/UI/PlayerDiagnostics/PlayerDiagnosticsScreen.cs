using Godot;
using StarPlayer.Application.Queries;

namespace StarPlayer.Client.UI;

/// <summary>
/// Phase 1 diagnostic player card. Thin presenter: it asks <see cref="PlayerInfoService"/> for public views by player id and
/// shows <see cref="PlayerCardFormatter"/> text. It cannot choose who the viewer is and never sees hidden development data.
/// Layout is QHD-only diagnostic work, not final design.
/// </summary>
public partial class PlayerDiagnosticsScreen : Control
{
    [Signal]
    public delegate void BackRequestedEventHandler();

    private ItemList _list = null!;
    private Label _card = null!;
    private Label _summary = null!;
    private Label _checks = null!;
    private PlayerInfoService? _players;
    private IReadOnlyList<PlayerListItem> _items = [];

    public string CardText => _card.Text;

    public string SummaryText => _summary.Text;

    public string CheckText => _checks.Text;

    public override void _Ready()
    {
        _list = GetNode<ItemList>("%PlayerList");
        _card = GetNode<Label>("%CardLabel");
        _summary = GetNode<Label>("%SummaryLabel");
        _checks = GetNode<Label>("%CheckLabel");
        _list.ItemSelected += index => ShowPlayer((int)index);
        GetNode<Button>("%BackButton").Pressed += () => EmitSignal(SignalName.BackRequested);
    }

    public void Initialize(PlayerInfoService players)
    {
        _players = players;
        Refresh();
    }

    /// <summary>Re-reads the list and runs the self-check; returns whether every check passed.</summary>
    public bool Refresh()
    {
        _list.Clear();
        if (_players is null || !_players.IsReady)
        {
            _summary.Text = _players?.LoadError ?? "선수 정보 서비스가 연결되지 않았습니다.";
            _card.Text = "";
            _checks.Text = "";
            return false;
        }

        var summary = _players.GetSummary()!;
        _summary.Text = $"{summary.Label}\n기준일 {summary.AsOfDate} · 내 선수 {summary.OwnPlayerName} · 치트 모드 {(summary.CheatMode ? "켜짐" : "꺼짐")} (치트 모드에서도 잠재력은 비공개) · 선수 {summary.PlayerCount}명 / 구단 {summary.ClubCount}개 · 관측 모델 {summary.ObservationModel}";

        _items = _players.ListPlayers();
        foreach (var item in _items)
            _list.AddItem($"{(item.IsOwnPlayer ? "★ " : "")}{item.DisplayName} · {item.ClubName} · {item.Kind} · {item.PositionLabel} ({item.PlayerId})");
        if (_items.Count > 0)
        {
            _list.Select(0);
            ShowPlayer(0);
        }

        var report = PlayerInfoSelfCheck.Run(_players);
        var passed = report.Steps.Count(step => step.Passed);
        _checks.Text = $"자체 점검: {(report.Passed ? "통과" : "실패")} ({passed}/{report.Steps.Count})\n"
            + string.Join("\n", report.Steps.Select(step => $"[{(step.Passed ? "통과" : "실패")}] {step.Name} — {step.Detail}"));
        return report.Passed;
    }

    /// <summary>Selects a player by id as a user click would, and returns the displayed card text.</summary>
    public string Select(string playerId)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            if (_items[i].PlayerId != playerId)
                continue;
            _list.Select(i);
            ShowPlayer(i);
        }

        return _card.Text;
    }

    public IReadOnlyList<string> ListedPlayerIds => _items.Select(item => item.PlayerId).ToList();

    private void ShowPlayer(int index)
    {
        if (_players is null || index < 0 || index >= _items.Count)
            return;
        var result = _players.GetPlayer(new PlayerQuery(_items[index].PlayerId));
        _card.Text = result.IsSuccess ? PlayerCardFormatter.Format(result.View!) : $"선수 정보를 표시할 수 없습니다: {result.ErrorMessage}";
    }
}
