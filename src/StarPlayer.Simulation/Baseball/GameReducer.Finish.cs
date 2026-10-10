using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;

namespace StarPlayer.Simulation.Baseball;

public static partial class GameReducer
{
    /// <summary>
    /// The single place where a validated play becomes official evidence: outs, runs (with who is charged), RBI, fielder credits,
    /// stat entries, the ScoredPlay, half-inning closure (earned-run reconstruction, LOB) and game end.
    /// </summary>
    private static Result<GameStep> FinishPlay(GameState s, PlayInput inp)
    {
        var rules = s.Rules;
        var bat = s.BattingSide;
        var fld = s.FieldingSide;
        var pitcher = s.Fielding.Pitcher;
        var catcher = s.Catcher(fld);
        IReadOnlyList<Plan> plans = inp.Plans;

        if (ValidatePlans(s, plans) is { } planError)
            return Result<GameStep>.Failure(planError);
        var outsOnPlay = plans.Count(p => p.To == 0);
        var outsAfter = s.Outs + outsOnPlay;
        var limit = rules.OutsPerHalfInning;

        var resolved = ResolveOrder(s, inp, plans, outsAfter);
        if (!resolved.IsSuccess)
            return Result<GameStep>.Failure(resolved.Error!.Value);
        var order = resolved.Value;
        // OBR 9.05(b)(1): no base hit on a batted ball that forces out a preceding runner. The force status of every out is derived from the state by ResolveOrder (a force
        // removed by an earlier out is respected, the way the out was made does not matter); the batting result label is only what is checked against it.
        if (inp.Result is BatterResult.Single or BatterResult.Double or BatterResult.Triple or BatterResult.HomeRun
            && plans.Any(p => p.From >= 1 && p.To == 0 && order.Class.TryGetValue(p.Runner.RunnerKey, out var cls) && cls == OutClassification.Force))
            return Fail<GameStep>("Game.hit_with_force_out",
                "타구로 선행 주자가 포스아웃되었으므로 안타로 기록할 수 없습니다(OBR 9.05(b)(1)). 결과를 안타로 기록하려면 포스아웃이 없는 플레이여야 하며, 엔진은 결과를 야수선택이나 실책 출루로 바꾸지 않습니다. 기록원이 실제 플레이에 맞는 공식 기록 분류로 다시 입력하세요.");
        // OBR 9.05(b)(1), second clause (C43): no base hit when a runner "would have been forced out except for a fielding error". What the input proves is the scorer's explicit
        // ErrorSavedOut cause on a runner whose out would have been a force out (derived from the state, like above). The event never links an error to the move it caused, so an
        // error-caused move is NOT taken as proof that every error is explained: with a runner on first (a forced runner exists) a hit whose error no move marks, or that records
        // two or more errors (whatever the number of error-caused moves), is refused as cause-unclear (C44, a Phase 2 support limit, not a baseball rule). Exactly one error with a
        // marking move is the scorer's statement, not a verified link; with no runner on first nothing is doubted. Nothing is relabelled.
        if (inp.Result is BatterResult.Single or BatterResult.Double or BatterResult.Triple)
        {
            if (plans.Any(p => p.From >= 1 && p.To != 0 && p.Cause == MoveCause.ErrorSavedOut && order.Class.TryGetValue(p.Runner.RunnerKey, out var saved) && saved == OutClassification.Force))
                return Fail<GameStep>("Game.hit_with_force_out_saved_by_error",
                    "실책이 없었다면 포스아웃되었을 주자가 있는 타구는 안타로 기록할 수 없습니다(OBR 9.05(b)(1)). 결과를 바꾸지 않고 거부합니다. 실제 플레이에 부합하는 공식 기록 분류로만 다시 입력할 수 있습니다.");
            // C44 (approved Phase 2 support limit, not a baseball rule): the event does not link an error to the move it caused, so with a runner on first one error is "explained" only
            // when some move carries an error cause, and two or more errors can never be shown explained by counting moves. Check order/error codes: hit_with_force_out, then
            // hit_with_force_out_saved_by_error (confirmed C43), then this one (unsupported).
            if (s.Bases.First is not null && inp.Errors.Count > 0 && (inp.Errors.Count >= 2 || !plans.Any(p => IsErrorCause(p.Cause))))
                return Fail<GameStep>("Game.hit_error_cause_unclear",
                    "1루 주자가 있는 안타 플레이에서 실책이 한 개도 입력으로 설명되지 않았거나(실책 진루·실책으로 아웃 면함 이동 없음) 실책이 2개 이상입니다. 현재 사건 모델에는 실책과 그 원인 이동을 1:1로 연결하는 정보가 없어 어떤 실책이 포스아웃을 막았는지 알 수 없습니다. 올바른 안타 판정을 확정할 수 없으므로 지원하지 않습니다(Phase 2 지원 범위 제한).");
        }
        var handed = HandOverFieldersChoiceSlot(s, inp, plans);
        if (!handed.IsSuccess)
            return Result<GameStep>.Failure(handed.Error!.Value);
        plans = handed.Value;
        var crossed = plans.Where(p => p.To == 4).OrderByDescending(p => p.From).ToList();
        var scoring = crossed.Where(p => order.CountedRuns.Contains(p.Runner.RunnerKey)).ToList();
        var voided = crossed.Where(p => !order.CountedRuns.Contains(p.Runner.RunnerKey)).Select(p => p.Runner.RunnerKey).ToHashSet();

        var walkOff = false;
        if (bat == TeamSide.Home && s.Inning >= rules.RegulationInnings && scoring.Count > 0 && s.HomeRuns + scoring.Count > s.AwayRuns)
        {
            walkOff = true;
            var need = s.AwayRuns - s.HomeRuns + 1;
            if (scoring.Count > need && !inp.IsHomeRun)
                return Fail<GameStep>("Game.run_after_winning_run", "끝내기 득점 이후의 주자 득점은 인정되지 않습니다.");
        }

        var creditsResult = ResolveCredits(s, plans);
        if (!creditsResult.IsSuccess) return Result<GameStep>.Failure(creditsResult.Error!.Value);
        var credits = creditsResult.Value;
        var errorCredits = new List<PlayerId>();
        foreach (var position in inp.Errors)
        {
            if (s.DefenderAt(fld, position) is not { } fielder)
                return Fail<GameStep>("Game.fielder_missing", $"{position} 자리에 선수가 없습니다.");
            errorCredits.Add(fielder);
        }

        // ---- runs and RBI
        var result = inp.Result;
        // Two different facts (docs/game-engine.md section 4): a DEFENSIVE double play (OBR 9.11, the fielders' DP credit) is two or more outs on one play that is not
        // a hit - a hit with two outs (a runner and the batter caught stretching it) is no double play at all; the batter GROUNDS INTO a double play (9.02(a)(17), the
        // GIDP statistic) only on a ground ball that is a force or a reverse-force double play, and only then is the RBI withheld (9.04(b)(1)).
        var hitResult = result is BatterResult.Single or BatterResult.Double or BatterResult.Triple or BatterResult.HomeRun;
        var dpPlay = outsOnPlay >= 2 && !hitResult;
        var groundedIntoDp = dpPlay && GroundedIntoForceDoublePlay(s, inp, plans, order);
        var rbiPlay = inp.IsPa && result is not (BatterResult.ReachedOnError or BatterResult.Strikeout or BatterResult.StrikeoutReachedBase) && !groundedIntoDp;
        // Run responsibility (OBR 9.16(g), RunResponsibility): each run is charged once, to a slot of a pitcher, not to the pitcher of the runner who scored.
        var (chargedTo, slotsAfter) = RunResponsibility.Settle(scoring.Select(p => p.Runner).ToList(),
            plans.Where(p => p.To is >= 1 and <= 3).OrderByDescending(p => p.To).Select(p => p.Runner).ToList(), Seniority(s));
        var runs = ImmutableList.CreateBuilder<ScoredRun>();
        for (var i = 0; i < scoring.Count; i++)
        {
            var plan = scoring[i];
            var rbiCause = plan.Cause is not (MoveCause.WildPitch or MoveCause.PassedBall or MoveCause.Steal or MoveCause.Balk or MoveCause.ErrorExtraBase or MoveCause.ErrorSavedOut);
            runs.Add(new ScoredRun(plan.Runner.RunnerKey, plan.Runner.Player, chargedTo[i], plan.Cause, rbiPlay && rbiCause && inp.Batter is { } b ? b : null));
        }

        var runList = runs.ToImmutable();
        var stats = new List<StatEntry>(inp.Pre);
        void Add(TeamSide team, PlayerId player, StatKey key, int amount = 1, PlayerId? partner = null) => stats.Add(new StatEntry(team, player, key, amount, inp.EventId, partner));

        if (inp.IsPa && inp.Batter is { } batter && result is { } r)
        {
            var charged = inp.ChargedPitcher ?? pitcher;
            Add(bat, batter, StatKey.BatPA);
            Add(fld, pitcher, StatKey.PitchBF);
            Add(fld, catcher, StatKey.BatteryBF, 1, pitcher);
            if (r is not (BatterResult.Walk or BatterResult.HitByPitch or BatterResult.SacrificeFly or BatterResult.SacrificeBunt))
                Add(bat, batter, StatKey.BatAB);
            switch (r)
            {
                case BatterResult.Single: Hit(StatKey.Bat1B); break;
                case BatterResult.Double: Hit(StatKey.Bat2B); break;
                case BatterResult.Triple: Hit(StatKey.Bat3B); break;
                case BatterResult.HomeRun: Hit(StatKey.BatHR); Add(fld, pitcher, StatKey.PitchHR); break;
                case BatterResult.Walk: Add(bat, batter, StatKey.BatBB); Add(fld, charged, StatKey.PitchBB); break;
                case BatterResult.HitByPitch: Add(bat, batter, StatKey.BatHBP); Add(fld, pitcher, StatKey.PitchHBP); break;
                case BatterResult.Strikeout or BatterResult.StrikeoutReachedBase: Add(bat, batter, StatKey.BatSO); Add(fld, pitcher, StatKey.PitchK); break;
                case BatterResult.SacrificeFly: Add(bat, batter, StatKey.BatSF); break;
                case BatterResult.SacrificeBunt: Add(bat, batter, StatKey.BatSH); break;
                case BatterResult.ReachedOnError: Add(bat, batter, StatKey.BatROE); break;
                case BatterResult.FieldersChoice: Add(bat, batter, StatKey.BatFC); break;
            }

            if (groundedIntoDp)
                Add(bat, batter, StatKey.BatGIDP);
            var rbi = runList.Count(x => x.RbiBatter is not null);
            if (rbi > 0)
                Add(bat, batter, StatKey.BatRBI, rbi);

            void Hit(StatKey kind)
            {
                Add(bat, batter, StatKey.BatH);
                Add(bat, batter, kind);
                Add(fld, pitcher, StatKey.PitchH);
            }
        }

        for (var i = 0; i < outsOnPlay; i++)
        {
            Add(fld, pitcher, StatKey.PitchOuts);
            Add(fld, catcher, StatKey.CatOuts);
            Add(fld, catcher, StatKey.BatteryOuts, 1, pitcher);
        }

        foreach (var credit in credits)
            Add(fld, credit.Player, credit.Kind == CreditKind.Putout ? StatKey.FldPO : StatKey.FldA);
        foreach (var fielder in errorCredits)
            Add(fld, fielder, StatKey.FldE);
        if (dpPlay && inp.IsPa)
            foreach (var fielder in credits.Select(c => c.Player).Distinct())
                Add(fld, fielder, StatKey.FldDP);

        foreach (var run in runList)
        {
            Add(bat, run.Runner, StatKey.RunR);
            Add(fld, run.ResponsiblePitcher, StatKey.PitchR);
        }

        foreach (var plan in plans.Where(p => p.Cause == MoveCause.Steal))
        {
            if (plan.To == 0)
            {
                Add(bat, plan.Runner.Player, StatKey.RunCS);
                if (plan.Credits.Any(c => c.Position == FieldPosition.Catcher))
                    Add(fld, catcher, StatKey.CatCS);
            }
            else
            {
                Add(bat, plan.Runner.Player, StatKey.RunSB);
                Add(fld, catcher, StatKey.CatSBA);
            }
        }

        if (inp.Pickoff)
            foreach (var plan in plans.Where(p => p.To == 0))
                Add(bat, plan.Runner.Player, StatKey.RunPicked);
        if (inp.WildPitch) Add(fld, pitcher, StatKey.PitchWP);
        if (inp.PassedBall) Add(fld, catcher, StatKey.FldPB);
        if (inp.Balk) Add(fld, pitcher, StatKey.PitchBK);

        // ---- the scored play
        var sequence = s.Plays.Count + 1;
        var playId = $"{s.GameId}:play-{sequence:0000}";
        var moveRecords = plans.OrderByDescending(p => p.From)
            .Select(p => new RunnerMoveRecord(p.Runner.RunnerKey, p.Runner.Player, p.From, p.To, p.Cause, p.OutKind, p.Runner.ResponsiblePitcher,
                order.Sequence.GetValueOrDefault(p.Runner.RunnerKey), order.Class.TryGetValue(p.Runner.RunnerKey, out var cls) ? cls : null,
                order.OutAt.TryGetValue(p.Runner.RunnerKey, out var outAt) ? outAt : null, p.Method, voided.Contains(p.Runner.RunnerKey))).ToImmutableList();
        var awayAfter = s.AwayRuns + (bat == TeamSide.Away ? runList.Count : 0);
        var homeAfter = s.HomeRuns + (bat == TeamSide.Home ? runList.Count : 0);
        var play = new ScoredPlay(playId, inp.EventId, sequence, s.Inning, s.Half, s.Outs, outsAfter, bat, inp.Kind, inp.Batter, pitcher, s.Batting.Pitcher, catcher,
            result, inp.Batted, inp.FinalCount, moveRecords, credits, inp.Errors, runList, awayAfter, homeAfter, dpPlay);

        // ---- new state
        var newBases = RunnerState.Empty;
        foreach (var plan in plans.Where(p => p.To is >= 1 and <= 3))
            newBases = newBases.With(plan.To, plan.Runner with { ResponsiblePitcher = slotsAfter[plan.Runner.RunnerKey] });
        var awayLine = s.AwayLine;
        var homeLine = s.HomeLine;
        if (runList.Count > 0)
        {
            if (bat == TeamSide.Away) awayLine = awayLine.SetItem(awayLine.Count - 1, awayLine[^1] + runList.Count);
            else homeLine = homeLine.SetItem(homeLine.Count - 1, homeLine[^1] + runList.Count);
        }

        var fieldingTeam = s.Fielding with { PitcherUsages = UpdateUsage(s.Fielding, outsOnPlay, inp.IsPa ? 1 : 0) };
        var battingTeam = inp.IsPa ? s.Batting with { NextBatterIndex = s.Batting.NextBatterIndex + 1 } : s.Batting;
        var next = s with
        {
            Outs = outsAfter,
            Bases = newBases,
            AwayRuns = awayAfter,
            HomeRuns = homeAfter,
            AwayLine = awayLine,
            HomeLine = homeLine,
            Plays = s.Plays.Add(play),
            Count = inp.IsPa ? PitchCountState.Zero : s.Count,
            Stage = inp.IsPa ? PaStage.Start : s.Stage,
            Approach = inp.IsPa ? null : s.Approach,
            WalkChargeOverride = inp.IsPa ? null : s.WalkChargeOverride,
            NextRunnerKey = inp.IsPa ? s.NextRunnerKey + 1 : s.NextRunnerKey,
            Away = bat == TeamSide.Away ? battingTeam : fieldingTeam,
            Home = bat == TeamSide.Home ? battingTeam : fieldingTeam,
        };

        next = Conclude(next, stats, inp.EventId, outsAfter == limit, walkOff);
        next = next with { Stats = s.Stats.AddRange(stats) };

        ImmutableList<GameFact> facts = inp.IsPa && inp.Batter is { } fb && result is { } fr
            ? ImmutableList.Create<GameFact>(new PlateAppearanceCompleted(s.GameId, playId, fb, pitcher, fr, runList.Count))
            : ImmutableList.Create<GameFact>(new RunnerPlayCompleted(s.GameId, playId, inp.Kind, runList.Count, outsOnPlay));
        return Result<GameStep>.Success(new GameStep(next, new GameStatDelta(inp.EventId, stats.ToImmutableList()), ImmutableList.Create(play), facts, false));
    }

    /// <summary>
    /// OBR definitions of terms (DOUBLE PLAY) with 9.02(a)(17) / 9.04(b)(1): the batter grounds into a double play when two outs on a ground ball are a force double
    /// play (both putouts force plays) or a reverse-force double play (the first out is a force play and the second is made on a runner for whom the first out removed
    /// the force). The batter-runner put out on a ground ball is himself a force play (the definition's own examples). The classification of each out comes from the
    /// state (<see cref="ResolveOrder"/>), never from the input, and the order of the outs is the verified sequence.
    /// </summary>
    private static bool GroundedIntoForceDoublePlay(GameState s, PlayInput inp, IReadOnlyList<Plan> plans, Resolution order)
    {
        if (!inp.IsPa || inp.Batted != BattedBallType.Ground)
            return false;
        var outs = order.Terminals.Where(t => t.IsOut && !t.ReconOnly).OrderBy(t => t.Sequence).ToList();
        if (outs.Count < 2)
            return false;
        var plan = plans.ToDictionary(p => p.Runner.RunnerKey);
        bool IsForce(Terminal t) => t.Class == OutClassification.Force || (plan[t.Key].From == 0 && t.Class == OutClassification.BatterRunner);
        bool IsReverseForce(Terminal t, Terminal first)
        {
            var from = plan[t.Key].From;
            if (from < 1 || t.Class != OutClassification.NonForce)
                return false;
            var wasForced = Enumerable.Range(1, from - 1).All(b => s.Bases.At(b) is not null);   // every base behind him was occupied when the batter became a runner
            return wasForced && plan[first.Key].From < from;                                     // and the first out removed the force: the batter-runner or a runner behind him
        }

        return IsForce(outs[0]) && (IsForce(outs[1]) || IsReverseForce(outs[1], outs[0]));
    }

    private static ImmutableList<PitcherUsage> UpdateUsage(TeamGameState team, int outs, int battersFaced)
    {
        var index = team.PitcherUsages.FindLastIndex(u => u.Pitcher == team.Pitcher);
        var usage = team.PitcherUsages[index];
        return team.PitcherUsages.SetItem(index, usage with { OutsRecorded = usage.OutsRecorded + outs, BattersFaced = usage.BattersFaced + battersFaced });
    }

    /// <summary>
    /// Closes a half-inning when it has three outs or the home team walks off: reconstructs earned runs, counts runners left on base,
    /// then moves to the next half, ends the game by score/limit, or ends it on the spot for a walk-off.
    /// </summary>
    private static GameState Conclude(GameState ns, List<StatEntry> entries, string eventId, bool halfOver, bool walkOff)
    {
        if (!halfOver && !walkOff)
            return ns;

        var rules = ns.Rules;
        var fld = ns.FieldingSide;
        var half = ns.Plays.Where(p => p.Inning == ns.Inning && p.Half == ns.Half).ToList();

        // Earned runs (OBR 9.16). The TEAM figure reconstructs the whole half-inning without the errors and passed balls. A PITCHER's run is judged in the inning as
        // reconstructed from the moment HE entered (9.16(i): a relief pitcher gets no benefit of the previous pitchers' chances for outs that were not accepted), so a
        // run charged to him can be earned although the team's figure says unearned, and the reverse never happens by his own pitcher's error. The first pitcher of the
        // half is judged from its start, which is the team reconstruction.
        var usages = ns.Team(fld).PitcherUsages;
        var views = new Dictionary<int, HashSet<int>>();
        HashSet<int> View(PlayerId pitcher)
        {
            var entrySequence = usages.LastOrDefault(u => u.Pitcher == pitcher)?.EntrySequence ?? 0;
            var start = Math.Max(0, half.FindIndex(p => p.Sequence > entrySequence));
            if (!views.TryGetValue(start, out var set))
                views[start] = set = EarnedRuns.Reconstruct(half, rules.OutsPerHalfInning, start);
            return set;
        }

        var runs = half.SelectMany(p => p.Runs).ToList();
        foreach (var run in runs.Where(run => View(run.ResponsiblePitcher).Contains(run.RunnerKey)))
            entries.Add(new StatEntry(fld, run.ResponsiblePitcher, StatKey.PitchER, 1, eventId));
        var teamEarned = EarnedRuns.Reconstruct(half, rules.OutsPerHalfInning, 0);
        var teamEarnedRuns = runs.Count(run => teamEarned.Contains(run.RunnerKey));
        if (teamEarnedRuns > 0)
            entries.Add(new StatEntry(fld, default, StatKey.TeamER, teamEarnedRuns, eventId));

        // Left on base (OBR 9.03(c) box proof: at-bats + walks + hit batters + sacrifices = runs + left on base + the opponent's putouts): every batter ends as a run,
        // a putout or left on base. The runners standing on base when the half ends are left (or, at a walk-off, when the game ends, by profile rule); a runner who
        // crossed the plate on the play that made the third out but whose run does not count (RunVoided) was neither put out nor scored, so he is left too. Each
        // runner key is counted once.
        var stranded = (halfOver || (walkOff && rules.CountWalkOffRunnersAsLeftOnBase) ? ns.Bases.Count : 0)
            + half.SelectMany(p => p.Moves).Where(m => m.RunVoided).Select(m => m.RunnerKey).Distinct().Count();
        if (stranded > 0)
            entries.Add(new StatEntry(ns.BattingSide, default, StatKey.TeamLOB, stranded, eventId));

        if (walkOff)
            return ns with { Status = GameStatus.Final, Outcome = GameOutcome.HomeWin };

        var regulation = rules.RegulationInnings;
        if (ns.Half == InningHalf.Top)
        {
            if (ns.Inning >= regulation && ns.HomeRuns > ns.AwayRuns)
                return ns with { Status = GameStatus.Final, Outcome = GameOutcome.HomeWin };
            return NextHalf(ns, ns.Inning, InningHalf.Bottom);
        }

        if (ns.Inning >= regulation)
        {
            if (ns.AwayRuns != ns.HomeRuns)
                return ns with { Status = GameStatus.Final, Outcome = ns.AwayRuns > ns.HomeRuns ? GameOutcome.AwayWin : GameOutcome.HomeWin };
            if (rules.ExtraInningsLimit is { } limit && ns.Inning >= limit)
                return ns with { Status = GameStatus.Final, Outcome = GameOutcome.Tie };
        }

        return NextHalf(ns, ns.Inning + 1, InningHalf.Top);
    }

    private static GameState NextHalf(GameState ns, int inning, InningHalf half) => ns with
    {
        Inning = inning,
        Half = half,
        Outs = 0,
        Count = PitchCountState.Zero,
        Bases = RunnerState.Empty,
        Stage = PaStage.Start,
        Approach = null,
        WalkChargeOverride = null,
        AwayLine = half == InningHalf.Top ? ns.AwayLine.Add(0) : ns.AwayLine,
        HomeLine = half == InningHalf.Bottom ? ns.HomeLine.Add(0) : ns.HomeLine,
    };
}
