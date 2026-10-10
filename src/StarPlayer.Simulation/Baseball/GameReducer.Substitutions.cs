using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;

namespace StarPlayer.Simulation.Baseball;

public static partial class GameReducer
{
    private static Result<GameStep> Substituted(GameState s, SubstitutionEvent e, GameState next, PlayerId outPlayer, PlayerId inPlayer, int slot, FieldPosition position)
    {
        var record = new SubstitutionRecord(e.Id, s.Inning, s.Half, e.Team, e.Kind, outPlayer, inPlayer, slot, position);
        next = next with { Substitutions = next.Substitutions.Add(record) };
        return Result<GameStep>.Success(new GameStep(next, GameStatDelta.Empty(e.Id), ImmutableList<ScoredPlay>.Empty,
            ImmutableList.Create<GameFact>(new SubstitutionMade(s.GameId, e.Id, e.Team, e.Kind, outPlayer, inPlayer)), false));
    }

    private static GameState WithTeam(GameState s, TeamSide side, TeamGameState team) =>
        side == TeamSide.Away ? s with { Away = team } : s with { Home = team };

    private static TeamGameState Enter(TeamGameState team, PlayerId incoming, bool intoLineup) => team with
    {
        Bench = team.Bench.Remove(incoming),
        Appeared = team.Appeared.Contains(incoming) ? team.Appeared : team.Appeared.Add(incoming),
        EverInLineup = intoLineup && !team.EverInLineup.Contains(incoming) ? team.EverInLineup.Add(incoming) : team.EverInLineup,
    };

    private static TeamGameState Leave(TeamGameState team, PlayerId outgoing) =>
        team.Removed.Contains(outgoing) ? team : team with { Removed = team.Removed.Add(outgoing) };

    /// <summary>
    /// OBR 5.11(a)(2): the DH named in the starting lineup must come to bat at least once unless the opposing club changes pitchers. True while that duty is
    /// still open for <paramref name="player"/>: he is this team's starting DH, the team still has its DH, the opposing club has not changed pitchers, and he has
    /// neither completed a plate appearance nor taken a pitch in the one he is in.
    /// </summary>
    private static bool StartingDhMustStillBat(GameState s, TeamSide side, PlayerId player)
    {
        var team = s.Team(side);
        if (!team.DhActive || team.StartingDh != player)
            return false;
        if (s.Team(side == TeamSide.Away ? TeamSide.Home : TeamSide.Away).PitcherUsages.Count > 1)
            return false;
        if (s.Plays.Any(p => p.Batting == side && p.Batter == player))
            return false;
        return !(side == s.BattingSide && s.Stage != PaStage.Start && team.CurrentBatterSlot.Player == player);
    }

    private static Result<GameStep> DhMustBat() =>
        Fail<GameStep>("Game.dh_must_bat", "선발 지명타자는 상대 팀이 투수를 바꾸기 전에는 최소 한 번 타석에 서야 하므로 첫 타석 전에 교체할 수 없습니다(OBR 5.11(a)(2)).");

    private static Result<GameStep> ApplySubstitution(GameState s, SubstitutionEvent e)
    {
        if (s.Stage == PaStage.InSequence)
            return Fail<GameStep>("Game.substitution_not_legal", "투구가 진행 중인 타석에서는 선수를 교체할 수 없습니다.");
        if (!Enum.IsDefined(e.Kind) || !Enum.IsDefined(e.Team))
            return Fail<GameStep>("Game.substitution_invalid", "교체 종류 또는 팀이 올바르지 않습니다.");

        var team = s.Team(e.Team);
        var rules = s.Rules;
        bool IsTwoWayDhPitching(PlayerId player) => team.DhActive && player == team.Pitcher && team.SlotOf(player) is { Position: FieldPosition.DesignatedHitter };

        switch (e.Kind)
        {
            case SubstitutionKind.PinchHit:
            {
                if (e.Team != s.BattingSide)
                    return Fail<GameStep>("Game.pinch_hit_team", "대타는 공격 중인 팀만 낼 수 있습니다.");
                if (!team.Bench.Contains(e.Player))
                    return Fail<GameStep>("Game.substitute_not_on_bench", "벤치에 없거나 이미 교체된 선수는 투입할 수 없습니다.");
                var slot = team.CurrentBatterSlot;
                if (IsTwoWayDhPitching(slot.Player))
                    return Fail<GameStep>("Game.two_way_dh_replaced_while_pitching", "투구 중인 투타 겸업 선수를 지명타자 자리에서 교체할 수 없습니다. 먼저 투수를 교체하세요.");
                if (StartingDhMustStillBat(s, e.Team, slot.Player))
                    return DhMustBat();
                var changed = Enter(Leave(team, slot.Player), e.Player, true) with { Lineup = team.Lineup.Replace(slot, slot with { Player = e.Player }) };
                return Substituted(s, e, WithTeam(s, e.Team, changed), slot.Player, e.Player, slot.Order, slot.Position);
            }
            case SubstitutionKind.PinchRun:
            {
                if (e.Team != s.BattingSide)
                    return Fail<GameStep>("Game.pinch_run_team", "대주자는 공격 중인 팀만 낼 수 있습니다.");
                if (e.Base is not { } @base || @base is < 1 or > 3 || s.Bases.At(@base) is not { } runner)
                    return Fail<GameStep>("Game.pinch_run_base", "대주자를 쓸 주자가 해당 루에 없습니다.");
                if (!team.Bench.Contains(e.Player))
                    return Fail<GameStep>("Game.substitute_not_on_bench", "벤치에 없거나 이미 교체된 선수는 투입할 수 없습니다.");
                if (IsTwoWayDhPitching(runner.Player))
                    return Fail<GameStep>("Game.two_way_dh_replaced_while_pitching", "투구 중인 투타 겸업 선수를 지명타자 자리에서 교체할 수 없습니다. 먼저 투수를 교체하세요.");
                var slot = team.SlotByOrder(runner.LineupOrder);
                var changed = Enter(Leave(team, runner.Player), e.Player, true) with { Lineup = team.Lineup.Replace(slot, slot with { Player = e.Player }) };
                var next = WithTeam(s, e.Team, changed) with { Bases = s.Bases.With(@base, runner with { Player = e.Player }) };
                return Substituted(s, e, next, runner.Player, e.Player, slot.Order, slot.Position);
            }
            case SubstitutionKind.DefensiveSub:
            {
                if (e.Slot is not { } order || order is < 1 or > 9)
                    return Fail<GameStep>("Game.substitution_slot_invalid", "교체할 타순 번호(1~9)가 올바르지 않습니다.");
                if (!team.Bench.Contains(e.Player))
                    return Fail<GameStep>("Game.substitute_not_on_bench", "벤치에 없거나 이미 교체된 선수는 투입할 수 없습니다.");
                var slot = team.SlotByOrder(order);
                if (slot.Position == FieldPosition.Pitcher)
                    return Fail<GameStep>("Game.use_pitching_change", "투수 자리는 투수 교체로만 바꿀 수 있습니다.");
                if (IsTwoWayDhPitching(slot.Player))
                    return Fail<GameStep>("Game.two_way_dh_replaced_while_pitching", "투구 중인 투타 겸업 선수를 지명타자 자리에서 교체할 수 없습니다. 먼저 투수를 교체하세요.");
                if (e.Team == s.BattingSide && (s.Bases.Occupied().Any(o => o.Runner.LineupOrder == order) || order == team.CurrentBatterSlot.Order))
                    return Fail<GameStep>("Game.use_pinch_substitution", "주루 중이거나 지금 타석에 설 선수는 대타/대주자로만 바꿀 수 있습니다.");
                if (slot.Position == FieldPosition.DesignatedHitter && StartingDhMustStillBat(s, e.Team, slot.Player))
                    return DhMustBat();
                var changed = Enter(Leave(team, slot.Player), e.Player, true) with { Lineup = team.Lineup.Replace(slot, slot with { Player = e.Player }) };
                return Substituted(s, e, WithTeam(s, e.Team, changed), slot.Player, e.Player, slot.Order, slot.Position);
            }
            case SubstitutionKind.PositionSwap:
            {
                if (e.Slot is not { } a || e.Slot2 is not { } b || a == b || a is < 1 or > 9 || b is < 1 or > 9)
                    return Fail<GameStep>("Game.substitution_slot_invalid", "위치를 맞바꿀 두 타순 번호가 올바르지 않습니다.");
                var first = team.SlotByOrder(a);
                var second = team.SlotByOrder(b);
                if (first.Position is FieldPosition.Pitcher or FieldPosition.DesignatedHitter || second.Position is FieldPosition.Pitcher or FieldPosition.DesignatedHitter)
                    return Fail<GameStep>("Game.position_swap_invalid", "투수·지명타자 자리는 위치 맞교환으로 바꿀 수 없습니다.");
                var lineup = team.Lineup.Replace(first, first with { Position = second.Position }).Replace(second, second with { Position = first.Position });
                return Substituted(s, e, WithTeam(s, e.Team, team with { Lineup = lineup }), first.Player, second.Player, first.Order, second.Position);
            }
            case SubstitutionKind.DhToField:
                return ApplyDhToField(s, e, team);
            case SubstitutionKind.NameDesignatedHitter:
                return team.DhActive
                    ? Fail<GameStep>("Game.dh_already_active", "경기 중에는 지명타자를 새로 지정할 수 없습니다. 지명타자는 경기 시작 때 정해집니다.")
                    : Fail<GameStep>("Game.dh_cannot_be_reactivated", "한 번 소멸한 지명타자는 그 경기에서 다시 쓸 수 없습니다.");
            default:
                return ApplyPitchingChange(s, e, team);
        }
    }

    /// <summary>
    /// DH forfeiture transition (profiles with <c>DhMayBeForfeited</c> only, OBR 5.11(a)(12), docs/game-engine.md section 4): the designated hitter takes the
    /// field position of the fielder in <c>Slot</c>, that fielder leaves the game, the current pitcher bats in that fielder's lineup slot,
    /// and this team's DH role ends for the rest of the game. The other team's DH state is untouched.
    /// </summary>
    private static Result<GameStep> ApplyDhToField(GameState s, SubstitutionEvent e, TeamGameState team)
    {
        if (!s.Rules.DhMayBeForfeited)
            return Fail<GameStep>("Game.dh_forfeit_not_allowed", "이 규칙 프로필은 경기 중 지명타자 소멸 전환을 허용하지 않습니다.");
        if (!team.DhActive)
            return Fail<GameStep>("Game.dh_not_active", "이 팀의 지명타자는 없거나 이미 소멸했습니다.");
        if (e.Slot is not { } order || order is < 1 or > 9)
            return Fail<GameStep>("Game.substitution_slot_invalid", "교체할 타순 번호(1~9)가 올바르지 않습니다.");
        var fielder = team.SlotByOrder(order);
        if (fielder.Position is FieldPosition.Pitcher or FieldPosition.DesignatedHitter)
            return Fail<GameStep>("Game.dh_forfeit_target_invalid", "지명타자가 대신 들어갈 자리는 야수 자리여야 합니다.");
        var dhSlot = team.Lineup.Single(x => x.Position == FieldPosition.DesignatedHitter);
        if (dhSlot.Player == team.Pitcher)
            return Fail<GameStep>("Game.two_way_dh_field_while_pitching", "투구 중인 투타 겸업 선수는 수비 위치로 옮길 수 없습니다. 먼저 투수를 교체하세요.");
        if (e.Team == s.BattingSide && (s.Bases.Occupied().Any(o => o.Runner.LineupOrder == order) || order == team.CurrentBatterSlot.Order))
            return Fail<GameStep>("Game.use_pinch_substitution", "주루 중이거나 지금 타석에 설 선수는 대타/대주자로만 바꿀 수 있습니다.");

        var pitcher = team.Pitcher;
        var lineup = team.Lineup.Replace(dhSlot, dhSlot with { Position = fielder.Position }).Replace(fielder, fielder with { Player = pitcher, Position = FieldPosition.Pitcher });
        var changed = Leave(team, fielder.Player) with
        {
            Lineup = lineup,
            DhActive = false,
            EverInLineup = team.EverInLineup.Contains(pitcher) ? team.EverInLineup : team.EverInLineup.Add(pitcher),
        };
        return Substituted(s, e, WithTeam(s, e.Team, changed), fielder.Player, pitcher, order, FieldPosition.Pitcher);
    }

    private static Result<GameStep> ApplyPitchingChange(GameState s, SubstitutionEvent e, TeamGameState team)
    {
        var rules = s.Rules;
        if (e.Team != s.FieldingSide)
            return Fail<GameStep>("Game.pitching_change_team", "투수 교체는 수비 중인 팀만 할 수 있습니다.");
        if (e.Player == default || e.Player == team.Pitcher)
            return Fail<GameStep>("Game.pitching_change_invalid", "현재 투수와 다른 새 투수를 지정해야 합니다.");
        if (team.PitcherUsages.Any(u => u.Pitcher == e.Player))
            return Fail<GameStep>("Game.pitcher_already_used", "이미 투구한 투수는 다시 마운드에 오를 수 없습니다.");
        if (team.Removed.Contains(e.Player))
            return Fail<GameStep>("Game.player_removed", "교체되어 물러난 선수는 다시 출전할 수 없습니다.");

        var old = team.Pitcher;
        var fromBench = team.Bench.Contains(e.Player);
        TeamGameState changed;
        if (fromBench)
        {
            changed = Enter(team, e.Player, intoLineup: !team.DhActive);
            if (!team.DhActive)
            {
                var pitcherSlot = team.SlotAt(FieldPosition.Pitcher)!;
                changed = Leave(changed, pitcherSlot.Player) with { Lineup = changed.Lineup.Replace(pitcherSlot, pitcherSlot with { Player = e.Player }) };
                if (pitcherSlot.Player != old)
                    changed = Leave(changed, old);
            }
            else if (team.SlotOf(old) is null)
                changed = Leave(changed, old);
        }
        else
        {
            var slot = team.SlotOf(e.Player);
            if (slot is null)
                return Fail<GameStep>("Game.pitcher_not_eligible", "벤치에도 타순에도 없는 선수는 투수가 될 수 없습니다.");
            if (team.DhActive)
            {
                if (slot.Position == FieldPosition.DesignatedHitter)
                {
                    // The DH takes a defensive position (the mound): OBR 5.11(a)(12) ends the DH role. The 5.11(b) exception belongs to the starting pitcher
                    // listed twice, and that player either is the pitcher now or has already pitched, so both were refused above: nobody reaches this branch
                    // as an exception. A profile that does not let the DH end refuses the move instead of keeping the role.
                    if (!rules.DhMayBeForfeited)
                        return Fail<GameStep>("Game.pitcher_not_eligible", "지명타자가 투수가 되면 지명타자 역할이 소멸하는데, 이 규칙 프로필은 경기 중 지명타자 소멸을 허용하지 않습니다.");
                    var lineup = team.Lineup.Replace(slot, slot with { Position = FieldPosition.Pitcher });
                    changed = Leave(team with { Lineup = lineup, DhActive = false }, old);
                }
                else if (rules.FielderToMoundForfeitsDh && slot.Position is not FieldPosition.Pitcher)
                {
                    // OBR 5.11(a)(14): a lineup fielder goes to the mound, the DH role ends. The text does not say who fills the vacated position, so the event must
                    // say it (Slot = the DH's batting-order number: the DH takes the position, the replaced pitcher leaves); nothing is chosen or corrected here.
                    var dhSlot = team.Lineup.Single(x => x.Position == FieldPosition.DesignatedHitter);
                    if (dhSlot.Player == old)
                        return Fail<GameStep>("Game.two_way_dh_forfeit_conflict", "투구 중인 투타 겸업 선수가 지명타자인 동안에는 야수를 투수로 올려 지명타자를 소멸시킬 수 없습니다.");
                    if (e.Slot is null)
                        return Fail<GameStep>("Game.dh_vacated_position_unspecified", "야수가 마운드에 오르면 비는 수비 위치를 누가 채울지 정해져 있지 않습니다. 지명타자가 채우려면 Slot에 지명타자의 타순 번호를 지정해야 합니다.");
                    if (e.Slot != dhSlot.Order)
                        return Fail<GameStep>("Game.dh_vacated_position_invalid", "비는 수비 위치는 지명타자만 채울 수 있습니다(다른 배치는 지원하지 않습니다). Slot은 지명타자의 타순 번호여야 합니다.");
                    var lineup = team.Lineup.Replace(slot, slot with { Position = FieldPosition.Pitcher }).Replace(dhSlot, dhSlot with { Position = slot.Position });
                    changed = Leave(team with { Lineup = lineup, DhActive = false }, old);
                }
                else
                    return Fail<GameStep>("Game.pitcher_not_eligible", "지명타자 제도에서는 지명타자 소멸이 허용되는 전환(야수가 투수가 되거나 지명타자가 투수가 됨)만 타순에서 곧바로 투수가 될 수 있습니다.");
            }
            else
            {
                var pitcherSlot = team.SlotAt(FieldPosition.Pitcher)!;
                var lineup = team.Lineup.Replace(slot, slot with { Position = FieldPosition.Pitcher }).Replace(pitcherSlot, pitcherSlot with { Position = slot.Position });
                changed = team with { Lineup = lineup };
            }
        }

        var ownRuns = s.Runs(e.Team);
        var theirRuns = s.Runs(e.Team == TeamSide.Away ? TeamSide.Home : TeamSide.Away);
        var usage = new PitcherUsage(e.Player, s.Plays.Count, s.Inning, s.Half, ownRuns - theirRuns, s.Bases.Count, 0, 0, 0);
        changed = changed with
        {
            Pitcher = e.Player,
            PitcherUsages = changed.PitcherUsages.Add(usage),
            Appeared = changed.Appeared.Contains(e.Player) ? changed.Appeared : changed.Appeared.Add(e.Player),
        };

        // OBR 9.16(h)(1): a walk to the first batter the reliever faces is the previous pitcher's when the batter was ahead in the count (2-0, 2-1, 3-0, 3-1, 3-2)
        var midCountWalk = s.Stage == PaStage.Decision && s.Count.Balls >= 2 && s.Count.Balls < rules.BallsForWalk && s.Count.Balls > s.Count.Strikes;
        var next = WithTeam(s, e.Team, changed) with { WalkChargeOverride = midCountWalk ? s.WalkChargeOverride ?? old : s.WalkChargeOverride };
        return Substituted(s, e, next, old, e.Player, 0, FieldPosition.Pitcher);
    }
}
