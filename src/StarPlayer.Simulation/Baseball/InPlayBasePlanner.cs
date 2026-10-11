using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Common;

namespace StarPlayer.Simulation.Baseball;

// Phase 3-C2B-2 (ADR-007, docs/game-engine.md section 23): the deterministic base play of one drawn in-play result. It decides the template, every EXISTING runner's base move
// (an explicit hold is a move), the batter's final base, the official record of a game-ending hit and whether an extra-base decision is still open. It draws nothing, issues no
// id and never calls the reducer: the official run, RBI, left-on-base and earned-run numbers stay the reducer's. A base plan never puts a runner out and never needs a Sequence,
// because a play that makes the third out holds every runner (U-6).

internal static class InPlayBasePlanner
{
    /// <summary>Plans the base play for a drawn combination on a state. Pure: no random number, no event id, no reducer call.</summary>
    public static Result<InPlayPlanFacts> Plan(GameState? state, string? battedBall, string? defenseResult)
    {
        if (state is null)
            return Result<InPlayPlanFacts>.Failure(new Error("InPlayPlan.state_missing", "경기 상태가 없어 인플레이 계획을 만들 수 없습니다."));
        if (state.Status != GameStatus.InProgress)
            return Result<InPlayPlanFacts>.Failure(new Error("InPlayPlan.state_invalid", "끝난 경기에서는 인플레이 계획을 만들 수 없습니다."));
        var resolved = InPlayTemplateCatalog.Resolve(battedBall, defenseResult);
        if (!resolved.IsSuccess)
            return Result<InPlayPlanFacts>.Failure(resolved.Error!.Value);
        var drawn = resolved.Value;

        var walkOffContext = InPlayOfficialCredit.WalkOffContext(state);
        var need = InPlayOfficialCredit.RunsNeeded(state);
        if (walkOffContext && need < 1)
            return Result<InPlayPlanFacts>.Failure(new Error("InPlayPlan.state_invalid", "이미 앞선 홈팀이 마지막 이닝 말에 공격 중인 경기 상태는 계획할 수 없습니다."));

        var official = InPlayOfficialCredit.Official(state, drawn.Template, need);
        var moves = BaseMoves(state, official);
        var runs = moves.Count(m => m.To == 4) + (official.Result == InPlayResult.HomeRun ? 1 : 0);
        var endsGame = walkOffContext && runs >= need;
        var effect = !ReferenceEquals(official, drawn.Template) ? InPlayWalkOffEffect.CreditReduced : endsGame ? InPlayWalkOffEffect.GameEnds : InPlayWalkOffEffect.None;
        var endsHalf = official.Result == InPlayResult.Out && state.Outs + 1 >= state.Rules.OutsPerHalfInning;

        InPlayWinningRunner? winner = null;
        if (endsGame)
        {
            // lead runner first; a home run's batter-runner scores last, under the key and player the reducer gives him (ApplyInPlay: NextRunnerKey, current batter slot)
            var scorers = moves.Where(m => m.To == 4).Select(m => new InPlayWinningRunner(InPlayScorerKind.ExistingRunner, m.RunnerKey, m.Runner, m.From)).ToList();
            if (official.Result == InPlayResult.HomeRun)
                scorers.Add(new InPlayWinningRunner(InPlayScorerKind.BatterRunner, state.NextRunnerKey, state.Batting.CurrentBatterSlot.Player, 0));
            winner = scorers[need - 1];                                               // endsGame means runs (= scorers.Count) >= need
        }

        var keys = ContextKeys(state, official);
        var runners = state.Bases.Occupied().Select(o => new InPlayBaseRunner(o.Base, o.Runner.RunnerKey, o.Runner.Player)).ToImmutableArray();
        var prePlay = new InPlayPrePlay(state.Inning, state.Half, state.Outs, state.AwayRuns, state.HomeRuns, runners);
        return Result<InPlayPlanFacts>.Success(new InPlayPlanFacts(
            drawn.BattedBall, drawn.BattedBallClass, drawn.Intensity, drawn.DefenseResult, drawn.Template, official, moves, runs, endsHalf, walkOffContext, need, effect, winner,
            keys, keys.Length > 0 && runners.Length > 1, prePlay));
    }

    /// <summary>One move for every existing runner, lead runner first (the approved 8 x 6 table of docs/game-engine.md section 23.3).</summary>
    private static ImmutableArray<InPlayRunnerPlan> BaseMoves(GameState state, InPlayTemplate official)
    {
        var bases = state.Bases;
        var builder = ImmutableArray.CreateBuilder<InPlayRunnerPlan>();
        foreach (var (b, runner) in bases.Occupied().OrderByDescending(o => o.Base))
        {
            // forced: every base behind him is occupied (the batter's run to first forces along the chain)
            var forced = Enumerable.Range(1, b - 1).All(x => bases.At(x) is not null);
            var (to, cause) = official.Result switch
            {
                InPlayResult.Out when official.Class == "ground" && state.Outs < state.Rules.OutsPerHalfInning - 1 => (forced ? b + 1 : b, MoveCause.Play),
                InPlayResult.Out => (b, MoveCause.Play),                                          // line, fly, pop: no tag-up, no double-off; a third-out play: everybody holds
                InPlayResult.Single or InPlayResult.Double or InPlayResult.Triple => (Math.Min(4, b + InPlayOfficialCredit.BasesOf(official.Result)), MoveCause.Play),
                InPlayResult.HomeRun => (4, MoveCause.Play),
                _ => forced ? (b + 1, MoveCause.Forced) : (b, MoveCause.Play),                    // reached on error: only the forced runners are pushed
            };
            builder.Add(new InPlayRunnerPlan(runner.RunnerKey, runner.Player, b, to, cause));
        }

        return builder.ToImmutable();
    }

    /// <summary>The declared <c>advancement</c> contexts of <c>prob-model-v1</c> that apply to the official result in this situation, in base order.</summary>
    private static ImmutableArray<string> ContextKeys(GameState state, InPlayTemplate official)
    {
        var keys = ImmutableArray.CreateBuilder<string>();
        var bases = state.Bases;
        if (official.Result == InPlayResult.Single)
        {
            if (bases.First is not null) keys.Add("single_runner_on_first");
            if (bases.Second is not null) keys.Add("single_runner_on_second");
        }
        else if (official.Result == InPlayResult.Double && bases.First is not null)
            keys.Add("double_runner_on_first");
        else if (official.Result == InPlayResult.Out && official.Class == "fly" && state.Outs < state.Rules.OutsPerHalfInning - 1 && bases.Third is not null)
            keys.Add("fly_out_runner_on_third");
        return keys.ToImmutable();
    }

    /// <summary>The reducer event of a plan. The caller supplies the working id and the draft's location (null only for internal checks of the plan itself).</summary>
    public static InPlayEvent BuildEvent(InPlayPlanFacts facts, string eventId, PitchLocation? location)
    {
        var official = facts.Official;
        var moves = facts.Moves.Select(m => new RunnerMove(m.From, m.To, m.Cause)).ToImmutableList();
        return new InPlayEvent(eventId, official.Batted, official.Result, false, moves,
            official.Credits.Length == 0 ? null : official.Credits.ToImmutableList(), official.Error is { } e ? ImmutableList.Create(e) : null, null, location);
    }
}
