using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;

namespace StarPlayer.Simulation.Baseball;

/// <summary>
/// Run responsibility, OBR 9.16(g) with its Comment: "charge each pitcher with the number of runners he put on base, rather than with the individual
/// runners". Each runner on base occupies one responsibility SLOT of the pitcher who put a runner there (<see cref="BaseRunner.ResponsiblePitcher"/>); the
/// slot is the quantity, the runner is only where it currently sits. Three rules move slots (docs/game-engine.md section 4b):
/// 1. a batter who reaches on a fielder's choice that puts out a runner takes the slot of the runner put out (the slot is not lost, 9.16(g) text); when the
///    runners put out belong to different pitchers the play is refused instead (<c>Game.ambiguous_responsibility</c>, Phase 2 support limit);
/// 2. a runner put out any other way (caught stealing, picked off, out on his own advance, out at home on a hit) takes his slot with him;
/// 3. a run is charged to the oldest slot among the scoring runners and the runners still on base, and the slot the scorer carried goes back to a runner still
///    on base (the Comment: charged "up to and including the number of runners such pitcher left on base").
/// </summary>
internal static class RunResponsibility
{
    /// <summary>The slot a fielder's-choice batter takes: that of the pitcher of the runners put out on the play (callers refuse the mixed case); none when nobody was put out.</summary>
    public static PlayerId? FieldersChoiceSlot(IEnumerable<BaseRunner> runnersPutOut, Func<PlayerId, int> seniority) =>
        runnersPutOut.Select(r => r.ResponsiblePitcher).OrderBy(seniority).Cast<PlayerId?>().FirstOrDefault();

    /// <summary>
    /// Charges the runs of one play. <paramref name="scorers"/> are in scoring order, <paramref name="stayers"/> are the runners still on base after the play,
    /// lead runner first. Returns the pitcher charged with each scorer's run and the slot each stayer now holds (a stayer keeps his own slot when it is still
    /// available, so nothing moves unless a run was charged to a slot that sat on a different runner).
    /// </summary>
    public static (IReadOnlyList<PlayerId> Charged, IReadOnlyDictionary<int, PlayerId> StayerSlots) Settle(
        IReadOnlyList<BaseRunner> scorers, IReadOnlyList<BaseRunner> stayers, Func<PlayerId, int> seniority)
    {
        var slots = scorers.Concat(stayers).Select(r => r.ResponsiblePitcher).OrderBy(seniority).ToList(); // OrderBy is stable: equal pitchers keep their order
        var charged = slots.Take(scorers.Count).ToList();
        var free = slots.Skip(scorers.Count).ToList();

        var held = new Dictionary<int, PlayerId>();
        var moved = new List<BaseRunner>();
        foreach (var runner in stayers)
            if (free.Remove(runner.ResponsiblePitcher)) held[runner.RunnerKey] = runner.ResponsiblePitcher;
            else moved.Add(runner);
        for (var i = 0; i < moved.Count; i++)
            held[moved[i].RunnerKey] = free[i];
        return (charged, held);
    }
}

public static partial class GameReducer
{
    /// <summary>Seniority of a pitcher among the defending team's pitchers of this game: earlier entry is older (the "preceding pitcher" of 9.16(g)).</summary>
    private static Func<PlayerId, int> Seniority(GameState s) => pitcher => s.Fielding.PitcherUsages.FindIndex(u => u.Pitcher == pitcher);

    /// <summary>
    /// Rule 1 above: the batter who reaches on a fielder's choice takes the slot of the runner put out on that play. When runners of two or more different
    /// pitchers are put out on the same fielder's choice, the input cannot say which out was the fielder's choice and which was an independent out on a
    /// runner's own advance (9.16(g) passes a slot on for the first only), so the play is refused (<c>Game.ambiguous_responsibility</c>) before any state
    /// changes. This is a Phase 2 support limit by owner decision, not a statement of the OBR or KBO rule.
    /// </summary>
    private static Result<IReadOnlyList<Plan>> HandOverFieldersChoiceSlot(GameState s, PlayInput inp, IReadOnlyList<Plan> plans)
    {
        if (inp.Result != BatterResult.FieldersChoice || plans.FirstOrDefault(p => p.From == 0) is not { To: >= 1 and <= 3 } batter)
            return Result<IReadOnlyList<Plan>>.Success(plans);
        var putOut = plans.Where(p => p.From >= 1 && p.To == 0).Select(p => p.Runner).ToList();
        if (putOut.Select(r => r.ResponsiblePitcher).Distinct().Count() > 1)
            return Fail<IReadOnlyList<Plan>>("Game.ambiguous_responsibility",
                "야수선택으로 타자가 출루하면서 서로 다른 투수의 책임 주자가 둘 이상 아웃되었습니다. 어느 아웃이 야수선택이고 어느 아웃이 주자의 독립적인 추가 진루 아웃인지 입력으로 알 수 없어 실점 책임을 정할 수 없으므로 지원하지 않습니다(Phase 2 지원 범위 제한). 실제 플레이에 부합하는 공식 기록 분류로만 다시 입력할 수 있으며, 엔진은 결과를 바꾸지 않습니다.");
        if (RunResponsibility.FieldersChoiceSlot(putOut, Seniority(s)) is not { } slot)
            return Result<IReadOnlyList<Plan>>.Success(plans);
        return Result<IReadOnlyList<Plan>>.Success(plans.Select(p => ReferenceEquals(p, batter) ? p with { Runner = p.Runner with { ResponsiblePitcher = slot } } : p).ToList());
    }
}
