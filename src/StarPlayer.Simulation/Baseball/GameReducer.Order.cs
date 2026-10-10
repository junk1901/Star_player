using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;

namespace StarPlayer.Simulation.Baseball;

public static partial class GameReducer
{
    /// <summary>
    /// The verified order and meaning of the outs and runs of one play: resolved sequence per runner key, the rule classification of each
    /// out (derived from the state, never trusted from the input), and which runs count under <see cref="ThirdOutRule"/>.
    /// </summary>
    private sealed record Resolution(
        Dictionary<int, int> Sequence,
        Dictionary<int, OutClassification> Class,
        Dictionary<int, int> OutAt,
        HashSet<int> CountedRuns,
        IReadOnlyList<Terminal> Terminals);

    /// <summary>An out for the live record, or an error-saved out / batter-reached-on-error that only the earned-run reconstruction treats as an out.</summary>
    private static bool IsReconOut(Plan p) =>
        p.To != 0 && (p.Cause == MoveCause.ErrorSavedOut || (p.From == 0 && p.Cause == MoveCause.PassedBall));

    /// <summary>
    /// Puts the play's outs and runs in order, derives each out's classification from the state (force only if the runner really was
    /// forced at that moment, the base he was forced to being where he was put out), checks the declared out kinds against it and decides
    /// which runs count. Rejects inputs without the information the rules need.
    /// </summary>
    private static Result<Resolution> ResolveOrder(GameState s, PlayInput inp, IReadOnlyList<Plan> plans, int outsAfter)
    {
        var limit = s.Rules.OutsPerHalfInning;
        if (plans.Any(p => (p.OutAt is not null || p.Method is not null) && !(p.To == 0 || IsReconOut(p))))
            return Fail<Resolution>("Game.out_at_without_out", "아웃 베이스와 아웃 방법은 아웃되는 주자(또는 실책으로 아웃을 면한 주자)에게만 지정할 수 있습니다.");
        var terminals = plans.Where(p => p.To is 0 or 4 || IsReconOut(p)).ToList();
        if (plans.Any(p => p.Sequence is not null && !terminals.Contains(p)))
            return Fail<Resolution>("Game.play_order_invalid", "아웃·득점 순서는 아웃되거나 득점하는 주자(또는 실책으로 아웃을 면한 주자)에게만 지정할 수 있습니다.");
        var batterPlan = plans.FirstOrDefault(p => p.From == 0);
        var batterIsTerminal = batterPlan is not null && terminals.Contains(batterPlan);
        var batterFirst = batterIsTerminal && inp.BatterOutFirst && batterPlan!.To == 0;
        var implicitFirst = batterFirst && batterPlan!.Sequence is null;

        var sequence = new Dictionary<int, int>();
        var explicitOnes = terminals.Count(p => p.Sequence.HasValue);
        var movable = terminals.Where(p => !(implicitFirst && ReferenceEquals(p, batterPlan))).ToList();
        if (explicitOnes > 0)
        {
            if (movable.Any(p => p.Sequence is null))
                return Fail<Resolution>("Game.play_order_incomplete", "아웃·득점 순서는 이 플레이의 모든 아웃과 득점에 지정하거나 하나도 지정하지 않아야 합니다.");
            var values = movable.Select(p => p.Sequence!.Value).ToList();
            if (values.Any(v => v < 1) || values.Distinct().Count() != values.Count)
                return Fail<Resolution>("Game.play_order_invalid", "아웃·득점 순서는 1 이상이며 서로 달라야 합니다.");
            foreach (var p in movable)
                sequence[p.Runner.RunnerKey] = p.Sequence!.Value;
            if (implicitFirst)
                sequence[batterPlan!.Runner.RunnerKey] = 0;
            if (batterFirst && sequence[batterPlan!.Runner.RunnerKey] != sequence.Values.Min())
                return Fail<Resolution>("Game.batter_out_order_invalid", "삼진 또는 포구된 뜬공/직선타/파울플라이의 타자 아웃은 항상 플레이의 첫 사건이어야 합니다.");
        }
        else
        {
            if (outsAfter == limit && plans.Any(p => p.To == 4))
                return Fail<Resolution>("Game.play_order_required", "3아웃과 득점이 함께 있는 플레이는 아웃과 득점의 발생 순서를 지정해야 합니다.");
            var ordered = new List<Plan>();
            if (batterFirst)
                ordered.Add(batterPlan!);
            ordered.AddRange(terminals.Where(p => p.From >= 1).OrderByDescending(p => p.From));
            if (batterIsTerminal && !batterFirst)
                ordered.Add(batterPlan!);
            for (var i = 0; i < ordered.Count; i++)
                sequence[ordered[i].Runner.RunnerKey] = i + 1;
        }

        var classes = new Dictionary<int, OutClassification>();
        var outAtByKey = new Dictionary<int, int>();
        var retired = new HashSet<int>();
        var batterForces = inp.BatterReaches;
        foreach (var plan in terminals.OrderBy(p => sequence[p.Runner.RunnerKey]))
        {
            var key = plan.Runner.RunnerKey;
            if (plan.From == 0)
            {
                if (plan.PostReach)
                {
                    // out after a safe hit: a non-force tag out beyond the base the hit gave him (validated against the hit base when the plan was built)
                    classes[key] = OutClassification.NonForce;
                    outAtByKey[key] = plan.OutAt!.Value;
                    if (plan.Method == OutMethod.BaseTouch)
                        return Fail<Resolution>("Game.appeal_not_supported", "베이스 터치로 비포스 주자를 아웃시키는 것은 어필 플레이이며 지원하지 않습니다.");
                    continue;
                }

                if (plan.To == 0 || IsReconOut(plan))
                    classes[key] = OutClassification.BatterRunner;
                if (plan.To == 0)
                    batterForces = false;
                if (plan.OutAt is not null || plan.Method is not null)
                    return Fail<Resolution>("Game.out_detail_invalid", "타자의 아웃에는 아웃 베이스나 방법을 따로 지정하지 않습니다.");
                continue;
            }

            var isOut = plan.To == 0;
            if (!isOut && !IsReconOut(plan))
                continue;

            var from = plan.From;
            var outAt = plan.OutAt ?? from + 1;
            if (outAt < from || outAt > 4)
                return Fail<Resolution>("Game.out_at_invalid", "아웃 베이스는 주자의 현재 루 이상 홈(4) 이하여야 합니다.");
            var forced = batterForces && Enumerable.Range(1, from - 1).All(b => s.Bases.At(b) is not null && !retired.Contains(b));
            var isForce = forced && outAt == from + 1;
            classes[key] = isForce ? OutClassification.Force : OutClassification.NonForce;
            outAtByKey[key] = outAt;
            if (!isOut)
                continue;

            if (plan.OutKind is null || !Enum.IsDefined(plan.OutKind.Value))
                return Fail<Resolution>("Game.out_kind_missing", "주자 아웃에는 아웃 종류(포스/태그)를 반드시 지정해야 합니다.");
            if (plan.OutKind == OutKind.Other)
                return Fail<Resolution>("Game.appeal_not_supported", "'기타' 아웃(어필·방해 등)은 지원하지 않습니다. 주자 아웃은 포스 또는 태그여야 합니다.");
            if (inp.CaughtBall && outAt == from)
                return Fail<Resolution>("Game.appeal_not_supported", "뜬공이 잡힌 뒤 원래 베이스로 돌아오지 못한 주자의 아웃(리터치 어필)은 지원하지 않습니다.");
            if (plan.Method is { } method && !Enum.IsDefined(method))
                return Fail<Resolution>("Game.out_method_invalid", "아웃 방법이 올바르지 않습니다.");
            if (isForce && plan.OutKind != OutKind.Force)
                return Fail<Resolution>("Game.out_kind_mismatch", "강제 상황에서 강제 진루 베이스에서 잡힌 아웃은 방법(태그/베이스 터치)과 관계없이 포스아웃입니다.");
            if (!isForce && plan.OutKind == OutKind.Force)
                return Fail<Resolution>("Game.force_out_invalid", "강제 상황이 아닌 주자(앞선 아웃으로 강제가 풀렸거나 강제 진루 베이스가 아닌 곳의 아웃)에게 포스아웃을 줄 수 없습니다.");
            if (plan.Method == OutMethod.BaseTouch && plan.OutKind == OutKind.Tag)
                return Fail<Resolution>("Game.out_method_invalid", "태그 아웃은 베이스 터치 방법과 함께 쓸 수 없습니다.");
            retired.Add(from);
        }

        var ordered2 = terminals.OrderBy(p => sequence[p.Runner.RunnerKey])
            .Select(p => new Terminal(p.Runner.RunnerKey, p.To == 4, p.To == 0 || IsReconOut(p), sequence[p.Runner.RunnerKey],
                classes.GetValueOrDefault(p.Runner.RunnerKey, OutClassification.NonForce), IsReconOut(p)))
            .ToList();
        var rule = ThirdOutRule.Evaluate(s.Outs, limit, ordered2, includeReconOuts: false);
        return Result<Resolution>.Success(new Resolution(sequence, classes, outAtByKey, rule.CountedRuns, ordered2));
    }
}
