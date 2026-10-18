using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.SimHarness;

/// <summary>
/// What an input set is FOR (F-A/B scope decision 6). <see cref="Neutral"/> isolates the engine: every rating 50, so the pitch-level rates (zone, swing, swing-and-miss) and the arithmetic of
/// every metric can be checked. <see cref="VirtualLeague"/> is the extension point of the later league-average validation (F1-F5); this phase gives it an explicit way to be supplied and
/// recorded but defines NO representative rating distribution and NO strategy mix, so no result of it is an official verdict.
/// </summary>
public enum CohortKind { Neutral, VirtualLeague }

/// <summary>The handedness relation of every plate appearance of a game (ADR-008 FC-D1.3: a property of the game, from its starters and lineups, never of an observed rate).</summary>
public enum HandednessStratum { Same, Opposite, Mixed }

/// <summary>
/// A deterministic per-game-index team rotation (ADR-008 FC-D1 / FC-D2, proposal §3.3 / §4): in game <c>g</c> (from 1) each team starts <c>Starters[(g - 1) mod n]</c>, the other starters sit on the bench,
/// and with <see cref="RotateBattingOrder"/> the batting order is shifted by <c>(g - 1) mod 9</c> (slot <c>k</c> gets the player of base slot <c>((k - 1 + s) mod 9) + 1</c>, positions stay with the
/// players). It is part of the cohort's <see cref="CohortInputs.InputsHash"/>; there is no RNG and no clock in it.
/// </summary>
public sealed record CohortRotation(string Id, ImmutableArray<PlayerId> AwayStarters, ImmutableArray<PlayerId> HomeStarters, bool RotateBattingOrder);

/// <summary>
/// The players of one run: both team setups, every player's sheet and the content-version-3 <see cref="PlayerInputSet"/> the game pins. <see cref="InputsHash"/> is the harness' own identity of
/// EVERYTHING the run reads from them (lineups, bench, bats / throws, every hitter, pitcher and catcher rating), because the engine's input-set identity covers only the six ratings its slots read
/// (fielding, speed, arm, framing, game calling, control) and not contact, power, eye, stuff or velocity.
/// <para>
/// The cohort is an immutable SNAPSHOT: <see cref="Create"/> copies the sheets into an <see cref="ImmutableSortedDictionary{TKey,TValue}"/> (PlayerId order, so enumeration is the same in every process) and both team setups into new <see cref="TeamSetup"/> values over
/// <see cref="ImmutableArray{T}"/>, and computes <see cref="InputsHash"/> from those copies alone. Nothing the caller keeps (its sheet list, its lineup or bench lists) or casts back from a property can change
/// what a game reads after the hash was taken. A <see cref="PlayerSheet"/> needs no copy: the ratings the engine reads are immutable values (the repertoire is a read-only copy).
/// </para>
/// </summary>
public sealed class CohortInputs
{
    private CohortInputs(string cohortId, CohortKind kind, string description, TeamSetup away, TeamSetup home, ImmutableSortedDictionary<PlayerId, PlayerSheet> sheets, PlayerInputSet inputSet, string inputsHash,
        CohortRotation? rotation)
    {
        Rotation = rotation;
        CohortId = cohortId;
        Kind = kind;
        Description = description;
        Away = away;
        Home = home;
        Sheets = sheets;
        InputSet = inputSet;
        InputsHash = inputsHash;
    }

    public string CohortId { get; }

    public CohortKind Kind { get; }

    public string Description { get; }

    public TeamSetup Away { get; }

    public TeamSetup Home { get; }

    public ImmutableSortedDictionary<PlayerId, PlayerSheet> Sheets { get; }

    public PlayerInputSet InputSet { get; }

    public string InputsHash { get; }

    /// <summary>The per-game rotation, or null: every game then uses <see cref="Away"/> / <see cref="Home"/> unchanged (the F-A/B behaviour, so <c>neutral-v1</c> and its hash are unchanged).</summary>
    public CohortRotation? Rotation { get; }

    /// <summary>The team setups of game <paramref name="gameIndex"/> (from 1): <see cref="Away"/> / <see cref="Home"/> without a rotation, else the rotation applied to them.</summary>
    public (TeamSetup Away, TeamSetup Home) SetupFor(int gameIndex)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(gameIndex, 1);
        if (Rotation is not { } r)
            return (Away, Home);
        return (Rotate(Away, r.AwayStarters, r.RotateBattingOrder, gameIndex), Rotate(Home, r.HomeStarters, r.RotateBattingOrder, gameIndex));
    }

    private static TeamSetup Rotate(TeamSetup team, ImmutableArray<PlayerId> starters, bool battingOrder, int gameIndex)
    {
        var starter = starters[(gameIndex - 1) % starters.Length];
        var bench = team.Bench.Prepend(team.StartingPitcher).Where(p => p != starter).ToImmutableArray();
        var lineup = team.Lineup.OrderBy(s => s.Order).ToList();
        var shift = battingOrder ? (gameIndex - 1) % lineup.Count : 0;
        var rotated = lineup.Select((_, k) => lineup[(k + shift) % lineup.Count] with { Order = k + 1 }).ToImmutableArray();
        return new TeamSetup(rotated, starter, bench);
    }

    /// <summary>
    /// The handedness stratum of game <paramref name="gameIndex"/>: Same when both starters face only batters who bat on their throwing side, Opposite when only batters of the other side, Mixed
    /// otherwise (a switch hitter, or a lineup with both sides). The harness has no substitution, so the starters pitch the whole game.
    /// </summary>
    public HandednessStratum StratumOf(int gameIndex)
    {
        var (away, home) = SetupFor(gameIndex);
        var relations = Relations(away.StartingPitcher, home).Concat(Relations(home.StartingPitcher, away)).Distinct().ToList();
        return relations.Count == 1 ? relations[0] : HandednessStratum.Mixed;
    }

    private IEnumerable<HandednessStratum> Relations(PlayerId pitcher, TeamSetup batters)
    {
        var throws = Sheets[pitcher].Player.Throws;
        foreach (var slot in batters.Lineup)
            yield return Sheets[slot.Player].Player.Bats switch
            {
                BattingSide.Right => throws == ThrowingHand.Right ? HandednessStratum.Same : HandednessStratum.Opposite,
                BattingSide.Left => throws == ThrowingHand.Left ? HandednessStratum.Same : HandednessStratum.Opposite,
                _ => HandednessStratum.Mixed,
            };
    }

    /// <summary>
    /// Builds an input set from explicit sheets. Every rostered player needs a sheet, a sheet for nobody on the rosters is refused (it would be an unrecorded input), and the engine's own
    /// checks (lineup, positions, catcher / pitcher ratings the model reads) run when a game starts and are never pre-empted or defaulted here.
    /// </summary>
    public static Result<CohortInputs> Create(string? cohortId, CohortKind kind, string? description, TeamSetup? away, TeamSetup? home, IEnumerable<PlayerSheet>? sheets, CohortRotation? rotation = null)
    {
        static Result<CohortInputs> Fail(string code, string message) => Result<CohortInputs>.Failure(new Error(code, message));
        if (string.IsNullOrWhiteSpace(cohortId) || cohortId.Length > 64 || !cohortId.All(c => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-' or '.'))
            return Fail("Harness.cohort_id_invalid", "코호트 ID는 영문·숫자·'_'·'-'·'.'로 된 1~64자여야 합니다.");
        if (!Enum.IsDefined(kind))
            return Fail("Harness.cohort_kind_invalid", "코호트 종류가 올바르지 않습니다.");
        if (away is null || home is null || sheets is null || description is null)
            return Fail("Harness.cohort_incomplete", "코호트에 팀 구성, 선수 목록, 설명이 모두 있어야 합니다.");

        var snapshotAway = Snapshot(away);
        var snapshotHome = Snapshot(home);
        if (snapshotAway is null || snapshotHome is null)
            return Fail("Harness.cohort_incomplete", "팀 구성의 라인업·벤치 목록과 라인업 항목이 모두 있어야 합니다.");
        away = snapshotAway;
        home = snapshotHome;

        var byId = new Dictionary<PlayerId, PlayerSheet>();
        foreach (var sheet in sheets)
            if (sheet is null || !byId.TryAdd(sheet.Id, sheet))
                return Fail("Harness.cohort_sheet_invalid", "선수 정보가 비어 있거나 같은 선수가 두 번 있습니다.");
        var rostered = Roster(away).Concat(Roster(home)).ToList();
        if (rostered.Any(p => !byId.ContainsKey(p)))
            return Fail("Harness.cohort_sheet_missing", "팀 구성에 있는 선수의 능력치 정보가 없습니다.");
        if (byId.Keys.Any(p => !rostered.Contains(p)))
            return Fail("Harness.cohort_sheet_unused", "팀 구성에 없는 선수의 능력치 정보가 있습니다(기록되지 않는 입력).");

        if (rotation is not null && RotationProblem(rotation, away, home) is { } problem)
            return Fail("Harness.cohort_rotation_invalid", problem);

        var set = PlayerInputSet.CreateForGameCalling(byId.Values);
        if (!set.IsSuccess)
            return Result<CohortInputs>.Failure(set.Error!.Value);
        var snapshot = byId.ToImmutableSortedDictionary(IdOrder);                            // the sheets the games will read; the hash below is taken from this very copy
        return Result<CohortInputs>.Success(new CohortInputs(cohortId, kind, description, away, home, snapshot, set.Value, Hash(cohortId, kind, away, home, snapshot, rotation), rotation));
    }

    /// <summary>An independent copy of a team setup over immutable arrays, or null when a list or one of its items is missing (the caller's lists are never retained).</summary>
    private static TeamSetup? Snapshot(TeamSetup team) =>
        team.Lineup is null || team.Bench is null || team.Lineup.Any(s => s is null)
            ? null
            : new TeamSetup(team.Lineup.ToImmutableArray(), team.StartingPitcher, team.Bench.ToImmutableArray());

    /// <summary>A rotation must start with the base starter, list each team's own bench pitchers at most once, and (for the batting order) have a nine-man lineup.</summary>
    private static string? RotationProblem(CohortRotation r, TeamSetup away, TeamSetup home)
    {
        if (string.IsNullOrWhiteSpace(r.Id) || r.Id.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.')))
            return "로테이션 ID가 올바르지 않습니다.";
        foreach (var (team, starters) in new[] { (away, r.AwayStarters), (home, r.HomeStarters) })
        {
            if (starters.IsDefaultOrEmpty || starters.Distinct().Count() != starters.Length || starters[0] != team.StartingPitcher)
                return "로테이션 선발 목록은 비어 있지 않고 중복이 없으며 기준 선발 투수로 시작해야 합니다.";
            if (starters.Skip(1).Any(p => !team.Bench.Contains(p)))
                return "로테이션 선발은 그 팀의 벤치(로스터)에 있어야 합니다.";
            if (r.RotateBattingOrder && team.Lineup.Count != 9)
                return "타순 로테이션은 9명 라인업이 필요합니다.";
        }

        return null;
    }

    private static readonly IComparer<PlayerId> IdOrder = Comparer<PlayerId>.Create((a, b) => string.CompareOrdinal(a.Value, b.Value));

    private static IEnumerable<PlayerId> Roster(TeamSetup team) => team.Lineup.Select(s => s.Player).Append(team.StartingPitcher).Concat(team.Bench);

    private static string Hash(string cohortId, CohortKind kind, TeamSetup away, TeamSetup home, ImmutableSortedDictionary<PlayerId, PlayerSheet> sheets, CohortRotation? rotation)
    {
        var text = new StringBuilder();
        text.Append("cohort-inputs-v1\n").Append(kind).Append('|').Append(cohortId).Append('\n');
        foreach (var (label, team) in new[] { ("A", away), ("H", home) })
        {
            foreach (var s in team.Lineup.OrderBy(s => s.Order))
                text.Append(label).Append("|L|").Append(s.Order).Append('|').Append(s.Player.Value).Append('|').Append(s.Position).Append('\n');
            text.Append(label).Append("|SP|").Append(team.StartingPitcher.Value).Append('\n');
            foreach (var b in team.Bench)
                text.Append(label).Append("|B|").Append(b.Value).Append('\n');
        }

        foreach (var sheet in sheets.Values.OrderBy(s => s.Id.Value, StringComparer.Ordinal))
        {
            var r = sheet.Ratings;
            text.Append(sheet.Id.Value).Append("|bats=").Append(sheet.Player.Bats).Append("|throws=").Append(sheet.Player.Throws)
                .Append("|H=").Append(r.Hitter is { } h ? string.Join(',', new[] { h.Contact, h.Power, h.Eye, h.Speed, h.Fielding, h.Arm }.Select(v => v.Value.ToString(CultureInfo.InvariantCulture))) : "-")
                .Append("|P=").Append(r.Pitcher is { } p
                    ? string.Join(',', new[] { p.Stuff, p.Control, p.Velocity, p.Endurance }.Select(v => v.Value.ToString(CultureInfo.InvariantCulture)))
                      + ";" + string.Join(';', p.Repertoire.Pitches.Select(x => $"{x.Type}={x.Grade.Value}"))
                    : "-")
                .Append("|C=").Append(r.Catcher is { } c ? string.Join(',', new[] { c.ReceivingFraming, c.Blocking, c.PopTime, c.GameCalling }.Select(v => v.Value.ToString(CultureInfo.InvariantCulture))) : "-")
                .Append('\n');
        }

        if (rotation is not null)                                                               // absent for a non-rotating cohort, so its F-A/B hash text is unchanged
        {
            text.Append("rotation=").Append(rotation.Id).Append("|batting_order=").Append(rotation.RotateBattingOrder ? "shift9" : "fixed").Append('\n');
            foreach (var (label, starters) in new[] { ("A", rotation.AwayStarters), ("H", rotation.HomeStarters) })
                for (var i = 0; i < starters.Length; i++)
                    text.Append(label).Append("|R|").Append(i).Append('|').Append(starters[i].Value).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))).ToLowerInvariant();
    }
}

/// <summary>
/// The neutral cohort (scope decision 6): two fictional clubs of identical players, every rating (hitting, pitching, catcher tools, running, fielding, arm) 50, all right-handed, with the
/// lineup shape of the existing execution fixtures (designated hitter, nine batters, a starting pitcher who does not bat, a two-player bench). Same-hand matchups mean the engine's
/// <c>matchup.handedness</c> table applies its <c>same_hand</c> factor to every pitch: the table has no neutral key for a real batter and pitcher, so a handedness-free cohort cannot exist.
/// </summary>
public static class NeutralCohort
{
    public const string Id = "neutral-v1";

    /// <summary>ADR-008 FC-D4 / proposal §6.2: the opposite-hand cohort of the strategy matrix — <c>neutral-v1</c> with the starter and reliever throwing left (batters still bat right, every rating 50).</summary>
    public const string OppositeId = "neutral-opp-v1";

    public static CohortInputs Create() => Build(Id, ThrowingHand.Right, "모든 능력치 50, 우투우타, 지명타자 라인업(중립 코호트)");

    public static CohortInputs CreateOpposite() => Build(OppositeId, ThrowingHand.Left, "모든 능력치 50, 우타자, 선발·구원 좌투, 지명타자 라인업(반대 손 중립 코호트)");

    private static CohortInputs Build(string cohortId, ThrowingHand pitcherThrows, string description)
    {
        var sheets = new List<PlayerSheet>();
        TeamSetup Team(string label, string club)
        {
            var positions = LineupPositions;
            var lineup = new List<LineupSlot>();
            for (var i = 0; i < positions.Length; i++)
            {
                var id = Pid($"sim-{label}-b{i + 1}");
                sheets.Add(Sheet(id, club, $"가상 {label} 타자 {i + 1}", ThrowingHand.Right, pitcher: false, hitter: true, catcher: positions[i] == FieldPosition.Catcher));
                lineup.Add(new LineupSlot(i + 1, id, positions[i]));
            }

            var starter = Pid($"sim-{label}-sp");
            sheets.Add(Sheet(starter, club, $"가상 {label} 선발", pitcherThrows, pitcher: true, hitter: false, catcher: false));
            var benchHitter = Pid($"sim-{label}-bh");
            var benchPitcher = Pid($"sim-{label}-bp");
            sheets.Add(Sheet(benchHitter, club, $"가상 {label} 벤치 타자", ThrowingHand.Right, pitcher: false, hitter: true, catcher: false));
            sheets.Add(Sheet(benchPitcher, club, $"가상 {label} 구원", pitcherThrows, pitcher: true, hitter: false, catcher: false));
            return new TeamSetup(lineup, starter, [benchHitter, benchPitcher]);
        }

        var away = Team("a", "club-sim-a");
        var home = Team("h", "club-sim-h");
        return Must(CohortInputs.Create(cohortId, CohortKind.Neutral, description, away, home, sheets));
    }

    internal static PlayerId Pid(string text) => Must(PlayerId.Create(text));

    internal static SkillValue S(int value = 50) => Must(SkillValue.Create(value));

    internal static readonly FieldPosition[] LineupPositions = [FieldPosition.CenterField, FieldPosition.Shortstop, FieldPosition.RightField, FieldPosition.DesignatedHitter, FieldPosition.LeftField,
        FieldPosition.FirstBase, FieldPosition.ThirdBase, FieldPosition.SecondBase, FieldPosition.Catcher];

    private static PlayerSheet Sheet(PlayerId id, string club, string name, ThrowingHand throws, bool pitcher, bool hitter, bool catcher) =>
        MakeSheet(id, club, name, throws, hitter ? new HitterRatings(S(), S(), S(), S(), S(), S()) : null, pitcher ? Pitcher(50, 50, 50) : null,
            catcher ? new CatcherRatings(S(), S(), S(), S()) : null);

    /// <summary>A pitcher's ratings: the given Stuff / Control / Velocity, Endurance 50 (no fatigue input exists) and a Fastball-50 repertoire.</summary>
    internal static PitcherRatings Pitcher(int stuff, int control, int velocity) =>
        new(S(stuff), S(control), S(velocity), S(), Must(PitchRepertoire.Create([new PitchRating(PitchType.Fastball, S())])));

    /// <summary>One fictional player of a harness cohort: bats right, the given throwing hand, state 50, a pitcher prefers the mound and everyone else centre field.</summary>
    internal static PlayerSheet MakeSheet(PlayerId id, string club, string name, ThrowingHand throws, HitterRatings? h, PitcherRatings? p, CatcherRatings? c)
    {
        var player = Must(Player.Create(id, name, new DateOnly(2000, 1, 1), throws, BattingSide.Right, Must(BodyMeasurements.Create(180, 80)), [], ""));
        var status = Must(PlayerStatus.Create([new OriginRecord(OriginType.HighSchool, null, null, null)], EntryPath.Draft, RegistrationStatus.Registered, LeagueAssignment.FirstTeam,
            [new ClubAffiliation(Must(ClubId.Create(club)), new DateOnly(2020, 1, 1), null)], CareerStatus.Active));
        return Must(PlayerSheet.Create(player, Must(Ratings.Create(h, p, c)), new PlayerState(S(), S(), S(), S(), S(), S()),
            Must(ProficiencyProfile.Create(p is not null ? FieldPosition.Pitcher : FieldPosition.CenterField, Array.Empty<(FieldPosition, SkillValue)>(), [])), status));
    }

    internal static T Must<T>(Result<T> result) =>
        result.IsSuccess ? result.Value : throw new InvalidOperationException($"코호트 구성 오류: {result.Error!.Value.Code} {result.Error.Value.Message}");
}
