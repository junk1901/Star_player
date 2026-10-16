using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;

namespace StarPlayer.Simulation.Baseball;

// Phase 3-D v2 Part B (owner decisions R1, S1): the immutable player input set a game reads its fielding / running / throwing slots from. It holds only the three base ratings the new
// slots need (D-ABILITY-1: the base rating of the current PlayerSheet; no condition, fatigue or position proficiency) and is resolver-internal: exact NPC ratings never reach a client.

/// <summary>
/// One player's base fielding, running and throwing ratings (0-100), taken from the shared <see cref="HitterRatings"/>. A player without hitter ratings (a pure pitcher) has none of the
/// three; the value is then <c>null</c> and asking for it is an error, never a default of 50.
/// <para>
/// <see cref="ReceivingFraming"/> (Phase 3-E, content version 2 only) is the player's base <c>ReceivingFraming</c> (0-100), or <c>null</c> for a player without catcher ratings. A set of content
/// version 1 never captures it - it is always <c>null</c> there AND absent from that version's identity text - so a v1 set can never carry a meaningful value its identity ignores.
/// </para>
/// </summary>
public sealed record PlayerRunFieldInput(PlayerId Player, int? Fielding, int? Speed, int? Arm, int? ReceivingFraming = null);

/// <summary>
/// An immutable snapshot of the players a game may read (S1). It is built once from trusted <see cref="PlayerSheet"/>s, copies what it needs and keeps no sheet, delegate or mutable
/// collection, so nothing that happens to a sheet afterwards can change it. <see cref="Id"/> is the SHA-256 of a canonical text of every entry in PlayerId order: it is the identity a
/// <see cref="GameExecution.PlayerInputsId"/> pins and a <see cref="PreparedPitch"/> binds. It is an identity, not a proof: the entries a prepared pitch used are compared as values too.
/// <para>
/// <b>Content versions (Phase 3-E).</b> <see cref="ContentVersion"/> is part of the type, never guessed from the identity text. Version 1 (<see cref="ContentV1"/>, made by <see cref="Create"/>) is the
/// original format and its identities are unchanged; version 2 (<see cref="ContentV2"/>, made by <see cref="CreateForFraming"/>) adds <c>ReceivingFraming</c> to every entry and to the identity
/// text. The two are different identities even for the same sheets. A later phase that stores a set must record the content version, the content AND the identity (ROADMAP Phase 4 note).
/// </para>
/// </summary>
public sealed class PlayerInputSet
{
    /// <summary>The original content format: Fielding, Speed and Arm per player.</summary>
    public const string ContentV1 = "player-input-set-v1";

    /// <summary>Phase 3-E: v1 plus <c>ReceivingFraming</c> per player (null token <c>-</c> for a player without catcher ratings).</summary>
    public const string ContentV2 = "player-input-set-v2";

    private readonly ImmutableDictionary<PlayerId, PlayerRunFieldInput> _byPlayer;

    private PlayerInputSet(string contentVersion, ImmutableArray<PlayerRunFieldInput> entries, string id)
    {
        ContentVersion = contentVersion;
        Entries = entries;
        _byPlayer = entries.ToImmutableDictionary(e => e.Player);
        Id = id;
    }

    /// <summary>Which content format this set (and its identity) has: <see cref="ContentV1"/> or <see cref="ContentV2"/>.</summary>
    public string ContentVersion { get; }

    /// <summary>The entries in PlayerId order.</summary>
    public ImmutableArray<PlayerRunFieldInput> Entries { get; }

    public string Id { get; }

    public int Count => Entries.Length;

    public PlayerRunFieldInput? Find(PlayerId player) => _byPlayer.TryGetValue(player, out var found) ? found : null;

    /// <summary>A null set, a null or repeated sheet is an error (nothing is skipped or merged). Content version 1: Fielding, Speed, Arm; its identity is exactly the one it always had.</summary>
    public static Result<PlayerInputSet> Create(IEnumerable<PlayerSheet?>? sheets) => Build(sheets, withFraming: false);

    /// <summary>The same, in content version 2: every entry also holds the player's base <c>ReceivingFraming</c> (null for a player without catcher ratings) and the identity covers it.</summary>
    public static Result<PlayerInputSet> CreateForFraming(IEnumerable<PlayerSheet?>? sheets) => Build(sheets, withFraming: true);

    private static Result<PlayerInputSet> Build(IEnumerable<PlayerSheet?>? sheets, bool withFraming)
    {
        static Result<PlayerInputSet> Fail(string code, string message) => Result<PlayerInputSet>.Failure(new Error(code, message));

        if (sheets is null)
            return Fail("PlayerInputSet.sheets_missing", "선수 입력 집합을 만들 선수 정보 목록이 없습니다.");
        var entries = new Dictionary<PlayerId, PlayerRunFieldInput>();
        foreach (var sheet in sheets)
        {
            if (sheet is null)
                return Fail("PlayerInputSet.sheet_missing", "선수 입력 집합에 빈 선수 정보가 있습니다.");
            var hitter = sheet.Ratings.Hitter;
            var entry = new PlayerRunFieldInput(sheet.Id, hitter?.Fielding.Value, hitter?.Speed.Value, hitter?.Arm.Value, withFraming ? sheet.Ratings.Catcher?.ReceivingFraming.Value : null);
            if (!entries.TryAdd(sheet.Id, entry))
                return Fail("PlayerInputSet.duplicate_player", $"선수 {sheet.Id}의 정보가 선수 입력 집합에 두 번 있습니다.");
        }

        var ordered = entries.Values.OrderBy(e => e.Player.Value, StringComparer.Ordinal).ToImmutableArray();
        var version = withFraming ? ContentV2 : ContentV1;
        return Result<PlayerInputSet>.Success(new PlayerInputSet(version, ordered, Digest(version, ordered)));
    }

    private static string Digest(string version, ImmutableArray<PlayerRunFieldInput> ordered)
    {
        static string N(int? v) => v is { } n ? n.ToString(CultureInfo.InvariantCulture) : "-";
        // v1 lines are byte-for-byte what they were before Phase 3-E; v2 appends the framing token to the same line
        var lines = version == ContentV2
            ? ordered.Select(e => $"{e.Player.Value}|fielding={N(e.Fielding)}|speed={N(e.Speed)}|arm={N(e.Arm)}|framing={N(e.ReceivingFraming)}")
            : ordered.Select(e => $"{e.Player.Value}|fielding={N(e.Fielding)}|speed={N(e.Speed)}|arm={N(e.Arm)}");
        var text = string.Join("\n", lines.Prepend(version));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    public override string ToString() => $"PlayerInputSet({Count} players, {ContentVersion})";
}

/// <summary>One representative fielder fixed for a pitch: the position, the player standing there in the game state, and that player's base fielding rating.</summary>
public sealed record FieldingInput(FieldPosition Position, PlayerId Player, int Fielding);

/// <summary>
/// The fielding inputs of one pitch (owner decision R1). A pitch is prepared BEFORE its batted ball exists, so the three representative positions - shortstop (ground and line balls),
/// centre field (fly balls) and second base (pop-ups) - are read from the game state and the <see cref="PlayerInputSet"/> up front and frozen here. The fielder is the representative of
/// the batted-ball class in this prototype's fixed-fielder model (ADR-007 U-3); it is NOT necessarily the player later credited with the putout or the retriever of a hit. No random
/// number chooses a fielder, and the set's identity travels with the inputs so a changed rating or defender is found when the preparation is used (S1).
/// </summary>
public sealed class PitchFieldingInputs
{
    /// <summary>The positions whose fielders can be responsible for some batted ball.</summary>
    public static readonly ImmutableArray<FieldPosition> Positions = [FieldPosition.Shortstop, FieldPosition.CenterField, FieldPosition.SecondBase];

    internal PitchFieldingInputs(string inputSetId, ImmutableArray<FieldingInput> entries)
    {
        InputSetId = inputSetId;
        Entries = entries;
    }

    public string InputSetId { get; }

    /// <summary>One entry per position of <see cref="Positions"/>, in that order.</summary>
    public ImmutableArray<FieldingInput> Entries { get; }

    /// <summary>The representative position of a batted-ball class: ground and line to the shortstop, fly to the centre fielder, pop-up to the second baseman; null for any other key.</summary>
    public static FieldPosition? ResponsiblePosition(string? battedBall) => battedBall switch
    {
        null => null,
        "popup" => FieldPosition.SecondBase,
        _ when battedBall.StartsWith("ground_", StringComparison.Ordinal) || battedBall.StartsWith("line_", StringComparison.Ordinal) => FieldPosition.Shortstop,
        _ when battedBall.StartsWith("fly_", StringComparison.Ordinal) => FieldPosition.CenterField,
        _ => null,
    };

    /// <summary>The representative fielder's rating for a batted-ball condition key, or null when the key is not a batted ball.</summary>
    public int? FieldingFor(string? battedBall) =>
        ResponsiblePosition(battedBall) is { } position ? Entries.First(e => e.Position == position).Fielding : null;

    /// <summary>
    /// Reads the current defenders of the FIELDING team from <paramref name="state"/> and their ratings from <paramref name="set"/>. A missing defender, a defender who is not in the set
    /// and a defender without hitter ratings are errors (codes <c>{prefix}.defender_missing</c>, <c>defender_input_missing</c>, <c>defender_ratings_missing</c>); nothing is defaulted to 50.
    /// </summary>
    internal static Result<PitchFieldingInputs> Build(GameState state, PlayerInputSet set, string prefix)
    {
        var entries = ImmutableArray.CreateBuilder<FieldingInput>(Positions.Length);
        foreach (var position in Positions)
        {
            if (state.DefenderAt(state.FieldingSide, position) is not { } defender)
                return Fail(prefix + ".defender_missing", $"수비 위치 {position}에 선수가 없어 수비 입력을 고정할 수 없습니다.");
            if (set.Find(defender) is not { } input)
                return Fail(prefix + ".defender_input_missing", $"{position} 수비수 {defender}가 선수 입력 집합에 없습니다.");
            if (input.Fielding is not { } fielding)
                return Fail(prefix + ".defender_ratings_missing", $"{position} 수비수 {defender}에게 타자(수비) 능력치가 없어 수비 보정을 만들 수 없습니다.");
            entries.Add(new FieldingInput(position, defender, fielding));
        }

        return Result<PitchFieldingInputs>.Success(new PitchFieldingInputs(set.Id, entries.MoveToImmutable()));
    }

    /// <summary>Value equality of the identity and every entry.</summary>
    internal bool SameAs(PitchFieldingInputs? other) =>
        other is not null && InputSetId == other.InputSetId && Entries.SequenceEqual(other.Entries);

    private static Result<PitchFieldingInputs> Fail(string code, string message) => Result<PitchFieldingInputs>.Failure(new Error(code, message));

    public override string ToString() => $"PitchFieldingInputs({Entries.Length} fielders)";
}
