using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.Simulation.Baseball;

// Phase 3-C2B-3 (ADR-007 "3-C2B-3-P policy approval", docs/game-engine.md section 25): resolves the extra-base decisions of a PendingAdvancement.
// Pure part (AdvancementPlanner): the declared contexts in lead-runner-first order (P-01) and the sequential conditional composition of drawn results (P-02/P-03/P-04/P-05).
// Resolve (below): the stale-input contract (PitchPreparation.CheckPrepared, P-10), the draws on game/runner@1 from H1 (P-07), the event, the unchanged reducer and the audit (P-06/P-09).
// Nothing is adopted: state, RNG position (H2), event id and receipts stay with 3-C3.

internal static class AdvancementPlanner
{
    /// <summary>
    /// The open decisions of a pending play in PROCESSING order: the lead runner (the larger start base) first (P-01). The stored `AdvancementKeys` keep the C2B-2 order (trailing
    /// runner first); this order is separate and fixed. A decision is built from the base plan only: the runner and the base plan put him on <c>PlanTo</c>, the attempt is one base further.
    /// </summary>
    public static Result<ImmutableArray<AdvancementContext>> Contexts(InPlayPlanFacts facts)
    {
        var starts = new List<(int Start, string Key)>();
        foreach (var key in facts.AdvancementKeys)
        {
            var start = key switch
            {
                "single_runner_on_first" => 1, "single_runner_on_second" => 2, "double_runner_on_first" => 1, "fly_out_runner_on_third" => 3,
                _ => 0,
            };
            if (start == 0)
                return Fail<ImmutableArray<AdvancementContext>>("Advancement.context_unknown", $"승인되지 않은 추가 진루 맥락입니다: '{key}'");
            if (starts.Any(s => s.Key == key))
                return Fail<ImmutableArray<AdvancementContext>>("Advancement.context_duplicated", $"같은 추가 진루 맥락이 두 번 선언되었습니다: '{key}'");
            starts.Add((start, key));
        }

        var contexts = ImmutableArray.CreateBuilder<AdvancementContext>();
        foreach (var (start, key) in starts.OrderByDescending(s => s.Start))
        {
            var move = facts.Moves.FirstOrDefault(m => m.From == start);
            if (move is null)
                return Fail<ImmutableArray<AdvancementContext>>("Advancement.runner_missing", $"'{key}' 맥락의 {start}루 주자가 기본 계획에 없습니다.");
            if (move.To is < 1 or > 3)
                return Fail<ImmutableArray<AdvancementContext>>("Advancement.runner_not_on_base", $"'{key}' 맥락의 주자가 기본 계획 뒤 누상에 있지 않습니다.");
            var retriever = facts.Official.Retriever ?? facts.Official.Credits.Where(c => c.Kind == CreditKind.Putout).Select(c => (FieldPosition?)c.Position).FirstOrDefault();
            if (retriever is null)
                return Fail<ImmutableArray<AdvancementContext>>("Advancement.retriever_missing", $"'{key}' 맥락의 타구를 처리한 수비 위치를 템플릿에서 알 수 없습니다.");
            var target = move.To + 1;
            var baseFielder = target switch { 3 => FieldPosition.ThirdBase, 4 => FieldPosition.Catcher, _ => (FieldPosition?)null };
            if (baseFielder is null)
                return Fail<ImmutableArray<AdvancementContext>>("Advancement.target_invalid", $"'{key}' 맥락의 시도 베이스({target}루)에 승인된 수비 기록이 없습니다.");
            contexts.Add(new AdvancementContext(contexts.Count + 1, key, move.RunnerKey, move.Runner, start, move.To, target, retriever.Value, baseFielder.Value));
        }

        return Result<ImmutableArray<AdvancementContext>>.Success(contexts.ToImmutable());
    }

    /// <summary>
    /// P-02/P-03: applies the drawn results one by one, lead runner first, each to the play as it then stands. A result that no longer applies because the third out or the winning run
    /// has happened is <see cref="AdvancementOutcome.Void"/>; a drawn extra base or out on an occupied base is attributed to <see cref="AdvancementOutcome.Hold"/> (policy B: nothing is
    /// re-drawn, nothing is renormalised, the drawn result is kept beside the applied one). Then every out and run gets its explicit sequence, lead runner first (P-05). The input is
    /// one drawn result (Extra, Hold or Out) per context in processing order; the draws themselves are not made here.
    /// </summary>
    public static Result<AdvancementComposition> Compose(InPlayPlanFacts facts, GameState state, IReadOnlyList<AdvancementOutcome> drawn)
    {
        if (state is null)
            return Fail<AdvancementComposition>("Advancement.state_missing", "경기 상태가 없어 추가 진루를 계획할 수 없습니다.");
        var made = Contexts(facts);
        if (!made.IsSuccess)
            return Result<AdvancementComposition>.Failure(made.Error!.Value);
        var contexts = made.Value;
        if (drawn is null || drawn.Count != contexts.Length || drawn.Any(d => d is not (AdvancementOutcome.Extra or AdvancementOutcome.Hold or AdvancementOutcome.Out)))
            return Fail<AdvancementComposition>("Advancement.drawn_invalid", $"맥락 {contexts.Length}개마다 확률표에서 뽑은 결과(Extra/Hold/Out) 하나가 필요합니다.");

        var work = AdvancementRules.Start(facts, state);
        var steps = ImmutableArray.CreateBuilder<AdvancementStep>(contexts.Length);
        for (var i = 0; i < contexts.Length; i++)
        {
            var context = contexts[i];
            AdvancementOutcome applied;
            AdvancementReason reason;
            if (AdvancementRules.IsEnded(work))
                (applied, reason) = (AdvancementOutcome.Void, AdvancementReason.PlayOver);
            else if (drawn[i] != AdvancementOutcome.Hold && !AdvancementRules.TargetOpen(context, work))
                (applied, reason) = (AdvancementOutcome.Hold, AdvancementReason.OccupiedBase);
            else
                (applied, reason) = (drawn[i], AdvancementReason.None);
            AdvancementRules.Apply(context, applied, work);
            steps.Add(new AdvancementStep(context, drawn[i], applied, reason, applied switch { AdvancementOutcome.Extra => context.TargetTo, AdvancementOutcome.Out => 0, _ => context.PlanTo }));
        }

        // every existing runner's final move, lead runner first; the explicit sequence is the lead-runner-first order of the play's outs and runs
        var finalTo = steps.ToDictionary(s => s.Context.StartBase, s => s);
        var moves = facts.Moves.OrderByDescending(m => m.From)
            .Select(m => finalTo.TryGetValue(m.From, out var step)
                ? new AdvancementMove(m.RunnerKey, m.Runner, m.From, m.To, step.FinalTo, m.Cause, null, step.Context)
                : new AdvancementMove(m.RunnerKey, m.Runner, m.From, m.To, m.To, m.Cause, null, null))
            .ToList();
        var sequence = 0;
        for (var i = 0; i < moves.Count; i++)
            if (moves[i].IsOut || moves[i].IsRun)
                moves[i] = moves[i] with { Sequence = ++sequence };

        var runs = moves.Count(m => m.IsRun);
        var outsAfter = work.Outs;
        if (runs != work.Runs || outsAfter != facts.PrePlay.Outs + (facts.Official.Result == InPlayResult.Out ? 1 : 0) + moves.Count(m => m.IsOut))
            return Fail<AdvancementComposition>("Advancement.plan_invalid", "합성한 주자 이동의 득점·아웃 수가 작업 상태와 다릅니다.");
        var walkOff = work.WalkOff && work.Runs >= work.Need;
        InPlayWinningRunner? winner = null;
        if (walkOff)
        {
            var winning = moves.Where(m => m.IsRun).OrderBy(m => m.Sequence).ElementAt(work.Need - 1);   // the need-th run in sequence order
            winner = new InPlayWinningRunner(InPlayScorerKind.ExistingRunner, winning.RunnerKey, winning.Runner, winning.From);
        }

        var composition = new AdvancementComposition(steps.ToImmutable(), [.. moves], outsAfter, runs, outsAfter >= work.Limit, walkOff, winner);

        // defence in depth: the pre-check must find the applied results legal, or no event is built from them
        var violations = AdvancementRules.Check(facts, state, contexts, composition.Steps.Select(s => s.Applied).ToArray());
        return violations.IsEmpty
            ? Result<AdvancementComposition>.Success(composition)
            : Fail<AdvancementComposition>("Advancement.plan_invalid", $"합성한 추가 진루가 규칙 사전 검사를 통과하지 못했습니다: {string.Join(",", violations)}");
    }

    private static Result<T> Fail<T>(string code, string message) => Result<T>.Failure(new Error(code, message));
}

public static class AdvancementPlanning
{
    /// <summary>The stages at which a caller's cancellation is honoured, in order (also the names an internal test hook sees; <c>drawing</c> is seen once per decision).</summary>
    internal static class Stage
    {
        public const string Start = "start";
        public const string Validated = "validated";
        public const string Restored = "restored";
        public const string IdIssued = "id_issued";
        public const string Drawing = "drawing";
        public const string Drawn = "drawn";
        public const string Composed = "composed";
        public const string EventBuilt = "event_built";
        public const string BeforeReducer = "before_reducer";
        public const string AfterReducer = "after_reducer";
        public const string Verified = "verified";
    }

    /// <summary>
    /// Resolves the open extra-base decisions of a <see cref="PendingAdvancement"/> into a finished, reducer-validated and still UNCONFIRMED <see cref="InPlayPitchCandidate"/>.
    /// <paramref name="rng"/> is the CURRENT authoritative position H0 (the position the pitch was prepared from), never the pending's H1. In this order and with no side effect on any
    /// input: (1) the shared 3-C2B-1 stale-input check <see cref="PitchPreparation.CheckPrepared"/>, unchanged; (2) H1 restored to a private copy; (3) the declared contexts and their
    /// approved rows; (4) a working event id from the read-only <paramref name="lastIssuedEventSequence"/> (range, exhaustion, receipt collision); (5) exactly one roll per context on
    /// <c>game/runner@1</c> from H1, whatever the result (the position afterwards is H2); (6) the lead-runner-first composition; (7) the event and the unchanged
    /// <see cref="GameReducer.Apply"/> on the original state; (8) the comparison of what the reducer derived with the rules. Adopting H2, the id, the receipt and the state together
    /// is 3-C3. A failure or a cancellation returns no candidate and leaves every input exactly as it was.
    /// </summary>
    public static Result<InPlayPitchCandidate> Resolve(PendingAdvancement? pending, GameState? state, GameRandomSnapshot? rng, TuningProfile? tuning,
        PlayerSheet? batter, PlayerSheet? pitcher, int lastIssuedEventSequence, CancellationToken cancellation = default, PlayerInputSet? playerInputs = null) =>
        Resolve(pending, state, rng, tuning, batter, pitcher, lastIssuedEventSequence, null, cancellation, playerInputs);

    /// <summary>The same, with a hook that sees each stage name before the cancellation token is checked at it (tests cancel at a chosen stage).</summary>
    internal static Result<InPlayPitchCandidate> Resolve(PendingAdvancement? pending, GameState? state, GameRandomSnapshot? rng, TuningProfile? tuning,
        PlayerSheet? batter, PlayerSheet? pitcher, int lastIssuedEventSequence, Action<string>? checkpoint, CancellationToken cancellation, PlayerInputSet? playerInputs = null)
    {
        void At(string stage)
        {
            checkpoint?.Invoke(stage);
            cancellation.ThrowIfCancellationRequested();
        }

        At(Stage.Start);
        if (PitchPreparation.CheckPrepared(pending?.Plan.Prepared, state, rng, tuning, batter, pitcher, playerInputs) is { } stale)
            return Result<InPlayPitchCandidate>.Failure(stale);
        At(Stage.Validated);
        var plan = pending!.Plan;
        var facts = plan.Facts;
        var prepared = plan.Prepared;
        var original = state!;

        // H0 was proved equal to the prepared start by CheckPrepared. H1 is NOT compared with the caller's position: it is where the C2A draw ended, a position nobody has adopted. It is
        // restored (structure and hash) into a private copy, which is where the later draws happen.
        var h1 = pending.RngAfterPitchDraw;
        var restored = GameRandom.Restore(h1, h1.StateHash);
        if (!restored.IsSuccess)
            return Result<InPlayPitchCandidate>.Failure(restored.Error!.Value);
        var random = restored.Value;
        At(Stage.Restored);

        var made = AdvancementPlanner.Contexts(facts);
        if (!made.IsSuccess)
            return Result<InPlayPitchCandidate>.Failure(made.Error!.Value);
        var contexts = made.Value;
        var distributions = new Distribution[contexts.Length];
        var abilities = new AdvancementAbilityAudit?[contexts.Length];

        // The policy is the GAME's (pinned in its execution contract), never the latest one: v1 (also every legacy game) is the neutral policy P-08, v2 passes the current runner's Speed and
        // the Retriever's Arm. Both slots go into ONE Compute call per context, so the row is multiplied and normalised exactly once (R4).
        var policy = original.Execution?.AdvancementPolicyVersion ?? AdvancementPolicy.Version;
        if (policy == AdvancementPolicy.VersionV2 && playerInputs is null)
            return Fail("Advancement.player_inputs_missing", $"추가 진루 정책 '{AdvancementPolicy.VersionV2}'은 선수 입력 집합이 있어야 합니다.");
        for (var i = 0; i < contexts.Length; i++)
        {
            IReadOnlyList<ModifierInput>? inputs = null;
            if (policy == AdvancementPolicy.VersionV2)
            {
                var read = AdvancementSlots.For(contexts[i], original, playerInputs!);
                if (!read.IsSuccess)
                    return Result<InPlayPitchCandidate>.Failure(read.Error!.Value);
                inputs = read.Value.Modifiers;
                abilities[i] = read.Value.Audit;
            }

            var computed = StageProbability.Compute(tuning!, "advancement", contexts[i].Key, inputs);
            if (!computed.IsSuccess)
                return Result<InPlayPitchCandidate>.Failure(computed.Error!.Value);
            if (!computed.Value.Distribution.Outcomes.SequenceEqual(AdvancementOutcomes))
                return Fail("Advancement.outcomes_unexpected", $"'{contexts[i].Key}' 확률표의 결과 순서가 승인된 순서(advance_extra, hold, out_on_bases)와 다릅니다.");
            distributions[i] = computed.Value.Distribution;
        }

        // the working id on a private counter; the caller's cursor is only read (the same checks, codes and order as InPlayPitchPlanning.Build)
        var ids = GameEventIds.Create(original.GameId, lastIssuedEventSequence);
        if (!ids.IsSuccess)
            return Result<InPlayPitchCandidate>.Failure(ids.Error!.Value);
        var id = ids.Value.Next();
        if (!id.IsSuccess)
            return Result<InPlayPitchCandidate>.Failure(id.Error!.Value);
        if (original.AppliedEvents.Any(r => r.EventId == id.Value))
            return Fail("InPlayCandidate.event_id_in_use", $"후보 사건 ID '{id.Value}'는 이미 이 경기에 쓰였습니다. 마지막 발급 순번이 오래됐을 수 있습니다.");
        At(Stage.IdIssued);

        // P-07: exactly one logical roll per declared context, always, in processing order; never re-drawn. Rejection sampling may use more raw outputs than calls.
        var rolls = new uint[contexts.Length];
        var rawDraws = new long[contexts.Length];
        var drawn = random.Atomically(work =>
        {
            var stream = work.Stream(GameRngStreams.Runner);
            for (var i = 0; i < contexts.Length; i++)
            {
                At(Stage.Drawing);
                var before = stream.Draws;
                rolls[i] = stream.NextBounded(ProbabilityScale.RollBound);
                rawDraws[i] = stream.Draws - before;
            }

            return Result<long>.Success(stream.Draws);
        }, cancellation);
        if (!drawn.IsSuccess)
            return Result<InPlayPitchCandidate>.Failure(drawn.Error!.Value);
        var h2 = random.Snapshot();
        var results = new AdvancementOutcome[contexts.Length];
        for (var i = 0; i < contexts.Length; i++)
        {
            var index = distributions[i].SelectIndex(rolls[i]);
            if (!index.IsSuccess)
                return Result<InPlayPitchCandidate>.Failure(index.Error!.Value);
            results[i] = (AdvancementOutcome)index.Value;                                    // Extra, Hold, Out follow the row's outcome order
        }

        At(Stage.Drawn);

        var composed = AdvancementPlanner.Compose(facts, original, results);
        if (!composed.IsSuccess)
            return Result<InPlayPitchCandidate>.Failure(composed.Error!.Value);
        var composition = composed.Value;
        At(Stage.Composed);

        var evt = AdvancementEventBuilder.Build(facts, composition, id.Value, prepared.Draft.Location);
        At(Stage.EventBuilt);

        // the reducer judges the event on the ORIGINAL state; its result stays an unconfirmed preview and the event is never changed afterwards
        At(Stage.BeforeReducer);
        var applied = GameReducer.Apply(original, evt);
        At(Stage.AfterReducer);
        if (AdvancementEventBuilder.Verify(facts, original, composition, evt, applied, out var record) is { } problem)
            return Result<InPlayPitchCandidate>.Failure(problem);
        var detail = Audit(plan, composition, rolls, rawDraws, distributions, h1, h2, evt, record!, policy, abilities, playerInputs?.Id);
        if (policy == AdvancementPolicy.VersionV2 && AdvancementEventBuilder.ThrowOutCreditProblem(detail.Decisions, applied.Value.NewPlays[0], original) is { } armProblem)
            return Result<InPlayPitchCandidate>.Failure(armProblem);
        At(Stage.Verified);

        var audit = plan.Audit with
        {
            Advancement = "resolved", FinalRngStateHash = h2.StateHash, WinningRunner = composition.Winner,
            WalkOffEffect = composition.WalkOffReached ? InPlayWalkOffEffect.GameEnds : plan.Audit.WalkOffEffect, AdvancementDetail = detail,
        };
        return Result<InPlayPitchCandidate>.Success(new InPlayPitchCandidate(plan, evt, applied.Value, h2, audit));
    }

    private static readonly string[] AdvancementOutcomes = ["advance_extra", "hold", "out_on_bases"];

    private static AdvancementAudit Audit(BaseInPlayPlan plan, AdvancementComposition composition, uint[] rolls, long[] rawDraws, Distribution[] distributions,
        GameRandomSnapshot h1, GameRandomSnapshot h2, InPlayEvent evt, AdvancementReducerRecord record, string policy, AdvancementAbilityAudit?[] abilities, string? playerInputsId)
    {
        var decisions = composition.Steps.Select((s, i) => new AdvancementDecisionAudit(
            i + 1, s.Context.Key, s.Context.RunnerKey, s.Context.Runner, s.Context.StartBase, s.Context.PlanTo, s.Context.TargetTo, rolls[i], rawDraws[i], [.. distributions[i].Ppm],
            s.Drawn, s.Applied, s.Reason, s.FinalTo, composition.Moves.First(m => m.RunnerKey == s.Context.RunnerKey).Sequence, abilities[i])).ToImmutableArray();
        var moves = composition.Moves.Select(m => new AdvancementMoveAudit(m.RunnerKey, m.Runner, m.From, m.BasePlanTo, m.To, m.Cause, m.Sequence, m.IsRun, m.IsOut)).ToImmutableArray();

        // what the extra-base decisions changed in the official record, compared with the base plan's own result
        var changes = ImmutableArray.CreateBuilder<string>();
        if (record.Result == BatterResult.SacrificeFly)
            changes.Add("batter_result_sacrifice_fly");
        if (composition.WalkOffReached)
            changes.Add("walkoff_game_ends");
        if (composition.Runs != plan.Runs)
            changes.Add("runs_changed_by_advancement");
        if (composition.OutsAfter != plan.State.Outs + (plan.OfficialResult == InPlayResult.Out ? 1 : 0))
            changes.Add("outs_changed_by_advancement");

        return new AdvancementAudit(
            policy, AdvancementPolicy.SequenceVersion, AdvancementPolicy.HoldPolicy, AdvancementPolicy.CouplingRule, GameRngStreams.Runner.Canonical, rolls.Length,
            rawDraws.Sum(), plan.Prepared.StartRngStateHash, h1.StateHash, h2.StateHash, decisions, moves, evt.Result, evt.Batted, record.Result, record.Runs, record.Rbi, record.AtBat,
            record.OutsAfter, record.HalfOver, record.LeftOnBase, record.GameEnded, composition.Winner, changes.ToImmutable(), "reducer_verified", policy == AdvancementPolicy.VersionV2 ? playerInputsId : null);
    }

    private static Result<InPlayPitchCandidate> Fail(string code, string message) => Result<InPlayPitchCandidate>.Failure(new Error(code, message));
}
