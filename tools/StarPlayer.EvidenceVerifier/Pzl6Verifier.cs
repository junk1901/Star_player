using System.Collections;
using System.Reflection;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.SimHarness;
using StarPlayer.SimHarness.Evidence;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.EvidenceVerifier;

/// <summary>
/// The existing event-format-2 JSON writer and reader (Infrastructure <c>ScriptedGameJson.WriteEventsV2</c> / <c>ReadEventsV2</c>). Passed in because the Infrastructure friend list and public surface are
/// pinned trusted boundaries (TrustedBoundaryTests): binding the real codec into this tool's binary is an open owner decision (B1 report); the Infrastructure test assembly binds it today.
/// </summary>
public sealed record EventJsonCodec(Func<IReadOnlyList<GameEvent>, string> Write, Func<string, Result<IReadOnlyList<GameEvent>>> Read)
{
    /// <summary>The existing Infrastructure event-format-2 writer / reader through its single public entry point (OWNER decision 2): the verifier's default; tests may pass another to tamper.</summary>
    public static EventJsonCodec Infrastructure { get; } = new(StarPlayer.Infrastructure.Fixtures.EventJsonV2Codec.Write, StarPlayer.Infrastructure.Fixtures.EventJsonV2Codec.Read);
}

/// <summary>One fixed #6 sample: its unit, the approved run configuration and the plan game, and the technical game result of the Integration document it must reproduce.</summary>
public sealed record Pzl6Sample(string UnitId, RunConfig Config, GameSpec Spec, JObject DocumentGame);

/// <summary>
/// T05: official PZL #6 on the FIXED sample (B1-T00 §6): the first 10 plan games of <c>automated</c> then of <c>non-automated</c>, never replaced (a failed or failing game stays). Six separate steps per game:
/// (1) the original events through the v2 writer, (2) read back and compared event by event, (3) re-written and compared byte for byte, (4) replayed by the existing reducer and the final state compared
/// structurally with a field-inventory coverage check, (5) a second independent production run from the approved inputs and seed, (6) Event / RNG / game-result hashes compared with the document.
/// Every boolean is the verifier's own result; the submitted values are only compared with it.
/// </summary>
public static class Pzl6Verifier
{
    /// <summary>The GameState members the final-state comparison covers. A member added to GameState and not listed here fails the coverage check (§6 "필드가 빠지면 인증 거부").</summary>
    public static readonly IReadOnlySet<string> StateInventory = new HashSet<string>(StringComparer.Ordinal)
    {
        "GameId", "Rules", "Inning", "Half", "Outs", "Count", "Bases", "AwayRuns", "HomeRuns", "AwayLine", "HomeLine", "Stage", "Approach", "WalkChargeOverride", "Away", "Home", "Plays", "Stats",
        "Substitutions", "AppliedEvents", "NextRunnerKey", "Status", "Outcome", "Final", "Execution", "BattingSide", "FieldingSide", "Batting", "Fielding",
    };

    /// <summary>OWNER decision 3 §4: the official record and the receipt are pinned too, and the set of fact types (every one is compared structurally, but a new one must be noticed).</summary>
    private static readonly (Type Type, string[] Members)[] RecordInventory =
    [
        (typeof(FinalizedGame), ["GameId", "Outcome", "AwayRuns", "HomeRuns", "RulesProfileId", "RulesContentHash", "BoxScore", "Execution"]),
        (typeof(EventReceipt), ["EventId", "Kind", "FingerprintVersion", "Fingerprint"]),
    ];

    private static readonly string[] FactInventory = ["GameCompleted", "PlateAppearanceCompleted", "RunnerPlayCompleted", "SubstitutionMade"];

    private static string? Gap(Type type, IEnumerable<string> pinned)
    {
        var actual = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0).Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        actual.Remove("EqualityContract");
        var expected = pinned.ToHashSet(StringComparer.Ordinal);
        return actual.SetEquals(expected) ? null : $"{type.Name} 비교 인벤토리 불일치: 누락 [{string.Join(", ", actual.Except(expected))}], 초과 [{string.Join(", ", expected.Except(actual))}]";
    }

    /// <summary>The inventory gap (a GameState / FinalizedGame / EventReceipt member or a GameFact type not covered), or null. A gap refuses #6 (the final state is then never "equal").</summary>
    public static string? CoverageGap()
    {
        if (Gap(typeof(GameState), StateInventory) is { } state)
            return state;
        foreach (var (type, members) in RecordInventory)
            if (Gap(type, members) is { } g)
                return g;
        var facts = typeof(GameFact).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(GameFact)) && !t.IsAbstract).Select(t => t.Name).Order(StringComparer.Ordinal).ToList();
        return facts.SequenceEqual(FactInventory, StringComparer.Ordinal) ? null : $"GameFact 형식 인벤토리 불일치: [{string.Join(", ", facts)}]";
    }

    /// <summary>
    /// OWNER decision 3 §4: the final-state comparison of one sample — (i) the two reducer replays (original / v2 round trip) field by field, (ii) the round-trip replay against the GameState the
    /// PRODUCTION executor held at the end, (iii) the round-trip replay's facts against the facts of the production commits, (iv) every compared type's inventory. A missing production state is a failure.
    /// </summary>
    public static string? FinalDifference(GameState original, GameState roundTrip, GameState? production, IReadOnlyList<GameFact> originalFacts, IReadOnlyList<GameFact> roundTripFacts,
        IReadOnlyList<GameFact> productionFacts)
    {
        if (CoverageGap() is { } gap)
            return gap;
        if (production is null)
            return "production: Production 최종 상태를 얻지 못했습니다";
        return FirstDifference(original, roundTrip, "replay") ?? FirstDifference(production, roundTrip, "production")
            ?? FirstDifference(originalFacts, roundTripFacts, "replay_facts") ?? FirstDifference(productionFacts, roundTripFacts, "production_facts");
    }

    /// <summary>Structural equality: scalars by value, sequences element by element, other objects by their public properties (records holding immutable lists are not compared by reference).</summary>
    public static string? FirstDifference(object? a, object? b, string path = "state", int depth = 0)
    {
        if (ReferenceEquals(a, b))
            return null;
        if (a is null || b is null)
            return path;
        if (depth > 16)
            return Equals(a, b) ? null : path;
        var t = a.GetType();
        if (t != b.GetType())
            return path;
        if (t.IsPrimitive || t.IsEnum || a is string or decimal || (t.IsValueType && t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Length == 0))
            return Equals(a, b) ? null : path;
        if (a is IEnumerable ea && b is IEnumerable eb)
        {
            var la = ea.Cast<object?>().ToList();
            var lb = eb.Cast<object?>().ToList();
            if (la.Count != lb.Count)
                return $"{path}.Count";
            for (var i = 0; i < la.Count; i++)
                if (FirstDifference(la[i], lb[i], $"{path}[{i}]", depth + 1) is { } d)
                    return d;
            return null;
        }

        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0 && p.Name != "EqualityContract"))
            if (FirstDifference(p.GetValue(a), p.GetValue(b), $"{path}.{p.Name}", depth + 1) is { } d)
                return d;
        return null;
    }

    /// <summary>The reducer replay of an event list: its final state and its facts (each step's facts, plus the completion fact when it finalizes — as the commit gate records them).</summary>
    private static Result<(GameState State, IReadOnlyList<GameFact> Facts)> Replay(Pzl6Sample s, IEnumerable<GameEvent> events)
    {
        static Result<(GameState, IReadOnlyList<GameFact>)> Fail(Error e) => Result<(GameState, IReadOnlyList<GameFact>)>.Failure(e);
        var (away, home) = s.Config.Cohort.SetupFor(s.Spec.GameIndex);
        var start = GameReducer.Start(new GameStart(s.Spec.GameId, s.Config.Rules, away, home, s.Config.Execution));
        if (!start.IsSuccess)
            return Fail(start.Error!.Value);
        var state = start.Value;
        var facts = new List<GameFact>();
        foreach (var e in events)
        {
            var step = GameReducer.Apply(state, e);
            if (!step.IsSuccess)
                return Fail(step.Error!.Value);
            state = step.Value.State;
            facts.AddRange(step.Value.Facts);
        }

        if (state.Status != GameStatus.Final)
            return Result<(GameState, IReadOnlyList<GameFact>)>.Success((state, facts));
        var final = GameReducer.Finalize(state);                                          // a game-ending event is finalized once, as the commit gate does
        if (!final.IsSuccess)
            return Fail(final.Error!.Value);
        facts.Add(GameReducer.CompletedFact(final.Value.Game));
        return Result<(GameState, IReadOnlyList<GameFact>)>.Success((final.Value.State, facts));
    }

    private static string EventHash(IEnumerable<EventReceipt> receipts) =>
        Hashing.Sha256(string.Join('\n', receipts.Select(r => $"{r.EventId}|{r.Kind}|{r.FingerprintVersion}|{r.Fingerprint}")));

    /// <summary>One sample's RoundTripSample object (§6) and the observation of its first production run (for the #6 coverage).</summary>
    public static (JObject Sample, ObservedGame Observed) Check(Pzl6Sample s, EventJsonCodec codec)
    {
        var events = new List<GameEvent>();
        var productionFacts = new List<GameFact>();
        var observer = new PzlEventObserver();
        var played = GameExecutor.Play(s.Config, s.Spec, default, c =>
        {
            events.Add(c.Event);
            productionFacts.AddRange(c.Facts);                                                             // OWNER decision 3 §4: the facts each production commit recorded
        }, observer);                                                                                      // step 5a: production run from the approved inputs
        var second = GameExecutor.Play(s.Config, s.Spec);                                                   // step 5b: an independent second run
        var doc = s.DocumentGame;
        var seedReplay = (played.ResultHash, played.EventHash, played.RngHash, played.Status) == (second.ResultHash, second.EventHash, second.RngHash, second.Status)
            && played.ResultHash == Schema.Text(doc["result_hash"]) && played.EventHash == Schema.Text(doc["event_hash"])
            && (doc["rng_hash"] is JString r ? r.Value : "-") == played.RngHash;

        // steps 1-3
        bool sequence = false, locations = false, receipts = false, rewrite = false, finalState = false, eventHash = false;
        var json = codec.Write(events);
        var read = codec.Read(json);
        if (read.IsSuccess)
        {
            var back = read.Value;
            sequence = back.Count == events.Count && back.Zip(events).All(p => p.First.Id == p.Second.Id && EventFingerprint.KindName(p.First) == EventFingerprint.KindName(p.Second));
            locations = sequence && back.Zip(events).All(p => Equals(GameEventLocation.Of(p.First), GameEventLocation.Of(p.Second)));
            receipts = sequence && back.Zip(events).All(p => EventFingerprint.Receipt(p.First) == EventFingerprint.Receipt(p.Second));
            rewrite = codec.Write(back) == json;
            // step 4: the reducer replays both lists; their final states are structurally equal and the read-back one is the game the engine played
            var original = Replay(s, events);
            var again = Replay(s, back);
            if (original.IsSuccess && again.IsSuccess)
            {
                var st = again.Value.State;
                finalState = FinalDifference(original.Value.State, st, observer.FinalState, original.Value.Facts, again.Value.Facts, productionFacts) is null
                    && st.AppliedEvents.Count == played.CommittedEvents
                    && (st.Final?.Outcome, st.AwayRuns, st.HomeRuns, st.Inning, st.Half) == (played.Outcome, played.AwayRuns, played.HomeRuns, played.FinalInning, played.FinalHalf);
                eventHash = EventHash(st.AppliedEvents) == played.EventHash && played.EventHash == Schema.Text(doc["event_hash"]);
            }
        }

        var replayMatch = sequence && locations && receipts && rewrite && finalState && eventHash;
        var located = events.Count(e => GameEventLocation.Of(e) is not null);
        var ef2 = events.Count(e => EventFingerprint.Receipt(e).FingerprintVersion == EventFingerprint.VersionV2);
        var sample = J.O(
            ("unit_id", J.S(s.UnitId)), ("index", J.N(s.Spec.GameIndex)), ("game_id", J.S(s.Spec.GameId)), ("seed", J.S(EvidenceScalars.U64(s.Spec.Seed))),
            ("game_result_hash", J.S(played.ResultHash)), ("event_hash", J.S(played.EventHash)), ("rng_hash", J.S(played.RngHash)), ("event_format_version", J.N(2)),
            ("event_count", J.N(events.Count)), ("located_event_count", J.N(located)), ("ef1_receipts", J.N(events.Count - ef2)), ("ef2_receipts", J.N(ef2)),
            ("event_sequence_equal", J.B(sequence)), ("locations_equal", J.B(locations)), ("receipts_equal", J.B(receipts)), ("rewrite_bytes_equal", J.B(rewrite)),
            ("final_state_equal", J.B(finalState)), ("event_hash_equal", J.B(eventHash)), ("event_replay_match", J.B(replayMatch)), ("seed_replay_match", J.B(seedReplay)));
        return (sample, new ObservedGame(played, observer));
    }

    /// <summary>
    /// §21 PZL6 "+ 20경기 JSON bundle" (B1-T11-03): for each fixed sample, in sample order, the existing v2 Writer's UTF-8 output of the events of its production run — the bytes the archive must keep
    /// (one GAME_RESULTS file per sample; the file names are the producer's, B-4).
    /// </summary>
    public static IReadOnlyList<(string UnitId, string GameId, byte[] Json)> Bundle(IReadOnlyList<Pzl6Sample> samples, EventJsonCodec codec) => samples.Select(s =>
    {
        var events = new List<GameEvent>();
        GameExecutor.Play(s.Config, s.Spec, default, c => events.Add(c.Event));
        return (s.UnitId, s.Spec.GameId, System.Text.Encoding.UTF8.GetBytes(codec.Write(events)));
    }).ToList();

    /// <summary>The #6 document of an Integration document (§6) from the 20 fixed samples; check PASS only when every boolean of every sample holds.</summary>
    public static JObject Document(JObject integration, IReadOnlyList<Pzl6Sample> samples, EventJsonCodec codec, IReadOnlyList<JObject> artifacts)
    {
        if (samples.Count != 20 || samples.Select((s, i) => (s.UnitId, s.Spec.GameIndex) != (i < 10 ? "automated" : "non-automated", i % 10 + 1)).Any(x => x))
            throw EvidenceCodes.Fail(EvidenceCodes.PzlMismatch, "Evidence.pzl6_sample_fixed", "/samples", "pzl6", "#6 표본은 모드별 계획의 첫 10경기, automated → non-automated 순서로 고정입니다(교체 금지).");
        var results = samples.Select(s => Check(s, codec)).ToList();
        string[] flags = ["event_sequence_equal", "locations_equal", "receipts_equal", "rewrite_bytes_equal", "final_state_equal", "event_hash_equal", "event_replay_match", "seed_replay_match"];
        var bad = results.SelectMany(r => flags.Where(f => r.Sample[f] is JBool { Value: false }).Select(f => (Game: Schema.Text(r.Sample["game_id"]), Flag: f))).ToList();
        var vs = Schema.Text(((JObject)integration["execution"])["validation_set_id"]);
        var check = J.O(("id", J.S("pzl-6")), ("status", J.S(bad.Count == 0 ? "PASS" : "FAIL")),
            ("reason", J.S(bad.Count == 0 ? "고정 20경기 모두 v2 JSON 왕복·재기록 바이트·Reducer 재생 최종 상태·Event Hash·Seed 재실행이 일치합니다." : $"불일치 {bad.Count}건(표본 교체 없음).")),
            ("coverage", UnitPzlEvidence.Coverage(results.Select(r => r.Observed))),
            ("violations", J.A(UnitPzlEvidence.Sorted(bad.Select(b => ((string?)b.Game, (string?)null, (string?)b.Flag, "pzl6_mismatch"))))), ("test_evidence", JNode.Null), ("deferral", JNode.Null));
        return J.O(
            ("schema", OfficialDocuments.SchemaHeader(EvidenceKind.Pzl6)),
            ("execution", JEditLite.Set((JObject)integration["execution"], "evidence_id", J.S(EvidenceIds.Pzl6(vs)))),
            ("approvals", integration["approvals"]),
            ("plan", J.O(("integration_evidence_id", ((JObject)integration["execution"])["evidence_id"]), ("selection_rule", J.S("FIRST_10_PER_MODE")), ("sample_count_per_mode", J.N(10)))),
            ("samples", J.A(results.Select(r => (JNode)r.Sample))),
            ("check", check),
            ("additional_evidence", J.O(("pzl_unit_ids", J.A()), ("pzl6_id", JNode.Null), ("artifacts", J.A(artifacts.OrderBy(a => Schema.Text(a["artifact_id"]), StringComparer.Ordinal))))));
    }
}

/// <summary>A one-level member replacement on an immutable object.</summary>
internal static class JEditLite
{
    public static JObject Set(JObject o, string name, JNode value) => new(o.Members.Select(m => m.Key == name ? new KeyValuePair<string, JNode>(name, value) : m).ToList());
}
