using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.SimHarness.Evidence;

/// <summary>One <c>catcher.framing</c> entry of a branch step's trace, in trace order (§5.4 FramingEntry).</summary>
public sealed record ObservedFraming(long FactorPpm, bool Applied);

/// <summary>One drawn probability decision of a pitch (§5.4 BranchStep), copied as plain values.</summary>
public sealed record ObservedStep(string Stage, string Condition, string Outcome, string Stream, uint Roll, long RawDraws, IReadOnlyList<ObservedFraming> Framing);

/// <summary>What one stream was used for in a pitch draft (§5.4 StreamUse).</summary>
public sealed record ObservedStreamUse(string Stream, int Calls, long RawDraws);

/// <summary>
/// The prepared pitch behind one committed pitch / take / in-play event (§5.4), copied as plain values the moment the commit is published. <see cref="H1"/> is the RNG position right after the
/// 3-C2A draft (<c>Draft.NextRng</c>); the runner values come from the in-play candidate's advancement audit (0 / 0 without an advancement). No hidden tendency or development value is copied.
/// </summary>
public sealed record ObservedPitch(
    string ContractVersion, string BranchKind, bool ExplicitTake, string Intent, string TargetZone, string ZoneModelVersion, string PitchZoneId, string AppliedBatterSide, string BoundaryClass,
    string BatterId, string PitcherId, string? CatcherId, string? PlayerInputSetId, int? InputReceivingFraming, int? FrozenReceivingFraming, string RulesProfileId, string RulesVersion,
    string RulesHash, string ProbabilityModelVersion, string TuningVersion, string TuningHash, string H1, IReadOnlyList<ObservedStep> Steps, IReadOnlyList<ObservedStreamUse> StreamUsage,
    int NextBoundedCalls, int RunnerCalls, long RunnerRawDraws);

/// <summary>One confirmed commit (§5.3 EventObservation input): its sequence, event, published receipt and the canonical RNG before (H0) and after (H2) the publish.</summary>
public sealed record ObservedEvent(int Sequence, GameEvent Event, EventReceipt Receipt, string RngBefore, string RngAfter, ObservedPitch? Pitch);

/// <summary>
/// T04a: the per-event observation seam of ONE game (B1-T00 §5.3-§5.4, §10.4, §11.3). <see cref="GameExecutor"/> calls <see cref="Started"/> once the game has begun and <see cref="Committed"/> right after each
/// successful publish, before any failure decision. It only READS immutable values (the commit record, the snapshots' canonical text, the prepared pitch and the advancement audit): it decides nothing,
/// draws nothing and is invisible to every result hash (game-result-v1, run-result-v1/v2, event / RNG hashes, config hash, attestation). With no observer the executor behaves exactly as before.
/// </summary>
public sealed class PzlEventObserver
{
    private const string FramingSlot = "catcher.framing";

    private readonly List<ObservedEvent> _events = [];

    /// <summary>The canonical RNG snapshot at the start of the game (null when the game never started: §10.4 case A).</summary>
    public string? StartRng { get; private set; }

    public IReadOnlyList<ObservedEvent> Events => _events;

    /// <summary>The canonical RNG at the end: the last published position, or the start position when nothing was published (case B), or null (case A).</summary>
    public string? FinalRng => _events.Count > 0 ? _events[^1].RngAfter : StartRng;

    /// <summary>OWNER decision 3 §4: the GameState the executor last held when the game ended (completed or stopped) — the production state #6 compares; null when the game never started.</summary>
    public GameState? FinalState { get; private set; }

    internal void Started(string startCanonical) => StartRng ??= startCanonical;

    /// <summary>Read-only, once at the end of a started game (an immutable record; nothing is drawn, decided or changed).</summary>
    internal void Finished(GameState state) => FinalState = state;

    internal void Committed(CommitRecord record, string rngBefore, string rngAfter, PreparedPitch? pitch, InPlayAudit? inPlay, RunConfig run) =>
        _events.Add(new ObservedEvent(record.EventSequence, record.Event, record.Receipt, rngBefore, rngAfter, pitch is null ? null : Pitch(pitch, inPlay, run)));

    private static ObservedPitch Pitch(PreparedPitch p, InPlayAudit? inPlay, RunConfig run)
    {
        var d = p.Draft;
        var catcher = p.Catcher?.Catcher;
        var advancement = inPlay?.AdvancementDetail;
        return new ObservedPitch(
            d.ContractVersion, d.Kind.ToString(), d.ExplicitTake, d.Intent.ToString(), d.Target.Zone.Value, d.Location.Model.Value, d.Location.Zone.Value, d.Location.Side.ToString(),
            d.Location.Boundary.ToString(), p.BatterId.Value, p.PitcherId.Value, catcher?.Value, p.PlayerInputsId ?? p.Catcher?.InputSetId,
            catcher is { } c ? run.Cohort.InputSet.Find(c)?.ReceivingFraming : null, p.Catcher?.ReceivingFraming, d.RulesProfileId, d.RulesVersion, d.RulesHash, d.ProbabilityModelVersion,
            d.TuningVersion, d.TuningHash, d.NextRng.Canonical(),
            d.Steps.Select(s => new ObservedStep(s.Stage, s.Condition, s.Outcome, s.Stream.Canonical, s.Roll, s.RawDraws,
                s.Trace.Modifiers.Where(m => m.Slot == FramingSlot).Select(m => new ObservedFraming(m.FactorPpm, m.Applied)).ToList())).ToList(),
            d.StreamUsage.OrderBy(u => u.Stream.Canonical, StringComparer.Ordinal).Select(u => new ObservedStreamUse(u.Stream.Canonical, u.Calls, u.RawDraws)).ToList(),
            d.NextBoundedCalls, advancement?.LogicalDraws ?? 0, advancement?.RawDraws ?? 0);
    }
}
