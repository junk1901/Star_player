using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;

namespace StarPlayer.Simulation.Baseball;

public static partial class GameReducer
{
    /// <summary>One runner's resolved destination for a play (before validation).</summary>
    private sealed record Plan(BaseRunner Runner, int From, int To, MoveCause Cause, OutKind? OutKind, ImmutableList<PositionCredit> Credits,
        int? Sequence = null, int? OutAt = null, OutMethod? Method = null, bool PostReach = false);

    private sealed class PlayInput
    {
        public required string EventId { get; init; }
        public required PlayKind Kind { get; init; }
        public required bool IsPa { get; init; }
        public BatterResult? Result { get; init; }
        public BattedBallType Batted { get; init; }
        public required List<Plan> Plans { get; init; }
        public ImmutableList<FieldPosition> Errors { get; init; } = ImmutableList<FieldPosition>.Empty;
        /// <summary>True when the batter-runner's own out is always the first event of the play (a strikeout or a caught fly/line/pop ball).</summary>
        public bool BatterOutFirst { get; init; }

        /// <summary>True for a ball caught for an out (fly, line, pop): a runner put out back at his own base then needs an appeal, which Star Player does not support.</summary>
        public bool CaughtBall { get; init; }
        public PitchCountState FinalCount { get; init; }
        public bool IsHomeRun { get; init; }
        public bool BatterReaches { get; init; }
        public bool WildPitch { get; init; }
        public bool PassedBall { get; init; }
        public bool Balk { get; init; }
        public bool Pickoff { get; init; }
        public PlayerId? Batter { get; init; }
        public PlayerId? ChargedPitcher { get; init; }
        public List<StatEntry> Pre { get; init; } = [];
    }

    private static bool IsErrorCause(MoveCause cause) => cause is MoveCause.ErrorExtraBase or MoveCause.ErrorSavedOut;

    private static bool IsFielding(FieldPosition position) => position != FieldPosition.DesignatedHitter;

    // ------------------------------------------------------------------ plans

    /// <summary>
    /// Turns explicit runner moves into plans and fills in runners without a move: they stay, or are pushed one base when forced
    /// by a batter who reaches. With <paramref name="requireAll"/> every runner must be described (batted-ball plays).
    /// </summary>
    private static Result<List<Plan>> CompletePlans(RunnerState bases, IEnumerable<RunnerMove> moves, bool batterReaches, bool requireAll, int? defaultTo = null)
    {
        var plans = new List<Plan>();
        foreach (var move in moves)
        {
            if (move.From is < 1 or > 3 || bases.At(move.From) is not { } runner)
                return Fail<List<Plan>>("Game.move_source_invalid", $"{move.From}루에 주자가 없습니다.");
            if (!Enum.IsDefined(move.Cause) || (move.OutKind is { } k && !Enum.IsDefined(k)))
                return Fail<List<Plan>>("Game.move_invalid", "주자 이동의 원인 또는 아웃 종류가 올바르지 않습니다.");
            plans.Add(new Plan(runner, move.From, move.To, move.Cause, move.OutKind, move.CreditList, move.Sequence, move.OutAt, move.Method));
        }

        foreach (var (@base, runner) in bases.Occupied())
        {
            if (plans.Any(p => p.From == @base))
                continue;
            if (requireAll && defaultTo is null)
                return Fail<List<Plan>>("Game.runner_move_missing", $"{@base}루 주자의 이동 결과가 지정되지 않았습니다.");
            if (defaultTo is { } to)
            {
                plans.Add(new Plan(runner, @base, to, MoveCause.Play, null, ImmutableList<PositionCredit>.Empty));
                continue;
            }

            var forced = batterReaches && Enumerable.Range(1, @base - 1).All(b => bases.At(b) is not null);
            plans.Add(new Plan(runner, @base, forced ? @base + 1 : @base, forced ? MoveCause.Forced : MoveCause.Play, null, ImmutableList<PositionCredit>.Empty));
        }

        return Result<List<Plan>>.Success(plans);
    }

    private static Error? ValidatePlans(GameState s, IReadOnlyList<Plan> plans)
    {
        var froms = plans.Select(p => p.From).ToList();
        if (froms.Distinct().Count() != froms.Count)
            return new Error("Game.runner_moved_twice", "같은 주자(또는 타자)가 한 플레이에 두 번 이동했습니다.");
        foreach (var plan in plans)
        {
            if (plan.To is < 0 or > 4)
                return new Error("Game.destination_invalid", "도착 베이스는 0(아웃)~4(득점)이어야 합니다.");
            if (plan.To != 0 && plan.To < Math.Max(plan.From, 1))
                return new Error("Game.runner_moved_backward", "주자는 뒤로 이동할 수 없습니다.");
            if (plan.From >= 1 && !ReferenceEquals(s.Bases.At(plan.From), plan.Runner) && s.Bases.At(plan.From) != plan.Runner)
                return new Error("Game.move_source_invalid", $"{plan.From}루의 주자와 이동 기록이 일치하지 않습니다.");
        }

        if (s.Bases.Occupied().Any(o => plans.All(p => p.From != o.Base)))
            return new Error("Game.runner_move_missing", "이동 결과가 없는 주자가 있습니다.");

        var onBase = plans.Where(p => p.To != 0).OrderByDescending(p => p.From).ToList();
        for (var i = 0; i < onBase.Count; i++)
        for (var j = i + 1; j < onBase.Count; j++)
        {
            if (onBase[j].To > onBase[i].To)
                return new Error("Game.runner_passed_runner", "뒤 주자가 앞 주자를 추월할 수 없습니다.");
            if (onBase[j].To == onBase[i].To && onBase[j].To < 4)
                return new Error("Game.base_occupied_twice", $"두 주자가 {onBase[j].To}루에 동시에 있을 수 없습니다.");
        }

        var outs = plans.Count(p => p.To == 0);
        if (s.Outs + outs > s.Rules.OutsPerHalfInning)
            return new Error("Game.too_many_outs", "한 이닝의 아웃 수를 넘는 플레이입니다.");

        foreach (var plan in plans.Where(p => p.To == 0))
        {
            if (plan.Credits.Count(c => c.Kind == CreditKind.Putout) != 1 || plan.Credits.Any(c => !IsFielding(c.Position) || !Enum.IsDefined(c.Position) || !Enum.IsDefined(c.Kind)))
                return new Error("Game.out_credit_invalid", "모든 아웃에는 수비 위치가 올바른 자살(putout) 기록이 정확히 하나 있어야 합니다.");
        }

        foreach (var plan in plans.Where(p => p.To != 0))
            if (plan.Credits.Count > 0)
                return new Error("Game.credits_without_out", "아웃이 아닌 이동에는 수비 기록을 붙일 수 없습니다.");
        return null;
    }

    private static Result<ImmutableList<FielderCredit>> ResolveCredits(GameState s, IEnumerable<Plan> plans)
    {
        var list = ImmutableList.CreateBuilder<FielderCredit>();
        foreach (var plan in plans.Where(p => p.To == 0))
            foreach (var credit in plan.Credits)
            {
                if (s.DefenderAt(s.FieldingSide, credit.Position) is not { } player)
                    return Fail<ImmutableList<FielderCredit>>("Game.fielder_missing", $"{credit.Position} 자리에 선수가 없습니다.");
                list.Add(new FielderCredit(credit.Position, player, credit.Kind));
            }

        return Result<ImmutableList<FielderCredit>>.Success(list.ToImmutable());
    }

    // ------------------------------------------------------------------ pitches

    /// <summary>Adds one real pitch to the pitcher's stint and returns the pitcher/catcher/battery stat entries for it.</summary>
    private static (GameState State, List<StatEntry> Entries) CountPitch(GameState s, string id)
    {
        var fld = s.FieldingSide;
        var pitcher = s.Fielding.Pitcher;
        var catcher = s.Catcher(fld);
        var team = s.Fielding;
        var index = team.PitcherUsages.FindLastIndex(u => u.Pitcher == pitcher);
        team = team with { PitcherUsages = team.PitcherUsages.SetItem(index, team.PitcherUsages[index] with { PitchCount = team.PitcherUsages[index].PitchCount + 1 }) };
        var entries = new List<StatEntry>
        {
            new(fld, pitcher, StatKey.PitchPitches, 1, id),
            new(fld, catcher, StatKey.CatPitches, 1, id),
            new(fld, catcher, StatKey.BatteryPitches, 1, id, pitcher),
        };
        return (fld == TeamSide.Away ? s with { Away = team } : s with { Home = team }, entries);
    }

    private static Result<GameStep> ApplyPitch(GameState s, string id, PitchSpec? spec, bool isTake)
    {
        if (spec is null || !Enum.IsDefined(spec.Outcome) || !Enum.IsDefined(spec.Mishap) || !Enum.IsDefined(spec.DroppedThirdStrike))
            return Fail<GameStep>("Game.pitch_invalid", "투구 정보가 올바르지 않습니다.");
        if (isTake && s.Stage == PaStage.InSequence)
            return Fail<GameStep>("Game.take_not_legal", "접근 방식이 정해진 타석에서는 Take를 쓸 수 없습니다.");
        if (!isTake && s.Stage != PaStage.InSequence)
            return Fail<GameStep>("Game.pitch_not_legal", "접근 방식을 먼저 정해야 투구를 진행할 수 있습니다.");
        if (isTake && spec.Outcome is not (PitchOutcome.Ball or PitchOutcome.CalledStrike or PitchOutcome.HitByPitch))
            return Fail<GameStep>("Game.take_outcome_invalid", "Take로 지켜본 투구는 볼, 스트라이크(루킹), 몸에 맞는 공만 가능합니다.");

        var rules = s.Rules;
        var fld = s.FieldingSide;
        var pitcher = s.Fielding.Pitcher;
        var catcher = s.Catcher(fld);
        var count = s.Count;
        PitchCountState newCount = count;
        BatterResult? ending = null;
        switch (spec.Outcome)
        {
            case PitchOutcome.Ball:
                if (count.Balls + 1 >= rules.BallsForWalk) ending = BatterResult.Walk; else newCount = count with { Balls = count.Balls + 1 };
                break;
            case PitchOutcome.CalledStrike or PitchOutcome.SwingingStrike:
                if (count.Strikes + 1 >= rules.StrikesForStrikeout) ending = BatterResult.Strikeout; else newCount = count with { Strikes = count.Strikes + 1 };
                break;
            case PitchOutcome.Foul:
                if (count.Strikes < rules.StrikesForStrikeout - 1) newCount = count with { Strikes = count.Strikes + 1 };
                break;
            case PitchOutcome.FoulBunt:
                if (count.Strikes + 1 >= rules.StrikesForStrikeout) ending = BatterResult.Strikeout; else newCount = count with { Strikes = count.Strikes + 1 };
                break;
            case PitchOutcome.HitByPitch:
                ending = BatterResult.HitByPitch;
                break;
        }

        // a hit by pitch makes the ball dead: no wild pitch / passed ball / dropped third strike on the same pitch and no runner move except
        // the plain forced advance the reducer fills in itself
        var dropped = spec.DroppedThirdStrike;
        if (spec.Outcome == PitchOutcome.HitByPitch)
        {
            if (spec.Mishap != PitchMishap.None || dropped != PitchMishap.None)
                return Fail<GameStep>("Game.hbp_with_mishap", "몸에 맞는 공은 볼 데드이므로 같은 투구에 폭투·포일·낫아웃을 붙일 수 없습니다.");
            if (spec.MoveList.Count > 0)
                return Fail<GameStep>("Game.hbp_extra_advance_invalid", "몸에 맞는 공에서는 강제 진루만 인정되며 다른 주자 이동을 지정할 수 없습니다.");
        }

        // mishap rules: WP and PB are separate, and every extra move must carry that cause
        if (spec.Mishap != PitchMishap.None && dropped != PitchMishap.None)
            return Fail<GameStep>("Game.mishap_conflict", "폭투/포일과 낫아웃을 한 투구에 함께 지정할 수 없습니다.");
        if (spec.Mishap != PitchMishap.None && spec.Outcome is PitchOutcome.Foul or PitchOutcome.FoulBunt)
            return Fail<GameStep>("Game.mishap_on_foul", "파울에는 폭투/포일을 붙일 수 없습니다.");
        if (dropped != PitchMishap.None)
        {
            if (ending != BatterResult.Strikeout)
                return Fail<GameStep>("Game.dropped_third_invalid", "낫아웃은 삼진 투구에서만 가능합니다.");
            if (s.Bases.First is not null && s.Outs < rules.OutsPerHalfInning - 1)
                return Fail<GameStep>("Game.dropped_third_not_allowed", "낫아웃은 1루가 비어 있거나 2아웃일 때만 타자가 뛸 수 있습니다.");
            if (spec.StrikeoutCredits is not null)
                return Fail<GameStep>("Game.dropped_third_credits", "낫아웃으로 타자가 출루하면 삼진 아웃 기록을 붙일 수 없습니다.");
        }

        if (spec.StrikeoutCredits is not null && (ending != BatterResult.Strikeout || dropped != PitchMishap.None))
            return Fail<GameStep>("Game.strikeout_credits_invalid", "삼진 아웃 기록은 타자가 삼진 아웃되는 투구에만 붙일 수 있습니다.");

        var cause = spec.Mishap != PitchMishap.None ? spec.Mishap : dropped;
        var causeMove = cause == PitchMishap.WildPitch ? MoveCause.WildPitch : MoveCause.PassedBall;
        if (spec.MoveList.Any(m => cause == PitchMishap.None || m.Cause != causeMove))
            return Fail<GameStep>("Game.moves_without_mishap", "투구 중 추가 진루는 폭투 또는 포일로만 지정할 수 있고 원인이 일치해야 합니다.");
        if (spec.Mishap != PitchMishap.None && spec.MoveList.Count == 0)
            return Fail<GameStep>("Game.mishap_without_move", "진루가 없는 폭투/포일은 기록하지 않습니다.");

        var (counted, pre) = CountPitch(s, id);
        var s1 = counted with
        {
            Count = ending is null ? newCount : count,
            Stage = ending is null ? (isTake ? PaStage.Decision : PaStage.InSequence) : s.Stage,
        };

        if (ending is null && spec.Mishap == PitchMishap.None)
            return Result<GameStep>.Success(new GameStep(s1 with { Stats = s1.Stats.AddRange(pre) }, new GameStatDelta(id, pre.ToImmutableList()), ImmutableList<ScoredPlay>.Empty, NoFacts, false));

        var bases = s.Bases;
        if (ending is null)
        {
            var plansOnly = CompletePlans(bases, spec.MoveList, batterReaches: false, requireAll: false);
            if (!plansOnly.IsSuccess) return Result<GameStep>.Failure(plansOnly.Error!.Value);
            return FinishPlay(s1, new PlayInput
            {
                EventId = id, Kind = spec.Mishap == PitchMishap.WildPitch ? PlayKind.WildPitch : PlayKind.PassedBall, IsPa = false,
                Plans = plansOnly.Value, FinalCount = newCount, WildPitch = spec.Mishap == PitchMishap.WildPitch, PassedBall = spec.Mishap == PitchMishap.PassedBall, Pre = pre,
            });
        }

        var batterSlot = s.Batting.CurrentBatterSlot;
        var charged = ending == BatterResult.Walk ? s.WalkChargeOverride ?? pitcher : pitcher;
        var batterRunner = new BaseRunner(batterSlot.Player, charged, s.NextRunnerKey, batterSlot.Order);
        var reaches = ending is BatterResult.Walk or BatterResult.HitByPitch || dropped != PitchMishap.None;
        var plans = CompletePlans(bases, spec.MoveList, reaches, requireAll: false);
        if (!plans.IsSuccess) return Result<GameStep>.Failure(plans.Error!.Value);
        var all = plans.Value;
        BatterResult result = ending.Value;
        if (reaches)
            all.Add(new Plan(batterRunner, 0, 1, dropped != PitchMishap.None ? causeMove : MoveCause.Forced, null, ImmutableList<PositionCredit>.Empty));
        else
        {
            var credits = spec.StrikeoutCredits ?? ImmutableList.Create(new PositionCredit(FieldPosition.Catcher, CreditKind.Putout));
            all.Add(new Plan(batterRunner, 0, 0, MoveCause.Play, OutKind.Other, credits));
        }

        if (dropped != PitchMishap.None)
            result = BatterResult.StrikeoutReachedBase;
        return FinishPlay(s1, new PlayInput
        {
            EventId = id, Kind = PlayKind.PlateAppearance, IsPa = true, Result = result, Batted = BattedBallType.None, Plans = all,
            FinalCount = ending == BatterResult.Walk ? count with { Balls = rules.BallsForWalk } : ending == BatterResult.Strikeout ? count with { Strikes = rules.StrikesForStrikeout } : count,
            BatterReaches = reaches, BatterOutFirst = !reaches, WildPitch = cause == PitchMishap.WildPitch, PassedBall = cause == PitchMishap.PassedBall,
            Batter = batterSlot.Player, ChargedPitcher = charged, Pre = pre,
        });

    }

    // ------------------------------------------------------------------ batted balls

    private static Result<GameStep> ApplyInPlay(GameState s, InPlayEvent e)
    {
        if (s.Stage != PaStage.InSequence)
            return Fail<GameStep>("Game.in_play_not_legal", "접근 방식을 정한 타석에서만 타구 결과를 적용할 수 있습니다.");
        if (!Enum.IsDefined(e.Result) || !Enum.IsDefined(e.Batted) || e.Batted == BattedBallType.None)
            return Fail<GameStep>("Game.in_play_invalid", "타구 종류 또는 결과가 올바르지 않습니다.");
        if (e.Result != InPlayResult.Out && e.BatterCreditList.Count > 0)
            return Fail<GameStep>("Game.batter_credits_invalid", "타자가 아웃이 아니면 타자 수비 기록을 붙일 수 없습니다.");
        if (e.Result != InPlayResult.Out && e.Sacrifice)
            return Fail<GameStep>("Game.sacrifice_invalid", "희생 표시는 아웃이 되는 타구에서만 쓸 수 있습니다.");

        var fld = s.FieldingSide;
        var pitcher = s.Fielding.Pitcher;
        var slot = s.Batting.CurrentBatterSlot;
        var batterRunner = new BaseRunner(slot.Player, pitcher, s.NextRunnerKey, slot.Order);
        var moves = e.MoveList.ToList();
        var batterMove = moves.FirstOrDefault(m => m.From == 0);
        var hitBase = e.Result switch { InPlayResult.Single => 1, InPlayResult.Double => 2, InPlayResult.Triple => 3, _ => 0 };
        var batterOutAfterReaching = batterMove is not null && hitBase > 0;
        if (batterMove is not null && e.Result != InPlayResult.ReachedOnError && !batterOutAfterReaching)
            return Fail<GameStep>("Game.batter_move_invalid", "타자의 도착 베이스는 결과로 정해집니다. 실책 출루(또는 안타 뒤 주루사)에서만 타자 이동을 지정할 수 있습니다.");
        if (batterOutAfterReaching)
        {
            // the batter hit safely and was then put out trying for more: he is put out AT a base beyond the one the hit gave him, which is the
            // only evidence that he reached it (no boolean claim); the out is a tag out, never a force out
            if (batterMove!.To != 0 || batterMove.Cause != MoveCause.Play)
                return Fail<GameStep>("Game.batter_move_invalid", "안타 뒤 타자의 이동은 추가 진루 중 아웃(To=0)만 지정할 수 있습니다.");
            if (batterMove.OutAt is not { } batterOutAt || batterOutAt <= hitBase || batterOutAt > 4)
                return Fail<GameStep>("Game.batter_reach_not_established", $"안타로 {hitBase}루에 도달한 타자가 아웃되려면 그보다 앞 베이스({hitBase + 1}~4)에서 아웃된 사건이어야 합니다. 도달 사실은 아웃 베이스로만 증명됩니다.");
            if (batterMove.OutKind is null)
                return Fail<GameStep>("Game.out_kind_missing", "주자 아웃에는 아웃 종류(포스/태그)를 반드시 지정해야 합니다.");
            if (batterMove.OutKind != OutKind.Tag)
                return Fail<GameStep>("Game.out_kind_mismatch", "1루에 도달한 뒤 추가 진루 중 아웃된 타자주자는 포스아웃이 아니라 태그 아웃입니다.");
            if (e.BatterSequence is not null && batterMove.Sequence is not null && e.BatterSequence != batterMove.Sequence)
                return Fail<GameStep>("Game.play_order_invalid", "타자의 순서는 BatterSequence와 이동의 Sequence 중 하나로만 지정하거나 같은 값이어야 합니다.");
        }
        moves.RemoveAll(m => m.From == 0);

        var homeRun = e.Result == InPlayResult.HomeRun;
        var batterReaches = e.Result != InPlayResult.Out;
        if (e.Result == InPlayResult.FieldersChoice && s.Bases.Count == 0)
            return Fail<GameStep>("Game.fielders_choice_invalid", "주자가 없는 상황에서는 야수 선택이 될 수 없습니다.");
        if (e.Result == InPlayResult.ReachedOnError && e.ErrorList.Count == 0)
            return Fail<GameStep>("Game.error_missing", "실책 출루에는 실책 기록이 하나 이상 필요합니다.");
        if ((moves.Any(m => IsErrorCause(m.Cause)) || (!batterOutAfterReaching && batterMove is { Cause: not MoveCause.ErrorExtraBase and not MoveCause.ErrorSavedOut })) && e.ErrorList.Count == 0)
            return Fail<GameStep>("Game.error_cause_without_error", "에러로 인한 진루에는 실책 기록이 필요합니다.");
        if (e.ErrorList.Any(p => !IsFielding(p) || !Enum.IsDefined(p)))
            return Fail<GameStep>("Game.error_position_invalid", "실책 위치가 올바르지 않습니다.");

        Result<List<Plan>> plans;
        if (homeRun)
        {
            if (moves.Any(m => m.To != 4))
                return Fail<GameStep>("Game.home_run_moves_invalid", "홈런에서는 모든 주자가 득점해야 합니다.");
            plans = CompletePlans(s.Bases, moves, true, true, defaultTo: 4);
        }
        else
            plans = CompletePlans(s.Bases, moves, batterReaches, requireAll: true);
        if (!plans.IsSuccess) return Result<GameStep>.Failure(plans.Error!.Value);

        var all = plans.Value;
        switch (e.Result)
        {
            case InPlayResult.Single or InPlayResult.Double or InPlayResult.Triple when batterOutAfterReaching:
                all.Add(new Plan(batterRunner, 0, 0, MoveCause.Play, OutKind.Tag, batterMove!.CreditList, batterMove.Sequence ?? e.BatterSequence, batterMove.OutAt, batterMove.Method, PostReach: true));
                break;
            case InPlayResult.Single: all.Add(new Plan(batterRunner, 0, 1, MoveCause.Play, null, ImmutableList<PositionCredit>.Empty)); break;
            case InPlayResult.Double: all.Add(new Plan(batterRunner, 0, 2, MoveCause.Play, null, ImmutableList<PositionCredit>.Empty)); break;
            case InPlayResult.Triple: all.Add(new Plan(batterRunner, 0, 3, MoveCause.Play, null, ImmutableList<PositionCredit>.Empty)); break;
            case InPlayResult.HomeRun: all.Add(new Plan(batterRunner, 0, 4, MoveCause.Play, null, ImmutableList<PositionCredit>.Empty, e.BatterSequence)); break;
            case InPlayResult.FieldersChoice: all.Add(new Plan(batterRunner, 0, 1, MoveCause.Play, null, ImmutableList<PositionCredit>.Empty)); break;
            case InPlayResult.ReachedOnError:
                all.Add(new Plan(batterRunner, 0, batterMove?.To ?? 1, MoveCause.ErrorSavedOut, null, ImmutableList<PositionCredit>.Empty));
                break;
            default: all.Add(new Plan(batterRunner, 0, 0, MoveCause.Play, OutKind.Other, e.BatterCreditList, e.BatterSequence)); break;
        }

        // classification of outs: sacrifice bunt (scorer's judgement flag) and sacrifice fly (rule-determined)
        BatterResult result = e.Result switch
        {
            InPlayResult.Single => BatterResult.Single,
            InPlayResult.Double => BatterResult.Double,
            InPlayResult.Triple => BatterResult.Triple,
            InPlayResult.HomeRun => BatterResult.HomeRun,
            InPlayResult.FieldersChoice => BatterResult.FieldersChoice,
            InPlayResult.ReachedOnError => BatterResult.ReachedOnError,
            _ => BatterResult.Out,
        };
        if (e.Result == InPlayResult.Out)
        {
            var outsOnPlay = all.Count(p => p.To == 0);
            var cleanRuns = all.Count(p => p.To == 4 && !IsErrorCause(p.Cause));
            if (e.Sacrifice)
            {
                var advanced = all.Any(p => p.From >= 1 && p.To > p.From && p.To != 0 && !IsErrorCause(p.Cause));
                if (e.Batted != BattedBallType.Bunt || outsOnPlay != 1 || !advanced)
                    return Fail<GameStep>("Game.sacrifice_bunt_invalid", "희생번트는 번트로 타자만 아웃되고 주자가 진루했을 때만 인정됩니다.");
                result = BatterResult.SacrificeBunt;
            }
            else if (e.Batted == BattedBallType.Fly && outsOnPlay == 1 && s.Outs < s.Rules.OutsPerHalfInning - 1 && cleanRuns > 0)
                result = BatterResult.SacrificeFly;
        }

        var (withPitch, pre) = CountPitch(s, e.Id); // the pitch that was put in play is a real pitch too
        return FinishPlay(withPitch, new PlayInput
        {
            Pre = pre,
            EventId = e.Id, Kind = PlayKind.PlateAppearance, IsPa = true, Result = result, Batted = e.Batted, Plans = all, Errors = e.ErrorList,
            FinalCount = s.Count, IsHomeRun = homeRun, BatterReaches = batterReaches || e.Batted is BattedBallType.Ground or BattedBallType.Bunt, Batter = slot.Player, ChargedPitcher = pitcher,
            BatterOutFirst = e.Result == InPlayResult.Out && e.Batted is BattedBallType.Fly or BattedBallType.Line or BattedBallType.Pop,
            CaughtBall = e.Result == InPlayResult.Out && e.Batted is BattedBallType.Fly or BattedBallType.Line or BattedBallType.Pop,
        });
    }

    // ------------------------------------------------------------------ runner plays

    private static Result<GameStep> ApplyRunnerPlay(GameState s, RunnerPlayEvent e)
    {
        if (e.Kind == RunnerPlayKind.Appeal)
            return Fail<GameStep>("Game.appeal_not_supported", "어필 플레이(누의 공과·리터치 실패 등)는 Star Player에서 지원하지 않습니다. 주자는 베이스를 적법하게 밟은 것으로 전제합니다.");
        if (!Enum.IsDefined(e.Kind))
            return Fail<GameStep>("Game.runner_play_invalid", "주자 플레이 종류가 올바르지 않습니다.");
        var moves = e.MoveList;
        if (moves.Any(m => m.From == 0))
            return Fail<GameStep>("Game.runner_play_batter", "타자는 주자 플레이로 이동시킬 수 없습니다.");
        if (e.ErrorList.Any(p => !IsFielding(p) || !Enum.IsDefined(p)))
            return Fail<GameStep>("Game.error_position_invalid", "실책 위치가 올바르지 않습니다.");
        if (moves.Any(m => IsErrorCause(m.Cause)) && e.ErrorList.Count == 0)
            return Fail<GameStep>("Game.error_cause_without_error", "에러로 인한 진루에는 실책 기록이 필요합니다.");
        if (moves.Count == 0)
            return Fail<GameStep>("Game.empty_play", "이동한 주자가 없는 주자 플레이는 기록하지 않습니다.");

        var plans = CompletePlans(s.Bases, moves, batterReaches: false, requireAll: false);
        if (!plans.IsSuccess) return Result<GameStep>.Failure(plans.Error!.Value);
        var all = plans.Value;
        PlayKind kind;
        switch (e.Kind)
        {
            case RunnerPlayKind.Steal:
                var steals = all.Where(p => p.Cause == MoveCause.Steal).ToList();
                if (steals.Count == 0 || steals.Any(p => p.To != 0 && p.To != p.From + 1))
                    return Fail<GameStep>("Game.steal_invalid", "도루는 한 베이스 진루(또는 도루 실패 아웃)로만 기록할 수 있습니다.");
                kind = steals.Any(p => p.To == 0) ? PlayKind.CaughtStealing : PlayKind.Steal;
                break;
            case RunnerPlayKind.Pickoff:
                if (!all.Any(p => p.To == 0))
                    return Fail<GameStep>("Game.pickoff_invalid", "견제사에는 아웃된 주자가 있어야 합니다.");
                kind = PlayKind.Pickoff;
                break;
            case RunnerPlayKind.Balk:
                if (all.Count(p => p.From >= 1) != s.Bases.Count || s.Bases.Count == 0 || all.Any(p => p.Cause != MoveCause.Balk || p.To != p.From + 1))
                    return Fail<GameStep>("Game.balk_invalid", "보크는 모든 주자가 정확히 한 베이스씩 진루해야 합니다.");
                kind = PlayKind.Balk;
                break;
            default:
                kind = PlayKind.OtherRunnerPlay;
                break;
        }

        return FinishPlay(s, new PlayInput
        {
            EventId = e.Id, Kind = kind, IsPa = false, Plans = all, Errors = e.ErrorList, FinalCount = s.Count,
            Balk = e.Kind == RunnerPlayKind.Balk, Pickoff = e.Kind == RunnerPlayKind.Pickoff,
        });
    }
}
