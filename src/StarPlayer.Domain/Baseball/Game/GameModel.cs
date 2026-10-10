using System.Collections.Immutable;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;

namespace StarPlayer.Domain.Baseball.Game;

public enum PaStage
{
    /// <summary>New plate appearance, 0-0: the player may choose an approach or an explicit Take.</summary>
    Start,

    /// <summary>Inside a plate appearance after a Take: the exact count is kept and control is back with the player.</summary>
    Decision,

    /// <summary>An approach is chosen and the real pitch sequence is running.</summary>
    InSequence,
}

public enum GameStatus { InProgress, Final }

public enum GameOutcome { AwayWin, HomeWin, Tie }

/// <summary>The real balls/strikes of the plate appearance. It is authoritative state, never presentation decoration.</summary>
public readonly record struct PitchCountState(int Balls, int Strikes)
{
    public static PitchCountState Zero => default;

    public override string ToString() => $"{Balls}-{Strikes}";
}

/// <summary>
/// A runner on base. <c>RunnerKey</c> is unique in the game and survives a pinch runner, <c>ResponsiblePitcher</c> is who is
/// charged if the runner scores (inherited runners stay with their original pitcher), <c>LineupOrder</c> is the batting-order slot.
/// </summary>
public sealed record BaseRunner(PlayerId Player, PlayerId ResponsiblePitcher, int RunnerKey, int LineupOrder);

/// <summary>Occupancy of first, second and third base. At most one runner per base by construction of the reducer.</summary>
public sealed record RunnerState(BaseRunner? First = null, BaseRunner? Second = null, BaseRunner? Third = null)
{
    public static RunnerState Empty { get; } = new();

    public int Count => (First is null ? 0 : 1) + (Second is null ? 0 : 1) + (Third is null ? 0 : 1);

    public BaseRunner? At(int @base) => @base switch { 1 => First, 2 => Second, 3 => Third, _ => null };

    public RunnerState With(int @base, BaseRunner? runner) => @base switch
    {
        1 => this with { First = runner },
        2 => this with { Second = runner },
        3 => this with { Third = runner },
        _ => throw new ArgumentOutOfRangeException(nameof(@base)),
    };

    public IEnumerable<(int Base, BaseRunner Runner)> Occupied()
    {
        if (First is not null) yield return (1, First);
        if (Second is not null) yield return (2, Second);
        if (Third is not null) yield return (3, Third);
    }
}

/// <summary>One batting-order slot: who bats there and the defensive position that player holds (DH, or Pitcher when no DH).</summary>
public sealed record LineupSlot(int Order, PlayerId Player, FieldPosition Position);

/// <summary>A pitcher's stint in this game, used for legality, decisions and the one-time battery exposure.</summary>
public sealed record PitcherUsage(PlayerId Pitcher, int EntrySequence, int EntryInning, InningHalf EntryHalf, int EnteredWithLead, int EnteredRunnersOnBase,
    int OutsRecorded, int BattersFaced, int PitchCount);

/// <summary>
/// Everything the game must know about one team: lineup, defense, pitcher stints, bench and who may not return. <c>DhActive</c> is this
/// team's own designated-hitter state during the game: it starts as the profile's <c>DesignatedHitter</c>, can only turn false (when the
/// profile allows the forfeiture transitions) and never turns true again; the opposing team's state is separate. <c>StartingDh</c> is the DH named in the
/// starting lineup (null without a DH); it decides the OBR 5.11(a)(2) first-plate-appearance duty and the 5.11(b) two-way exception.
/// </summary>
public sealed record TeamGameState(
    ImmutableList<LineupSlot> Lineup,
    PlayerId Pitcher,
    PlayerId StartingPitcher,
    ImmutableList<PitcherUsage> PitcherUsages,
    ImmutableList<PlayerId> Bench,
    ImmutableList<PlayerId> Removed,
    ImmutableList<PlayerId> Appeared,
    ImmutableList<PlayerId> EverInLineup,
    int NextBatterIndex,
    bool DhActive,
    PlayerId? StartingDh = null)
{
    /// <summary>OBR 5.11(b): the starting pitcher listed a second time as the DH. Fixed by the starting lineup; nobody else can ever hold this exception.</summary>
    public bool HasTwoWayStarter => StartingDh is { } dh && dh == StartingPitcher;

    public LineupSlot SlotByOrder(int order) => Lineup.Single(s => s.Order == order);

    public LineupSlot? SlotOf(PlayerId player) => Lineup.FirstOrDefault(s => s.Player == player);

    public LineupSlot? SlotAt(FieldPosition position) => Lineup.FirstOrDefault(s => s.Position == position);

    public LineupSlot CurrentBatterSlot => SlotByOrder(NextBatterIndex % 9 + 1);
}

public sealed record SubstitutionRecord(string EventId, int Inning, InningHalf Half, TeamSide Team, SubstitutionKind Kind, PlayerId Out, PlayerId In, int Slot, FieldPosition Position);

/// <summary>
/// What the game remembers about one processed event so the same id can be recognised safely: the id, the event kind and a deterministic
/// SHA-256 fingerprint of the normalised payload (<c>EventFingerprint</c>, version in <see cref="FingerprintVersion"/>). The same id with the
/// same fingerprint is a harmless duplicate; the same id with another kind or payload is a conflict.
/// </summary>
public sealed record EventReceipt(string EventId, string Kind, string FingerprintVersion, string Fingerprint);

/// <summary>Setup for one team at the start of a game.</summary>
public sealed record TeamSetup(IReadOnlyList<LineupSlot> Lineup, PlayerId StartingPitcher, IReadOnlyList<PlayerId> Bench);

public sealed record GameStart(string GameId, GameRuleSnapshot Rules, TeamSetup Away, TeamSetup Home);

/// <summary>
/// The complete, resumable state of one game. It is immutable: every reducer call returns a new value and a rejected event
/// returns an error and leaves this one untouched. Counts, outs and bases are authoritative; the plays, stat entries and
/// substitutions are append-only evidence from which every official number is rebuilt.
/// </summary>
public sealed record GameState(
    string GameId,
    GameRuleSnapshot Rules,
    int Inning,
    InningHalf Half,
    int Outs,
    PitchCountState Count,
    RunnerState Bases,
    int AwayRuns,
    int HomeRuns,
    ImmutableList<int> AwayLine,
    ImmutableList<int> HomeLine,
    PaStage Stage,
    ApproachKind? Approach,
    PlayerId? WalkChargeOverride,
    TeamGameState Away,
    TeamGameState Home,
    ImmutableList<ScoredPlay> Plays,
    ImmutableList<StatEntry> Stats,
    ImmutableList<SubstitutionRecord> Substitutions,
    ImmutableList<EventReceipt> AppliedEvents,
    int NextRunnerKey,
    GameStatus Status,
    GameOutcome? Outcome,
    FinalizedGame? Final)
{
    public TeamSide BattingSide => Half == InningHalf.Top ? TeamSide.Away : TeamSide.Home;

    public TeamSide FieldingSide => Half == InningHalf.Top ? TeamSide.Home : TeamSide.Away;

    public TeamGameState Team(TeamSide side) => side == TeamSide.Away ? Away : Home;

    public TeamGameState Batting => Team(BattingSide);

    public TeamGameState Fielding => Team(FieldingSide);

    public int Runs(TeamSide side) => side == TeamSide.Away ? AwayRuns : HomeRuns;

    /// <summary>The player now defending at <paramref name="position"/> for the fielding team (the pitcher for <see cref="FieldPosition.Pitcher"/>).</summary>
    public PlayerId? DefenderAt(TeamSide side, FieldPosition position) =>
        position == FieldPosition.Pitcher ? Team(side).Pitcher : Team(side).SlotAt(position)?.Player;

    public PlayerId Catcher(TeamSide side) => Team(side).SlotAt(FieldPosition.Catcher)!.Player;
}
