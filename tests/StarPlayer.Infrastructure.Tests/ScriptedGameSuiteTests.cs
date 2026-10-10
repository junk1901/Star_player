using System.Text.Json.Nodes;
using StarPlayer.Application.Ports;
using StarPlayer.Application.Projections;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Infrastructure.Fixtures;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 2 acceptance: 100 complete scripted games, generated together with expected results by an independent scorer
/// (tests/Fixtures/generator), are run through the real reducer and every number is compared.
/// </summary>
public class ScriptedGameSuiteTests
{
    private static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "StarPlayer.sln")))
                return d.FullName;
        throw new InvalidOperationException("StarPlayer.sln을 찾지 못했습니다.");
    }

    private static string Rules => File.ReadAllText(Path.Combine(Root, "content", "rules", "game_rules_profiles.json"));

    private static string HundredGamesJson => File.ReadAllText(Path.Combine(Root, "tests", "Fixtures", "phase2_scripted_games_100.json"));

    private static IReadOnlyList<ScriptedGame> HundredGames()
    {
        var loaded = new ScriptedGameSource(Rules, HundredGamesJson).Load();
        Assert.True(loaded.IsSuccess, loaded.Error?.Message);
        return loaded.Value;
    }

    [Fact]
    public void The_suite_has_one_hundred_complete_games_with_independent_expected_results_and_every_rule_profile()
    {
        var games = HundredGames();

        Assert.Equal(100, games.Count);
        Assert.All(games, g => Assert.NotNull(g.Expected));
        Assert.Equal(100, games.Select(g => g.Id).Distinct().Count());
        Assert.Equal(
            ["kbo-oriented-unverified-v0", "test-dh-cap10", "test-dh-cap11", "test-dh-no-two-way", "test-dh-unlimited", "test-no-dh-cap11"],
            games.Select(g => g.Rules.ProfileId).Distinct().Order().ToArray());
        Assert.True(games.Sum(g => g.Events.Count) > 20000);
    }

    [Fact]
    public void All_one_hundred_games_replay_to_the_independent_expected_box_scores_and_reconcile()
    {
        var service = new GameDiagnosticsService(new ScriptedGameSource(Rules, HundredGamesJson));
        Assert.True(service.IsReady, service.LoadError);
        var failures = new List<string>();

        foreach (var item in service.ListGames())
        {
            var run = service.Select(item.Id);
            if (run.IsSuccess)
                run = service.RunToEnd();
            if (!run.IsSuccess)
            {
                failures.Add($"{item.Id}: {run.ErrorCode} {run.ErrorMessage}");
                continue;
            }

            if (!run.View!.IsOver)
                failures.Add($"{item.Id}: 경기가 끝나지 않음");
            foreach (var problem in service.Verify())
                failures.Add($"{item.Id}: {problem}");
        }

        Assert.True(failures.Count == 0, $"{failures.Count}건 불일치:\n" + string.Join("\n", failures.Take(25)));
    }

    [Fact]
    public void The_suite_covers_the_required_situations()
    {
        var games = HundredGames();
        var covered = new HashSet<string>();
        foreach (var g in games)
        {
            var e = g.Expected!;
            if (e.AwayLine.Count > 9) covered.Add("extra innings");
            if (e.Outcome == "tie") covered.Add("tie at the limit");
            if (e.Outcome == "home_win" && e.HomeLine.Count < e.AwayLine.Count) covered.Add("home skips the last bottom");
            if (e.Outcome == "home_win" && e.HomeLine.Count == e.AwayLine.Count) covered.Add("home bats last and wins");
            if (e.Decisions.ContainsKey("save")) covered.Add("save");
            foreach (var (key, label) in new[]
            {
                ("batsf", "sacrifice fly"), ("batsh", "sacrifice bunt"), ("batgidp", "double play"), ("pitchwp", "wild pitch"), ("fldpb", "passed ball"),
                ("runsb", "stolen base"), ("runcs", "caught stealing"), ("runpicked", "pickoff"), ("pitchbk", "balk"), ("batroe", "reached on error"),
                ("batfc", "fielder's choice"), ("bathbp", "hit by pitch"), ("bathr", "home run"), ("bat3b", "triple"), ("bat2b", "double"),
            })
                if (e.Players.Values.Any(p => p.ContainsKey(key)))
                    covered.Add(label);
            foreach (var ev in g.Events)
            {
                if (ev is TakeEvent) covered.Add("one-pitch take");
                if (ev is PitchEvent { Spec.DroppedThirdStrike: not PitchMishap.None }) covered.Add("dropped third strike");
                if (ev is SubstitutionEvent s) covered.Add("substitution " + s.Kind);
            }

            if (e.Players.Values.Any(p => p.ContainsKey("pitchr") && p.ContainsKey("pitcher") && p["pitchr"] > p["pitcher"]) ||
                e.Players.Values.Any(p => p.ContainsKey("pitchr") && !p.ContainsKey("pitcher")))
                covered.Add("unearned run");
            if (g.Away.StartingPitcher.Value == "a5" || g.Home.StartingPitcher.Value == "h5") covered.Add("two-way starter");
            if (!g.Rules.DesignatedHitter) covered.Add("no DH");
        }

        string[] required =
        [
            "extra innings", "tie at the limit", "home skips the last bottom", "home bats last and wins", "save", "sacrifice fly", "sacrifice bunt", "double play",
            "wild pitch", "passed ball", "stolen base", "caught stealing", "pickoff", "balk", "reached on error", "fielder's choice", "hit by pitch", "home run", "triple",
            "double", "one-pitch take", "dropped third strike", "substitution PinchHit", "substitution PinchRun", "substitution DefensiveSub", "substitution PositionSwap",
            "substitution PitchingChange", "unearned run", "two-way starter", "no DH",
        ];
        var missing = required.Where(r => !covered.Contains(r)).ToList();
        Assert.True(missing.Count == 0, "시나리오에 없는 상황: " + string.Join(", ", missing));
    }

    [Fact]
    public void Invariants_hold_after_every_single_event_of_all_one_hundred_games()
    {
        foreach (var game in HundredGames())
        {
            var state = GameReducer.Start(new GameStart(game.Id, game.Rules, game.Away, game.Home)).Value;
            foreach (var e in game.Events)
            {
                var step = GameReducer.Apply(state, e);
                Assert.True(step.IsSuccess, $"{game.Id} {e.Id}: {step.Error?.Code} {step.Error?.Message}");
                state = step.Value.State;

                var awayRuns = state.Plays.Where(p => p.Batting == TeamSide.Away).Sum(p => p.Runs.Count);
                var homeRuns = state.Plays.Where(p => p.Batting == TeamSide.Home).Sum(p => p.Runs.Count);
                Assert.True((state.AwayRuns, state.HomeRuns) == (awayRuns, homeRuns), $"{game.Id} {e.Id}: 점수에 ScoredPlay 근거가 없음");
                Assert.True(state.AwayRuns == state.AwayLine.Sum() && state.HomeRuns == state.HomeLine.Sum(), $"{game.Id} {e.Id}: 이닝별 득점 합이 점수와 다름");
                Assert.True(state.Outs is >= 0 and <= 3 && (state.Status == GameStatus.Final || state.Outs < 3), $"{game.Id} {e.Id}: 아웃 수가 올바르지 않음");
                var keys = state.Bases.Occupied().Select(o => o.Runner.RunnerKey).ToList();
                Assert.True(keys.Count == keys.Distinct().Count(), $"{game.Id} {e.Id}: 한 주자가 두 베이스에 있음");
                var players = state.Bases.Occupied().Select(o => o.Runner.Player).ToList();
                Assert.True(players.Count == players.Distinct().Count(), $"{game.Id} {e.Id}: 한 선수가 두 베이스에 있음");
                Assert.True(state.Count.Balls < state.Rules.BallsForWalk && state.Count.Strikes < state.Rules.StrikesForStrikeout, $"{game.Id} {e.Id}: 볼카운트 범위 오류");
                Assert.True(state.Stats.Where(x => x.Key == StatKey.RunR).Sum(x => x.Amount) == state.AwayRuns + state.HomeRuns, $"{game.Id} {e.Id}: 득점 기록 합 불일치");
                Assert.True(state.Stats.All(x => x.Amount >= 0), $"{game.Id} {e.Id}: 음수 기록");
            }

            Assert.Equal(GameStatus.Final, state.Status);
            var first = GameReducer.Finalize(state).Value;
            var second = GameReducer.Finalize(first.State).Value;
            Assert.True(first.WasNew && !second.WasNew && ReferenceEquals(first.Game, second.Game), $"{game.Id}: 경기 확정이 한 번이 아님");
        }
    }

    [Fact]
    public void Replaying_every_event_of_a_finished_game_a_second_time_changes_no_record()
    {
        var game = HundredGames()[0];
        var state = GameReducer.Start(new GameStart(game.Id, game.Rules, game.Away, game.Home)).Value;
        foreach (var e in game.Events)
            state = GameReducer.Apply(state, e).Value.State;
        var finalized = GameReducer.Finalize(state).Value.State;

        foreach (var e in game.Events)
        {
            var again = GameReducer.Apply(finalized, e);
            Assert.True(again.IsSuccess && again.Value.WasDuplicate);
            Assert.Same(finalized, again.Value.State);
        }

        Assert.Equal(finalized.Stats.Count, state.Stats.Count);
    }

    // ---- the comparison itself must fail when a number is wrong (otherwise 100 green games prove nothing)

    [Theory]
    [InlineData("away_runs", "원정 득점")]
    [InlineData("away_hits", "원정 안타")]
    [InlineData("away_lob", "원정 잔루")]
    public void A_tampered_expected_total_is_reported(string field, string label)
    {
        var tampered = Tamper(root => root["games"]![0]!["expected"]![field] = root["games"]![0]!["expected"]![field]!.GetValue<int>() + 1);

        var problems = VerifyFirst(tampered);

        Assert.Contains(problems, p => p.StartsWith(label, StringComparison.Ordinal));
    }

    [Fact]
    public void A_tampered_player_catcher_battery_decision_or_appearance_expectation_is_reported()
    {
        var firstPlayerStats = JsonNode.Parse(HundredGamesJson)!["games"]![0]!["expected"]!["players"]!.AsObject().First();
        var stat = firstPlayerStats.Value!.AsObject().First().Key;

        Assert.Contains(VerifyFirst(Tamper(root =>
            root["games"]![0]!["expected"]!["players"]![firstPlayerStats.Key]![stat] = firstPlayerStats.Value![stat]!.GetValue<int>() + 1)), p => p.StartsWith($"선수 {firstPlayerStats.Key} {stat}", StringComparison.Ordinal));
        Assert.Contains(VerifyFirst(Tamper(root => root["games"]![0]!["expected"]!["catchers"]!.AsObject().First().Value!["pitches"] = 1)), p => p.StartsWith("포수", StringComparison.Ordinal));
        Assert.Contains(VerifyFirst(Tamper(root => root["games"]![0]!["expected"]!["batteries"]!.AsObject().First().Value!["outs"] = 99)), p => p.StartsWith("배터리", StringComparison.Ordinal));
        Assert.Contains(VerifyFirst(Tamper(root => root["games"]![0]!["expected"]!["decisions"]!["win"] = "nobody")), p => p.StartsWith("투수 결정", StringComparison.Ordinal));
        Assert.Contains(VerifyFirst(Tamper(root => root["games"]![0]!["expected"]!["appearances"]!.AsArray().RemoveAt(0))), p => p.StartsWith("경기 참가", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reconciler_reports_a_broken_identity()
    {
        var service = new GameDiagnosticsService(new ScriptedGameSource(Rules, HundredGamesJson));
        service.Select("g001");
        service.RunToEnd();
        Assert.Empty(service.Verify());

        // break one record on purpose: drop a pitcher's run from the stat lines
        var game = HundredGames()[0];
        var state = GameReducer.Start(new GameStart(game.Id, game.Rules, game.Away, game.Home)).Value;
        foreach (var e in game.Events)
            state = GameReducer.Apply(state, e).Value.State;
        var broken = state with { Stats = state.Stats.Remove(state.Stats.First(x => x.Key == StatKey.BatH)) };
        var box = BoxScoreBuilder.Build(broken);

        Assert.Contains(BoxScoreReconciler.Check(box), p => p.Contains("안타"));
    }

    [Fact]
    public void The_reconciler_checks_the_obr_9_03_c_box_proof_and_reports_a_missing_left_on_base_runner()
    {
        var game = HundredGames().First(g => g.Rules.CountWalkOffRunnersAsLeftOnBase);
        var state = GameReducer.Start(new GameStart(game.Id, game.Rules, game.Away, game.Home)).Value;
        foreach (var e in game.Events)
            state = GameReducer.Apply(state, e).Value.State;
        Assert.Empty(BoxScoreReconciler.Check(BoxScoreBuilder.Build(state)));

        // forget one stranded runner: the box no longer proves
        var lob = state.Stats.First(x => x.Key == StatKey.TeamLOB);
        var broken = state with { Stats = state.Stats.Replace(lob, lob with { Amount = lob.Amount - 1 }) };

        Assert.Contains(BoxScoreReconciler.Check(BoxScoreBuilder.Build(broken)), p => p.Contains("박스 증명"));
    }

    private static string Tamper(Action<JsonNode> edit)
    {
        var node = JsonNode.Parse(HundredGamesJson)!;
        edit(node);
        return node.ToJsonString();
    }

    private static IReadOnlyList<string> VerifyFirst(string gamesJson)
    {
        var service = new GameDiagnosticsService(new ScriptedGameSource(Rules, gamesJson));
        Assert.True(service.IsReady, service.LoadError);
        service.Select("g001");
        service.RunToEnd();
        return service.Verify();
    }
}
