using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;

namespace StarPlayer.Simulation.Baseball;

/// <summary>
/// Rebuilds every official number of a finished game from its append-only evidence (stat entries, scored plays, pitcher stints):
/// player lines, team totals, appearances, pitcher decisions, and the catcher / battery exposure that is counted once per game.
/// It uses nothing but the state, so the same state always gives the same box score.
/// </summary>
public static class BoxScoreBuilder
{
    public static BoxScore Build(GameState state)
    {
        var lines = state.Stats
            .Where(e => e.Player != default)
            .GroupBy(e => (e.Team, e.Player))
            .OrderBy(g => g.Key.Team).ThenBy(g => g.Key.Player.Value, StringComparer.Ordinal)
            .Select(g => new PlayerStatLine(g.Key.Team, g.Key.Player,
                g.GroupBy(e => e.Key).Where(k => k.Sum(e => e.Amount) != 0).ToImmutableSortedDictionary(k => k.Key, k => k.Sum(e => e.Amount))))
            .ToImmutableList();

        int Sum(TeamSide team, StatKey key) => state.Stats.Where(e => e.Team == team && e.Key == key).Sum(e => e.Amount);
        TeamTotals Totals(TeamSide team) => new(state.Runs(team), Sum(team, StatKey.BatH), Sum(team, StatKey.FldE), Sum(team, StatKey.TeamLOB));

        var appearances = ImmutableList.CreateBuilder<GameAppearance>();
        foreach (var side in new[] { TeamSide.Away, TeamSide.Home })
        {
            var team = state.Team(side);
            foreach (var player in team.Appeared.OrderBy(p => p.Value, StringComparer.Ordinal))
                appearances.Add(new GameAppearance(side, player, team.EverInLineup.Contains(player), team.PitcherUsages.Any(u => u.Pitcher == player)));
        }

        var catchers = state.Stats
            .Where(e => e.Key is StatKey.CatPitches or StatKey.CatOuts or StatKey.CatSBA or StatKey.CatCS)
            .GroupBy(e => (e.Team, e.Player))
            .OrderBy(g => g.Key.Team).ThenBy(g => g.Key.Player.Value, StringComparer.Ordinal)
            .Select(g => new CatcherExposure(state.GameId, g.Key.Team, g.Key.Player,
                g.Where(e => e.Key == StatKey.CatPitches).Sum(e => e.Amount), g.Where(e => e.Key == StatKey.CatOuts).Sum(e => e.Amount),
                g.Where(e => e.Key == StatKey.CatSBA).Sum(e => e.Amount), g.Where(e => e.Key == StatKey.CatCS).Sum(e => e.Amount),
                state.Stats.Where(e => e.Team == g.Key.Team && e.Player == g.Key.Player && e.Key == StatKey.FldPB).Sum(e => e.Amount)))
            .ToImmutableList();

        var batteries = state.Stats
            .Where(e => e.Key is StatKey.BatteryPitches or StatKey.BatteryOuts or StatKey.BatteryBF && e.Partner is not null)
            .GroupBy(e => (e.Team, Catcher: e.Player, Pitcher: e.Partner!.Value))
            .OrderBy(g => g.Key.Team).ThenBy(g => g.Key.Catcher.Value, StringComparer.Ordinal).ThenBy(g => g.Key.Pitcher.Value, StringComparer.Ordinal)
            .Select(g => new BatteryExposure(state.GameId, g.Key.Team, g.Key.Catcher, g.Key.Pitcher,
                g.Where(e => e.Key == StatKey.BatteryPitches).Sum(e => e.Amount), g.Where(e => e.Key == StatKey.BatteryOuts).Sum(e => e.Amount),
                g.Where(e => e.Key == StatKey.BatteryBF).Sum(e => e.Amount), 1))
            .ToImmutableList();

        return new BoxScore(state.GameId, state.AwayLine, state.HomeLine, Totals(TeamSide.Away), Totals(TeamSide.Home), lines, appearances.ToImmutable(),
            Decisions(state), catchers, batteries);
    }

    /// <summary>
    /// Win / loss / save, with the simplifications documented in docs/game-engine.md: the winner is the pitcher of record when the
    /// winning team took the lead it never gave up (a starter needs the rule's minimum outs, otherwise the reliever with the most
    /// outs, earliest first); the loss is charged to the pitcher responsible for the run that gave that lead.
    /// </summary>
    internal static ImmutableList<PitcherDecision> Decisions(GameState state)
    {
        if (state.Outcome is not { } outcome || outcome == GameOutcome.Tie)
            return ImmutableList<PitcherDecision>.Empty;
        var winner = outcome == GameOutcome.AwayWin ? TeamSide.Away : TeamSide.Home;
        var loser = winner == TeamSide.Away ? TeamSide.Home : TeamSide.Away;

        int winnerRuns = 0, loserRuns = 0;
        (PlayerId WinPitcher, PlayerId LossPitcher)? takeover = null;
        foreach (var play in state.Plays)
            foreach (var run in play.Runs)
            {
                var before = winnerRuns - loserRuns;
                if (play.Batting == winner) winnerRuns++; else loserRuns++;
                var after = winnerRuns - loserRuns;
                if (before <= 0 && after > 0)
                    takeover = (play.BattingTeamPitcher, run.ResponsiblePitcher);
                else if (after <= 0)
                    takeover = null;
            }

        if (takeover is not { } lead)
            return ImmutableList<PitcherDecision>.Empty;

        var winTeam = state.Team(winner);
        var winPitcher = lead.WinPitcher;
        var usage = winTeam.PitcherUsages.First(u => u.Pitcher == winPitcher);
        if (winPitcher == winTeam.StartingPitcher && usage.OutsRecorded < state.Rules.StarterWinMinOuts)
        {
            var relievers = winTeam.PitcherUsages.Where(u => u.Pitcher != winTeam.StartingPitcher).ToList();
            if (relievers.Count > 0)
                winPitcher = relievers.OrderByDescending(u => u.OutsRecorded).ThenBy(u => u.EntrySequence).First().Pitcher;
        }

        var decisions = ImmutableList.CreateBuilder<PitcherDecision>();
        decisions.Add(new PitcherDecision(winner, winPitcher, DecisionKind.Win));
        decisions.Add(new PitcherDecision(loser, lead.LossPitcher, DecisionKind.Loss));

        var finisher = winTeam.PitcherUsages[^1];
        if (finisher.Pitcher != winPitcher && finisher.EnteredWithLead > 0 && LeadHeldSince(state, winner, finisher.EntrySequence))
        {
            var rules = state.Rules;
            var closer = (finisher.EnteredWithLead <= rules.SaveMaxLead && finisher.OutsRecorded >= rules.SaveMinOuts)
                || finisher.EnteredWithLead <= finisher.EnteredRunnersOnBase + 2
                || finisher.OutsRecorded >= rules.SaveLongOuts;
            if (closer)
                decisions.Add(new PitcherDecision(winner, finisher.Pitcher, DecisionKind.Save));
        }

        return decisions.ToImmutable();
    }

    private static bool LeadHeldSince(GameState state, TeamSide winner, int entrySequence)
    {
        int winnerRuns = 0, loserRuns = 0;
        foreach (var play in state.Plays)
        {
            foreach (var _ in play.Runs)
                if (play.Batting == winner) winnerRuns++; else loserRuns++;
            if (play.Sequence > entrySequence && winnerRuns - loserRuns <= 0)
                return false;
        }

        return true;
    }
}
