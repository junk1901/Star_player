using System.Collections.Immutable;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;

namespace StarPlayer.Domain.Baseball.Game;

public enum PlayKind { PlateAppearance, Steal, CaughtStealing, Pickoff, Balk, WildPitch, PassedBall, OtherRunnerPlay }

public enum BatterResult
{
    Single, Double, Triple, HomeRun, Walk, HitByPitch, Strikeout, StrikeoutReachedBase,
    Out, FieldersChoice, ReachedOnError, SacrificeFly, SacrificeBunt,
}

/// <summary>How the rules classify an out for the third-out run rule: made on the batter-runner, a force out, or any other out.</summary>
public enum OutClassification { BatterRunner, Force, NonForce }

/// <summary>
/// A resolved runner result with its cause, kept as scorer evidence. <c>Sequence</c> is the resolved order of the play's outs and runs
/// (0 for a non-terminal move), <c>Classification</c> is set for outs (and for an error that saved a runner from an out: what the out would
/// have been), <c>OutAt</c> and <c>Method</c> describe where and how, and <c>RunVoided</c> is true when the runner crossed the plate but the
/// run did not count (third out of the inning made first or by a force/batter-runner out).
/// </summary>
public sealed record RunnerMoveRecord(int RunnerKey, PlayerId Runner, int From, int To, MoveCause Cause, OutKind? OutKind, PlayerId ResponsiblePitcher,
    int Sequence = 0, OutClassification? Classification = null, int? OutAt = null, OutMethod? Method = null, bool RunVoided = false);

public sealed record FielderCredit(FieldPosition Position, PlayerId Player, CreditKind Kind);

/// <summary>A run that scored, with who is charged and who gets the RBI. The earned/unearned verdict is decided when the half-inning closes.</summary>
public sealed record ScoredRun(int RunnerKey, PlayerId Runner, PlayerId ResponsiblePitcher, MoveCause Cause, PlayerId? RbiBatter);

/// <summary>
/// The official evidence for one scoring-relevant play: a plate-appearance result, steal, pickoff, balk, wild pitch or passed
/// ball. Every change of the score has a ScoredPlay with the runs it produced. WP and PB stay distinct kinds and causes.
/// </summary>
public sealed record ScoredPlay(
    string Id,
    string EventId,
    int Sequence,
    int Inning,
    InningHalf Half,
    int OutsBefore,
    int OutsAfter,
    TeamSide Batting,
    PlayKind Kind,
    PlayerId? Batter,
    PlayerId Pitcher,
    PlayerId BattingTeamPitcher,
    PlayerId Catcher,
    BatterResult? Result,
    BattedBallType Batted,
    PitchCountState FinalCount,
    ImmutableList<RunnerMoveRecord> Moves,
    ImmutableList<FielderCredit> Credits,
    ImmutableList<FieldPosition> Errors,
    ImmutableList<ScoredRun> Runs,
    int AwayRunsAfter,
    int HomeRunsAfter,
    bool IsDoublePlay);

public enum StatKey
{
    BatPA, BatAB, BatH, Bat1B, Bat2B, Bat3B, BatHR, BatBB, BatHBP, BatSO, BatSF, BatSH, BatRBI, BatGIDP, BatROE, BatFC,
    RunR, RunSB, RunCS, RunPicked,
    PitchOuts, PitchBF, PitchH, PitchR, PitchER, PitchBB, PitchK, PitchHBP, PitchHR, PitchWP, PitchBK, PitchPitches,
    FldPO, FldA, FldE, FldDP, FldPB,
    CatPitches, CatOuts, CatSBA, CatCS,
    BatteryPitches, BatteryOuts, BatteryBF,
    TeamLOB,

    /// <summary>Earned runs of the team as a whole (OBR 9.16 reconstruction of the half-inning from its start); an individual pitcher's <see cref="PitchER"/> is kept separately (9.16(i)).</summary>
    TeamER,
}

/// <summary>One counting fact with its source event id. Player is the default id for team-level facts; Partner is the pitcher of a battery fact.</summary>
public sealed record StatEntry(TeamSide Team, PlayerId Player, StatKey Key, int Amount, string SourceId, PlayerId? Partner = null);

/// <summary>The counting facts one reducer step produced, each tagged with its source event.</summary>
public sealed record GameStatDelta(string SourceEventId, ImmutableList<StatEntry> Entries)
{
    public static GameStatDelta Empty(string eventId) => new(eventId, ImmutableList<StatEntry>.Empty);
}

/// <summary>Actual catching in this game: counted from real pitches and outs while the player was the catcher, not from a C label.</summary>
public sealed record CatcherExposure(string GameId, TeamSide Team, PlayerId Catcher, int PitchesReceived, int OutsWhileCatching, int StolenBasesAllowed, int CaughtStealing, int PassedBalls);

/// <summary>One pitcher-catcher pair that really worked together. <c>SharedAppearances</c> is 1 for a game however many events the pair shared.</summary>
public sealed record BatteryExposure(string GameId, TeamSide Team, PlayerId Catcher, PlayerId Pitcher, int Pitches, int Outs, int BattersFaced, int SharedAppearances);

public enum DecisionKind { Win, Loss, Save }

public sealed record PitcherDecision(TeamSide Team, PlayerId Pitcher, DecisionKind Kind);

/// <summary>A player's summed counting stats for the game (only non-zero keys), in fixed key order.</summary>
public sealed record PlayerStatLine(TeamSide Team, PlayerId Player, ImmutableSortedDictionary<StatKey, int> Stats)
{
    public int Get(StatKey key) => Stats.TryGetValue(key, out var value) ? value : 0;
}

/// <summary>A player's one participation in the game; a two-way player appears once even if he both bats and pitches.</summary>
public sealed record GameAppearance(TeamSide Team, PlayerId Player, bool InBattingOrder, bool Pitched);

public sealed record TeamTotals(int Runs, int Hits, int Errors, int LeftOnBase);

public sealed record BoxScore(
    string GameId,
    ImmutableList<int> AwayLine,
    ImmutableList<int> HomeLine,
    TeamTotals Away,
    TeamTotals Home,
    ImmutableList<PlayerStatLine> Players,
    ImmutableList<GameAppearance> Appearances,
    ImmutableList<PitcherDecision> Decisions,
    ImmutableList<CatcherExposure> CatcherExposures,
    ImmutableList<BatteryExposure> BatteryExposures);

/// <summary>The one-time result of finishing a game. Finalizing again returns this same value, never a second one.</summary>
public sealed record FinalizedGame(string GameId, GameOutcome Outcome, int AwayRuns, int HomeRuns, string RulesProfileId, string RulesContentHash, BoxScore BoxScore,
    GameExecution? Execution = null);

public enum GameActionKind { Approach, Take, Pitch, InPlay, RunnerPlay, Substitution, Finalize }

/// <summary>What the game will accept next. Anything else is rejected with an explicit error.</summary>
public sealed record LegalActionSet(PaStage Stage, ImmutableList<GameActionKind> Actions, bool DefenseComplete, bool IsOver)
{
    public bool Allows(GameActionKind kind) => Actions.Contains(kind);
}

// Typed facts that precede any narrative text (ROADMAP Phase 2 "Events").
public abstract record GameFact(string GameId);

public sealed record PlateAppearanceCompleted(string GameId, string PlayId, PlayerId Batter, PlayerId Pitcher, BatterResult Result, int RunsScored) : GameFact(GameId);

public sealed record RunnerPlayCompleted(string GameId, string PlayId, PlayKind Kind, int RunsScored, int OutsMade) : GameFact(GameId);

public sealed record SubstitutionMade(string GameId, string EventId, TeamSide Team, SubstitutionKind Kind, PlayerId Out, PlayerId In) : GameFact(GameId);

public sealed record GameCompleted(string GameId, GameOutcome Outcome, int AwayRuns, int HomeRuns) : GameFact(GameId);

/// <summary>Result of applying one event. A duplicate event id returns the unchanged state with <see cref="WasDuplicate"/> and no new evidence.</summary>
public sealed record GameStep(GameState State, GameStatDelta Delta, ImmutableList<ScoredPlay> NewPlays, ImmutableList<GameFact> Facts, bool WasDuplicate);

/// <summary>Result of finalizing: the final game and whether this call created it.</summary>
public sealed record FinalizeResult(GameState State, FinalizedGame Game, bool WasNew);
