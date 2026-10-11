using System.Collections.Immutable;
using System.Text.Json;
using StarPlayer.Application.Ports;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;

namespace StarPlayer.Infrastructure.Fixtures;

// JSON for GameRules profiles and scripted games (docs/game-engine.md "Fixture formats"). Same conventions as the Phase 1 pack:
// snake_case, unknown members rejected, every value rebuilt through Domain factories, located typed errors.

internal sealed class RuleProfilesDto
{
    public required int FormatVersion { get; set; }
    public required string Note { get; set; }
    public required List<RuleProfileEntryDto> Profiles { get; set; }
}

internal class RuleProfileEntryDto
{
    public required string ProfileId { get; set; }
    public required string Version { get; set; }
    public required string Status { get; set; }
    public required string Provenance { get; set; }
    public required List<string> OpenItems { get; set; }
    public required int OutsPerHalfInning { get; set; }
    public required int BallsForWalk { get; set; }
    public required int StrikesForStrikeout { get; set; }
    public required int RegulationInnings { get; set; }
    public required int? ExtraInningsLimit { get; set; }
    public required bool TieAllowed { get; set; }
    public required bool DesignatedHitter { get; set; }
    public required bool TwoWayPlayerRule { get; set; }
    public required int StarterWinMinOuts { get; set; }
    public required int SaveMaxLead { get; set; }
    public required int SaveMinOuts { get; set; }
    public required int SaveLongOuts { get; set; }
    public required bool DhMayBeForfeited { get; set; }
    public required bool CountWalkOffRunnersAsLeftOnBase { get; set; }
    public required bool FielderToMoundForfeitsDh { get; set; }
    public required string RulePolicy { get; set; }
}

// gamerules-v2 (format_version 2): the Phase 2 fields plus the two required Pitch Zone Lite fields. A format 1 file still rejects them as unknown members.
internal sealed class RuleProfilesV2Dto
{
    public required int FormatVersion { get; set; }
    public required string Note { get; set; }
    public required List<RuleProfileEntryV2Dto> Profiles { get; set; }
}

internal sealed class RuleProfileEntryV2Dto : RuleProfileEntryDto
{
    public required string CalledPitchMode { get; set; }
    public required string ZoneModel { get; set; }
}

internal sealed class GameSetDto
{
    public required int FormatVersion { get; set; }
    public required string Label { get; set; }
    public required List<ScriptedGameDto> Games { get; set; }
}

internal sealed class ScriptedGameDto
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public required string Rules { get; set; }
    public required TeamDto Away { get; set; }
    public required TeamDto Home { get; set; }
    public required List<EventDto> Events { get; set; }
    public ExpectedDto? Expected { get; set; }
}

internal sealed class TeamDto
{
    public required List<SlotDto> Lineup { get; set; }
    public required string Pitcher { get; set; }
    public required List<string> Bench { get; set; }
}

internal sealed class SlotDto
{
    public required int Order { get; set; }
    public required string Player { get; set; }
    public required string Position { get; set; }
}

internal sealed class CreditDto
{
    public required string Position { get; set; }
    public required string Kind { get; set; }
}

internal sealed class MoveDto
{
    public required int From { get; set; }
    public required int To { get; set; }
    public string? Cause { get; set; }
    public string? OutKind { get; set; }
    public List<CreditDto>? Credits { get; set; }
    public int? Sequence { get; set; }
    public int? OutAt { get; set; }
    public string? OutMethod { get; set; }
}

internal class EventDto
{
    public required string Id { get; set; }
    public required string Type { get; set; }
    public string? Approach { get; set; }
    public string? Outcome { get; set; }
    public string? Mishap { get; set; }
    public string? DroppedThird { get; set; }
    public List<MoveDto>? Moves { get; set; }
    public List<CreditDto>? StrikeoutCredits { get; set; }
    public string? Batted { get; set; }
    public string? Result { get; set; }
    public bool? Sacrifice { get; set; }
    public List<CreditDto>? BatterCredits { get; set; }
    public List<string>? Errors { get; set; }
    public int? BatterSequence { get; set; }
    public string? Kind { get; set; }
    public string? Team { get; set; }
    public string? Player { get; set; }
    public int? Base { get; set; }
    public int? Slot { get; set; }
    public int? Slot2 { get; set; }
}

// Event format 2 (Phase 3-C1): the legacy EventDto plus the pitch location. Kept as a separate type so that the legacy reader still rejects a "location" member.
internal sealed class EventLocationDto
{
    public required string ZoneModel { get; set; }
    public required string Zone { get; set; }
    public required string Side { get; set; }
    public required string Boundary { get; set; }
}

internal sealed class EventV2Dto : EventDto
{
    public EventLocationDto? Location { get; set; }
}

internal sealed class EventsV2Dto
{
    public required int FormatVersion { get; set; }
    public required List<EventV2Dto> Events { get; set; }
}

internal sealed class ExpectedDto
{
    public required string Outcome { get; set; }
    public required int AwayRuns { get; set; }
    public required int HomeRuns { get; set; }
    public required List<int> AwayLine { get; set; }
    public required List<int> HomeLine { get; set; }
    public required int AwayHits { get; set; }
    public required int HomeHits { get; set; }
    public required int AwayErrors { get; set; }
    public required int HomeErrors { get; set; }
    public required int AwayLob { get; set; }
    public required int HomeLob { get; set; }
    public required Dictionary<string, Dictionary<string, int>> Players { get; set; }
    public required Dictionary<string, Dictionary<string, int>> Catchers { get; set; }
    public required Dictionary<string, Dictionary<string, int>> Batteries { get; set; }
    public required Dictionary<string, string> Decisions { get; set; }
    public required List<string> Appearances { get; set; }
}

internal static class EnumText
{
    public static T Parse<T>(string? text, string where) where T : struct, Enum
    {
        foreach (var value in Enum.GetValues<T>())
            if (SnakeCase.From(value.ToString()) == text)
                return value;
        throw new GameJsonException("Fixture.enum_invalid", $"{where}: '{text}'은(는) {typeof(T).Name}의 올바른 값이 아닙니다.");
    }
}

internal sealed class GameJsonException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

internal static class GameRulesJson
{
    public const int LegacyFormat = 1;
    public const int PitchZoneFormat = 2;

    /// <summary>Reads a GameRules profile file. Format 1 is the Phase 2 field set (no called-pitch mode); format 2 requires the called-pitch mode and zone model of every profile.</summary>
    public static Result<IReadOnlyList<GameRuleSnapshot>> Read(string json)
    {
        try
        {
            var format = PeekFormat(json);
            if (format is { } known && known != LegacyFormat && known != PitchZoneFormat)
                throw new GameJsonException("Fixture.version_unsupported", $"지원하지 않는 규칙 프로필 형식 버전입니다: {known}");
            var list = format == PitchZoneFormat ? ReadV2(json) : ReadV1(json);
            if (list.Select(r => r.ProfileId).Distinct().Count() != list.Count)
                throw new GameJsonException("Fixture.duplicate_id", "규칙 프로필 ID가 중복되었습니다.");
            return Result<IReadOnlyList<GameRuleSnapshot>>.Success(list);
        }
        catch (GameJsonException e)
        {
            return Result<IReadOnlyList<GameRuleSnapshot>>.Failure(new Error(e.Code, e.Message));
        }
        catch (JsonException e)
        {
            return Result<IReadOnlyList<GameRuleSnapshot>>.Failure(new Error("Fixture.json_invalid", $"JSON 형식이 올바르지 않습니다: {e.Message}"));
        }
    }

    // Only chooses the reader; a missing or non-number format_version (null) falls through to the legacy reader, which reports it as before.
    private static int? PeekFormat(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("format_version", out var format)
            && format.ValueKind == JsonValueKind.Number && format.TryGetInt32(out var number) ? number : null;
    }

    private static List<GameRuleSnapshot> ReadV1(string json)
    {
        var dto = JsonSerializer.Deserialize<RuleProfilesDto>(json, WorldJson.Options) ?? throw new GameJsonException("Fixture.json_invalid", "JSON 내용이 비어 있습니다.");
        if (dto.FormatVersion != LegacyFormat)
            throw new GameJsonException("Fixture.version_unsupported", $"지원하지 않는 규칙 프로필 형식 버전입니다: {dto.FormatVersion}");
        return dto.Profiles.Select(p => Build(p, null, default)).ToList();
    }

    private static List<GameRuleSnapshot> ReadV2(string json)
    {
        var dto = JsonSerializer.Deserialize<RuleProfilesV2Dto>(json, WorldJson.Options) ?? throw new GameJsonException("Fixture.json_invalid", "JSON 내용이 비어 있습니다.");
        var list = new List<GameRuleSnapshot>();
        foreach (var p in dto.Profiles)
        {
            var mode = EnumText.Parse<CalledPitchMode>(p.CalledPitchMode, $"profiles[{p.ProfileId}].called_pitch_mode");
            var zone = ZoneModelVersion.Create(p.ZoneModel);
            if (!zone.IsSuccess)
                throw new GameJsonException(zone.Error!.Value.Code, $"profiles[{p.ProfileId}].zone_model: {zone.Error.Value.Message}");
            list.Add(Build(p, mode, zone.Value));
        }

        return list;
    }

    private static GameRuleSnapshot Build(RuleProfileEntryDto p, CalledPitchMode? mode, ZoneModelVersion zone)
    {
        var made = GameRuleSnapshot.Create(p.ProfileId, p.Version, EnumText.Parse<GameRulesStatus>(p.Status, $"profiles[{p.ProfileId}].status"), p.Provenance, p.OpenItems,
            p.OutsPerHalfInning, p.BallsForWalk, p.StrikesForStrikeout, p.RegulationInnings, p.ExtraInningsLimit, p.TieAllowed, p.DesignatedHitter,
            p.TwoWayPlayerRule, p.StarterWinMinOuts, p.SaveMaxLead, p.SaveMinOuts, p.SaveLongOuts, p.DhMayBeForfeited, p.CountWalkOffRunnersAsLeftOnBase, p.FielderToMoundForfeitsDh, p.RulePolicy,
            mode, zone);
        if (!made.IsSuccess)
            throw new GameJsonException(made.Error!.Value.Code, $"profiles[{p.ProfileId}]: {made.Error.Value.Message}");
        return made.Value;
    }
}

internal static class ScriptedGameJson
{
    public static Result<IReadOnlyList<ScriptedGame>> Read(string json, IReadOnlyList<GameRuleSnapshot> rules)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<GameSetDto>(json, WorldJson.Options) ?? throw new GameJsonException("Fixture.json_invalid", "JSON 내용이 비어 있습니다.");
            if (dto.FormatVersion != 1)
                throw new GameJsonException("Fixture.version_unsupported", $"지원하지 않는 경기 시나리오 형식 버전입니다: {dto.FormatVersion}");
            var games = dto.Games.Select(g => ToGame(g, rules)).ToList();
            if (games.Select(g => g.Id).Distinct().Count() != games.Count)
                throw new GameJsonException("Fixture.duplicate_id", "경기 ID가 중복되었습니다.");
            return Result<IReadOnlyList<ScriptedGame>>.Success(games);
        }
        catch (GameJsonException e)
        {
            return Result<IReadOnlyList<ScriptedGame>>.Failure(new Error(e.Code, e.Message));
        }
        catch (JsonException e)
        {
            return Result<IReadOnlyList<ScriptedGame>>.Failure(new Error("Fixture.json_invalid", $"JSON 형식이 올바르지 않습니다: {e.Message}"));
        }
    }

    private static PlayerId Player(string text, string where)
    {
        var made = PlayerId.Create(text);
        return made.IsSuccess ? made.Value : throw new GameJsonException(made.Error!.Value.Code, $"{where}: {made.Error.Value.Message}");
    }

    private static ScriptedGame ToGame(ScriptedGameDto g, IReadOnlyList<GameRuleSnapshot> rules)
    {
        var rule = rules.FirstOrDefault(r => r.ProfileId == g.Rules) ?? throw new GameJsonException("Game.rules_profile_unknown", $"{g.Id}: 규칙 프로필 '{g.Rules}'이(가) 없습니다.");
        return new ScriptedGame(g.Id, g.Title, rule, Team(g.Away, $"{g.Id}.away"), Team(g.Home, $"{g.Id}.home"),
            Events(g.Events, $"{g.Id}.events", (e, where) => ToEvent(e, where)), g.Expected is null ? null : ToExpected(g.Expected));
    }

    private static TeamSetup Team(TeamDto t, string where) => new(
        t.Lineup.Select(s => new LineupSlot(s.Order, Player(s.Player, where), EnumText.Parse<FieldPosition>(s.Position, where))).ToList(),
        Player(t.Pitcher, where), t.Bench.Select(b => Player(b, where)).ToList());

    // `required` and List<T> only guarantee that a member is present: an explicit JSON null passes both, as a whole list and as an element of one.
    // An optional list that is null stays legal (the callers keep its meaning); a null ELEMENT is always an error with the exact path, never skipped or defaulted.
    private static T Item<T>(T? item, string path) where T : class =>
        item ?? throw new GameJsonException("Fixture.field_missing", $"필수 항목이 비어 있습니다(null): {path}");

    /// <summary>Builds the events of a list ("events" of a legacy array, a format 2 envelope or a scripted game). A null list or a null element fails the whole read.</summary>
    private static List<GameEvent> Events<T>(List<T>? list, string path, Func<T, string, GameEvent> make) where T : class
    {
        var checkedList = list is null ? throw new GameJsonException("Fixture.field_missing", $"필수 항목이 비어 있습니다(null): {path}") : list;
        var events = new List<GameEvent>(checkedList.Count);
        for (var i = 0; i < checkedList.Count; i++)
            events.Add(make(Item(checkedList[i], $"{path}[{i}]"), $"{path}[{i}]"));
        return events;
    }

    private static ImmutableList<PositionCredit>? Credits(List<CreditDto>? list, string where, string field) =>
        list?.Select((c, i) =>
        {
            var credit = Item(c, $"{where}.{field}[{i}]");
            return new PositionCredit(EnumText.Parse<FieldPosition>(credit.Position, where), EnumText.Parse<CreditKind>(credit.Kind, where));
        }).ToImmutableList();

    private static ImmutableList<FieldPosition>? Errors(List<string>? list, string where) =>
        list?.Select((p, i) => EnumText.Parse<FieldPosition>(Item(p, $"{where}.errors[{i}]"), where)).ToImmutableList();

    private static ImmutableList<RunnerMove>? Moves(List<MoveDto>? list, string where) =>
        list?.Select((entry, i) =>
        {
            var m = Item(entry, $"{where}.moves[{i}]");
            return new RunnerMove(m.From, m.To, m.Cause is null ? MoveCause.Play : EnumText.Parse<MoveCause>(m.Cause, where),
                m.OutKind is null ? null : EnumText.Parse<OutKind>(m.OutKind, where), Credits(m.Credits, where, $"moves[{i}].credits"), m.Sequence, m.OutAt,
                m.OutMethod is null ? null : EnumText.Parse<OutMethod>(m.OutMethod, where));
        }).ToImmutableList();

    private static GameEvent ToEvent(EventDto e, string where, PitchLocation? location = null)
    {
        PitchSpec Spec() => new(EnumText.Parse<PitchOutcome>(e.Outcome, where),
            e.Mishap is null ? PitchMishap.None : EnumText.Parse<PitchMishap>(e.Mishap, where), Moves(e.Moves, where),
            e.DroppedThird is null ? PitchMishap.None : EnumText.Parse<PitchMishap>(e.DroppedThird, where), Credits(e.StrikeoutCredits, where, "strikeout_credits"), location);
        return e.Type switch
        {
            "approach" => new ApproachEvent(e.Id, EnumText.Parse<ApproachKind>(e.Approach, where)),
            "pitch" => new PitchEvent(e.Id, Spec()),
            "take" => new TakeEvent(e.Id, Spec()),
            "in_play" => new InPlayEvent(e.Id, EnumText.Parse<BattedBallType>(e.Batted, where), EnumText.Parse<InPlayResult>(e.Result, where), e.Sacrifice ?? false,
                Moves(e.Moves, where), Credits(e.BatterCredits, where, "batter_credits"), Errors(e.Errors, where), e.BatterSequence, location),
            "runner_play" => new RunnerPlayEvent(e.Id, EnumText.Parse<RunnerPlayKind>(e.Kind, where), Moves(e.Moves, where),
                Errors(e.Errors, where)),
            "substitution" => new SubstitutionEvent(e.Id, EnumText.Parse<SubstitutionKind>(e.Kind, where), EnumText.Parse<TeamSide>(e.Team, where),
                e.Player is null ? default : Player(e.Player, where), e.Base, e.Slot, e.Slot2),
            _ => throw new GameJsonException("Fixture.event_type_unknown", $"{where}: 알 수 없는 사건 종류 '{e.Type}'입니다."),
        };
    }

    // ---------------------------------------------------------------- events and event receipts (resume contract, docs/game-engine.md section 3a)

    /// <summary>Reads a legacy (format 1) JSON array of events (the same shape as the <c>events</c> member of a scripted game). Events have no location and get none.</summary>
    public static Result<IReadOnlyList<GameEvent>> ReadEvents(string json)
    {
        try
        {
            using (var document = JsonDocument.Parse(json))
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                    throw new GameJsonException("Fixture.version_unsupported", "이 읽기는 사건 배열(레거시)만 읽습니다. format_version을 가진 사건 묶음은 버전 2 읽기로 읽어야 합니다.");
            var dtos = JsonSerializer.Deserialize<List<EventDto>>(json, WorldJson.Options) ?? throw new GameJsonException("Fixture.json_invalid", "JSON 내용이 비어 있습니다.");
            return Result<IReadOnlyList<GameEvent>>.Success(Events(dtos, "events", (e, where) => ToEvent(e, where)));
        }
        catch (GameJsonException e)
        {
            return Result<IReadOnlyList<GameEvent>>.Failure(new Error(e.Code, e.Message));
        }
        catch (JsonException e)
        {
            return Result<IReadOnlyList<GameEvent>>.Failure(new Error("Fixture.json_invalid", $"JSON 형식이 올바르지 않습니다: {e.Message}"));
        }
    }

    /// <summary>Writes the legacy array. It cannot carry a location, so an event with one is refused (<c>Fixture.location_requires_v2</c>) instead of losing it.</summary>
    public static string WriteEvents(IEnumerable<GameEvent> events)
    {
        var list = events.ToList();
        if (list.FirstOrDefault(e => GameEventLocation.Of(e) is not null) is { } located)
            throw new GameJsonException("Fixture.location_requires_v2", $"사건 '{located.Id}'는 투구 위치를 가지고 있어 레거시 사건 배열로 쓸 수 없습니다. 버전 2 쓰기를 사용하세요.");
        return JsonSerializer.Serialize(list.Select(ToDto).ToList(), WorldJson.Options);
    }

    public const int EventFormatV2 = 2;

    private static bool IsPitchBearing(string? type) => type is "pitch" or "take" or "in_play";

    /// <summary>
    /// Reads the format 2 event envelope <c>{format_version: 2, events: [...]}</c>. Every pitch-bearing event (<c>pitch</c>, <c>take</c>, <c>in_play</c>) must carry a valid
    /// <c>location</c>; nothing is defaulted or corrected. A non-pitch event must not carry one. A bare array (legacy) is not accepted here and is never promoted.
    /// </summary>
    public static Result<IReadOnlyList<GameEvent>> ReadEventsV2(string json)
    {
        try
        {
            using (var document = JsonDocument.Parse(json))
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    throw new GameJsonException("Fixture.version_unsupported", "버전 2 사건 묶음은 format_version을 가진 객체여야 합니다. 사건 배열은 레거시 형식이며 위치를 만들어 주지 않습니다.");
            var dto = JsonSerializer.Deserialize<EventsV2Dto>(json, WorldJson.Options) ?? throw new GameJsonException("Fixture.json_invalid", "JSON 내용이 비어 있습니다.");
            if (dto.FormatVersion != EventFormatV2)
                throw new GameJsonException("Fixture.version_unsupported", $"지원하지 않는 사건 형식 버전입니다: {dto.FormatVersion}");
            return Result<IReadOnlyList<GameEvent>>.Success(Events(dto.Events, "events", ToEventV2));
        }
        catch (GameJsonException e)
        {
            return Result<IReadOnlyList<GameEvent>>.Failure(new Error(e.Code, e.Message));
        }
        catch (JsonException e)
        {
            return Result<IReadOnlyList<GameEvent>>.Failure(new Error("Fixture.json_invalid", $"JSON 형식이 올바르지 않습니다: {e.Message}"));
        }
    }

    private static GameEvent ToEventV2(EventV2Dto e, string where)
    {
        if (!IsPitchBearing(e.Type))
            return e.Location is null ? ToEvent(e, where) : throw new GameJsonException("Fixture.location_unexpected", $"{where}: 투구가 아닌 사건('{e.Type}')에는 위치가 없습니다.");
        var l = e.Location ?? throw new GameJsonException("Fixture.location_missing", $"{where}: 투구 사건('{e.Type}')에는 위치(location)가 필요합니다.");
        var model = ZoneModelVersion.Create(l.ZoneModel);
        if (!model.IsSuccess)
            throw new GameJsonException(model.Error!.Value.Code, $"{where}.location.zone_model: {model.Error.Value.Message}");
        var zone = PitchZoneId.Create(l.Zone);
        if (!zone.IsSuccess)
            throw new GameJsonException(zone.Error!.Value.Code, $"{where}.location.zone: {zone.Error.Value.Message}");
        var made = PitchLocation.Create(model.Value, zone.Value, EnumText.Parse<AppliedBatterSide>(l.Side, $"{where}.location.side"),
            EnumText.Parse<BoundaryClass>(l.Boundary, $"{where}.location.boundary"));
        if (!made.IsSuccess)
            throw new GameJsonException(made.Error!.Value.Code, $"{where}.location: {made.Error.Value.Message}");
        return ToEvent(e, where, made.Value);
    }

    /// <summary>Writes the format 2 envelope. A pitch-bearing event without a location is refused (<c>Fixture.location_missing</c>): new output never lacks one.</summary>
    public static string WriteEventsV2(IEnumerable<GameEvent> events) => JsonSerializer.Serialize(new EventsV2Dto
    {
        FormatVersion = EventFormatV2,
        Events = events.Select(ToDtoV2).ToList(),
    }, WorldJson.Options);

    private static EventV2Dto ToDtoV2(GameEvent e)
    {
        var location = GameEventLocation.Of(e);
        if (location is null && e is PitchEvent or TakeEvent or InPlayEvent)
            throw new GameJsonException("Fixture.location_missing", $"사건 '{e.Id}'는 투구 사건이지만 위치가 없어 버전 2로 쓸 수 없습니다.");
        var d = ToDto(e);
        return new EventV2Dto
        {
            Id = d.Id, Type = d.Type, Approach = d.Approach, Outcome = d.Outcome, Mishap = d.Mishap, DroppedThird = d.DroppedThird, Moves = d.Moves,
            StrikeoutCredits = d.StrikeoutCredits, Batted = d.Batted, Result = d.Result, Sacrifice = d.Sacrifice, BatterCredits = d.BatterCredits, Errors = d.Errors,
            BatterSequence = d.BatterSequence, Kind = d.Kind, Team = d.Team, Player = d.Player, Base = d.Base, Slot = d.Slot, Slot2 = d.Slot2,
            Location = location is null ? null : new EventLocationDto
            {
                ZoneModel = location.Model.Value, Zone = location.Zone.Value, Side = Text(location.Side), Boundary = Text(location.Boundary),
            },
        };
    }

    private static string Text<T>(T value) where T : struct, Enum => SnakeCase.From(value.ToString());

    private static List<CreditDto>? CreditsDto(ImmutableList<PositionCredit>? list) =>
        list?.Select(c => new CreditDto { Position = Text(c.Position), Kind = Text(c.Kind) }).ToList();

    private static List<MoveDto>? MovesDto(ImmutableList<RunnerMove>? list) =>
        list?.Select(m => new MoveDto
        {
            From = m.From, To = m.To, Cause = Text(m.Cause), OutKind = m.OutKind is { } k ? Text(k) : null, Credits = CreditsDto(m.Credits),
            Sequence = m.Sequence, OutAt = m.OutAt, OutMethod = m.Method is { } method ? Text(method) : null,
        }).ToList();

    private static EventDto ToDto(GameEvent e) => e switch
    {
        ApproachEvent a => new EventDto { Id = a.Id, Type = "approach", Approach = Text(a.Approach) },
        PitchEvent p => PitchDto(p.Id, "pitch", p.Spec),
        TakeEvent t => PitchDto(t.Id, "take", t.Spec),
        InPlayEvent i => new EventDto
        {
            Id = i.Id, Type = "in_play", Batted = Text(i.Batted), Result = Text(i.Result), Sacrifice = i.Sacrifice, Moves = MovesDto(i.Moves),
            BatterCredits = CreditsDto(i.BatterCredits), Errors = i.Errors?.Select(Text).ToList(), BatterSequence = i.BatterSequence,
        },
        RunnerPlayEvent r => new EventDto { Id = r.Id, Type = "runner_play", Kind = Text(r.Kind), Moves = MovesDto(r.Moves), Errors = r.Errors?.Select(Text).ToList() },
        SubstitutionEvent s => new EventDto
        {
            Id = s.Id, Type = "substitution", Kind = Text(s.Kind), Team = Text(s.Team), Player = s.Player == default ? null : s.Player.Value,
            Base = s.Base, Slot = s.Slot, Slot2 = s.Slot2,
        },
        _ => throw new GameJsonException("Fixture.event_type_unknown", "알 수 없는 사건 종류입니다."),
    };

    private static EventDto PitchDto(string id, string type, PitchSpec spec) => new()
    {
        Id = id, Type = type, Outcome = Text(spec.Outcome), Mishap = Text(spec.Mishap), DroppedThird = Text(spec.DroppedThirdStrike),
        Moves = MovesDto(spec.Moves), StrikeoutCredits = CreditsDto(spec.StrikeoutCredits),
    };

    private static ExpectedGame ToExpected(ExpectedDto x)
    {
        static IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Nested(Dictionary<string, Dictionary<string, int>> d) =>
            d.ToDictionary(kv => kv.Key, kv => (IReadOnlyDictionary<string, int>)kv.Value);
        return new ExpectedGame(x.Outcome, x.AwayRuns, x.HomeRuns, x.AwayLine, x.HomeLine, x.AwayHits, x.HomeHits, x.AwayErrors, x.HomeErrors, x.AwayLob, x.HomeLob,
            Nested(x.Players), Nested(x.Catchers), Nested(x.Batteries), x.Decisions, x.Appearances);
    }
}

internal sealed class EventReceiptDto
{
    public required string EventId { get; set; }
    public required string Kind { get; set; }
    public required string FingerprintVersion { get; set; }
    public required string Fingerprint { get; set; }
}

/// <summary>
/// The event receipts of a game, as they belong in a resumable game-state fixture: <c>[{event_id, kind, fingerprint_version, fingerprint}]</c> in
/// processing order. The fingerprint rules are those of <c>EventFingerprint</c> (version <c>ef1</c>); no SQLite mapping exists before Phase 4.
/// </summary>
internal static class EventReceiptJson
{
    public static string Write(IEnumerable<EventReceipt> receipts) =>
        JsonSerializer.Serialize(receipts.Select(r => new EventReceiptDto { EventId = r.EventId, Kind = r.Kind, FingerprintVersion = r.FingerprintVersion, Fingerprint = r.Fingerprint }).ToList(), WorldJson.Options);

    public static Result<IReadOnlyList<EventReceipt>> Read(string json)
    {
        try
        {
            var dtos = JsonSerializer.Deserialize<List<EventReceiptDto>>(json, WorldJson.Options) ?? throw new GameJsonException("Fixture.json_invalid", "JSON 내용이 비어 있습니다.");
            if (dtos.Select(d => d.EventId).Distinct().Count() != dtos.Count)
                throw new GameJsonException("Fixture.duplicate_id", "사건 영수증의 사건 ID가 중복되었습니다.");
            return Result<IReadOnlyList<EventReceipt>>.Success(dtos.Select(d => new EventReceipt(d.EventId, d.Kind, d.FingerprintVersion, d.Fingerprint)).ToList());
        }
        catch (GameJsonException e)
        {
            return Result<IReadOnlyList<EventReceipt>>.Failure(new Error(e.Code, e.Message));
        }
        catch (JsonException e)
        {
            return Result<IReadOnlyList<EventReceipt>>.Failure(new Error("Fixture.json_invalid", $"JSON 형식이 올바르지 않습니다: {e.Message}"));
        }
    }
}
