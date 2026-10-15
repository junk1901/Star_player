using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;

namespace StarPlayer.Simulation.Baseball;

// Phase 3-C2B-3 (ADR-007 "3-C2B-3-P policy approval", docs/game-engine.md section 25, docs/reports/PHASE_3C2B3_POLICY_DECISION_DRAFT.md P-01..P-10): the types of the
// extra-base (`advancement`) decision. Resolver-internal like the draft and the candidate they end in: no Application or Infrastructure signature may name them (a test pins it).
// Nothing here is adopted anywhere: state, RNG position (H2), event id and receipts stay with 3-C3.

/// <summary>Policy identifiers recorded in the audit (P-09). They name the approved decisions; none is a KBO 2026 certification.</summary>
public static class AdvancementPolicy
{
    /// <summary>P-01 lead-runner-first draw order, P-02 sequential conditional composition, P-04 rule pre-check, P-07 fixed one-draw-per-context consumption, P-08 neutral abilities.</summary>
    public const string Version = "advancement-policy-v1";

    /// <summary>
    /// Phase 3-D (owner decisions R2-R4, S3): the same draw contract with the two ability slots wired. Each context passes the current runner's <c>runner.speed</c> and the Retriever's
    /// <c>defense.arm</c> to ONE <c>advancement</c> computation; the policy is pinned per game by <see cref="GameExecution.AdvancementPolicyVersion"/>. v1 (above) stays the neutral policy.
    /// </summary>
    public const string VersionV2 = "advancement-policy-v2";

    /// <summary>P-05 `Prototype Sequence Policy v1`: a deterministic simulation assumption (lead runner's run before a trailing runner's non-force out), not an observed order.</summary>
    public const string SequenceVersion = "prototype-sequence-v1";

    /// <summary>P-03 policy B: the approved row is drawn as it is; an impossible result is attributed to Hold and both facts are recorded.</summary>
    public const string HoldPolicy = "B";

    /// <summary>P-02 R1: a base that is occupied cannot be attempted, so both the extra base and being thrown out there are impossible.</summary>
    public const string CouplingRule = "attempt-coupled-v1";
}

/// <summary>
/// The result of one extra-base decision. <see cref="Extra"/>, <see cref="Hold"/> and <see cref="Out"/> are the three approved outcomes of the `advancement` row
/// (<c>advance_extra</c>, <c>hold</c>, <c>out_on_bases</c>); <see cref="Void"/> is only ever an APPLIED result: the play was already over, so the drawn result does not apply.
/// A Void runner stays where the base plan put him, exactly like a Hold, but the two are different facts.
/// </summary>
public enum AdvancementOutcome { Extra, Hold, Out, Void }

/// <summary>Why the applied result differs from (or equals) the drawn one.</summary>
public enum AdvancementReason
{
    /// <summary>The drawn result was applied as drawn.</summary>
    None,

    /// <summary>The base to be attempted was occupied (P-02 R1), so the drawn extra base or out became Hold (P-03 B).</summary>
    OccupiedBase,

    /// <summary>The third out or the winning run had already happened (P-02 R2): the result is Void.</summary>
    PlayOver,
}

/// <summary>
/// The two ability inputs one decision of <see cref="AdvancementPolicy.VersionV2"/> used (R2, R3): the runner standing on the start base NOW (a pinch runner is the current player; the
/// <c>RunnerKey</c> of the decision names the runner slot, not the person) with his base Speed, and the fielder at the Retriever position NOW with his base Arm. The Retriever is the
/// fielder credited with the assist of a throw-out; the BaseFielder (the receiver) is never an input.
/// </summary>
public sealed record AdvancementAbilityAudit(PlayerId Runner, int RunnerSpeed, FieldPosition Retriever, PlayerId RetrieverPlayer, int RetrieverArm);

/// <summary>
/// One decision as drawn and as applied (P-09). <c>Order</c> is 1-based in processing order (lead runner first); <c>Roll</c> is the exact value of the one <c>NextBounded(1,000,000)</c> call.
/// <c>Abilities</c> is set only under <see cref="AdvancementPolicy.VersionV2"/> and says which two players' ratings produced <c>Ppm</c>; under v1 it is <c>null</c> (neutral abilities).
/// </summary>
public sealed record AdvancementDecisionAudit(
    int Order, string ContextKey, int RunnerKey, PlayerId Runner, int StartBase, int PlanTo, int TargetTo, uint Roll, long RawDraws, ImmutableArray<int> Ppm,
    AdvancementOutcome Drawn, AdvancementOutcome Applied, AdvancementReason Reason, int FinalTo, int? Sequence, AdvancementAbilityAudit? Abilities = null);

/// <summary>One existing runner's final move on the play, with the event sequence it carries (null when the play has no sequenced out or run).</summary>
public sealed record AdvancementMoveAudit(int RunnerKey, PlayerId Runner, int From, int BasePlanTo, int FinalTo, MoveCause Cause, int? Sequence, bool IsRun, bool IsOut);

/// <summary>
/// The P-09 audit of a resolved play: Simulation-internal evidence, NOT part of the <c>ef1</c>/<c>ef2</c> text, the event JSON or any hash, and neither committed nor persisted
/// by this phase. The event INPUT (<see cref="EventResult"/>) and what the unchanged reducer DERIVED (<see cref="ReducerBatterResult"/>, runs, RBI ...) are kept as separate fields.
/// </summary>
public sealed record AdvancementAudit(
    string PolicyVersion, string SequencePolicyVersion, string HoldPolicy, string CouplingRule, string DrawStream, int LogicalDraws, long RawDraws,
    string StartRngStateHash, string AfterPitchRngStateHash, string FinalRngStateHash,
    ImmutableArray<AdvancementDecisionAudit> Decisions, ImmutableArray<AdvancementMoveAudit> FinalMoves,
    InPlayResult EventResult, BattedBallType EventBatted, BatterResult ReducerBatterResult, int ReducerRuns, int ReducerRbi, bool ReducerAtBat, int ReducerOutsAfter,
    bool ReducerHalfOver, int ReducerLeftOnBase, bool GameEnded, InPlayWinningRunner? WinningRunner, ImmutableArray<string> RecordChanges, string Verification,
    string? PlayerInputsId = null);

// ---------------------------------------------------------------------------------------------------------------------------------------------------------------------
// internal working types of the pure planner

/// <summary>One open extra-base decision of a pending play: the runner, where the base plan left him, the base he would attempt (4 = home) and the two fielders an out would credit.</summary>
internal sealed record AdvancementContext(int Order, string Key, int RunnerKey, PlayerId Runner, int StartBase, int PlanTo, int TargetTo, FieldPosition Retriever, FieldPosition BaseFielder);

/// <summary>A decision after composition: the drawn result, the applied result, the reason and the base the runner ends on (0 = out, 4 = scored).</summary>
internal sealed record AdvancementStep(AdvancementContext Context, AdvancementOutcome Drawn, AdvancementOutcome Applied, AdvancementReason Reason, int FinalTo);

/// <summary>One existing runner's final move; <c>Sequence</c> is set for every out and run when the play has any (all or none, ordered lead runner first).</summary>
internal sealed record AdvancementMove(int RunnerKey, PlayerId Runner, int From, int BasePlanTo, int To, MoveCause Cause, int? Sequence, AdvancementContext? Context)
{
    public bool IsRun => To == 4;

    public bool IsOut => To == 0;
}

/// <summary>The composed play: every decision, every runner's final move and the facts the rules derive from them.</summary>
internal sealed record AdvancementComposition(
    ImmutableArray<AdvancementStep> Steps, ImmutableArray<AdvancementMove> Moves, int OutsAfter, int Runs, bool HalfOver, bool WalkOffReached, InPlayWinningRunner? Winner);
