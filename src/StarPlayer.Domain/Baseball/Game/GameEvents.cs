using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;

namespace StarPlayer.Domain.Baseball.Game;

// Inputs of the deterministic reducer. Every event has a stable id (idempotency key). There is no probability anywhere:
// a scripted source or, from Phase 3, a resolver supplies these facts and the reducer only checks and applies them.
// Bases are numbers: 0 = the batter (as a source) or "out" (as a destination), 1-3 = first to third, 4 = home / scored.

public enum TeamSide { Away, Home }

public enum InningHalf { Top, Bottom }

/// <summary>Plate-appearance approach chosen by the player (D1). Phase 2 records it and gives it no effect.</summary>
public enum ApproachKind { Normal, Patient, Aggressive }

public enum PitchOutcome { Ball, CalledStrike, SwingingStrike, Foul, FoulBunt, HitByPitch }

/// <summary>A pitch that got away from the battery. Wild pitch is the pitcher's, passed ball the catcher's; they are never merged.</summary>
public enum PitchMishap { None, WildPitch, PassedBall }

public enum BattedBallType { None, Ground, Fly, Line, Pop, Bunt }

public enum InPlayResult { Single, Double, Triple, HomeRun, Out, FieldersChoice, ReachedOnError }

/// <summary>
/// <c>Appeal</c> exists only so that an appeal request can be named and refused: Star Player does not support appeal plays (missed base, failed
/// retouch, appeal third outs, fourth-out appeals) by owner decision of 2026-10-11 (docs/game-engine.md section 4a).
/// </summary>
public enum RunnerPlayKind { Steal, Pickoff, Balk, Other, Appeal }

/// <summary>Why a runner moved; it drives RBI and the earned-run reconstruction.</summary>
public enum MoveCause { Play, Forced, WildPitch, PassedBall, Steal, Balk, ErrorExtraBase, ErrorSavedOut }

/// <summary>
/// The rule category of a runner out. It is declared with the event and then CHECKED against the state: <c>Force</c> only when the runner
/// really was forced at that moment (batter-runner not yet retired, every base behind him still occupied by a runner not yet retired) and
/// the out was made at the base he was forced to; <c>Tag</c>/<c>Other</c> only when he was not.
/// </summary>
public enum OutKind { Force, Tag, Other }

/// <summary>The physical way an out was made. It never decides whether the out is a force out (the state does).</summary>
public enum OutMethod { Tag, BaseTouch }

public enum CreditKind { Putout, Assist }

public enum SubstitutionKind
{
    PinchHit, PinchRun, DefensiveSub, PositionSwap, PitchingChange,

    /// <summary>The designated hitter takes a fielding position (profiles with DhMayBeForfeited; OBR 5.11(a)(12)); the DH role ends for that team.</summary>
    DhToField,

    /// <summary>A request to name a designated hitter during play. It is never legal: the DH is fixed at the start and cannot be re-created.</summary>
    NameDesignatedHitter,
}

/// <summary>A fielder credit by defensive position; the reducer resolves the player from the current defense.</summary>
public readonly record struct PositionCredit(FieldPosition Position, CreditKind Kind);

/// <summary>
/// One runner's result on a play. <c>From</c> 0 is the batter; <c>To</c> 0 is out, 4 is scoring.
/// <c>Sequence</c> is the order (1, 2, 3...) in which the play's outs and runs happened; it is required for every out and run of a play
/// that has a third out and a run (the order decides whether the run counts) and optional otherwise, but all or none. <c>OutAt</c> is the
/// base where an out was made (default: the next base, <c>From + 1</c>; <c>From</c> for a pickoff back at his base). <c>OutKind</c> is
/// required for every runner out and checked against the state; <c>Method</c> is the physical way (tag or base touch).
/// </summary>
public sealed record RunnerMove(int From, int To, MoveCause Cause = MoveCause.Play, OutKind? OutKind = null, ImmutableList<PositionCredit>? Credits = null,
    int? Sequence = null, int? OutAt = null, OutMethod? Method = null)
{
    public ImmutableList<PositionCredit> CreditList => Credits ?? ImmutableList<PositionCredit>.Empty;
}

/// <summary>
/// Facts of one actual pitch. A dropped third strike is only legal with first base open or two outs; the batter then reaches first.
/// <c>Location</c> is the confirmed Pitch Zone Lite location (zone, applied batter side, boundary class; never the pitcher's target). Phase 2 events have
/// none (legacy data, never inferred); the Phase 3 pitch resolver always supplies one. An event with a location is identified by fingerprint <c>ef2</c>.
/// </summary>
public sealed record PitchSpec(PitchOutcome Outcome, PitchMishap Mishap = PitchMishap.None, ImmutableList<RunnerMove>? Moves = null,
    PitchMishap DroppedThirdStrike = PitchMishap.None, ImmutableList<PositionCredit>? StrikeoutCredits = null, PitchLocation? Location = null)
{
    public ImmutableList<RunnerMove> MoveList => Moves ?? ImmutableList<RunnerMove>.Empty;
}

public abstract record GameEvent(string Id);

/// <summary>Chooses the approach for the plate appearance; the real pitch sequence then follows as <see cref="PitchEvent"/>s.</summary>
public sealed record ApproachEvent(string Id, ApproachKind Approach) : GameEvent(Id);

/// <summary>One pitch inside an approach-driven plate appearance.</summary>
public sealed record PitchEvent(string Id, PitchSpec Spec) : GameEvent(Id);

/// <summary>The explicit one-pitch action (D1): consumes exactly one actual pitch, then hands control back if the PA continues.</summary>
public sealed record TakeEvent(string Id, PitchSpec Spec) : GameEvent(Id);

/// <summary>
/// A batted-ball result that ends the plate appearance. <c>BatterSequence</c> orders the batter's own out or home run among the play's other
/// outs and runs (see <see cref="RunnerMove"/>); a batter caught out on a fly/line/pop ball is always first. It stands for one actual pitch by itself
/// (no <see cref="PitchEvent"/> precedes it), so its optional <c>Location</c> is that pitch's confirmed location (see <see cref="PitchSpec"/>).
/// </summary>
public sealed record InPlayEvent(string Id, BattedBallType Batted, InPlayResult Result, bool Sacrifice = false, ImmutableList<RunnerMove>? Moves = null,
    ImmutableList<PositionCredit>? BatterCredits = null, ImmutableList<FieldPosition>? Errors = null, int? BatterSequence = null, PitchLocation? Location = null) : GameEvent(Id)
{
    public ImmutableList<RunnerMove> MoveList => Moves ?? ImmutableList<RunnerMove>.Empty;
    public ImmutableList<PositionCredit> BatterCreditList => BatterCredits ?? ImmutableList<PositionCredit>.Empty;
    public ImmutableList<FieldPosition> ErrorList => Errors ?? ImmutableList<FieldPosition>.Empty;
}

/// <summary>A play with runners that is not a plate-appearance result: steal, pickoff, balk or another runner event.</summary>
public sealed record RunnerPlayEvent(string Id, RunnerPlayKind Kind, ImmutableList<RunnerMove>? Moves = null, ImmutableList<FieldPosition>? Errors = null) : GameEvent(Id)
{
    public ImmutableList<RunnerMove> MoveList => Moves ?? ImmutableList<RunnerMove>.Empty;
    public ImmutableList<FieldPosition> ErrorList => Errors ?? ImmutableList<FieldPosition>.Empty;
}

/// <summary>
/// A roster move. <c>Team</c> is the side making it. PinchHit replaces the batter now due; PinchRun uses <c>Base</c>;
/// DefensiveSub uses <c>Slot</c> (batting-order number 1-9); PositionSwap uses <c>Slot</c> and <c>Slot2</c>; PitchingChange
/// brings <c>Player</c> (bench, or a lineup player who may legally pitch) to the mound.
/// </summary>
public sealed record SubstitutionEvent(string Id, SubstitutionKind Kind, TeamSide Team, PlayerId Player = default, int? Base = null, int? Slot = null, int? Slot2 = null) : GameEvent(Id);

/// <summary>The pitch location an event carries, if any. Only <see cref="PitchEvent"/>, <see cref="TakeEvent"/> (through <see cref="PitchSpec"/>) and <see cref="InPlayEvent"/> can carry one; no other event ever does.</summary>
public static class GameEventLocation
{
    public static PitchLocation? Of(GameEvent e) => e switch
    {
        PitchEvent p => p.Spec.Location,
        TakeEvent t => t.Spec.Location,
        InPlayEvent i => i.Location,
        _ => null,
    };
}
