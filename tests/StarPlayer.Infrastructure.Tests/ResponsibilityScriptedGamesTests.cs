using System.Text.Json.Nodes;
using StarPlayer.Application.Ports;
using StarPlayer.Application.Projections;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Infrastructure.Fixtures;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// C33 (OBR 9.16(g)) integration set: 30 complete games drawn by the Python scorer (tests/Fixtures/generator) because each contains at least one run charged to a
/// pitcher other than the one who put the scoring runner on base (a slot passed on by a fielder's choice). The 100-game set does not contain such a run
/// (the generator counts 0), so this set is what exercises the rule at game scale. It is an integration check, not the independent oracle: the hand-computed
/// OBR examples in StarPlayer.Simulation.Tests/ResponsibilityLedgerTests are.
/// </summary>
public class ResponsibilityScriptedGamesTests
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

    private static string GamesJson => File.ReadAllText(Path.Combine(Root, "tests", "Fixtures", "phase2_c33_responsibility_games_30.json"));

    private static IReadOnlyList<ScriptedGame> Games()
    {
        var loaded = new ScriptedGameSource(Rules, GamesJson).Load();
        Assert.True(loaded.IsSuccess, loaded.Error?.Message);
        return loaded.Value;
    }

    [Fact]
    public void Every_game_of_the_set_charges_at_least_one_run_through_a_moved_slot()
    {
        // the generator's sidecar says, per game, how many runs were charged to a pitcher other than the one who put the scoring runner on base
        var meta = JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "tests", "Fixtures", "phase2_c33_responsibility_games_30.meta.json")))!["divergent_runs_per_game"]!.AsObject();
        var games = Games();

        Assert.Equal(30, games.Count);
        Assert.Equal(games.Select(g => g.Id).Order(), meta.Select(kv => kv.Key).Order());
        Assert.All(meta, kv => Assert.True(kv.Value!.GetValue<int>() >= 1, kv.Key));
        Assert.True(meta.Sum(kv => kv.Value!.GetValue<int>()) >= 30);
    }

    [Fact]
    public void All_games_replay_to_the_independent_expected_box_scores_including_every_pitchers_runs_and_earned_runs()
    {
        var service = new GameDiagnosticsService(new ScriptedGameSource(Rules, GamesJson));
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
    public void After_every_event_each_run_is_charged_once_and_the_pitchers_runs_add_up_to_the_credited_runs()
    {
        foreach (var game in Games())
        {
            var state = GameReducer.Start(new GameStart(game.Id, game.Rules, game.Away, game.Home)).Value;
            foreach (var e in game.Events)
            {
                var step = GameReducer.Apply(state, e);
                Assert.True(step.IsSuccess, $"{game.Id} {e.Id}: {step.Error?.Code} {step.Error?.Message}");
                state = step.Value.State;

                var runs = state.Plays.SelectMany(p => p.Runs).ToList();
                Assert.True(runs.Count == state.AwayRuns + state.HomeRuns, $"{game.Id} {e.Id}: 인정 득점과 득점 기록 수가 다름");
                Assert.True(runs.Select(r => r.RunnerKey).Distinct().Count() == runs.Count || runs.GroupBy(r => r.RunnerKey).All(g => g.Count() == 1), $"{game.Id} {e.Id}: 한 득점이 두 번 기록됨");
                Assert.True(state.Stats.Where(x => x.Team == TeamSide.Home && x.Key == StatKey.PitchR).Sum(x => x.Amount) == state.AwayRuns, $"{game.Id} {e.Id}: 홈 투수 실점 합 != 원정 득점");
                Assert.True(state.Stats.Where(x => x.Team == TeamSide.Away && x.Key == StatKey.PitchR).Sum(x => x.Amount) == state.HomeRuns, $"{game.Id} {e.Id}: 원정 투수 실점 합 != 홈 득점");
            }
        }
    }

    [Fact]
    public void Writing_and_reading_the_events_as_json_keeps_every_fingerprint_and_replays_to_the_same_charges()
    {
        static GameState Replay(ScriptedGame game, IEnumerable<GameEvent> events)
        {
            var state = GameReducer.Start(new GameStart(game.Id, game.Rules, game.Away, game.Home)).Value;
            foreach (var e in events)
            {
                var step = GameReducer.Apply(state, e);
                Assert.True(step.IsSuccess, $"{game.Id} {e.Id}: {step.Error?.Code}");
                state = step.Value.State;
            }

            return state;
        }

        foreach (var game in Games())
        {
            var read = ScriptedGameJson.ReadEvents(ScriptedGameJson.WriteEvents(game.Events));
            Assert.True(read.IsSuccess, read.Error?.Message);
            for (var i = 0; i < game.Events.Count; i++)
                Assert.True(EventFingerprint.Receipt(game.Events[i]) == EventFingerprint.Receipt(read.Value[i]), $"{game.Id} {game.Events[i].Id}: 지문이 JSON 왕복 후 달라짐");

            var original = Replay(game, game.Events);
            var again = Replay(game, read.Value);
            Assert.Equal(original.AppliedEvents, again.AppliedEvents);
            Assert.Equal(original.Stats, again.Stats);
            Assert.Equal(original.Plays.SelectMany(p => p.Runs).Select(r => (r.RunnerKey, r.Runner, r.ResponsiblePitcher)),
                again.Plays.SelectMany(p => p.Runs).Select(r => (r.RunnerKey, r.Runner, r.ResponsiblePitcher)));
        }
    }
}
