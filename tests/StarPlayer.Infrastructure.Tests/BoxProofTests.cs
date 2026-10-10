using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Infrastructure.Fixtures;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// OBR 9.03(c) (evidence row C19): a box score proves when at-bats + walks + hit batters + sacrifices equal runs + left on base + the opponent's putouts.
/// Until 2026-10-11 the engine did not count a runner whose run was voided by the third out as left on base, and the proof failed in 70 of the 200 team-games of
/// the then-current 100 games (short by exactly the number of voided runs). The proof is now asserted EXACTLY, with no adjustment, for every team-game, and the
/// games must still contain the situation that used to break it. The hand-worked cases are in ObrDeviationTests (Simulation).
/// </summary>
public class BoxProofTests
{
    private static string Root
    {
        get
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "StarPlayer.sln")))
                    return d.FullName;
            throw new InvalidOperationException("StarPlayer.sln을 찾지 못했습니다.");
        }
    }

    [Fact]
    public void The_box_proof_of_obr_9_03_c_balances_exactly_in_all_two_hundred_team_games_including_the_ones_with_voided_runs()
    {
        var loaded = new ScriptedGameSource(File.ReadAllText(Path.Combine(Root, "content", "rules", "game_rules_profiles.json")),
            File.ReadAllText(Path.Combine(Root, "tests", "Fixtures", "phase2_scripted_games_100.json"))).Load();
        Assert.True(loaded.IsSuccess, loaded.Error?.Message);

        int teamGames = 0, withVoidedRuns = 0, withWalkOffLeft = 0;
        foreach (var game in loaded.Value)
        {
            var state = GameReducer.Start(new GameStart(game.Id, game.Rules, game.Away, game.Home)).Value;
            foreach (var e in game.Events)
                state = GameReducer.Apply(state, e).Value.State;
            var box = BoxScoreBuilder.Build(state);

            foreach (var bat in new[] { TeamSide.Away, TeamSide.Home })
            {
                var fld = bat == TeamSide.Away ? TeamSide.Home : TeamSide.Away;
                int Sum(TeamSide team, StatKey key) => box.Players.Where(p => p.Team == team).Sum(p => p.Get(key));
                var batters = Sum(bat, StatKey.BatAB) + Sum(bat, StatKey.BatBB) + Sum(bat, StatKey.BatHBP) + Sum(bat, StatKey.BatSF) + Sum(bat, StatKey.BatSH);
                var totals = bat == TeamSide.Away ? box.Away : box.Home;
                var proof = totals.Runs + totals.LeftOnBase + Sum(fld, StatKey.FldPO);

                teamGames++;
                Assert.True(batters == proof, $"{game.Id} {bat}: 타수+볼넷+사구+희생 {batters} ≠ 득점 {totals.Runs} + 잔루 {totals.LeftOnBase} + 상대 아웃 {proof - totals.Runs - totals.LeftOnBase}");
                if (state.Plays.Any(p => p.Batting == bat && p.Moves.Any(m => m.RunVoided)))
                    withVoidedRuns++;
                if (bat == TeamSide.Home && state.Outcome == GameOutcome.HomeWin && state.Half == InningHalf.Bottom && state.Bases.Count > 0)
                    withWalkOffLeft++;
            }
        }

        Assert.Equal(200, teamGames);
        Assert.True(withVoidedRuns >= 40, $"검산이 예전에 깨지던 상황(무효 득점)이 경기 세트에 충분히 있어야 한다: {withVoidedRuns}건");
        Assert.True(withWalkOffLeft >= 1, "끝내기 경기에 누상 주자가 남은 사례가 있어야 한다");
    }
}
