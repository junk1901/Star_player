using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Tests.InPlayFeasibility;
using Root = StarPlayer.Simulation.Tests.InPlayFeasibility.InPlayStates.Root;
using States = StarPlayer.Simulation.Tests.InPlayFeasibility.InPlayStates;

namespace StarPlayer.Simulation.Tests.InPlay;

/// <summary>
/// Phase 3-C2B-2 Codex review fix (Medium): the winning scorer of a walk-off must tell apart "no winning scorer", "an existing runner scored the winning run" and "the
/// batter-runner scored it". Every case is judged against the real reducer's <c>ScoredPlay.Runs</c> (the need-th scorer, in scoring order), not against the planner itself.
/// </summary>
public class InPlayWinningRunnerTests
{
    private static (InPlayPlanFacts Plan, GameStep Step, GameState State) Run(GameState s, string batted, string result)
    {
        var made = InPlayBasePlanner.Plan(s, batted, result);
        Assert.True(made.IsSuccess, made.Error?.Code);
        var applied = GameReducer.Apply(s, InPlayBasePlanner.BuildEvent(made.Value, "w1", FeasibilityHarness.Location));
        Assert.True(applied.IsSuccess, applied.Error?.Code);
        return (made.Value, applied.Value, s);
    }

    private static void AssertBatterRunnerWon(GameState s, string batted, string result)
    {
        var (plan, step, _) = Run(s, batted, result);
        var runs = step.NewPlays.Single().Runs;
        var batter = s.Batting.CurrentBatterSlot.Player;

        var w = plan.WinningRunner;
        Assert.NotNull(w);
        Assert.Equal(InPlayScorerKind.BatterRunner, w.Kind);
        Assert.Equal(0, w.StartingBase);
        Assert.Equal(batter, w.Player);                                                       // the actual batter
        Assert.Equal(s.NextRunnerKey, w.RunnerKey);                                           // the key the reducer gives the batter-runner
        Assert.DoesNotContain(plan.Moves, m => m.RunnerKey == w.RunnerKey);                   // never an existing runner's key
        Assert.Equal(w.RunnerKey, runs[plan.RunsNeeded - 1].RunnerKey);
        Assert.Equal(w.Player, runs[plan.RunsNeeded - 1].Runner);
        Assert.Equal(runs.Count - 1, plan.RunsNeeded - 1);                                    // the batter scored last, and it was the winning run
        Assert.Equal(runs[^1].RunnerKey, w.RunnerKey);
        Assert.Equal(InPlayWalkOffEffect.GameEnds, plan.WalkOffEffect);
        Assert.Equal(GameStatus.Final, step.State.Status);
    }

    [Fact]
    public void Tied_in_the_bottom_of_the_ninth_with_nobody_on_a_home_run_is_won_by_the_batter_runner() =>
        AssertBatterRunnerWon(States.Make(Root.WalkOff, 0, 0, 0), "fly_hard", "home_run");

    [Fact]
    public void One_behind_with_a_runner_on_first_a_home_run_is_won_by_the_batter_runner() =>
        AssertBatterRunnerWon(States.Make(Root.WalkOff, 1, 1, 0), "fly_hard", "home_run");

    [Fact]
    public void Two_behind_with_first_and_second_a_home_run_is_won_by_the_batter_runner() =>
        AssertBatterRunnerWon(States.Make(Root.WalkOff, 2, 3, 0), "fly_hard", "home_run");

    [Fact]
    public void Three_behind_with_the_bases_loaded_a_grand_slam_is_won_by_the_batter_runner() =>
        AssertBatterRunnerWon(States.Make(Root.WalkOff, 3, 7, 0), "fly_hard", "home_run");

    private static void AssertExistingRunnerWon(GameState s, string batted, string result, int startingBase)
    {
        var (plan, step, _) = Run(s, batted, result);
        var runs = step.NewPlays.Single().Runs;
        var runner = s.Bases.At(startingBase)!;

        var w = plan.WinningRunner;
        Assert.NotNull(w);
        Assert.Equal(InPlayScorerKind.ExistingRunner, w.Kind);
        Assert.Equal(startingBase, w.StartingBase);
        Assert.Equal(runner.RunnerKey, w.RunnerKey);                                          // the existing key is preserved
        Assert.Equal(runner.Player, w.Player);
        Assert.NotEqual(s.NextRunnerKey, w.RunnerKey);
        Assert.Equal(runs[plan.RunsNeeded - 1].RunnerKey, w.RunnerKey);
        Assert.Equal(runs[plan.RunsNeeded - 1].Runner, w.Player);
    }

    [Fact]
    public void Tied_with_a_runner_on_third_a_home_run_is_won_by_that_runner() =>
        AssertExistingRunnerWon(States.Make(Root.WalkOff, 0, 4, 0), "fly_hard", "home_run", 3);

    [Fact]
    public void Tied_with_a_runner_on_third_a_drawn_double_is_won_by_that_runner() =>
        AssertExistingRunnerWon(States.Make(Root.WalkOff, 0, 4, 0), "line_medium", "double", 3);

    [Fact]
    public void One_behind_with_nobody_on_a_home_run_only_ties_and_has_no_winning_scorer()
    {
        var s = States.Make(Root.WalkOff, 1, 0, 0);
        var (plan, step, _) = Run(s, "fly_hard", "home_run");

        Assert.Null(plan.WinningRunner);
        Assert.Equal(InPlayWalkOffEffect.None, plan.WalkOffEffect);
        Assert.Equal(GameStatus.InProgress, step.State.Status);
        Assert.Equal(step.State.AwayRuns, step.State.HomeRuns);                               // tied, the game goes on
    }

    [Fact]
    public void A_home_run_in_an_ordinary_inning_has_no_walk_off_winning_scorer()
    {
        var (plan, _, _) = Run(States.Make(Root.GeneralBottom, 0, 0, 0), "fly_hard", "home_run");

        Assert.Null(plan.WinningRunner);
        Assert.False(plan.WalkOffContext);
    }

    [Fact]
    public void No_winning_scorer_and_the_batter_runner_are_different_data_in_the_audit()
    {
        var none = Run(States.Make(Root.WalkOff, 1, 0, 0), "fly_hard", "home_run").Plan;
        var batter = Run(States.Make(Root.WalkOff, 0, 0, 0), "fly_hard", "home_run").Plan;

        Assert.Null(none.WinningRunner);
        Assert.Equal(InPlayScorerKind.BatterRunner, batter.WinningRunner!.Kind);
        Assert.NotEqual(none.WinningRunner, batter.WinningRunner);
    }

    [Fact]
    public void A_walk_off_home_run_scores_the_existing_runners_first_then_the_batter_in_the_reducer_order()
    {
        var s = States.Make(Root.WalkOff, 3, 7, 0);
        var (plan, step, _) = Run(s, "fly_hard", "home_run");
        var runs = step.NewPlays.Single().Runs;

        Assert.Equal(4, runs.Count);
        Assert.Equal(plan.Moves.Where(m => m.To == 4).Select(m => m.RunnerKey).Append(s.NextRunnerKey), runs.Select(r => r.RunnerKey));   // lead runner first, batter last
        Assert.Equal(runs[3].RunnerKey, plan.WinningRunner!.RunnerKey);
        Assert.Equal(4, plan.RunsNeeded);
    }
}
