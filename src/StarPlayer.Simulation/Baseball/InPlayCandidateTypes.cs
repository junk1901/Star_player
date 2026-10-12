using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.Simulation.Baseball;

// Phase 3-C2B-2 (ADR-007 "Decisions (3-C2B-2 ...)", docs/game-engine.md section 23): the result types of the in-play stage. Resolver-internal like the draft they come from:
// no Application or Infrastructure signature may name them (a test pins it). Nothing here is adopted anywhere: state, RNG position, event id and receipts stay with 3-C3.
// The two outcomes of a prepared in-play pitch are SIBLING classes (not one class with optional parts): a PendingAdvancement can never be mistaken for, cast to or converted
// into an InPlayPitchCandidate, because it has no event, id, receipt or unconfirmed step at all.

/// <summary>How the walk-off official-record rule (<c>walkoff-credit-v1</c>) touched the play.</summary>
public enum InPlayWalkOffEffect
{
    /// <summary>The game is not decided by this play (or the rule does not apply to it).</summary>
    None,

    /// <summary>The play ends the game; the drawn result is also the official result.</summary>
    GameEnds,

    /// <summary>The play ends the game and the official hit is smaller than the drawn one (OBR 9.06(f)); the facts of the play are those of the smaller hit.</summary>
    CreditReduced,
}

/// <summary>One existing runner's base-plan result, in scoring order (lead runner first). <c>To</c> 4 = scored; <c>To == From</c> is an explicit hold.</summary>
public sealed record InPlayRunnerPlan(int RunnerKey, PlayerId Runner, int From, int To, MoveCause Cause);

/// <summary>Who scored a walk-off's winning run: a runner who was already on base, or the batter-runner (an out-of-the-park home run's last scorer).</summary>
public enum InPlayScorerKind
{
    ExistingRunner,
    BatterRunner,
}

/// <summary>
/// The scorer of the run that ends a walk-off (the need-th run, lead runner first, a home run's batter last). <c>RunnerKey</c>/<c>Player</c> are the ones the reducer records in
/// <c>ScoredRun</c>: an existing runner's own key, or for the batter-runner the state's <c>NextRunnerKey</c> and the current batter. <c>StartingBase</c> is 1-3, or 0 for the batter-runner.
/// A play without a winning scorer has no value at all (null), never a batter-runner placeholder.
/// </summary>
public sealed record InPlayWinningRunner(InPlayScorerKind Kind, int RunnerKey, PlayerId Player, int StartingBase);

/// <summary>A runner standing on a base before the play.</summary>
public sealed record InPlayBaseRunner(int Base, int RunnerKey, PlayerId Runner);

/// <summary>The situation before the play (audit data).</summary>
public sealed record InPlayPrePlay(int Inning, InningHalf Half, int Outs, int AwayRuns, int HomeRuns, ImmutableArray<InPlayBaseRunner> Runners);

/// <summary>
/// The D5 audit record of one in-play plan (ADR-007 item 9, docs/game-engine.md section 23.8): Simulation-internal evidence, NOT part of the <c>ef1</c>/<c>ef2</c> text, the event
/// JSON or any hash, and neither committed nor persisted by this phase. <see cref="CandidateEventId"/> exists only for a finished candidate (null for a plan and for a pending
/// advancement); <see cref="FinalRngStateHash"/> is the position after the 3-C2A draw (no further number is drawn here).
/// </summary>
public sealed record InPlayAudit(
    string InPlayPolicyVersion, string WalkOffCreditVersion, string TemplateCatalogVersion, string GameId, string? CandidateEventId,
    string OriginalBattedBall, string? OriginalIntensity, string OriginalDefenseResult, InPlayResult OfficialResult, string DrawnTemplateId, string OfficialTemplateId,
    InPlayPrePlay PrePlay, int BatterFinalBase, ImmutableArray<InPlayRunnerPlan> RunnerMoves, InPlayWinningRunner? WinningRunner, InPlayWalkOffEffect WalkOffEffect,
    string RecordChangeReason, ImmutableArray<string> VirtualFacts,
    string RulesProfileId, string RulesVersion, string RulesHash, string TuningVersion, string TuningHash, string ProbabilityModelVersion,
    string StartRngStateHash, string FinalRngStateHash, string Advancement, ImmutableArray<string> AdvancementContextKeys, bool CompositeRunnerContext,
    AdvancementAudit? AdvancementDetail = null);

/// <summary>
/// Everything the base plan decided, computed from a game state and a drawn (batted ball, defence result) only. No random number, event id or reducer call is involved. Internal:
/// the public <see cref="BaseInPlayPlan"/> adds the prepared pitch it came from.
/// </summary>
internal sealed record InPlayPlanFacts(
    string BattedBall, string BattedBallClass, string? Intensity, string DefenseResult, InPlayTemplate Drawn, InPlayTemplate Official,
    ImmutableArray<InPlayRunnerPlan> Moves, int Runs, bool EndsHalfInning, bool WalkOffContext, int RunsNeeded, InPlayWalkOffEffect WalkOffEffect, InPlayWinningRunner? WinningRunner,
    ImmutableArray<string> AdvancementKeys, bool Composite, InPlayPrePlay PrePlay)
{
    public bool WalkOffEndsGame => WalkOffEffect != InPlayWalkOffEffect.None;

    /// <summary>The base plan leaves an extra-base decision open: one of the declared <c>advancement</c> contexts applies and nothing already ended the play.</summary>
    public bool AdvancementRequired => AdvancementKeys.Length > 0 && !WalkOffEndsGame && !EndsHalfInning;

    public string RecordChangeReason => WalkOffEffect == InPlayWalkOffEffect.CreditReduced ? "walkoff_credit_reduced" : "none";
}

/// <summary>
/// The deterministic base plan of one real in-play pitch: template, every existing runner's base move, the official record and whether an extra-base decision is still open.
/// It exists only for a real <see cref="PreparedPitch"/> (no constructor, no setter); building it draws nothing and changes nothing.
/// </summary>
public sealed class BaseInPlayPlan
{
    internal BaseInPlayPlan(PreparedPitch prepared, InPlayPlanFacts facts)
    {
        Prepared = prepared;
        Facts = facts;
        var draft = prepared.Draft;
        Audit = new InPlayAudit(
            InPlayPolicy.MappingVersion, facts.WalkOffContext ? InPlayPolicy.WalkOffCreditVersion : "not_applicable", InPlayPolicy.TemplateCatalogVersion, prepared.GameId, null,
            facts.BattedBall, facts.Intensity, facts.DefenseResult, facts.Official.Result, facts.Drawn.Id, facts.Official.Id,
            facts.PrePlay, facts.Official.BatterDestination, facts.Moves, facts.WinningRunner, facts.WalkOffEffect, facts.RecordChangeReason, facts.Official.VirtualFacts,
            draft.RulesProfileId, prepared.RulesVersion, prepared.RulesHash, prepared.TuningVersion, prepared.TuningHash, prepared.ProbabilityModelVersion,
            prepared.StartRngStateHash, draft.NextRng.StateHash, facts.AdvancementRequired ? "pending" : "not_required", facts.AdvancementKeys, facts.Composite);
    }

    internal InPlayPlanFacts Facts { get; }

    /// <summary>The real preparation this plan came from (it keeps the original state, the draft, its location and the RNG positions).</summary>
    public PreparedPitch Prepared { get; }

    public GameState State => Prepared.State;
    public string GameId => Prepared.GameId;

    public string PolicyVersion => InPlayPolicy.MappingVersion;
    public string WalkOffCreditVersion => Audit.WalkOffCreditVersion;
    public string TemplateCatalogVersion => InPlayPolicy.TemplateCatalogVersion;

    /// <summary>The drawn batted ball as the 3-C2A draft states it (<c>ground_weak</c> ... <c>popup</c>), its class (<c>ground|line|fly|pop</c>) and its intensity (null for a pop-up).</summary>
    public string BattedBall => Facts.BattedBall;
    public string BattedBallClass => Facts.BattedBallClass;
    public string? Intensity => Facts.Intensity;

    /// <summary>The drawn <c>defense_result</c> text, unchanged.</summary>
    public string OriginalDefenseResult => Facts.DefenseResult;
    public InPlayResult DrawnResult => Facts.Drawn.Result;
    public InPlayResult OfficialResult => Facts.Official.Result;
    public string DrawnTemplateId => Facts.Drawn.Id;
    public string OfficialTemplateId => Facts.Official.Id;

    /// <summary>The batter-runner's final base (0 = out, 1-3, 4 = home run); equal to the credited base of the official result.</summary>
    public int BatterFinalBase => Facts.Official.BatterDestination;
    public ImmutableArray<InPlayRunnerPlan> Moves => Facts.Moves;
    public ImmutableArray<PositionCredit> BatterCredits => Facts.Official.Credits;
    public ImmutableArray<FieldPosition> Errors => Facts.Official.Error is { } e ? [e] : [];

    /// <summary>Runs the base plan scores (runners reaching home, plus the batter on a home run).</summary>
    public int Runs => Facts.Runs;
    public bool EndsHalfInning => Facts.EndsHalfInning;
    public bool WalkOffContext => Facts.WalkOffContext;
    public int RunsNeededToLead => Facts.RunsNeeded;
    public InPlayWalkOffEffect WalkOffEffect => Facts.WalkOffEffect;
    public bool WalkOffEndsGame => Facts.WalkOffEndsGame;
    public InPlayWinningRunner? WinningRunner => Facts.WinningRunner;
    public string RecordChangeReason => Facts.RecordChangeReason;

    /// <summary>True when a declared <c>advancement</c> context applies and the plan ended neither the half-inning nor the game: the extra-base decision belongs to 3-C2B-3.</summary>
    public bool AdvancementRequired => Facts.AdvancementRequired;

    /// <summary>Every declared <c>advancement</c> context key that applies to this situation (a composite situation can carry several).</summary>
    public ImmutableArray<string> AdvancementContextKeys => Facts.AdvancementKeys;

    /// <summary>More than one runner was on base: the model declares no context, order or interaction for such a situation (U-1).</summary>
    public bool CompositeRunnerContext => Facts.Composite;

    public InPlayAudit Audit { get; }

    public override string ToString() => $"BaseInPlayPlan({GameId} {BattedBall}/{OriginalDefenseResult} -> {OfficialTemplateId})";
}

/// <summary>The two possible outcomes of <see cref="InPlayPitchPlanning.Build"/>. A closed set (internal constructor): <see cref="InPlayPitchCandidate"/> or <see cref="PendingAdvancement"/>.</summary>
public abstract class InPlayPlanOutcome
{
    internal InPlayPlanOutcome(BaseInPlayPlan plan) => Plan = plan;

    public BaseInPlayPlan Plan { get; }
}

/// <summary>
/// A finished, reducer-validated and still UNCONFIRMED in-play candidate. <see cref="UnconfirmedStep"/> is what <see cref="GameReducer.Apply"/> returned for <see cref="Event"/> on the
/// original state: a preview for validation only. Nothing was published; <see cref="NextRng"/> equals the 3-C2A draft's position (no number was drawn here) and <see cref="EventId"/>
/// came from a private working counter. Adopting state, RNG position, id and receipt together is 3-C3.
/// </summary>
public sealed class InPlayPitchCandidate : InPlayPlanOutcome
{
    internal InPlayPitchCandidate(BaseInPlayPlan plan, InPlayEvent gameEvent, GameStep unconfirmedStep, GameRandomSnapshot nextRng) : this(plan, gameEvent, unconfirmedStep, nextRng, null)
    {
    }

    /// <summary>The candidate of a resolved pending advancement (3-C2B-3): <paramref name="resolvedAudit"/> is the base plan's audit completed with the advancement decisions and H2.</summary>
    internal InPlayPitchCandidate(BaseInPlayPlan plan, InPlayEvent gameEvent, GameStep unconfirmedStep, GameRandomSnapshot nextRng, InPlayAudit? resolvedAudit) : base(plan)
    {
        Event = gameEvent;
        UnconfirmedStep = unconfirmedStep;
        NextRng = nextRng;
        Audit = (resolvedAudit ?? plan.Audit) with { CandidateEventId = gameEvent.Id };
    }

    public InPlayEvent Event { get; }
    public string EventId => Event.Id;
    public GameStep UnconfirmedStep { get; }
    public GameRandomSnapshot NextRng { get; }
    public string ContractVersion => InPlayPolicy.CandidateContractVersion;
    public InPlayAudit Audit { get; }

    public override string ToString() => $"InPlayPitchCandidate({EventId} {Plan.OfficialResult})";
}

/// <summary>
/// An in-play pitch whose base plan leaves an extra-base decision open (3-C2B-3 draws it on the <c>advancement</c> stream). It is NOT a candidate: it has no event, event id, receipt,
/// reducer step or adoptable RNG position. It keeps the prepared pitch, the base plan, every applicable <c>advancement</c> context key, the composite marker and the RNG position
/// AFTER the 3-C2A draw, which is where the later draw must start. This phase draws nothing further.
/// </summary>
public sealed class PendingAdvancement : InPlayPlanOutcome
{
    internal PendingAdvancement(BaseInPlayPlan plan) : base(plan) { }

    public ImmutableArray<string> ContextKeys => Plan.AdvancementContextKeys;
    public bool IsComposite => Plan.CompositeRunnerContext;

    /// <summary>The stream position after the 3-C2A pitch draw: the start of the future advancement draw. Not an adopted position.</summary>
    public GameRandomSnapshot RngAfterPitchDraw => Plan.Prepared.Draft.NextRng;

    public override string ToString() => $"PendingAdvancement({Plan.GameId} {string.Join("+", ContextKeys)})";
}
