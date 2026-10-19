using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.SimHarness;

/// <summary>
/// What one game's committed events showed about Pitch Zone Lite clauses #5, #10 and #12a (ADR-008 FC-D8, proposal §10 / §10.1). Only scalar facts and one canonical text, so it is derived data in no
/// hash: <c>game-result-v1</c>, <c>run-result-v1/v2</c>, the config hash and every receipt are unchanged by it.
/// </summary>
public sealed record PzlGameObservation(
    long Events, long LocatedEvents, long Ef2Receipts, long VersionMismatches, long FingerprintMismatches, long ForeignEventIds, long DuplicateEventIds, long EventIdCollisions,
    long ReceiptLogMismatches, long CalledPitchSteps, long FramingInputs, long FramingEntries, long FramingNonNeutral, string DrawnStreams)
{
    public static PzlGameObservation None { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "");
}

/// <summary>
/// Observes the CONFIRMED commits of one game (a <see cref="CommitRecord"/> exists only for a published commit) and the pitch preparation each pitch commit came from. It never changes or chooses
/// anything; the engine's own checks stay authoritative. #5: every receipt is recomputed from its event, its version must be <c>ef2</c> exactly when the event carries a location, every event id must be
/// this game's canonical id for its sequence, used once, and the final receipt log must be exactly the commits. #10: every <c>catcher.framing</c> entry of a committed pitch's trace and its factor.
/// #12a: every stream a committed draft drew from and every stream of the final RNG state.
/// </summary>
public sealed class PzlRecorder
{
    private const string FramingSlot = "catcher.framing";

    // the neutral factor, ProbabilityScale.Denominator (the harness sources never name the probability scale, EndToEndAndBoundaryTests)
    private const int NeutralFactorPpm = 1_000_000;

    private readonly string _gameId;
    private readonly GameEventIds? _ids;
    private readonly Dictionary<string, string> _seen = new(StringComparer.Ordinal);
    private readonly List<EventReceipt> _commits = [];
    private readonly SortedSet<string> _streams = new(StringComparer.Ordinal);
    private long _located, _ef2, _version, _fingerprint, _foreign, _duplicate, _collision, _called, _inputs, _entries, _nonNeutral;

    public PzlRecorder(string gameId)
    {
        _gameId = gameId;
        var ids = GameEventIds.Create(gameId);
        _ids = ids.IsSuccess ? ids.Value : null;
    }

    public void Observe(CommitRecord record, PreparedPitch? prepared)
    {
        ArgumentNullException.ThrowIfNull(record);
        Observe(record.Event, record.Receipt, record.EventSequence, prepared?.Draft, prepared?.Catcher?.ReceivingFraming);
    }

    /// <summary>The check itself on plain values, so a hand-made violation can be shown (visible to the harness tests only).</summary>
    internal void Observe(GameEvent gameEvent, EventReceipt receipt, int sequence, PitchBranchDraft? draft, int? frozenFraming)
    {
        _commits.Add(receipt);
        var located = GameEventLocation.Of(gameEvent) is not null;
        if (located)
            _located++;
        if (receipt.FingerprintVersion == EventFingerprint.VersionV2)
            _ef2++;
        if (receipt.FingerprintVersion != (located ? EventFingerprint.VersionV2 : EventFingerprint.Version))
            _version++;
        if (receipt != EventFingerprint.Receipt(gameEvent) || receipt.EventId != gameEvent.Id)
            _fingerprint++;
        if (_ids is null || _ids.ParseSequence(receipt.EventId) is not { IsSuccess: true } parsed || parsed.Value != sequence)
            _foreign++;
        if (_seen.TryGetValue(receipt.EventId, out var fingerprint))
        {
            if (fingerprint == receipt.Fingerprint)
                _duplicate++;
            else
                _collision++;
        }
        else
            _seen.Add(receipt.EventId, receipt.Fingerprint);

        if (frozenFraming is not null)
            _inputs++;
        if (draft is null)
            return;
        foreach (var use in draft.StreamUsage)
            _streams.Add(use.Stream.Canonical);
        foreach (var step in draft.Steps)
        {
            _streams.Add(step.Stream.Canonical);
            if (step.Stage == StageIds.CalledPitch)
                _called++;
            foreach (var m in step.Trace.Modifiers.Where(m => m.Slot == FramingSlot))
            {
                _entries++;
                if (m.FactorPpm != NeutralFactorPpm)
                    _nonNeutral++;
            }
        }
    }

    /// <summary>
    /// The observation once the game stopped: <paramref name="applied"/> is the final state's receipt log and <paramref name="drawnStreams"/> the canonical ids of the streams its RNG state has drawn
    /// from (both null when the game never started).
    /// </summary>
    public PzlGameObservation Finish(IReadOnlyList<EventReceipt>? applied, IEnumerable<string>? drawnStreams)
    {
        foreach (var s in drawnStreams ?? [])
            _streams.Add(s);
        applied ??= [];
        var log = Math.Abs(applied.Count - _commits.Count) + applied.Zip(_commits).Count(p => p.First != p.Second);
        return new PzlGameObservation(_commits.Count, _located, _ef2, _version, _fingerprint, _foreign, _duplicate, _collision, log, _called, _inputs, _entries, _nonNeutral,
            string.Join(',', _streams));
    }

    public override string ToString() => $"PzlRecorder({_gameId})";
}

/// <summary>One clause's result for ONE run. A run-level result is never the official clause PASS (that needs the F-C3 official runs, ADR-008 FC-D8.4 / FC-D12).</summary>
public sealed record PzlCheck(string Id, string Clause, string Status, string Reason, IReadOnlyDictionary<string, long> Counts);

/// <summary>
/// The run-level Pitch Zone Lite checks owed by F-C2 (ADR-008 consequences: "PZL #5, #6, #10 and #12a checks"); #6 is a test of the Infrastructure JSON reader (the harness may not reference
/// Infrastructure) and is not computed here. Statuses: <c>PASS</c>, <c>FAIL</c>, <c>NOT_EVALUATED</c> (nothing to check), <c>NOT_APPLICABLE</c>, and <c>DEFERRED</c> for #12b, which is never PASS in Phase 3
/// (FC-D8.2). No threshold other than the clause's own "zero" is used.
/// </summary>
public static class PzlChecks
{
    public const string Pass = "PASS", Fail = "FAIL", NotEvaluated = "NOT_EVALUATED", NotApplicable = "NOT_APPLICABLE", Deferred = "DEFERRED";

    /// <summary>
    /// The registered game stream set fixed by proposal §10.1 (12a): pitch, contact, field, runner, location, ai, target, all version 1, in ordinal order. That the Simulation registry is exactly this set is
    /// asserted by a harness test (the harness sources never name the RNG types, EndToEndAndBoundaryTests); a run passes 12a only if every stream it drew is in this set.
    /// </summary>
    public static IReadOnlyList<string> PinnedStreams { get; } =
        ["game/ai@1", "game/contact@1", "game/field@1", "game/location@1", "game/pitch@1", "game/runner@1", "game/target@1"];

    public static IReadOnlyList<PzlCheck> Evaluate(RunResult run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var games = run.Games.Select(g => g.Pzl ?? PzlGameObservation.None).ToList();
        return [Clause5(run, games), Clause10(run, games), Clause12a(games, PinnedStreams), Clause12b()];
    }

    private static PzlCheck Clause5(RunResult run, IReadOnlyList<PzlGameObservation> games)
    {
        long Sum(Func<PzlGameObservation, long> f) => games.Sum(f);
        var duplicateGames = run.Games.Count - run.Games.Select(g => g.Game.GameId).Distinct(StringComparer.Ordinal).Count();
        var counts = new SortedDictionary<string, long>(StringComparer.Ordinal)
        {
            ["events"] = Sum(o => o.Events), ["located_events"] = Sum(o => o.LocatedEvents), ["ef2_receipts"] = Sum(o => o.Ef2Receipts),
            ["ef1_receipts"] = Sum(o => o.Events - o.Ef2Receipts), ["version_mismatches"] = Sum(o => o.VersionMismatches), ["fingerprint_mismatches"] = Sum(o => o.FingerprintMismatches),
            ["foreign_event_ids"] = Sum(o => o.ForeignEventIds), ["duplicate_event_ids"] = Sum(o => o.DuplicateEventIds), ["event_id_collisions"] = Sum(o => o.EventIdCollisions),
            ["receipt_log_mismatches"] = Sum(o => o.ReceiptLogMismatches), ["duplicate_game_ids"] = duplicateGames,
        };
        var bad = counts.Where(c => c.Key is not ("events" or "located_events" or "ef2_receipts" or "ef1_receipts") && c.Value != 0).Select(c => $"{c.Key} {c.Value}").ToList();
        var (status, reason) = bad.Count > 0 ? (Fail, string.Join(", ", bad))
            : counts["events"] == 0 ? (NotEvaluated, "확정된 사건이 없습니다.")
            : (Pass, "위치 있는 사건은 모두 ef2·없는 사건은 모두 ef1, receipt 재계산 일치, EventId는 각 경기의 정규 ID로 한 번씩, 실행 안 GameId 고유 → 실행 전체 EventId 충돌 0.");
        return new PzlCheck("pzl-5", "new `ef2` events are identified and collision-checked", status, reason, counts);
    }

    private static PzlCheck Clause10(RunResult run, IReadOnlyList<PzlGameObservation> games)
    {
        var counts = new SortedDictionary<string, long>(StringComparer.Ordinal)
        {
            ["called_pitch_steps"] = games.Sum(o => o.CalledPitchSteps), ["framing_inputs"] = games.Sum(o => o.FramingInputs),
            ["framing_entries"] = games.Sum(o => o.FramingEntries), ["framing_non_neutral"] = games.Sum(o => o.FramingNonNeutral),
        };
        var (status, reason) = run.Config.Rules.CalledPitchMode != CalledPitchMode.Automated
            ? (NotApplicable, $"판정 모드 {run.Config.Rules.CalledPitchMode?.ToString() ?? "미선언"}: #10은 Automated(ABS) 실행만 판정합니다(수치는 진단).")
            : counts["framing_non_neutral"] > 0 ? (Fail, $"Automated 실행에서 중립(1,000,000)이 아닌 Framing 계수 {counts["framing_non_neutral"]}건.")
            : counts["called_pitch_steps"] == 0 ? (NotEvaluated, "확정된 called_pitch 단계가 없어 확인할 대상이 없습니다.")
            : (Pass, "Automated 실행의 모든 확정 투구에서 비중립 Framing 계수 0건.");
        return new PzlCheck("pzl-10", "ABS mode gives zero Framing effect", status, reason, counts);
    }

    /// <param name="allowed">The stream set a run may draw from: <see cref="PinnedStreams"/> in production (a parameter so a test can show a presentation stream is refused).</param>
    internal static PzlCheck Clause12a(IReadOnlyList<PzlGameObservation> games, IReadOnlyList<string> allowed)
    {
        var drawn = games.SelectMany(o => o.DrawnStreams.Split(',', StringSplitOptions.RemoveEmptyEntries)).ToHashSet(StringComparer.Ordinal);
        var foreign = drawn.Where(s => !allowed.Contains(s, StringComparer.Ordinal)).Order(StringComparer.Ordinal).ToList();
        var nonGame = allowed.Count(s => !s.StartsWith("game/", StringComparison.Ordinal));
        var counts = new SortedDictionary<string, long>(StringComparer.Ordinal)
        {
            ["pinned_streams"] = allowed.Count, ["pinned_non_game_namespace"] = nonGame, ["drawn_streams"] = drawn.Count, ["drawn_unregistered"] = foreign.Count,
        };
        var (status, reason) = nonGame > 0 ? (Fail, $"고정 Stream 집합 [{string.Join(", ", allowed)}]에 game/* 밖의 Stream이 있습니다.")
            : foreign.Count > 0 ? (Fail, $"고정 집합 밖의 Stream을 소비했습니다: {string.Join(", ", foreign)}.")
            : drawn.Count == 0 ? (NotEvaluated, "소비된 Stream이 없습니다.")
            : (Pass, $"소비된 Stream [{string.Join(", ", drawn.Order(StringComparer.Ordinal))}]은 모두 고정된 game/* 7개 안이며 presentation Stream이 없습니다(등록 집합 = 고정 집합은 시험이 확인).");
        return new PzlCheck("pzl-12a", "gameplay RNG is not consumed by BCL/LMV — 12a current RNG boundary", status, reason, counts);
    }

    private static PzlCheck Clause12b() => new("pzl-12b", "gameplay RNG is not consumed by BCL/LMV — 12b real BCL/LMV features", Deferred,
        "BCL/LMV 기능 미구현: DEFERRED_TO_PHASE_5_6_28 (BCL Phase 5·6·28, LMV-A Phase 5·28). Phase 3에서 PASS로 기록하지 않음(ADR-008 FC-D8.2).", new SortedDictionary<string, long>());

    public static string Text(IReadOnlyList<PzlCheck> checks) =>
        "PZL checks (this run only; official clause PASS needs the F-C3 runs): " + string.Join("  ", checks.Select(c => $"{c.Id} {c.Status}"));
}
