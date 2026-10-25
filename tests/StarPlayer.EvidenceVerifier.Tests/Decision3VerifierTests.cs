using System.Text;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.SimHarness;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.EvidenceVerifier.Tests;

/// <summary>
/// B1 OWNER decision 3 through the verifier, on the same shared world as the T11 tests: §5 GAME_RESULTS (missing, duplicate, order, SHA, content), §7 the 20 #6 JSON files as PZL6 and their separation from
/// the Unit GAME_RESULTS, §4 the production state / fact comparison, §2 the RAW_INVENTORY of real receipts at issuance and at the Phase boundary.
/// </summary>
public sealed partial class T11CertificationTests
{
    private static List<JObject> Refs(JObject doc) => ((JArray)((JObject)doc["additional_evidence"])["artifacts"]).Items.Cast<JObject>().ToList();

    private static JObject WithRefs(JObject doc, IEnumerable<JObject> refs) =>
        (JObject)JEditT.Set(doc, "/additional_evidence/artifacts", J.A(refs.OrderBy(x => Schema.Text(x["artifact_id"]), StringComparer.Ordinal).Cast<JNode>().ToArray()));

    private byte[] Bytes(JObject reference) => Archive.TryRead(w.Archive, Schema.Text(reference["relative_path"]))!;

    private JObject NewFile(byte[] bytes, JObject like) => Archive.WriteNew(w.Archive, $"tampered/{Guid.NewGuid():N}/{Path.GetFileName(Schema.Text(like["relative_path"]))}", bytes,
        Schema.Text(like["artifact_id"]), Schema.Text(like["role"]), like["unit_id"] is JString u ? u.Value : null, like["game_id"] is JString g ? g.Value : null);

    private EvidenceError Verify(JObject reference) => Refused(() => Verifier.VerifyFamily(w.FamilyRequest("fc3-int", reference)));

    /// <summary>The integration document with its first Unit PZL's GAME_RESULTS reference replaced by <paramref name="edit"/> of the unit's references.</summary>
    private JObject WithUnitRefs(Func<JObject, List<JObject>, IEnumerable<JObject>> edit) => WithUnit(Integration().Document, u => WithRefs(u, edit(u, Refs(u))));

    // ---------------------------------------------------------------- §5 GAME_RESULTS through the verifier

    [Fact]
    public void Decision3_5_the_verifier_requires_one_game_results_per_unit_pzl_and_checks_its_bytes_order_and_content()
    {
        var (reference, _, _) = Integration();
        var v = Verifier.VerifyFamily(w.FamilyRequest("fc3-int", reference));
        Assert.All(v.PzlUnits, u => Assert.Single(Refs(u), x => Schema.Text(x["role"]) == "GAME_RESULTS"));
        Assert.Contains(v.SourceFiles, f => Schema.Text(f["role"]) == "GAME_RESULTS");                     // bound to the receipt's source list

        static JObject Results(List<JObject> refs) => refs.Single(x => Schema.Text(x["role"]) == "GAME_RESULTS");
        // missing / duplicate
        Assert.Equal("Archive.game_results_count", Verify(WithUnitRefs((_, refs) => refs.Where(x => Schema.Text(x["role"]) != "GAME_RESULTS"))).Cause);
        Assert.Equal("Archive.game_results_count", Verify(WithUnitRefs((_, refs) =>
            [.. refs, (JObject)JEditT.Set(NewFile(Bytes(Results(refs)), Results(refs)), "/artifact_id", J.S("game-results-extra"))])).Cause);
        // the file's bytes changed but the reference kept the old SHA
        Assert.Equal("Archive.raw_mismatch", Verify(WithUnitRefs((_, refs) =>
            refs.Select(x => ReferenceEquals(x, Results(refs)) ? (JObject)JEditT.Set(x, "/relative_path", JEditT.Get(NewFile([.. Bytes(x), (byte)'x'], x), "/relative_path")) : x))).Cause);
        // order: two games swapped (re-hashed reference)
        JObject Rewritten(List<JObject> refs, Func<List<string>, IEnumerable<string>> lines)
        {
            var r = Results(refs);
            var text = Encoding.UTF8.GetString(Bytes(r)).TrimEnd('\n').Split('\n').ToList();
            return NewFile(Encoding.UTF8.GetBytes(string.Concat(lines(text).Select(l => l + "\n"))), r);
        }

        Assert.Equal("GameResults.order", Verify(WithUnitRefs((_, refs) =>
            refs.Select(x => ReferenceEquals(x, Results(refs)) ? Rewritten(refs, t => [t[0], t[2], t[1], .. t.Skip(3)]) : x))).Cause);
        // content: one GameResult changed (well-formed, re-hashed) → the comparison with the parent row and the re-projection
        var e = Verify(WithUnitRefs((_, refs) => refs.Select(x => ReferenceEquals(x, Results(refs))
            ? Rewritten(refs, t => [t[0], Jcs.Text(JEditT.Set(StrictJson.Parse(Encoding.UTF8.GetBytes(t[1])), "/game/home_runs", J.N(99))), .. t.Skip(2)]) : x)));
        Assert.Equal((EvidenceCodes.ReplayMismatch, "GameResults.game_differs"), (e.Code, e.Cause));
    }

    // ---------------------------------------------------------------- §7 PZL6 JSON roles

    /// <summary>The integration document whose #6 document is re-written with <paramref name="edit"/> of its references.</summary>
    private JObject WithPzl6Refs(Func<List<JObject>, IEnumerable<JObject>> edit)
    {
        var doc = Integration().Document;
        var refs = Refs(doc);
        var pzl6Ref = refs.Single(x => Schema.Text(x["role"]) == "PZL6");
        var pzl6 = (JObject)StrictJson.Parse(Bytes(pzl6Ref));
        var newPzl6 = NewFile(Jcs.Bytes(WithRefs(pzl6, edit(Refs(pzl6)))), pzl6Ref);
        return EvidenceWriter.WriteDocument(w.Archive, WithRefs(doc, refs.Select(x => ReferenceEquals(x, pzl6Ref) ? newPzl6 : x)), "FAMILY_RESULT", $"tampered-{Guid.NewGuid():N}.json");
    }

    [Fact]
    public void Decision3_7_the_twenty_pzl6_json_files_are_pzl6_artifacts_and_never_counted_as_unit_game_results()
    {
        var (_, _, pzl6) = Integration();
        var json = Refs(pzl6).Where(x => Schema.Text(x["role"]) == "PZL6").ToList();
        Assert.Equal(20, json.Count);
        Assert.All(json, x => Assert.Matches("^pzl6-json-(automated|non-automated)-(0[1-9]|10)$", Schema.Text(x["artifact_id"])));
        Assert.All(json, x => Assert.IsType<JString>(x["game_id"]));

        // the old role, a missing file, a wrong id
        Assert.Equal("Archive.reverse_reference", Verify(WithPzl6Refs(refs => refs.Select(x => (JObject)JEditT.Set(x, "/role", J.S("GAME_RESULTS"))))).Cause);
        Assert.Equal("Archive.pzl6_json_count", Verify(WithPzl6Refs(refs => refs.Skip(1))).Cause);
        Assert.Equal("Archive.pzl6_json_count", Verify(WithPzl6Refs(refs => refs.Select((x, i) => i == 0 ? (JObject)JEditT.Set(x, "/artifact_id", J.S("pzl6-json-automated-1")) : x))).Cause);
        // one JSON's bytes differ from the existing Writer's output (re-hashed reference)
        Assert.Equal("Evidence.pzl6_bundle_differs", Verify(WithPzl6Refs(refs => refs.Select((x, i) => i == 0 ? NewFile([.. Bytes(x), (byte)' '], x) : x))).Cause);
        // a Unit GAME_RESULTS with a game id (what a #6 JSON looked like before) and a GAME_RESULTS named by the Family document itself
        Assert.Equal("Archive.game_results_count", Verify(WithUnitRefs((_, refs) => refs.Select(x =>
            Schema.Text(x["role"]) == "GAME_RESULTS" ? (JObject)JEditT.Set(x, "/game_id", JEditT.Get(json[0], "/game_id")) : x))).Cause);
        var doc = Integration().Document;
        var unitResults = Refs((JObject)StrictJson.Parse(Bytes(Refs(doc).First(x => Schema.Text(x["role"]) == "UNIT_PZL")))).Single(x => Schema.Text(x["role"]) == "GAME_RESULTS");
        Assert.Equal("Archive.reverse_reference", Verify(EvidenceWriter.WriteDocument(w.Archive, WithRefs(doc, [.. Refs(doc), unitResults]), "FAMILY_RESULT", $"tampered-{Guid.NewGuid():N}.json")).Cause);
    }

    // ---------------------------------------------------------------- §4 production state and facts

    [Fact]
    public void Decision3_4_the_final_state_comparison_covers_the_production_state_and_the_commit_facts()
    {
        var run = w.Integration.Units[0].Run;
        var facts = new List<GameFact>();
        var observer = new PzlEventObserver();
        GameExecutor.Play(run.Config, run.Specs[0], default, c => facts.AddRange(c.Facts), observer);
        var state = observer.FinalState!;
        Assert.Null(Pzl6Verifier.CoverageGap());
        Assert.Null(Pzl6Verifier.FinalDifference(state, state, state, facts, facts, facts));
        Assert.Contains(facts, f => f is GameCompleted);                                                  // the completion fact recorded at finalization
        // a production state differing only in a non-summary field (the box-score statistics), the missing production state, a missing or changed fact
        Assert.StartsWith("production.Stats", Pzl6Verifier.FinalDifference(state, state, state with { Stats = state.Stats.RemoveAt(0) }, facts, facts, facts));
        Assert.StartsWith("production.Final", Pzl6Verifier.FinalDifference(state, state, state with { Final = state.Final! with { RulesContentHash = new string('0', 64) } }, facts, facts, facts));
        Assert.StartsWith("production:", Pzl6Verifier.FinalDifference(state, state, null, facts, facts, facts));
        Assert.StartsWith("production_facts", Pzl6Verifier.FinalDifference(state, state, state, facts, facts, facts.Skip(1).ToList()));
        Assert.StartsWith("replay_facts", Pzl6Verifier.FinalDifference(state, state, state, facts.Take(facts.Count - 1).ToList(), facts, facts));
        // the real #6 of this world passes with the production comparison on
        Assert.Equal("PASS", Schema.Text(((JObject)Integration().Pzl6["check"])["status"]));
    }

    // ---------------------------------------------------------------- §2 RAW_INVENTORY of real receipts

    [Fact]
    public void Decision3_2_a_receipt_s_raw_inventory_is_its_original_source_list_at_issuance_and_at_the_phase_boundary()
    {
        var (o, cal, integration, calR, intR) = Certified();
        var receipt = Receipts.Validate(calR.Bytes);
        Receipts.CheckInventory(receipt, o.Production.ArchiveRoot);
        var sources = ((JArray)receipt["source_files"]).Items.Cast<JObject>().ToList();
        var invSource = sources.Single(f => Schema.Text(f["role"]) == "RAW_INVENTORY");
        static JObject Plain(JObject f) => new(f.Members.Where(m => m.Key != "origin").ToList());
        var listed = Archive.ReadInventory(Archive.TryRead(o.Production.ArchiveRoot, Schema.Text(invSource["relative_path"]))!).ToList();
        Assert.Equal(sources.Count(f => Schema.Text(f["origin"]) == "ORIGINAL"), listed.Count);

        JObject WithInventory(byte[] bytes, bool keepSha = false)
        {
            var file = Archive.WriteNew(o.Production.ArchiveRoot, $"tampered/{Guid.NewGuid():N}/raw-inventory.json", bytes, Schema.Text(invSource["artifact_id"]), "RAW_INVENTORY");
            var replaced = keepSha ? (JObject)JEditT.Set(invSource, "/relative_path", file["relative_path"]) : (JObject)JEditMembers.With(file, "origin", J.S("VERIFIER_DERIVED"));
            return (JObject)JEditT.Set(receipt, "/source_files", J.A(sources.Select(f => ReferenceEquals(f, invSource) ? replaced : f).Cast<JNode>().ToArray()));
        }

        string Check(JObject r) => Refused(() => Receipts.CheckInventory(r, o.Production.ArchiveRoot)).Cause;
        Assert.Equal("Receipt.inventory_mismatch", Check(WithInventory(Archive.Inventory(listed.Skip(1)))));                           // one original missing
        Assert.Equal("Receipt.inventory_mismatch", Check(WithInventory(Archive.Inventory([.. listed, Archive.Reference("zz-extra", "DIAGNOSTICS", null, null, "zz.json", [1])]))));   // one added
        Assert.Equal("Archive.raw_mismatch", Check(WithInventory(Archive.Inventory(listed.Skip(1)), keepSha: true)));                // the bytes are not the source file's SHA
        Assert.Equal("Inventory.order", Check(WithInventory(Jcs.Bytes(J.O(("artifacts", J.A([.. listed, listed[0]])), ("format", J.S(Archive.InventoryFormat))))))); // duplicate
        Assert.Equal("Receipt.inventory_missing", Check((JObject)JEditT.Set(receipt, "/source_files", J.A(sources.Where(f => !ReferenceEquals(f, invSource)).Cast<JNode>().ToArray()))));
        Assert.Equal("Receipt.inventory_missing", Check((JObject)JEditT.Set(receipt, "/source_files",
            J.A(sources.Select(f => ReferenceEquals(f, invSource) ? (JObject)JEditMembers.With(Plain(f), "origin", J.S("ORIGINAL")) : f).Cast<JNode>().ToArray()))));

        // at the Phase boundary: a registered child whose inventory leaves an original out is refused there
        var forged = (JObject)JEditT.Set(WithInventory(Archive.Inventory(listed.Skip(1))), "/receipt_id", J.S(Guid.NewGuid().ToString("D")));
        var child = Receipts.Issue(forged, o.Production.ArchiveRoot, o.Registry, () => null);
        var dom = Certification.VerifyFamily(o.Session, "fc3-dom", o.Production.Dominance().Reference);
        Assert.Equal("Receipt.inventory_mismatch", Refused(() => Certification.IssuePhase(o.Session, cal, integration, dom, [child, intR, intR with { ReceiptId = Guid.NewGuid().ToString("D") }],
            new VerifiedRegressionProof(new TestProfileRun(o.Session, new Dictionary<string, JObject>()), J.O()))).Cause);
    }
}
