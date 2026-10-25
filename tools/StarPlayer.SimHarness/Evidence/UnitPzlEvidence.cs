using System.Security.Cryptography;
using System.Text;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.SimHarness.Evidence;

/// <summary>The per-game observation of one unit: the executor's result and the observer that saw its commits (null observer = observation missing, never "zero events").</summary>
public sealed record ObservedGame(GameRunResult Result, PzlEventObserver? Observer);

/// <summary>
/// T04b: the per-event PZL evidence of one unit (B1-T00 §5, §7, §10): EventObservation rows, the per-game <c>events_digest</c> (§10.5), the Unit PZL document and its GAME_EVENTS bundle. Every executed game
/// is included — failed ones with their confirmed commits — and a missing observation is <c>Evidence.pzl_incomplete</c>, not an empty one. The four checks keep the existing <see cref="PzlChecks"/> verdicts and
/// reasons; their coverage follows §7 (NSC-6).
/// </summary>
public static class UnitPzlEvidence
{
    public const string BundleFormat = "simharness-game-events-v1";
    public const string EventsDigestPrefix = "STARPLAYER-APLUS-PZL-EVENTS-V1";
    public const int MaxRecordBytes = 1 << 20;

    private static EvidenceException Incomplete(string pointer, string message, string? game = null) =>
        new(new EvidenceError(EvidenceCodes.PzlIncomplete, "Evidence.observation_missing", pointer, "pzl_observation", message, Game: game));

    // ---------------------------------------------------------------- one event (§5.3-§5.4)

    public static JObject EventObservation(ObservedEvent e, string gameId)
    {
        var p = e.Pitch;
        var kind = EventFingerprint.KindName(e.Event);
        if ((kind is "pitch" or "take" or "in_play") != (p is not null))
            throw Incomplete($"/{e.Sequence}/pitch", $"{e.Event.Id}: 투구 사건에는 PreparedPitch 관찰이 있어야 하고, 그 밖의 사건에는 없어야 합니다.", gameId);
        return J.O(
            ("sequence", J.N(e.Sequence)), ("event_id", J.S(e.Event.Id)), ("kind", J.S(kind)), ("canonical_event", J.S(EventFingerprint.Canonical(e.Event))),
            ("receipt", J.O(("event_id", J.S(e.Receipt.EventId)), ("kind", J.S(e.Receipt.Kind)), ("fingerprint_version", J.S(e.Receipt.FingerprintVersion)), ("fingerprint", J.S(e.Receipt.Fingerprint)))),
            ("rng_before", J.S(e.RngBefore)), ("rng_after", J.S(e.RngAfter)), ("pitch", p is null ? JNode.Null : Pitch(p, e, gameId)));
    }

    private static JObject Pitch(ObservedPitch p, ObservedEvent e, string gameId)
    {
        if (p.CatcherId is null || p.PlayerInputSetId is null || p.InputReceivingFraming is null)
            throw Incomplete($"/{e.Sequence}/pitch/prepared", $"{e.Event.Id}: PreparedPitch의 포수·입력 집합·InputSet Framing 값이 관찰되지 않았습니다.", gameId);
        return J.O(
            ("branch_contract_version", J.S(p.ContractVersion)), ("branch_kind", J.S(p.BranchKind)), ("explicit_take", J.B(p.ExplicitTake)), ("intent", J.S(p.Intent)), ("target_zone", J.S(p.TargetZone)),
            ("location", J.O(("zone_model_version", J.S(p.ZoneModelVersion)), ("pitch_zone_id", J.S(p.PitchZoneId)), ("applied_batter_side", J.S(p.AppliedBatterSide)), ("boundary_class", J.S(p.BoundaryClass)))),
            ("prepared", J.O(("batter_id", J.S(p.BatterId)), ("pitcher_id", J.S(p.PitcherId)), ("catcher_id", J.S(p.CatcherId)), ("player_input_set_id", J.S(p.PlayerInputSetId)),
                ("input_receiving_framing", J.N(p.InputReceivingFraming.Value)), ("frozen_receiving_framing", J.Nn(p.FrozenReceivingFraming)), ("rules_profile_id", J.S(p.RulesProfileId)),
                ("rules_version", J.S(p.RulesVersion)), ("rules_hash", J.S(p.RulesHash)), ("probability_model_version", J.S(p.ProbabilityModelVersion)), ("tuning_version", J.S(p.TuningVersion)),
                ("tuning_hash", J.S(p.TuningHash)))),
            ("h1_rng_canonical", J.S(p.H1)),
            ("steps", J.A(p.Steps.Select(s => (JNode)J.O(("stage", J.S(s.Stage)), ("condition", J.S(s.Condition)), ("outcome", J.S(s.Outcome)), ("stream", J.S(s.Stream)), ("roll", J.N(s.Roll)),
                ("raw_draws", J.S(EvidenceScalars.D64(s.RawDraws))),
                ("framing_entries", J.A(s.Framing.Select(f => (JNode)J.O(("slot", J.S("catcher.framing")), ("factor_ppm", J.N(f.FactorPpm)), ("applied", J.B(f.Applied)))))))))),
            ("stream_usage", J.A(p.StreamUsage.Select(u => (JNode)J.O(("stream", J.S(u.Stream)), ("calls", J.N(u.Calls)), ("raw_draws", J.S(EvidenceScalars.D64(u.RawDraws))))))),
            ("next_bounded_calls", J.N(p.NextBoundedCalls)), ("runner_calls", J.N(p.RunnerCalls)), ("runner_raw_draws", J.S(EvidenceScalars.D64(p.RunnerRawDraws))));
    }

    /// <summary>§10.5: <c>"sha256:" + hex(SHA256(ASCII(prefix) || 0x0A || UTF8(JCS(EventObservation[]))))</c>, computed by streaming the array.</summary>
    public static string EventsDigest(IEnumerable<JObject> events)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.ASCII.GetBytes(EventsDigestPrefix + "\n"));
        using var buffer = new MemoryStream();
        Jcs.WriteArray(buffer, events);
        hash.AppendData(buffer.GetBuffer(), 0, (int)buffer.Length);
        return "sha256:" + Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    /// <summary>The EventObservation rows of one executed game; refuses a missing observer or a count that is not the game's committed events (§10.3).</summary>
    public static IReadOnlyList<JObject> Events(ObservedGame g)
    {
        var id = g.Result.Game.GameId;
        if (g.Observer is null)
            throw Incomplete($"/games/{g.Result.Game.GameIndex}", $"{id}: 경기 관찰이 없습니다(관찰 누락은 0건 관찰과 다릅니다).", id);
        if (g.Observer.Events.Count != g.Result.CommittedEvents)
            throw Incomplete($"/games/{g.Result.Game.GameIndex}", $"{id}: 관찰한 사건 {g.Observer.Events.Count}건이 확정 사건 {g.Result.CommittedEvents}건과 다릅니다.", id);
        return g.Observer.Events.Select(e => EventObservation(e, id)).ToList();
    }

    // ---------------------------------------------------------------- the Unit PZL document (§5, §7)

    public static JObject PzlGame(ObservedGame g, string eventsDigest)
    {
        var r = g.Result;
        var o = r.Pzl ?? throw Incomplete($"/games/{r.Game.GameIndex}/summary", $"{r.Game.GameId}: PZL 관찰 요약이 없습니다(진단용 None 대체는 공식 검증에 쓰지 않음).", r.Game.GameId);
        var started = r.RngHash != "-";
        if (started != (g.Observer?.FinalRng is not null))
            throw Incomplete($"/games/{r.Game.GameIndex}/final_rng_canonical", $"{r.Game.GameId}: RNG 최종 상태 관찰이 경기 시작 여부와 맞지 않습니다.", r.Game.GameId);
        return J.O(
            ("index", J.N(r.Game.GameIndex)), ("game_id", J.S(r.Game.GameId)), ("seed", J.S(EvidenceScalars.U64(r.Game.Seed))), ("status", J.S(r.Status.ToString())),
            ("game_result_hash", J.S(r.ResultHash)), ("event_hash", J.S(r.EventHash)), ("rng_hash", J.Sn(started ? r.RngHash : null)), ("committed_events", J.N(r.CommittedEvents)),
            ("summary", J.O(
                ("events", J.N(o.Events)), ("located_events", J.N(o.LocatedEvents)), ("ef2_receipts", J.N(o.Ef2Receipts)), ("version_mismatches", J.N(o.VersionMismatches)),
                ("fingerprint_mismatches", J.N(o.FingerprintMismatches)), ("foreign_event_ids", J.N(o.ForeignEventIds)), ("duplicate_event_ids", J.N(o.DuplicateEventIds)),
                ("event_id_collisions", J.N(o.EventIdCollisions)), ("receipt_log_mismatches", J.N(o.ReceiptLogMismatches)), ("called_pitch_steps", J.N(o.CalledPitchSteps)),
                ("framing_inputs", J.N(o.FramingInputs)), ("framing_entries", J.N(o.FramingEntries)), ("framing_non_neutral", J.N(o.FramingNonNeutral)),
                ("drawn_streams", J.A(o.DrawnStreams.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => (JNode)J.S(s)))))),
            ("events_digest", J.S(eventsDigest)), ("final_rng_canonical", J.Sn(started ? g.Observer!.FinalRng : null)), ("observation_present", J.B(true)));
    }

    /// <summary>§7 coverage of a set of observed games (NSC-6): Completed games, every confirmed event (failed games included), pitch-carrying events and observed called-pitch steps.</summary>
    public static JObject Coverage(IEnumerable<ObservedGame> games)
    {
        long completed = 0, events = 0, pitches = 0, called = 0;
        foreach (var g in games)
        {
            if (g.Result.Completed)
                completed++;
            foreach (var e in g.Observer?.Events ?? [])
            {
                events++;
                if (e.Pitch is { } p)
                {
                    pitches++;
                    called += p.Steps.Count(s => s.Stage == StageIds.CalledPitch);
                }
            }
        }

        return J.O(("completed_games", J.N(completed)), ("events", J.N(events)), ("pitches", J.N(pitches)), ("called_pitch_steps", J.N(called)));
    }

    public static JObject ZeroCoverage() => J.O(("completed_games", J.N(0)), ("events", J.N(0)), ("pitches", J.N(0)), ("called_pitch_steps", J.N(0)));

    /// <summary>The four Unit PZL checks: status and reason from <see cref="PzlChecks.Evaluate"/> (unchanged), violations per game from the same counts, coverage over every executed game.</summary>
    public static IReadOnlyList<JObject> Checks(RunResult run, IReadOnlyList<ObservedGame> games)
    {
        var existing = PzlChecks.Evaluate(run);
        var coverage = Coverage(games);
        static (string? G, string? E, string? F, string C) V(string code, string? game, string? field = null) => (game, null, field, code);
        var violations = new Dictionary<string, List<(string? G, string? E, string? F, string C)>>(StringComparer.Ordinal)
        {
            ["pzl-5"] = [], ["pzl-10"] = [], ["pzl-12a"] = [], ["pzl-12b"] = [],
        };
        string[] five = ["version_mismatches", "fingerprint_mismatches", "foreign_event_ids", "duplicate_event_ids", "event_id_collisions", "receipt_log_mismatches"];
        foreach (var g in run.Games)
        {
            var o = g.Pzl!;
            long[] counts = [o.VersionMismatches, o.FingerprintMismatches, o.ForeignEventIds, o.DuplicateEventIds, o.EventIdCollisions, o.ReceiptLogMismatches];
            violations["pzl-5"].AddRange(five.Zip(counts).Where(p => p.Second != 0).Select(p => V(p.First, g.Game.GameId)));
            if (o.FramingNonNeutral != 0 && run.Config.Rules.CalledPitchMode == CalledPitchMode.Automated)
                violations["pzl-10"].Add(V("framing_non_neutral", g.Game.GameId));
            violations["pzl-12a"].AddRange(o.DrawnStreams.Split(',', StringSplitOptions.RemoveEmptyEntries).Where(s => !PzlChecks.PinnedStreams.Contains(s, StringComparer.Ordinal))
                .Select(s => V("drawn_unregistered", g.Game.GameId, s)));
        }

        foreach (var dup in run.Games.GroupBy(g => g.Game.GameId, StringComparer.Ordinal).Where(x => x.Count() > 1))
            violations["pzl-5"].Add(V("duplicate_game_ids", dup.Key));

        return existing.Select(c => J.O(
            ("id", J.S(c.Id)), ("status", J.S(c.Status)), ("reason", J.S(c.Reason)), ("coverage", c.Id == "pzl-12b" ? ZeroCoverage() : coverage),
            ("violations", J.A(Sorted(violations[c.Id]))), ("test_evidence", JNode.Null),
            ("deferral", c.Id == "pzl-12b" ? Deferral() : JNode.Null))).ToList();
    }

    public static JObject Deferral() => J.O(("code", J.S("DEFERRED_TO_PHASE_5_6_28")), ("bcl_phases", J.A(J.N(5), J.N(6), J.N(28))), ("lmv_a_phases", J.A(J.N(5), J.N(28))));

    /// <summary>Violations in (game_id, event_id, field, code) ordinal order with nulls first, duplicates removed (§7).</summary>
    public static IEnumerable<JNode> Sorted(IEnumerable<(string? G, string? E, string? F, string C)> v)
    {
        static int Cmp(string? a, string? b) => a is null ? (b is null ? 0 : -1) : b is null ? 1 : string.CompareOrdinal(a, b);
        var list = v.Distinct().ToList();
        list.Sort((x, y) => Cmp(x.G, y.G) is var c and not 0 ? c : Cmp(x.E, y.E) is var d and not 0 ? d : Cmp(x.F, y.F) is var f and not 0 ? f : string.CompareOrdinal(x.C, y.C));
        return list.Select(x => (JNode)J.O(("code", J.S(x.C)), ("game_id", J.Sn(x.G)), ("event_id", J.Sn(x.E)), ("field", J.Sn(x.F))));
    }

    /// <summary>The Unit PZL document of one executed unit (artifacts: the references of its own GAME_EVENTS bundle etc., written before it, §21).</summary>
    public static JObject Document(EvidenceContext ctx, string family, string unitId, string gitSha, RunResult run, IReadOnlyList<ObservedGame> games, IReadOnlyList<JObject> artifacts)
    {
        if (games.Count != run.Games.Count || games.Zip(run.Games).Any(p => !ReferenceEquals(p.First.Result, p.Second)))
            throw Incomplete("/games", "관찰한 경기 목록이 실행된 모든 경기(부모 실행 순서)와 다릅니다.");
        var vs = ctx.ValidationSetId;
        var rows = games.Select(g => PzlGame(g, EventsDigest(Events(g)))).ToList();
        return J.O(
            ("schema", OfficialDocuments.SchemaHeader(EvidenceKind.UnitPzl)),
            ("execution", OfficialDocuments.Execution(EvidenceIds.UnitPzl(vs, family, unitId), ctx, run.Config.RunId, family, gitSha)),
            ("approvals", ctx.Approvals()),
            ("plan", J.O(("unit_id", J.S(unitId)), ("config_hash", J.S(run.ConfigHash)), ("run_result_version", J.Sn(run.ResultVersion)),
                ("run_result_hash", J.Sn(run.ResultVersion is null ? null : run.RunResultHash)))),
            ("registered_streams", J.A(PzlChecks.PinnedStreams.Select(s => (JNode)J.S(s)))),
            ("games", J.A(rows)),
            ("checks", J.A(Checks(run, games))),
            ("additional_evidence", J.O(("pzl_unit_ids", J.A()), ("pzl6_id", JNode.Null), ("artifacts", J.A(artifacts.OrderBy(a => Schema.Text(a["artifact_id"]), StringComparer.Ordinal))))));
    }

    // ---------------------------------------------------------------- GAME_EVENTS bundle (§10)

    public static JObject BundleHeader(EvidenceContext ctx, string family, string runId, string unitId) => J.O(
        ("bundle", J.S(BundleFormat)), ("contracts", (JObject)OfficialDocuments.SchemaHeader(EvidenceKind.UnitPzl)["contracts"]), ("family", J.S(family)), ("run_id", J.S(runId)),
        ("unit_evidence_id", J.S(EvidenceIds.UnitPzl(ctx.ValidationSetId, family, unitId))), ("unit_id", J.S(unitId)), ("validation_set_id", J.S(ctx.ValidationSetId)));

    public static JObject BundleGame(GameRunResult r, string digest) => J.O(("committed_events", J.N(r.CommittedEvents)), ("events_digest", J.S(digest)), ("game_id", J.S(r.Game.GameId)),
        ("index", J.N(r.Game.GameIndex)), ("record", J.S("game")), ("seed", J.S(EvidenceScalars.U64(r.Game.Seed))), ("status", J.S(r.Status.ToString())));

    /// <summary>Writes the bundle (§10.1-§10.2): records are <c>JCS(object) || 0x0A</c>, ≤ 1 MiB each, header, then per game its record and exactly its events, then the end record.</summary>
    public static void WriteBundle(Stream output, EvidenceContext ctx, string family, string runId, string unitId, IReadOnlyList<ObservedGame> games)
    {
        void Record(JNode n)
        {
            var bytes = Jcs.Bytes(n);
            if (bytes.Length > MaxRecordBytes)
                throw new EvidenceException(new EvidenceError(EvidenceCodes.ArchiveInvalid, "Bundle.record_too_large", "", "bundle", $"레코드 {bytes.Length}바이트가 1 MiB를 넘습니다."));
            output.Write(bytes);
            output.WriteByte((byte)'\n');
        }

        Record(BundleHeader(ctx, family, runId, unitId));
        long total = 0;
        foreach (var g in games)
        {
            var events = Events(g);
            Record(BundleGame(g.Result, EventsDigest(events)));
            foreach (var e in events)
                Record(e);
            total += events.Count;
        }

        Record(J.O(("executed_games", J.N(games.Count)), ("record", J.S("end")), ("total_events", J.N(total))));
    }

    public static byte[] Bundle(EvidenceContext ctx, string family, string runId, string unitId, IReadOnlyList<ObservedGame> games)
    {
        using var stream = new MemoryStream();
        WriteBundle(stream, ctx, family, runId, unitId, games);
        return stream.ToArray();
    }

    /// <summary>The parsed records of a bundle read from ONE snapshot of its bytes: the header, per game the game record with its raw event lines, and the end record.</summary>
    public sealed record ParsedBundle(JObject Header, IReadOnlyList<(JObject Game, IReadOnlyList<byte[]> EventLines)> Games, JObject End);

    /// <summary>
    /// The format half of §10.6: line framing (LF only, no CR / BOM / empty line, ends with LF, ≤ 1 MiB per record), every record strictly parsed against its closed shape and already in JCS form, the
    /// record order of §10.2, consecutive game indexes and sequences, and the counts. Format violations are <c>archive_invalid</c>; the comparison with a re-observation is <see cref="CompareGame"/>.
    /// </summary>
    /// <summary>§10.1 framing shared by the JSON-lines bundles: LF only, no CR / BOM / empty line, ends with LF, ≤ 1 MiB per record. Violations are <c>archive_invalid</c> with <paramref name="prefix"/> causes.</summary>
    internal static List<byte[]> Records(ReadOnlySpan<byte> raw, string prefix)
    {
        EvidenceException Bad(string cause, string message, int line) => new(new EvidenceError(EvidenceCodes.ArchiveInvalid, $"{prefix}.{cause}", $"/line/{line}", "bundle", message));
        if (raw.Length == 0 || raw[^1] != (byte)'\n')
            throw Bad("framing", "번들의 마지막 바이트는 LF여야 합니다.", 0);
        if (raw.IndexOf((byte)'\r') >= 0)
            throw Bad("framing", "번들에 CR이 있습니다.", 0);
        var lines = new List<byte[]>();
        var rest = raw[..^1];
        while (true)
        {
            var at = rest.IndexOf((byte)'\n');
            var line = at < 0 ? rest : rest[..at];
            if (line.Length == 0)
                throw Bad("framing", "빈 행이 있습니다.", lines.Count + 1);
            if (line.Length > MaxRecordBytes)
                throw Bad("record_too_large", "레코드가 1 MiB를 넘습니다.", lines.Count + 1);
            lines.Add(line.ToArray());
            if (at < 0)
                break;
            rest = rest[(at + 1)..];
        }

        return lines;
    }

    /// <summary>One record strictly parsed against its closed shape and already in JCS form (a BOM fails the strict reader).</summary>
    internal static JObject Record(byte[] line, Shape shape, int lineNumber, string prefix)
    {
        EvidenceException Bad(string cause, string message) => new(new EvidenceError(EvidenceCodes.ArchiveInvalid, $"{prefix}.{cause}", $"/line/{lineNumber}", "bundle", message));
        JNode n;
        try
        {
            n = StrictJson.Parse(line, "bundle");
            shape(n, "");
        }
        catch (EvidenceException e)
        {
            throw Bad("record_invalid", $"{e.Error.Cause}: {e.Error.Message}");
        }

        if (!Jcs.Bytes(n).AsSpan().SequenceEqual(line))
            throw Bad("not_canonical", "레코드가 JCS 바이트가 아닙니다.");
        return (JObject)n;
    }

    public static ParsedBundle ReadBundle(ReadOnlySpan<byte> raw)
    {
        static EvidenceException Bad(string cause, string message, int line) => new(new EvidenceError(EvidenceCodes.ArchiveInvalid, cause, $"/line/{line}", "bundle", message));
        var lines = Records(raw, "Bundle");
        JObject Parse(int i, Shape shape) => Record(lines[i], shape, i + 1, "Bundle");

        var header = Parse(0, EvidenceShapes.BundleHeader);
        var games = new List<(JObject, IReadOnlyList<byte[]>)>();
        var i = 1;
        long total = 0;
        while (i < lines.Count - 1)
        {
            var game = Parse(i, EvidenceShapes.BundleGame);
            var n = (int)Schema.Long(game["committed_events"]);
            if (i + n > lines.Count - 2)                                                        // lines i+1..i+n are its events; the last line is the end record
                throw Bad("Bundle.count", "Game 레코드의 committed_events만큼 사건 행이 없습니다.", i + 1);
            var events = new List<byte[]>();
            for (var k = 1; k <= n; k++)
            {
                Parse(i + k, EvidenceShapes.EventObservation);                                    // index / sequence order and identity are compared with the re-execution (pzl_mismatch, N17)
                events.Add(lines[i + k]);
            }

            games.Add((game, events));
            total += n;
            i += n + 1;
        }

        if (i != lines.Count - 1)
            throw Bad("Bundle.count", "End 레코드가 마지막 행이 아닙니다.", i + 1);
        var end = Parse(i, EvidenceShapes.BundleEnd);
        if (Schema.Long(end["executed_games"]) != games.Count || Schema.Long(end["total_events"]) != total)
            throw Bad("Bundle.count", "End 레코드의 executed_games·total_events가 실제 레코드 수와 다릅니다.", i + 1);
        return new ParsedBundle(header, games, end);
    }

    /// <summary>
    /// §10.6 (a)-(c) for ONE game, after it was re-executed: every re-observed event's JCS equals the bundle's next event line byte for byte, the re-observed digest equals the bundle's and the Unit PZL
    /// document's, and the game record matches. A shortfall is <c>pzl_incomplete</c>, any difference <c>pzl_mismatch</c>.
    /// </summary>
    public static EvidenceError? CompareGame((JObject Game, IReadOnlyList<byte[]> EventLines) bundled, JObject documentGame, ObservedGame again)
    {
        var id = again.Result.Game.GameId;
        EvidenceError Mismatch(string cause, string message) => new(EvidenceCodes.PzlMismatch, cause, $"/games/{again.Result.Game.GameIndex}", "pzl_reobservation", message, Game: id);
        if (Schema.Text(bundled.Game["game_id"]) != id || Schema.Long(bundled.Game["index"]) != again.Result.Game.GameIndex
            || Schema.Text(bundled.Game["seed"]) != EvidenceScalars.U64(again.Result.Game.Seed))
            return Mismatch("Evidence.game_identity_mismatch", $"{id}: Game 레코드의 index·game_id·seed가 승인 계획의 경기가 아닙니다(A07).");
        IReadOnlyList<JObject> events;
        try
        {
            events = Events(again);
        }
        catch (EvidenceException e)
        {
            return e.Error;
        }

        if (bundled.EventLines.Count < events.Count)
            return new EvidenceError(EvidenceCodes.PzlIncomplete, "Evidence.bundle_events_missing", $"/games/{again.Result.Game.GameIndex}", "pzl_reobservation",
                $"{id}: 번들 사건 행 {bundled.EventLines.Count}개가 재관찰 {events.Count}건보다 적습니다.", Game: id);
        if (bundled.EventLines.Count > events.Count)
            return Mismatch("Evidence.bundle_events_extra", $"{id}: 번들 사건 행이 재관찰보다 많습니다.");
        for (var i = 0; i < bundled.EventLines.Count; i++)
        {
            var row = (JObject)StrictJson.Parse(bundled.EventLines[i], "bundle");
            if (Schema.Text(row["kind"]) is "pitch" or "take" or "in_play" && row["pitch"] is JNull)
                return new EvidenceError(EvidenceCodes.PzlIncomplete, "Evidence.prepared_pitch_missing", $"/games/{again.Result.Game.GameIndex}/events/{i}/pitch", "pzl_reobservation",
                    $"{id}: {i + 1}번째 투구 사건의 PreparedPitch 관찰이 없습니다(A08).", Game: id);
            if (Schema.Long(row["sequence"]) != i + 1)
                return Mismatch("Evidence.event_sequence", $"{id}: {i + 1}번째 사건 행의 sequence가 {Schema.Long(row["sequence"])}입니다(순서·누락·중복, N17).");
        }

        for (var i = 0; i < events.Count; i++)
            if (!Jcs.Bytes(events[i]).AsSpan().SequenceEqual(bundled.EventLines[i]))
                return Mismatch("Evidence.event_differs", $"{id}: {i + 1}번째 사건 행이 재관찰과 바이트가 다릅니다.");
        var digest = EventsDigest(events);
        if (Schema.Text(bundled.Game["events_digest"]) != digest || Schema.Text(documentGame["events_digest"]) != digest)
            return Mismatch("Evidence.events_digest_mismatch", $"{id}: events_digest가 재관찰 값과 다릅니다(N14).");
        if (!Jcs.Bytes(bundled.Game).AsSpan().SequenceEqual(Jcs.Bytes(BundleGame(again.Result, digest))))
            return Mismatch("Evidence.game_record_mismatch", $"{id}: Game 레코드가 재실행 결과와 다릅니다.");
        return Jcs.Bytes(documentGame).AsSpan().SequenceEqual(Jcs.Bytes(PzlGame(again, digest)))
            ? null
            : Mismatch("Evidence.pzl_game_mismatch", $"{id}: Unit PZL games[]가 재관찰 결과와 다릅니다.");
    }
}
