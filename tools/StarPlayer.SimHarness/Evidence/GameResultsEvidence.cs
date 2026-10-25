namespace StarPlayer.SimHarness.Evidence;

/// <summary>
/// OWNER decision 3 §5: the §21 GAME_RESULTS bundle <c>simharness-game-results-v1</c> — one per Unit, referenced by that Unit's Unit PZL document (R-13: Family / Dominance → Unit PZL → bundle).
/// Records <c>JCS(object) || 0x0A</c> (the §10.1 framing of GAME_EVENTS): Header, one <c>{game, record}</c> per executed game in parent order (failed games included, never replaced) whose <c>game</c> is the
/// §3.6 GameResult exactly as the parent Family / Dominance document carries it, then End. Its own role and file: never mixed with GAME_EVENTS.
/// </summary>
public static class GameResultsEvidence
{
    public const string Format = "simharness-game-results-v1";

    public static JObject Header(EvidenceContext ctx, string family, string runId, string unitId) => J.O(
        ("bundle", J.S(Format)), ("contracts", (JObject)OfficialDocuments.SchemaHeader(EvidenceKind.UnitPzl)["contracts"]), ("family", J.S(family)), ("run_id", J.S(runId)),
        ("unit_evidence_id", J.S(EvidenceIds.UnitPzl(ctx.ValidationSetId, family, unitId))), ("unit_id", J.S(unitId)), ("validation_set_id", J.S(ctx.ValidationSetId)));

    /// <summary>The bundle of one unit's run: every executed game's GameResult as <see cref="OfficialDocuments.Game"/> projects it into the parent document.</summary>
    public static byte[] Bundle(EvidenceContext ctx, string family, string unitId, RunResult run)
    {
        var dominance = family == DominanceMatrix.Family;
        using var output = new MemoryStream();
        void Write(JNode n)
        {
            var bytes = Jcs.Bytes(n);
            if (bytes.Length > UnitPzlEvidence.MaxRecordBytes)
                throw new EvidenceException(new EvidenceError(EvidenceCodes.ArchiveInvalid, "GameResults.record_too_large", "", "bundle", $"레코드 {bytes.Length}바이트가 1 MiB를 넘습니다."));
            output.Write(bytes);
            output.WriteByte((byte)'\n');
        }

        Write(Header(ctx, family, run.Config.RunId, unitId));
        foreach (var g in run.Games)
            Write(J.O(("game", OfficialDocuments.Game(g, dominance)), ("record", J.S("game"))));
        Write(J.O(("completed_games", J.N(run.CompletedCount)), ("executed_games", J.N(run.Games.Count)), ("failed_games", J.N(run.FailedCount)), ("record", J.S("end"))));
        return output.ToArray();
    }

    public sealed record Parsed(JObject Header, IReadOnlyList<JObject> Games, JObject End);

    /// <summary>The format half: framing, closed shapes, JCS form, record order (Header, Games, End), consecutive <c>game.index</c> 1..n and End.executed_games = n. Violations are <c>archive_invalid</c>.</summary>
    public static Parsed Read(ReadOnlySpan<byte> raw)
    {
        static EvidenceException Bad(string cause, string message, int line) => new(new EvidenceError(EvidenceCodes.ArchiveInvalid, cause, $"/line/{line}", "bundle", message));
        var lines = UnitPzlEvidence.Records(raw, "GameResults");
        if (lines.Count < 2)
            throw Bad("GameResults.count", "Header와 End 레코드가 있어야 합니다.", lines.Count);
        var header = UnitPzlEvidence.Record(lines[0], EvidenceShapes.GameResultsHeader, 1, "GameResults");
        var games = new List<JObject>();
        for (var i = 1; i < lines.Count - 1; i++)
        {
            var g = UnitPzlEvidence.Record(lines[i], EvidenceShapes.GameResultsGame, i + 1, "GameResults");
            if (Schema.Long(((JObject)g["game"])["index"]) != i)
                throw Bad("GameResults.order", $"{i}번째 Game 레코드의 index가 {i}가 아닙니다(순서·누락·교체).", i + 1);
            games.Add((JObject)g["game"]);
        }

        var end = UnitPzlEvidence.Record(lines[^1], EvidenceShapes.GameResultsEnd, lines.Count, "GameResults");
        if (Schema.Long(end["executed_games"]) != games.Count)
            throw Bad("GameResults.count", "End.executed_games가 Game 레코드 수와 다릅니다.", lines.Count);
        return new Parsed(header, games, end);
    }

    /// <summary>
    /// The content half for ONE unit, after the replay: the header bound to its Unit PZL (S3/S5/S6, <c>reference_invalid</c>), every <c>game</c> byte-identical to the parent document's row and to
    /// <paramref name="expectedRows"/> (the verifier's re-projection), its result hash the Unit PZL's <c>game_result_hash</c>, and End's counts the parent Technical's (<c>replay_mismatch</c>).
    /// </summary>
    public static EvidenceError? Compare(Parsed bundle, JObject unitDoc, JObject parentTechnical, IReadOnlyList<JObject> expectedRows)
    {
        var unit = Schema.Text(((JObject)unitDoc["plan"])["unit_id"]);
        EvidenceError Err(string code, string cause, string message) => new(code, cause, "", "game_results", message, Unit: unit);
        var h = bundle.Header;
        var ex = (JObject)unitDoc["execution"];
        if (!Jcs.Bytes(h["contracts"]).AsSpan().SequenceEqual(Jcs.Bytes(((JObject)unitDoc["schema"])["contracts"])) || Schema.Text(h["family"]) != Schema.Text(ex["family"])
            || Schema.Text(h["run_id"]) != Schema.Text(ex["run_id"]) || Schema.Text(h["validation_set_id"]) != Schema.Text(ex["validation_set_id"]) || Schema.Text(h["unit_id"]) != unit
            || Schema.Text(h["unit_evidence_id"]) != Schema.Text(ex["evidence_id"]))
            return Err(EvidenceCodes.ReferenceInvalid, "GameResults.header", "GAME_RESULTS Header가 Unit PZL 문서와 다릅니다.");
        var parentRows = Schema.Items(parentTechnical["games"]).Select(Schema.Obj).ToList();
        var pzlGames = Schema.Items(unitDoc["games"]).Select(Schema.Obj).ToList();
        if (bundle.Games.Count != expectedRows.Count || bundle.Games.Count != parentRows.Count || bundle.Games.Count != pzlGames.Count)
            return Err(EvidenceCodes.ReplayMismatch, "GameResults.count", $"GAME_RESULTS 경기 {bundle.Games.Count}개가 재실행·부모 문서·Unit PZL의 실행 경기 수와 다릅니다.");
        for (var k = 0; k < bundle.Games.Count; k++)
        {
            var row = Jcs.Bytes(bundle.Games[k]);
            if (!row.AsSpan().SequenceEqual(Jcs.Bytes(parentRows[k])) || !row.AsSpan().SequenceEqual(Jcs.Bytes(expectedRows[k]))
                || Schema.Text(bundle.Games[k]["result_hash"]) != Schema.Text(pzlGames[k]["game_result_hash"]))
                return Err(EvidenceCodes.ReplayMismatch, "GameResults.game_differs", $"{k + 1}번째 GameResult가 부모 문서·재실행 투영·Unit PZL과 다릅니다.");
        }

        var e = bundle.End;
        return Schema.Long(e["completed_games"]) != Schema.Long(parentTechnical["games_completed"]) || Schema.Long(e["failed_games"]) != Schema.Long(parentTechnical["games_failed"])
               || Schema.Long(e["executed_games"]) != Schema.Long(parentTechnical["games_executed"])
            ? Err(EvidenceCodes.ReplayMismatch, "GameResults.end", "End 집계가 부모 Technical과 다릅니다.")
            : null;
    }
}
