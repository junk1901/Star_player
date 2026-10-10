using System.Globalization;
using StarPlayer.Application.Ports;
using StarPlayer.Application.Sessions;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Domain.Teams;

namespace StarPlayer.Application.Queries;

/// <summary>
/// Phase 1 query use case behind the player diagnostic screen. It owns the session's <see cref="ViewerContext"/>, loads the
/// world once through the internal <see cref="IPlayerWorldSource"/>, and answers with <see cref="PlayerPublicView"/> only.
/// It has no public constructor (Infrastructure's composition builds it), the controlled player is whatever the trusted
/// world binding says, and screens pass only a player id: they cannot choose who the viewer is or what is exact. Hidden
/// development data is never read here.
/// </summary>
public sealed class PlayerInfoService
{
    private readonly ViewerContext? _viewer;
    private readonly DateOnly _asOf;
    private readonly string _label = "";
    private readonly Dictionary<PlayerId, PlayerSheet> _sheets = [];
    private readonly Dictionary<ClubId, Club> _clubs = [];
    private readonly List<PlayerSheet> _ordered = [];
    private readonly Dictionary<PlayerId, JerseyNumberAssignment> _jerseyOnAsOf = [];

    /// <summary>Null when the world loaded and passed validation; otherwise a Korean explanation.</summary>
    public string? LoadError { get; }

    public bool IsReady => LoadError is null;

    internal PlayerInfoService(IPlayerWorldSource source, PlayerSessionOptions? options = null)
    {
        options ??= new PlayerSessionOptions();
        Result<PlayerWorld> loaded;
        try
        {
            loaded = source.Load();
        }
        catch (Exception exception)
        {
            LoadError = $"선수 데이터를 불러오지 못했습니다: {exception.Message}";
            return;
        }

        if (!loaded.IsSuccess)
        {
            LoadError = $"선수 데이터를 불러오지 못했습니다: {loaded.Error!.Value.Message} ({loaded.Error.Value.Code})";
            return;
        }

        var world = loaded.Value;
        var controlled = world.ControlledPlayer; // trusted binding; no option or query can replace it

        foreach (var sheet in world.Players)
        {
            if (!_sheets.TryAdd(sheet.Id, sheet))
            {
                LoadError = $"같은 선수 ID가 두 번 있습니다: {sheet.Id}";
                return;
            }

            if (!sheet.Player.AgeOn(world.AsOfDate).IsSuccess)
            {
                LoadError = $"기준일이 {sheet.Id} 선수의 생년월일보다 빠릅니다.";
                return;
            }
        }

        if (!_sheets.ContainsKey(controlled))
        {
            LoadError = $"세션의 내 선수 {controlled}을(를) 선수 목록에서 찾지 못했습니다.";
            return;
        }

        _asOf = world.AsOfDate; // CurrentClub below reads it
        foreach (var club in world.Clubs)
            _clubs[club.Id] = club;
        _ordered.AddRange(world.Players.OrderBy(p => CurrentClub(p)?.Value ?? "", StringComparer.Ordinal).ThenBy(p => p.Id.Value, StringComparer.Ordinal));
        _label = world.Label;
        foreach (var sheet in world.Players)
        {
            // Corrupt (overlapping) number rows are an explicit load error, never "whichever row came first".
            var active = world.Jerseys.ActiveOn(sheet.Id, world.AsOfDate);
            if (!active.IsSuccess)
            {
                LoadError = $"등번호 데이터가 올바르지 않습니다: {active.Error!.Value.Message}";
                return;
            }

            if (active.Value is { } current)
                _jerseyOnAsOf[sheet.Id] = current;
        }

        _viewer = new ViewerContext(controlled, options.CheatMode);
    }

    public WorldSummaryView? GetSummary() =>
        _viewer is null ? null : new WorldSummaryView(_label, _asOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _sheets[_viewer.ControlledPlayer].Player.DisplayName, _viewer.CheatMode, _sheets.Count, _clubs.Count, ObservationPolicy.Version);

    public IReadOnlyList<PlayerListItem> ListPlayers() =>
        _viewer is null ? [] : _ordered.Select(sheet => new PlayerListItem(
            sheet.Id.Value,
            sheet.Player.DisplayName,
            CurrentClub(sheet) is { } id && _clubs.TryGetValue(id, out var club) ? club.Name : "무소속",
            KoreanLabels.Kind(sheet.Kind),
            KoreanLabels.Position(sheet.Proficiency.PreferredPosition),
            _viewer.IsSelf(sheet.Id))).ToList();

    public PlayerQueryResult GetPlayer(PlayerQuery query)
    {
        if (_viewer is null)
            return PlayerQueryResult.Failure("PlayerInfo.not_ready", LoadError ?? "선수 데이터가 준비되지 않았습니다.");
        var id = PlayerId.Create(query?.PlayerId);
        if (!id.IsSuccess || !_sheets.TryGetValue(id.Value, out var sheet))
            return PlayerQueryResult.Failure("PlayerInfo.player_not_found", "해당 선수를 찾을 수 없습니다.");

        var clubId = CurrentClub(sheet);
        var club = clubId is { } c && _clubs.TryGetValue(c, out var found) ? found : null;
        var jersey = clubId is { } owner && _jerseyOnAsOf.TryGetValue(sheet.Id, out var active) && active.Club == owner ? active : null;
        var viewerClub = CurrentClub(_sheets[_viewer.ControlledPlayer]);
        var clubmate = clubId is not null && clubId == viewerClub;

        return PlayerQueryResult.Success(PlayerVisibilityPolicy.Project(sheet, _viewer, new PlayerProjectionContext(_asOf, club, jersey, clubmate)));
    }

    private ClubId? CurrentClub(PlayerSheet sheet) =>
        sheet.Status.Affiliations.FirstOrDefault(a => a.From <= _asOf && (a.To is null || _asOf < a.To))?.Club;
}
