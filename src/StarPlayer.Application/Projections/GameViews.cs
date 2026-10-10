namespace StarPlayer.Application.Projections;

// Public read models for the Phase 2 game diagnostic screen. Plain strings, numbers and bools only: no Domain type, no
// way to mutate a game. Everything shown is committed state (the reducer's own count, outs, bases and official evidence).

public sealed record ScriptedGameListItem(string Id, string Title, string RulesLabel, int EventCount);

public sealed record BatterLineView(string Team, string Player, int PlateAppearances, int AtBats, int Runs, int Hits, int RunsBattedIn, int Walks, int Strikeouts, int HomeRuns, int StolenBases);

public sealed record PitcherLineView(string Team, string Player, string InningsPitched, int Hits, int Runs, int EarnedRuns, int Walks, int Strikeouts, int HomeRuns, int WildPitches, int Pitches, string Decision);

public sealed record CatcherLineView(string Team, string Player, int PitchesReceived, int OutsWhileCatching, int StolenBasesAllowed, int CaughtStealing, int PassedBalls);

public sealed record BatteryLineView(string Team, string Catcher, string Pitcher, int Pitches, int Outs, int BattersFaced);

public sealed record BoxScoreView(
    IReadOnlyList<BatterLineView> Batters,
    IReadOnlyList<PitcherLineView> Pitchers,
    IReadOnlyList<CatcherLineView> Catchers,
    IReadOnlyList<BatteryLineView> Batteries,
    string AwayTotals,
    string HomeTotals,
    IReadOnlyList<string> Decisions);

public sealed record GameView(
    string GameId,
    string Title,
    string RulesLabel,
    string RulesStatusLabel,
    string InningLabel,
    int Balls,
    int Strikes,
    int Outs,
    bool FirstOccupied,
    bool SecondOccupied,
    bool ThirdOccupied,
    string FirstRunner,
    string SecondRunner,
    string ThirdRunner,
    int AwayRuns,
    int HomeRuns,
    int AwayHits,
    int HomeHits,
    int AwayErrors,
    int HomeErrors,
    IReadOnlyList<string> InningHeaders,
    IReadOnlyList<string> AwayLine,
    IReadOnlyList<string> HomeLine,
    string CurrentBatter,
    string CurrentPitcher,
    string StageLabel,
    IReadOnlyList<string> LegalActions,
    string LastEventText,
    string NextEventText,
    int EventsApplied,
    int EventsTotal,
    bool IsOver,
    string ResultText,
    BoxScoreView? BoxScore);

/// <summary>Outcome of a diagnostic game command: exactly one of <see cref="View"/> or the error pair is set.</summary>
public sealed record GameViewResult(GameView? View, string? ErrorCode, string? ErrorMessage)
{
    public bool IsSuccess => View is not null;

    public static GameViewResult Success(GameView view) => new(view, null, null);

    public static GameViewResult Failure(string code, string message) => new(null, code, message);
}
