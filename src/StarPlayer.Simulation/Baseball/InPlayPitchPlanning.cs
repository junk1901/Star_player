using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.Simulation.Baseball;

// Phase 3-C2B-2 (ADR-007, docs/game-engine.md section 23): turns a REAL prepared in-play pitch into either a finished, reducer-validated and still UNCONFIRMED candidate or a
// pending advancement. Nothing here draws a random number, adopts a state, an RNG position, an event id or a receipt, or persists anything: that is 3-C3 / Phase 4. The stale-
// preparation contract is PitchPreparation.CheckPrepared, the very check the non-in-play builder uses.

public static class InPlayPitchPlanning
{
    /// <summary>The stages at which a caller's cancellation is honoured, in order (also the names an internal test hook sees).</summary>
    internal static class Stage
    {
        public const string Start = "start";
        public const string Validated = "validated";
        public const string Planning = "planning";
        public const string Planned = "planned";
        public const string BeforeReducer = "before_reducer";
        public const string AfterReducer = "after_reducer";
        public const string Verified = "verified";
    }

    /// <summary>
    /// Re-checks that the preparation is still current (the shared 3-C2B-1 contract) and turns its in-play draft into the deterministic base plan. No random number, no event id,
    /// no reducer call. A pitch that did not end in play is refused (<c>InPlayPlan.not_in_play</c>); the draft's batted ball and defence result must be an approved combination.
    /// </summary>
    public static Result<BaseInPlayPlan> PlanBase(PreparedPitch? prepared, GameState? state, GameRandomSnapshot? rng, TuningProfile? tuning,
        PlayerSheet? batter, PlayerSheet? pitcher, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        if (PitchPreparation.CheckPrepared(prepared, state, rng, tuning, batter, pitcher) is { } stale)
            return Result<BaseInPlayPlan>.Failure(stale);
        return Plan(prepared!, state!);
    }

    /// <summary>
    /// Plans the base play and, when no extra-base decision is open, builds the in-play event candidate: a working id from the read-only <paramref name="lastIssuedEventSequence"/>,
    /// an <see cref="InPlayEvent"/> carrying the draft's location, judged by the unchanged <see cref="GameReducer.Apply"/> on the original state and checked against the plan. The
    /// result is a <see cref="InPlayPitchCandidate"/> or a <see cref="PendingAdvancement"/> (which has no event, id or receipt and ignores the cursor). Nothing is adopted anywhere.
    /// </summary>
    public static Result<InPlayPlanOutcome> Build(PreparedPitch? prepared, GameState? state, GameRandomSnapshot? rng, TuningProfile? tuning,
        PlayerSheet? batter, PlayerSheet? pitcher, int lastIssuedEventSequence, CancellationToken cancellation = default) =>
        Build(prepared, state, rng, tuning, batter, pitcher, lastIssuedEventSequence, null, cancellation);

    /// <summary>The same, with a hook that sees each stage name before the cancellation token is checked at it (tests cancel at a chosen stage).</summary>
    internal static Result<InPlayPlanOutcome> Build(PreparedPitch? prepared, GameState? state, GameRandomSnapshot? rng, TuningProfile? tuning,
        PlayerSheet? batter, PlayerSheet? pitcher, int lastIssuedEventSequence, Action<string>? checkpoint, CancellationToken cancellation)
    {
        void At(string stage)
        {
            checkpoint?.Invoke(stage);
            cancellation.ThrowIfCancellationRequested();
        }

        At(Stage.Start);
        if (PitchPreparation.CheckPrepared(prepared, state, rng, tuning, batter, pitcher) is { } stale)
            return Result<InPlayPlanOutcome>.Failure(stale);
        At(Stage.Validated);

        At(Stage.Planning);
        var planned = Plan(prepared!, state!);
        if (!planned.IsSuccess)
            return Result<InPlayPlanOutcome>.Failure(planned.Error!.Value);
        var plan = planned.Value;
        At(Stage.Planned);

        // a decision about an extra base is not made here: no id, no event, no receipt, nothing adoptable (3-C2B-3 draws it from the position after the pitch draw)
        if (plan.AdvancementRequired)
            return Result<InPlayPlanOutcome>.Success(new PendingAdvancement(plan));

        // the id on a private counter; the caller's cursor is only read
        var original = state!;
        var ids = GameEventIds.Create(original.GameId, lastIssuedEventSequence);
        if (!ids.IsSuccess)
            return Result<InPlayPlanOutcome>.Failure(ids.Error!.Value);
        var id = ids.Value.Next();
        if (!id.IsSuccess)
            return Result<InPlayPlanOutcome>.Failure(id.Error!.Value);
        if (original.AppliedEvents.Any(r => r.EventId == id.Value))
            return Fail("InPlayCandidate.event_id_in_use", $"후보 사건 ID '{id.Value}'는 이미 이 경기에 쓰였습니다. 마지막 발급 순번이 오래됐을 수 있습니다.");

        var candidate = InPlayBasePlanner.BuildEvent(plan.Facts, id.Value, prepared!.Draft.Location);

        // the reducer judges the candidate on the ORIGINAL state; its result stays an unconfirmed preview
        At(Stage.BeforeReducer);
        var applied = GameReducer.Apply(original, candidate);
        At(Stage.AfterReducer);
        if (Verify(plan.Facts, original, candidate, applied) is { } problem)
            return Result<InPlayPlanOutcome>.Failure(problem);
        At(Stage.Verified);
        return Result<InPlayPlanOutcome>.Success(new InPlayPitchCandidate(plan, candidate, applied.Value, prepared.Draft.NextRng));
    }

    private static Result<BaseInPlayPlan> Plan(PreparedPitch prepared, GameState state)
    {
        if (prepared.Draft.Kind != PitchBranchKind.InPlay)
            return Result<BaseInPlayPlan>.Failure(new Error("InPlayPlan.not_in_play", "인플레이로 끝나지 않은 투구는 인플레이 계획을 만들 수 없습니다."));
        var facts = InPlayBasePlanner.Plan(state, prepared.Draft.BattedBall, prepared.Draft.DefenseResult);
        return facts.IsSuccess
            ? Result<BaseInPlayPlan>.Success(new BaseInPlayPlan(prepared, facts.Value))
            : Result<BaseInPlayPlan>.Failure(facts.Error!.Value);
    }

    /// <summary>
    /// Checks the reducer's verdict on the built event against the plan, in this order: the reducer's own refusal is passed through; a duplicate is never a new candidate; the
    /// receipt must be the candidate's own <c>ef2</c> receipt; exactly one actual pitch must have been counted for the ORIGINAL fielding team's pitcher; and the scored play must
    /// agree with the plan (official result, runs, credits, errors, one move per runner and the batter, the half-inning end and a walk-off's final home win). Returns the first
    /// problem or null. The official run, RBI, left-on-base and earned-run numbers are the reducer's; they are never recomputed here.
    /// </summary>
    internal static Error? Verify(InPlayPlanFacts facts, GameState original, InPlayEvent candidate, Result<GameStep> applied)
    {
        if (VerifyApplied(original, candidate, applied) is { } basic)
            return basic;
        var step = applied.Value;

        var play = step.NewPlays.Count == 1 ? step.NewPlays[0] : null;
        var officialOk = play is not null
            && play.Result?.ToString() == facts.Official.Result.ToString()   // the reducer's BatterResult names match the event's InPlayResult names
            && play.Runs.Count == facts.Runs
            && play.Credits.Count == facts.Official.Credits.Length
            && play.Errors.Count == (facts.Official.Error is null ? 0 : 1)
            && play.Moves.Count == original.Bases.Count + 1
            && (play.OutsAfter >= original.Rules.OutsPerHalfInning) == facts.EndsHalfInning
            && (!facts.WalkOffEndsGame || (step.State.Status == GameStatus.Final && step.State.Outcome == GameOutcome.HomeWin));
        return officialOk ? null : new Error("InPlayCandidate.official_record_invalid", "리듀서가 확정한 공식 기록이 계획과 다릅니다.");
    }

    /// <summary>
    /// The checks every in-play candidate shares (the base plan here, the resolved advancement in 3-C2B-3), in this order: the reducer's own refusal is passed through; a
    /// duplicate is never a new candidate; the receipt must be the candidate's own <c>ef2</c> receipt; exactly one actual pitch must have been counted for the ORIGINAL
    /// fielding team's pitcher. Returns the first problem or null.
    /// </summary>
    internal static Error? VerifyApplied(GameState original, InPlayEvent candidate, Result<GameStep> applied)
    {
        if (!applied.IsSuccess)
            return applied.Error;
        var step = applied.Value;
        if (step.WasDuplicate)
            return new Error("InPlayCandidate.event_duplicate", "이미 적용된 사건과 같은 내용이라 새 후보로 인정할 수 없습니다.");
        var receipt = step.State.AppliedEvents.LastOrDefault();
        if (receipt is null || receipt.EventId != candidate.Id || receipt.FingerprintVersion != EventFingerprint.VersionV2 || GameEventLocation.Of(candidate) is null)
            return new Error("InPlayCandidate.receipt_invalid", "후보의 영수증이 위치를 담은 ef2 형식이 아닙니다.");
        if (PitchPreparation.PitchesOf(step.State, original) != PitchPreparation.PitchesOf(original, original) + 1)
            return new Error("InPlayCandidate.pitch_count_invalid", "후보가 실제 투구를 정확히 한 번 세지 않았습니다.");
        return null;
    }

    private static Result<InPlayPlanOutcome> Fail(string code, string message) => Result<InPlayPlanOutcome>.Failure(new Error(code, message));
}
