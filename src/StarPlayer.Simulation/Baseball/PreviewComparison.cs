using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;

namespace StarPlayer.Simulation.Baseball;

/// <summary>
/// The structural comparison of a candidate's unconfirmed preview with the step the reducer produced again on the authoritative state (C3-B, game-engine 27.18 B-03).
/// It names the FIRST differing field path (<c>null</c> = identical). Every member of <see cref="GameState"/>, <see cref="TeamGameState"/>, <see cref="ScoredPlay"/>,
/// <see cref="GameStep"/> and <see cref="GameStatDelta"/> is compared by an explicit line below, collections by content and order (never by <c>ImmutableList</c> reference; <see cref="GameState.Execution"/> by record value, so a different park snapshot, tuning pin or policy is a difference), so
/// a member added to one of those types is not compared until someone adds it here; field-inventory tests fail first and force that review. The leaf records inside the
/// collections (runners, lineup slots, receipts, moves, credits, stat entries, facts ...) hold only scalars, enums and value-type ids, so their value equality is structural;
/// a test pins that too. <see cref="GameState.Rules"/> is compared by reference: the reducer preserves it and a rebuilt snapshot is a different profile instance.
/// </summary>
internal static class PreviewComparison
{
    public static string? FirstDifference(GameStep? preview, GameStep? official)
    {
        if (preview is null || official is null)
            return preview is null && official is null ? null : "Step";
        return State(preview.State, official.State, "Step.State")
            ?? Seq(preview.NewPlays, official.NewPlays, "Step.NewPlays", Play)
            ?? Delta(preview.Delta, official.Delta, "Step.Delta")
            ?? Seq(preview.Facts, official.Facts, "Step.Facts", Same)
            ?? Scalar(preview.WasDuplicate, official.WasDuplicate, "Step.WasDuplicate");
    }

    private static string? State(GameState? a, GameState? b, string p)
    {
        if (a is null || b is null)
            return a is null && b is null ? null : p;
        return Scalar(a.GameId, b.GameId, p + ".GameId")
            ?? Ref(a.Rules, b.Rules, p + ".Rules")
            ?? Scalar(a.Execution, b.Execution, p + ".Execution")
            ?? Scalar(a.Inning, b.Inning, p + ".Inning")
            ?? Scalar(a.Half, b.Half, p + ".Half")
            ?? Scalar(a.Outs, b.Outs, p + ".Outs")
            ?? Scalar(a.Count, b.Count, p + ".Count")
            ?? Scalar(a.Bases.First, b.Bases.First, p + ".Bases.First")
            ?? Scalar(a.Bases.Second, b.Bases.Second, p + ".Bases.Second")
            ?? Scalar(a.Bases.Third, b.Bases.Third, p + ".Bases.Third")
            ?? Scalar(a.AwayRuns, b.AwayRuns, p + ".AwayRuns")
            ?? Scalar(a.HomeRuns, b.HomeRuns, p + ".HomeRuns")
            ?? Seq(a.AwayLine, b.AwayLine, p + ".AwayLine", Same)
            ?? Seq(a.HomeLine, b.HomeLine, p + ".HomeLine", Same)
            ?? Scalar(a.Stage, b.Stage, p + ".Stage")
            ?? Scalar(a.Approach, b.Approach, p + ".Approach")
            ?? Scalar(a.WalkChargeOverride, b.WalkChargeOverride, p + ".WalkChargeOverride")
            ?? Team(a.Away, b.Away, p + ".Away")
            ?? Team(a.Home, b.Home, p + ".Home")
            ?? Seq(a.Plays, b.Plays, p + ".Plays", Play)
            ?? Seq(a.Stats, b.Stats, p + ".Stats", Same)
            ?? Seq(a.Substitutions, b.Substitutions, p + ".Substitutions", Same)
            ?? Seq(a.AppliedEvents, b.AppliedEvents, p + ".AppliedEvents", Same)
            ?? Scalar(a.NextRunnerKey, b.NextRunnerKey, p + ".NextRunnerKey")
            ?? Scalar(a.Status, b.Status, p + ".Status")
            ?? Scalar(a.Outcome, b.Outcome, p + ".Outcome")
            ?? Final(a, b, p + ".Final");
    }

    /// <summary>A preview and a re-run are both produced by <c>Apply</c>, which never finalizes: both must still be <c>null</c> here (<c>Finalize</c> comes later in the same stage). Anything else is a difference.</summary>
    private static string? Final(GameState a, GameState b, string p) => a.Final is null && b.Final is null ? null : p;

    private static string? Team(TeamGameState? a, TeamGameState? b, string p)
    {
        if (a is null || b is null)
            return a is null && b is null ? null : p;
        return Seq(a.Lineup, b.Lineup, p + ".Lineup", Same)
            ?? Scalar(a.Pitcher, b.Pitcher, p + ".Pitcher")
            ?? Scalar(a.StartingPitcher, b.StartingPitcher, p + ".StartingPitcher")
            ?? Seq(a.PitcherUsages, b.PitcherUsages, p + ".PitcherUsages", Same)
            ?? Seq(a.Bench, b.Bench, p + ".Bench", Same)
            ?? Seq(a.Removed, b.Removed, p + ".Removed", Same)
            ?? Seq(a.Appeared, b.Appeared, p + ".Appeared", Same)
            ?? Seq(a.EverInLineup, b.EverInLineup, p + ".EverInLineup", Same)
            ?? Scalar(a.NextBatterIndex, b.NextBatterIndex, p + ".NextBatterIndex")
            ?? Scalar(a.DhActive, b.DhActive, p + ".DhActive")
            ?? Scalar(a.StartingDh, b.StartingDh, p + ".StartingDh");
    }

    private static string? Play(ScoredPlay? a, ScoredPlay? b, string p)
    {
        if (a is null || b is null)
            return a is null && b is null ? null : p;
        return Scalar(a.Id, b.Id, p + ".Id")
            ?? Scalar(a.EventId, b.EventId, p + ".EventId")
            ?? Scalar(a.Sequence, b.Sequence, p + ".Sequence")
            ?? Scalar(a.Inning, b.Inning, p + ".Inning")
            ?? Scalar(a.Half, b.Half, p + ".Half")
            ?? Scalar(a.OutsBefore, b.OutsBefore, p + ".OutsBefore")
            ?? Scalar(a.OutsAfter, b.OutsAfter, p + ".OutsAfter")
            ?? Scalar(a.Batting, b.Batting, p + ".Batting")
            ?? Scalar(a.Kind, b.Kind, p + ".Kind")
            ?? Scalar(a.Batter, b.Batter, p + ".Batter")
            ?? Scalar(a.Pitcher, b.Pitcher, p + ".Pitcher")
            ?? Scalar(a.BattingTeamPitcher, b.BattingTeamPitcher, p + ".BattingTeamPitcher")
            ?? Scalar(a.Catcher, b.Catcher, p + ".Catcher")
            ?? Scalar(a.Result, b.Result, p + ".Result")
            ?? Scalar(a.Batted, b.Batted, p + ".Batted")
            ?? Scalar(a.FinalCount, b.FinalCount, p + ".FinalCount")
            ?? Seq(a.Moves, b.Moves, p + ".Moves", Same)
            ?? Seq(a.Credits, b.Credits, p + ".Credits", Same)
            ?? Seq(a.Errors, b.Errors, p + ".Errors", Same)
            ?? Seq(a.Runs, b.Runs, p + ".Runs", Same)
            ?? Scalar(a.AwayRunsAfter, b.AwayRunsAfter, p + ".AwayRunsAfter")
            ?? Scalar(a.HomeRunsAfter, b.HomeRunsAfter, p + ".HomeRunsAfter")
            ?? Scalar(a.IsDoublePlay, b.IsDoublePlay, p + ".IsDoublePlay");
    }

    private static string? Delta(GameStatDelta? a, GameStatDelta? b, string p)
    {
        if (a is null || b is null)
            return a is null && b is null ? null : p;
        return Scalar(a.SourceEventId, b.SourceEventId, p + ".SourceEventId") ?? Seq(a.Entries, b.Entries, p + ".Entries", Same);
    }

    private static string? Scalar<T>(T a, T b, string path) => EqualityComparer<T>.Default.Equals(a, b) ? null : path;

    private static string? Ref(object? a, object? b, string path) => ReferenceEquals(a, b) ? null : path;

    private static string? Same<T>(T? a, T? b, string path) => EqualityComparer<T?>.Default.Equals(a, b) ? null : path;

    /// <summary>Content and order: the count first (<c>path.Count</c>), then each element in turn (<c>path[i]</c> or the element comparer's deeper path).</summary>
    private static string? Seq<T>(ImmutableList<T>? a, ImmutableList<T>? b, string path, Func<T?, T?, string, string?> element)
    {
        if (a is null || b is null)
            return a is null && b is null ? null : path;
        if (a.Count != b.Count)
            return path + ".Count";
        for (var i = 0; i < a.Count; i++)
            if (element(a[i], b[i], $"{path}[{i}]") is { } difference)
                return difference;
        return null;
    }
}
