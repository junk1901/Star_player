using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.Simulation.Baseball;

// Phase 3-C2A (ADR-007, docs/game-engine.md section 21): ONE pitch's probability branch, drawn from the real seeded streams.
// The result is a DRAFT. It is not a GameEvent, is never applied to a GameState and has no event id or fingerprint; turning a draft into a legal event is 3-C2B,
// committing it together with ids, receipts and the RNG is 3-C3. Resolver-internal: nothing here may appear in an Application or Infrastructure signature (a test pins it).

/// <summary>
/// The pitcher's intent for one pitch: the condition key of the <c>target_zone</c> stage. It is an explicit input. Nothing here chooses an intent from the batter's
/// hidden tendency or from any other hidden value (advanced weakness-exploiting AI is deferred, ROADMAP 1.14).
/// </summary>
public enum PitchIntent { Balanced, Attack, Careful }

/// <summary>What the drawn branch ends in. The mapping to a legal <c>PitchSpec</c>/<c>InPlayEvent</c> is 3-C2B's job.</summary>
public enum PitchBranchKind { HitByPitch, CalledBall, CalledStrike, SwingingStrike, Foul, InPlay }

/// <summary>
/// Everything the branch needs from its caller. <see cref="State"/> is only READ (it supplies the rules, the game status and the plate-appearance stage); the resolver
/// never changes it. <see cref="Rng"/> is the caller's current RNG position: the resolver continues from it on a private copy and hands the next position back in the
/// draft. A missing batter side or pitcher hand is an error, never a default (<see cref="BattingSide"/>/<see cref="ThrowingHand"/> are nullable on purpose).
/// <see cref="BatterTendency"/> (Phase 3-D-1) is the batter's HIDDEN zone tendency for the game resolver only; null means the neutral vector of the tuning's reference, which is
/// still supplied to the two tendency stages. It is neither stored in the draft nor printed.
/// <see cref="Abilities"/> (Phase 3-D) are the six base ratings of the two players; when given, the resolver also derives approach, count pressure and handedness from the state and
/// the applied side and passes the nine live slots' <see cref="ModifierInput"/>s to their stages. Null means no modifier at all (the ability-free computation, as before 3-D).
/// </summary>
public sealed record PitchBranchRequest(
    GameState? State, TuningProfile? Tuning, PitchIntent Intent, bool ExplicitTake, BattingSide? BatterDeclaredSide, ThrowingHand? PitcherThrows, GameRandomSnapshot? Rng,
    BatterZoneTendency? BatterTendency = null, PitchAbilities? Abilities = null);

/// <summary>One probability decision of the branch, with the stream it drew from and the exact roll (audit data; the trace is resolver-internal).</summary>
public sealed record PitchBranchStep(string Stage, string Condition, string Outcome, RngStreamId Stream, uint Roll, long RawDraws, ProbabilityTrace Trace);

/// <summary>What one stream was used for in this draft: <see cref="Calls"/> decisions (each exactly one <c>NextBounded(1,000,000)</c>) and the raw 32-bit outputs they consumed.</summary>
public sealed record PitchBranchStreamUse(RngStreamId Stream, int Calls, long RawDraws);

/// <summary>
/// The draft of one pitch. <see cref="Target"/> is what the pitcher aimed at and <see cref="Location"/> is where the pitch actually went (drawn before every outcome);
/// they are different types and facts. Only the stages that were reached are filled. <see cref="NextRng"/> is the stream position after the draft, for the caller to
/// adopt only when it accepts the draft.
/// </summary>
public sealed record PitchBranchDraft(
    string ContractVersion, PitchIntent Intent, bool ExplicitTake, TargetZone Target, PitchLocation Location, PitchBranchKind Kind,
    string BatterDecision, string? CalledPitch, string? SwingResult, string? BattedBall, string? DefenseResult,
    ImmutableArray<PitchBranchStep> Steps, int NextBoundedCalls, ImmutableArray<PitchBranchStreamUse> StreamUsage, GameRandomSnapshot NextRng,
    string ProbabilityModelVersion, string TuningVersion, string TuningHash, string RulesProfileId, string RulesVersion, string RulesHash)
{
    /// <summary>The batter side actually applied to this pitch (also inside <see cref="Location"/>).</summary>
    public AppliedBatterSide AppliedSide => Location.Side;

    public override string ToString() => $"PitchBranchDraft({Kind} {Location.Zone} {ContractVersion})";
}
