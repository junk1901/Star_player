using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Common;

namespace StarPlayer.Simulation.Baseball;

// Phase 3-C2B-3 (P-05, P-06, P-09): turns a composed play into the real InPlayEvent and compares what the UNCHANGED GameReducer derived from it with what the rules expect.
// The event carries only an actual InPlayResult (a caught fly ball is Out; InPlayResult has no SacrificeFly): the official batter result (BatterResult.SacrificeFly), the runs,
// the RBI, the at-bat and the left-on-base numbers are the reducer's to derive, are compared here and are NEVER written back into an event after Apply.

/// <summary>What the reducer derived from the final event (audit data, P-09): kept apart from the event input.</summary>
internal sealed record AdvancementReducerRecord(BatterResult Result, int Runs, int Rbi, bool AtBat, int OutsAfter, bool HalfOver, int LeftOnBase, bool GameEnded);

internal static class AdvancementEventBuilder
{
    /// <summary>The final event: the base plan's batted ball, real result, credits and location, with every runner's final move. An out carries the tag-out details of the approved contexts.</summary>
    public static InPlayEvent Build(InPlayPlanFacts facts, AdvancementComposition composition, string eventId, PitchLocation? location)
    {
        var moves = composition.Moves.Select(m =>
        {
            var move = new RunnerMove(m.From, m.To, m.Cause, Sequence: m.Sequence);
            if (m.IsOut && m.Context is { } context)
            {
                // every approved attempt is a non-force tag out at the base after the one the base plan reached; the retriever's assist and the base fielder's putout credit it
                move = move with
                {
                    OutKind = OutKind.Tag, OutAt = context.TargetTo, Method = OutMethod.Tag,
                    Credits = ImmutableList.Create(new PositionCredit(context.Retriever, CreditKind.Assist), new PositionCredit(context.BaseFielder, CreditKind.Putout)),
                };
            }

            return move;
        });
        return InPlayBasePlanner.BuildEvent(facts, eventId, location) with { Moves = moves.ToImmutableList() };
    }

    /// <summary>
    /// Compares the reducer's verdict with the composition, in this order: the checks every in-play candidate shares (a refusal is passed through, a duplicate, the ef2 receipt, exactly
    /// one counted pitch); then the scored play against the rules: batter result, counted runs (the unchanged third-out rule over the final sequence), outs, half-inning end, a game
    /// that ends exactly with the winning run, one record per runner with its final base, sequence and voided flag, the winning scorer, credits and errors, and the reducer's own
    /// RBI, at-bat and left-on-base numbers. Returns the first problem or null; <paramref name="record"/> is set only on success. The numbers are never recomputed into the play.
    /// </summary>
    public static Error? Verify(InPlayPlanFacts facts, GameState original, AdvancementComposition composition, InPlayEvent candidate, Result<GameStep> applied,
        out AdvancementReducerRecord? record)
    {
        record = null;
        if (InPlayPitchPlanning.VerifyApplied(original, candidate, applied) is { } basic)
            return basic;
        var step = applied.Value;
        var play = step.NewPlays.Count == 1 ? step.NewPlays[0] : null;
        if (play is null)
            return Invalid("the step has no single scored play");

        var limit = original.Rules.OutsPerHalfInning;
        var batterOut = facts.Official.Result == InPlayResult.Out;
        var runnerOuts = composition.Moves.Count(m => m.IsOut);
        var runs = composition.Moves.Where(m => m.IsRun).ToList();

        // the outs and runs in the order they happened (batter's own out first), then the unchanged third-out rule
        var terminals = new List<Terminal>();
        if (batterOut)
            terminals.Add(new Terminal(original.NextRunnerKey, false, true, 0, OutClassification.BatterRunner, false));
        foreach (var m in composition.Moves.Where(m => m.Sequence is not null).OrderBy(m => m.Sequence))
            terminals.Add(new Terminal(m.RunnerKey, m.IsRun, m.IsOut, m.Sequence!.Value, OutClassification.NonForce, false));
        var counted = AdvancementRules.CountedRuns(original.Outs, limit, terminals);
        var voided = runs.Count(m => !counted.Contains(m.RunnerKey));

        var expectedResult = ExpectedBatterResult(facts, original, composition, batterOut ? 1 + runnerOuts : runnerOuts);
        var endOnBase = composition.Moves.Count(m => m.To is >= 1 and <= 3) + (facts.Official.BatterDestination is >= 1 and <= 3 ? 1 : 0);
        var expectedLob = (composition.HalfOver || (composition.WalkOffReached && original.Rules.CountWalkOffRunnersAsLeftOnBase) ? endOnBase : 0) + voided;
        var rbiRuns = play.Runs.Count(r => r.RbiBatter is not null);
        var entries = step.Delta.Entries;
        var rbi = entries.Where(e => e.Key == StatKey.BatRBI).Sum(e => e.Amount);
        var atBat = entries.Where(e => e.Key == StatKey.BatAB).Sum(e => e.Amount) == 1;
        var lob = entries.Where(e => e.Key == StatKey.TeamLOB).Sum(e => e.Amount);
        var ended = step.State.Status == GameStatus.Final;

        if (play.Result != expectedResult)
            return Invalid($"batter result {play.Result} expected {expectedResult}");
        if (play.Runs.Count != counted.Count || composition.Runs != runs.Count)
            return Invalid($"counted runs {play.Runs.Count} expected {counted.Count} (composed {composition.Runs} runs)");
        if (play.OutsAfter != composition.OutsAfter || composition.OutsAfter != original.Outs + (batterOut ? 1 : 0) + runnerOuts)
            return Invalid($"outs after {play.OutsAfter} expected {composition.OutsAfter}");
        if ((play.OutsAfter >= limit) != composition.HalfOver)
            return Invalid("half-inning end");
        if (composition.WalkOffReached ? !(ended && step.State.Outcome == GameOutcome.HomeWin) : (!composition.HalfOver && ended))
            return Invalid("the game must end exactly with the winning run");
        if (play.Moves.Count != original.Bases.Count + 1)
            return Invalid("one record per runner and the batter");
        if (play.Credits.Count != facts.Official.Credits.Length + 2 * runnerOuts || play.Errors.Count != (facts.Official.Error is null ? 0 : 1))
            return Invalid("credits or errors");
        foreach (var m in composition.Moves)
        {
            var moved = play.Moves.FirstOrDefault(r => r.RunnerKey == m.RunnerKey);
            if (moved is null || moved.From != m.From || moved.To != m.To || moved.Sequence != (m.Sequence ?? 0) || moved.RunVoided != (m.IsRun && !counted.Contains(m.RunnerKey)))
                return Invalid($"runner {m.RunnerKey} record");
        }

        if (composition.Winner is { } winner)
        {
            var winning = play.Runs.Count >= facts.RunsNeeded ? play.Runs[facts.RunsNeeded - 1] : null;
            if (winning is null || winning.RunnerKey != winner.RunnerKey)
                return Invalid("the winning scorer");
        }

        if (rbiRuns != counted.Count || rbi != counted.Count)
            return Invalid($"RBI {rbi} expected {counted.Count}");
        if (atBat != (expectedResult != BatterResult.SacrificeFly))
            return Invalid("at-bat");
        if (lob != expectedLob)
            return Invalid($"left on base {lob} expected {expectedLob}");

        record = new AdvancementReducerRecord(play.Result!.Value, play.Runs.Count, rbi, atBat, play.OutsAfter, play.OutsAfter >= limit, lob, ended);
        return null;
    }

    /// <summary>
    /// R3 under <see cref="AdvancementPolicy.VersionV2"/>: every runner thrown out on the play must have been credited with an assist by exactly the fielder whose Arm was an input - the
    /// player standing at the Retriever position in the ORIGINAL state - and a decision that was not an out must have no such input credit. Compared with the reducer's own scored
    /// play (position, player and kind of each fielder credit), not with the event the builder wrote. Returns the problem or null.
    /// </summary>
    public static Error? ThrowOutCreditProblem(IReadOnlyList<AdvancementDecisionAudit> decisions, ScoredPlay play, GameState original)
    {
        foreach (var d in decisions)
        {
            if (d.Abilities is not { } a)
                return Mismatch($"'{d.ContextKey}' 결정에 Arm 입력 기록이 없습니다.");
            if (original.DefenderAt(original.FieldingSide, a.Retriever) != a.RetrieverPlayer)
                return Mismatch($"'{d.ContextKey}'의 Arm 입력 선수 {a.RetrieverPlayer}가 {a.Retriever} 위치의 현재 수비수가 아닙니다.");
        }

        foreach (var group in decisions.Where(d => d.Applied == AdvancementOutcome.Out).GroupBy(d => (d.Abilities!.Retriever, d.Abilities.RetrieverPlayer)))
            if (play.Credits.Count(c => c.Kind == CreditKind.Assist && c.Position == group.Key.Retriever && c.Player == group.Key.RetrieverPlayer) < group.Count())
                return Mismatch($"주루사의 보살이 Arm 입력 선수 {group.Key.RetrieverPlayer}({group.Key.Retriever})에게 기록되지 않았습니다.");
        return null;
    }

    private static Error Mismatch(string detail) => new("Advancement.arm_credit_mismatch", $"송구 Arm 입력 선수와 보살 기록이 맞지 않습니다: {detail}");

    /// <summary>
    /// The batter result the reducer must derive from the final event. A hit or reaching on an error keeps its name; a caught ball that is Out is a sacrifice fly only when it was a
    /// fly ball, the batter is the play's only out, the play started with fewer than two outs and at least one run scored on the play (the reducer's rule); otherwise it is Out.
    /// </summary>
    private static BatterResult ExpectedBatterResult(InPlayPlanFacts facts, GameState original, AdvancementComposition composition, int outsOnPlay) => facts.Official.Result switch
    {
        InPlayResult.Single => BatterResult.Single,
        InPlayResult.Double => BatterResult.Double,
        InPlayResult.Triple => BatterResult.Triple,
        InPlayResult.HomeRun => BatterResult.HomeRun,
        InPlayResult.FieldersChoice => BatterResult.FieldersChoice,
        InPlayResult.ReachedOnError => BatterResult.ReachedOnError,
        _ => facts.Official.Batted == BattedBallType.Fly && outsOnPlay == 1 && original.Outs < original.Rules.OutsPerHalfInning - 1 && composition.Moves.Any(m => m.IsRun)
            ? BatterResult.SacrificeFly
            : BatterResult.Out,
    };

    private static Error Invalid(string detail) => new("Advancement.official_record_invalid", $"리듀서가 확정한 공식 기록이 추가 진루 계획과 다릅니다: {detail}");
}
