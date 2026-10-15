using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;

namespace StarPlayer.Simulation.Baseball;

/// <summary>
/// The deterministic baseball reducer (C08, Phase 2): it checks a supplied event against the resolved GameRules snapshot and
/// the current state and returns the next state plus the official evidence, or an explicit error and nothing else. It never
/// chooses an outcome, uses no random numbers, clock or I/O, and does not mutate its inputs.
/// </summary>
public static partial class GameReducer
{
    private static readonly FieldPosition[] FieldingPositions =
    [
        FieldPosition.Catcher, FieldPosition.FirstBase, FieldPosition.SecondBase, FieldPosition.ThirdBase, FieldPosition.Shortstop,
        FieldPosition.LeftField, FieldPosition.CenterField, FieldPosition.RightField,
    ];

    internal static Result<T> Fail<T>(string code, string message) => Result<T>.Failure(new Error(code, message));

    public static Result<GameState> Start(GameStart? start)
    {
        if (start is null || string.IsNullOrWhiteSpace(start.GameId) || start.GameId != start.GameId.Trim())
            return Fail<GameState>("Game.start_invalid", "경기 시작 정보 또는 경기 ID가 올바르지 않습니다.");
        if (start.Rules is null)
            return Fail<GameState>("Game.rules_missing", "경기 규칙 스냅샷이 없습니다.");
        if (GameExecutionRules.Validate(start.Execution) is { } badExecution)
            return Result<GameState>.Failure(badExecution);

        var away = BuildTeam(start.Rules, start.Away, "원정");
        if (!away.IsSuccess) return Result<GameState>.Failure(away.Error!.Value);
        var home = BuildTeam(start.Rules, start.Home, "홈");
        if (!home.IsSuccess) return Result<GameState>.Failure(home.Error!.Value);
        if (away.Value.Appeared.Concat(away.Value.Bench).Intersect(home.Value.Appeared.Concat(home.Value.Bench)).Any())
            return Fail<GameState>("Game.player_on_both_teams", "같은 선수가 두 팀에 등록되어 있습니다.");

        var state = new GameState(
            start.GameId, start.Rules, 1, InningHalf.Top, 0, PitchCountState.Zero, RunnerState.Empty, 0, 0,
            ImmutableList.Create(0), ImmutableList<int>.Empty, PaStage.Start, null, null, away.Value, home.Value,
            ImmutableList<ScoredPlay>.Empty, ImmutableList<StatEntry>.Empty, ImmutableList<SubstitutionRecord>.Empty, ImmutableList<EventReceipt>.Empty,
            1, GameStatus.InProgress, null, null, start.Execution);
        return Result<GameState>.Success(state);
    }

    private static Result<TeamGameState> BuildTeam(GameRuleSnapshot rules, TeamSetup? setup, string label)
    {
        if (setup is null || setup.Lineup is null || setup.Bench is null)
            return Fail<TeamGameState>("Game.team_missing", $"{label} 팀 구성이 없습니다.");
        var lineup = setup.Lineup.OrderBy(s => s.Order).ToList();
        if (lineup.Count != 9 || !lineup.Select(s => s.Order).SequenceEqual(Enumerable.Range(1, 9)))
            return Fail<TeamGameState>("Game.lineup_invalid", $"{label} 타순은 1~9번이 정확히 한 번씩 있어야 합니다.");
        if (lineup.Any(s => s.Player == default))
            return Fail<TeamGameState>("Game.lineup_invalid", $"{label} 타순에 선수 ID가 없는 칸이 있습니다.");
        if (setup.StartingPitcher == default)
            return Fail<TeamGameState>("Game.pitcher_missing", $"{label} 선발 투수가 없습니다.");

        var expected = rules.DesignatedHitter
            ? FieldingPositions.Append(FieldPosition.DesignatedHitter).ToList()
            : FieldingPositions.Append(FieldPosition.Pitcher).ToList();
        if (!lineup.Select(s => s.Position).Order().SequenceEqual(expected.Order()))
            return Fail<TeamGameState>("Game.lineup_positions_invalid", $"{label} 수비 위치가 규칙({(rules.DesignatedHitter ? "지명타자 있음" : "지명타자 없음")})과 맞지 않습니다.");

        var distinct = lineup.Select(s => s.Player).Distinct().Count() == 9;
        if (!distinct)
            return Fail<TeamGameState>("Game.lineup_duplicate_player", $"{label} 타순에 같은 선수가 두 번 있습니다.");
        if (rules.DesignatedHitter)
        {
            var pitcherInLineup = lineup.FirstOrDefault(s => s.Player == setup.StartingPitcher);
            if (pitcherInLineup is not null && !(rules.TwoWayPlayerRule && pitcherInLineup.Position == FieldPosition.DesignatedHitter))
                return Fail<TeamGameState>("Game.pitcher_in_lineup", $"{label} 선발 투수는 투타 겸업 규칙이 있을 때 지명타자 자리에만 들어갈 수 있습니다.");
        }
        else if (lineup.Single(s => s.Position == FieldPosition.Pitcher).Player != setup.StartingPitcher)
            return Fail<TeamGameState>("Game.pitcher_slot_mismatch", $"{label} 타순의 투수 자리 선수와 선발 투수가 다릅니다.");

        var bench = setup.Bench.ToList();
        if (bench.Any(p => p == default) || bench.Distinct().Count() != bench.Count
            || bench.Any(p => p == setup.StartingPitcher || lineup.Any(s => s.Player == p)))
            return Fail<TeamGameState>("Game.bench_invalid", $"{label} 벤치에 중복되거나 이미 출전 중인 선수가 있습니다.");

        var onField = lineup.Select(s => s.Player).Append(setup.StartingPitcher).Distinct().ToList();
        var usage = new PitcherUsage(setup.StartingPitcher, 0, 1, InningHalf.Top, 0, 0, 0, 0, 0);
        return Result<TeamGameState>.Success(new TeamGameState(
            lineup.ToImmutableList(), setup.StartingPitcher, setup.StartingPitcher, ImmutableList.Create(usage), bench.ToImmutableList(),
            ImmutableList<PlayerId>.Empty, onField.ToImmutableList(), lineup.Select(s => s.Player).ToImmutableList(), 0, rules.DesignatedHitter,
            rules.DesignatedHitter ? lineup.Single(s => s.Position == FieldPosition.DesignatedHitter).Player : null));
    }

    /// <summary>Whether the team could take the field right now: with no active DH the pitcher must also be the player in the pitcher's lineup slot.</summary>
    internal static bool DefenseComplete(TeamGameState team) =>
        team.DhActive || team.SlotAt(FieldPosition.Pitcher)?.Player == team.Pitcher;

    public static LegalActionSet LegalActions(GameState state)
    {
        if (state.Status == GameStatus.Final)
            return new LegalActionSet(state.Stage, state.Final is null ? [GameActionKind.Finalize] : [], true, true);
        var defenseComplete = DefenseComplete(state.Fielding);
        if (!defenseComplete)
            return new LegalActionSet(state.Stage, [GameActionKind.Substitution], false, false);

        var actions = ImmutableList.CreateBuilder<GameActionKind>();
        switch (state.Stage)
        {
            case PaStage.Start or PaStage.Decision:
                actions.Add(GameActionKind.Approach);
                actions.Add(GameActionKind.Take);
                break;
            case PaStage.InSequence:
                actions.Add(GameActionKind.Pitch);
                actions.Add(GameActionKind.InPlay);
                break;
        }

        if (state.Bases.Count > 0)
            actions.Add(GameActionKind.RunnerPlay);
        if (state.Stage != PaStage.InSequence)
            actions.Add(GameActionKind.Substitution);
        return new LegalActionSet(state.Stage, actions.ToImmutable(), true, false);
    }
}
