using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.SimHarness.Tests.Evidence;

/// <summary>
/// B1-T07: M(D) removes only the artifact references, the family payload binds every Unit PZL / #6 child to its parent, the A+ is the prefixed SHA of the JCS payload, and the Phase root refuses mixed
/// sets (A01, A02), reused run ids and coverage outside the §7 scope (A10-a, A25-a, A25-b) without a "coverage 0" rule (N36-b).
/// </summary>
public sealed class EvidenceHashTests(FamilyOfficialWorld fam) : IClassFixture<FamilyOfficialWorld>
{
    private static readonly EvidenceContext Ctx = OfficialDocumentTests.Ctx;

    /// <summary>Re-plays an executed unit with observers (same config and plan, so the same result) and projects its Unit PZL document.</summary>
    internal static JObject PzlDoc(FamilyUnitRun u, string family, EvidenceContext ctx)
    {
        var observers = new Dictionary<int, PzlEventObserver>();
        var run = SimRunner.RunSpecs(u.Run.Config, u.Run.Specs, play: s => GameExecutor.Play(u.Run.Config, s, default, null, observers[s.GameIndex] = new PzlEventObserver()));
        Assert.Equal(u.Run.RunResultHash, run.RunResultHash);
        return UnitPzlEvidence.Document(ctx, family, u.Plan.Pinned.Unit.Id, u.Plan.Report.Attestation!.GitCommitSha, run,
            run.Games.Select(g => new ObservedGame(g, observers[g.Game.GameIndex])).ToList(), []);
    }

    /// <summary>A schema-valid #6 document over the integration plan's first 10 games per mode (all checks true; the real T05 verification lives in the verifier).</summary>
    internal static JObject Pzl6Doc(JObject integration)
    {
        var units = Schema.Items(integration["units"]).Select(Schema.Obj).ToList();
        var samples = Enumerable.Range(0, 20).Select(i =>
        {
            var unit = units[i < 10 ? 0 : 1];
            var game = (JObject)JEdit.Get(unit, $"/technical/games/{i % 10}");
            return (JNode)J.O(("unit_id", J.S(i < 10 ? "automated" : "non-automated")), ("index", game["index"]), ("game_id", game["game_id"]), ("seed", game["seed"]),
                ("game_result_hash", game["result_hash"]), ("event_hash", game["event_hash"]), ("rng_hash", game["rng_hash"]), ("event_format_version", J.N(2)),
                ("event_count", game["committed_events"]), ("located_event_count", J.N(0)), ("ef1_receipts", J.N(0)), ("ef2_receipts", J.N(0)),
                ("event_sequence_equal", J.B(true)), ("locations_equal", J.B(true)), ("receipts_equal", J.B(true)), ("rewrite_bytes_equal", J.B(true)), ("final_state_equal", J.B(true)),
                ("event_hash_equal", J.B(true)), ("event_replay_match", J.B(true)), ("seed_replay_match", J.B(true)));
        });
        var vs = Schema.Text(JEdit.Get(integration, "/execution/validation_set_id"));
        return J.O(("schema", OfficialDocuments.SchemaHeader(EvidenceKind.Pzl6)),
            ("execution", JEdit.Set(integration["execution"], "/evidence_id", J.S(EvidenceIds.Pzl6(vs)))), ("approvals", integration["approvals"]),
            ("plan", J.O(("integration_evidence_id", JEdit.Get(integration, "/execution/evidence_id")), ("selection_rule", J.S("FIRST_10_PER_MODE")), ("sample_count_per_mode", J.N(10)))),
            ("samples", J.A(samples)),
            ("check", J.O(("id", J.S("pzl-6")), ("status", J.S("PASS")), ("reason", J.S("fixture")), ("coverage", J.O(("completed_games", J.N(20)), ("events", J.N(0)), ("pitches", J.N(0)),
                ("called_pitch_steps", J.N(0)))), ("violations", J.A()), ("test_evidence", JNode.Null), ("deferral", JNode.Null))),
            ("additional_evidence", J.O(("pzl_unit_ids", J.A()), ("pzl6_id", JNode.Null), ("artifacts", J.A()))));
    }

    private (JObject Doc, List<JObject> Units, JObject Pzl6) Integration(EvidenceContext? ctx = null)
    {
        var c = ctx ?? Ctx;
        var doc = OfficialDocuments.Family(fam.IntResult, c, []);
        var units = fam.IntResult.Units.Select(u => PzlDoc(u, "fc3-int", c)).ToList();
        var pzl6 = Pzl6Doc(doc);
        OfficialDocumentTests.Validate(EvidenceKind.Family, doc);
        units.ForEach(u => OfficialDocumentTests.Validate(EvidenceKind.UnitPzl, u));
        OfficialDocumentTests.Validate(EvidenceKind.Pzl6, pzl6);
        return (doc, units, pzl6);
    }

    private static string Code(Action a) => Assert.Throws<EvidenceException>(a).Error.Code;

    [Fact]
    public void The_integration_payload_binds_its_children_and_its_a_plus_ignores_only_the_artifact_references()
    {
        var (doc, units, pzl6) = Integration();
        var p = EvidenceHash.Family(doc, units, pzl6);
        Assert.Equal(EvidenceHash.APlus(EvidenceHash.FamilyPrefix, Jcs.Bytes(p.Payload)), p.Hash);
        Assert.Equal(["hash_contract", "pzl6", "pzl_units", "result"], p.Payload.Members.Select(m => m.Key).Order(StringComparer.Ordinal));
        Assert.False(((JObject)JEdit.Get(p.Payload, "/result/additional_evidence")).Has("artifacts"));

        // A12 / A18: other archive paths and raw SHAs (artifact references) give the same A+; any semantic field changes it
        var artifact = J.O(("artifact_id", J.S("a1")), ("role", J.S("DIAGNOSTICS")), ("unit_id", JNode.Null), ("game_id", JNode.Null), ("relative_path", J.S("logs/x.txt")),
            ("raw_sha256", J.S(new string('b', 64))), ("byte_length", J.N(3)));
        var withRef = (JObject)JEdit.Set(doc, "/additional_evidence/artifacts", J.A(artifact));
        Assert.Equal(p.Hash, EvidenceHash.Family(withRef, units, pzl6).Hash);
        Assert.NotEqual(p.Hash, EvidenceHash.Family((JObject)JEdit.Set(doc, "/aggregate_evaluation/reason", J.S("다른 이유")), units, pzl6).Hash);

        // binding: swapped units, another unit's hashes, a #6 sample replaced by the 11th game (A10), a missing #6
        Assert.Equal(EvidenceCodes.ReferenceInvalid, Code(() => EvidenceHash.Family(doc, [units[1], units[0]], pzl6)));
        Assert.Equal(EvidenceCodes.HashMismatch, Code(() => EvidenceHash.Family(doc, [(JObject)JEdit.Set(units[0], "/plan/config_hash", J.S(new string('c', 64))), units[1]], pzl6)));
        var eleventh = (JObject)JEdit.Get(doc, "/units/0/technical/games/10");
        var a10 = JEdit.Set(JEdit.Set(JEdit.Set(pzl6, "/samples/9/game_id", eleventh["game_id"]), "/samples/9/seed", eleventh["seed"]), "/samples/9/game_result_hash", eleventh["result_hash"]);
        Assert.Equal(EvidenceCodes.PzlMismatch, Code(() => EvidenceHash.Family(doc, units, (JObject)a10)));
        Assert.Equal(EvidenceCodes.ReferenceInvalid, Code(() => EvidenceHash.Family(doc, units, null)));
        Assert.Equal(EvidenceCodes.PzlIncomplete, Code(() => EvidenceHash.Family(doc, [(JObject)JEdit.Remove(units[0], "/games/99"), units[1]], pzl6)));
    }

    [Fact]
    public void A01_a_child_document_from_another_validation_set_is_an_approval_mismatch()
    {
        var (doc, units, pzl6) = Integration();
        var other = Integration(Ctx with { ValidationSetId = "vs-b1-other" });
        Assert.Equal(EvidenceCodes.ApprovalMismatch, Code(() => EvidenceHash.Family(doc, [other.Units[0], units[1]], pzl6)));
        Assert.Equal(EvidenceCodes.ApprovalMismatch, Code(() => EvidenceHash.Family(doc, units, (JObject)JEdit.Set(pzl6, "/approvals/freeze_id", J.S("freeze-other")))));
    }

    // ---------------------------------------------------------------- Phase root on minimal payloads (only what Phase reads)

    private static JObject Coverage(long c, long e, long p, long s) => J.O(("completed_games", J.N(c)), ("events", J.N(e)), ("pitches", J.N(p)), ("called_pitch_steps", J.N(s)));

    private static JObject Unit(string family, string unitId, long events) =>
        J.O(("execution", J.O(("family", J.S(family)))), ("plan", J.O(("unit_id", J.S(unitId)))), ("checks", J.A(J.O(("coverage", Coverage(1, events, events / 2, 1))))));

    private static JObject Result(string family, string runId, string vs = "vs-p") => J.O(
        ("schema", J.O(("contracts", OfficialDocuments.SchemaHeader(EvidenceKind.Family)["contracts"]))),
        ("execution", J.O(("family", J.S(family)), ("run_id", J.S(runId)), ("validation_set_id", J.S(vs)), ("evidence_id", J.S($"{vs}.{family}.result")))),
        ("approvals", OfficialDocumentTests.Ctx.Approvals()));

    private static EvidencePayload Child(string kind, JObject result, IEnumerable<JObject> units, JObject? pzl6 = null)
    {
        var payload = J.O(("result", result), ("pzl_units", J.A(units)), ("pzl6", pzl6 is null ? JNode.Null : pzl6));
        return new EvidencePayload(kind, payload, Jcs.Bytes(payload), EvidenceHash.APlus(EvidenceHash.FamilyPrefix, Jcs.Bytes(payload)));
    }

    private static (EvidencePayload Cal, EvidencePayload Int, EvidencePayload Dom) Children(string vs = "vs-p", string domRun = "run-dom")
    {
        var cal = Child("CALIBRATION", Result("fc3-cal", "run-cal"), new[] { "L1", "L2", "L3", "L4" }.Select(u => Unit("fc3-cal", u, 10)));
        var pzl6 = J.O(("check", J.O(("coverage", Coverage(20, 600, 300, 50)))));
        var integration = Child("INTEGRATION", Result("fc3-int", "run-int"), new[] { "automated", "non-automated" }.Select(u => Unit("fc3-int", u, 100)), pzl6);
        var dom = Child("DOMINANCE", Result("fc3-dom", domRun, vs), DominanceMatrix.Cells.Select(c => Unit("fc3-dom", c.Id, 2)));
        return (cal, integration, dom);
    }

    private static List<JObject> Checks((EvidencePayload Cal, EvidencePayload Int, EvidencePayload Dom) c)
    {
        var units = new[] { c.Cal, c.Int, c.Dom }.SelectMany(p => Schema.Items(p.Payload["pzl_units"]).Select(Schema.Obj)).ToList();
        long[] Sum(IEnumerable<JObject> us) => EvidenceShapes.CoverageFields.Select(f => us.Sum(u => Schema.Long(JEdit.Get(u, $"/checks/0/coverage/{f}")))).ToArray();
        var all = Sum(units);
        var auto = Sum(units.Where(u => EvidenceRules.ModeOf(Schema.Text(JEdit.Get(u, "/execution/family")), Schema.Text(JEdit.Get(u, "/plan/unit_id"))) == "Automated"));
        var te = J.O(("profile_id", J.S("pzl-profile-test")), ("profile_sha256", J.S(new string('d', 64))), ("source_git_sha", J.S(new string('e', 40))), ("cases", J.A()));
        return EvidenceHash.PhaseCheckIds.Select(id =>
        {
            var cov = id switch
            {
                "pzl-1" or "pzl-2" or "pzl-3" or "pzl-5" or "pzl-12a" => all,
                "pzl-10" => auto,
                "pzl-6" => [20, 600, 300, 50],
                _ => [0L, 0, 0, 0],
            };
            var needs = id is "pzl-4" or "pzl-6" or "pzl-7" or "pzl-8" or "pzl-9" or "pzl-10" or "pzl-11" or "pzl-12a";
            return J.O(("id", J.S(id)), ("status", J.S(id == "pzl-12b" ? "DEFERRED" : "PASS")), ("reason", J.S("fixture")), ("coverage", Coverage(cov[0], cov[1], cov[2], cov[3])),
                ("violations", J.A()), ("test_evidence", needs ? te : JNode.Null), ("deferral", id == "pzl-12b" ? UnitPzlEvidence.Deferral() : JNode.Null));
        }).ToList();
    }

    [Fact]
    public void The_phase_root_names_three_children_of_one_set_and_its_checks_keep_the_section_7_scope()
    {
        var c = Children();
        var checks = Checks(c);
        var phase = EvidenceHash.Phase(c.Cal, c.Int, c.Dom, checks);
        Assert.Equal(EvidenceHash.APlus(EvidenceHash.PhasePrefix, phase.Canonical), phase.Hash);
        Assert.Equal([c.Cal.Hash, c.Int.Hash, c.Dom.Hash], Schema.Items(phase.Payload["children"]).Select(x => Schema.Text(((JObject)x)["evidence_hash"])));

        // A01 / A02: a dominance child of another validation set (same Git SHA is never enough), reused run id, swapped roles
        var mixed = Children(vs: "vs-other");
        Assert.Equal(EvidenceCodes.ApprovalMismatch, Code(() => EvidenceHash.Phase(c.Cal, c.Int, mixed.Dom, checks)));
        var reused = Children(domRun: "run-cal");
        Assert.Equal(EvidenceCodes.ApprovalMismatch, Code(() => EvidenceHash.Phase(reused.Cal, reused.Int, reused.Dom, Checks(reused))));
        Assert.Equal(EvidenceCodes.ReferenceInvalid, Code(() => EvidenceHash.Phase(c.Int, c.Cal, c.Dom, checks)));

        List<JObject> With(int i, string pointer, JNode v) => checks.Select((x, k) => k == i ? (JObject)JEdit.Set(x, pointer, v) : x).ToList();
        // A25-a: a test-profile check with observation coverage; A25-b: #10 counting the non-automated units; A10-a: #6 not the 20-game sample scope
        Assert.Equal(EvidenceCodes.PzlMismatch, Code(() => EvidenceHash.Phase(c.Cal, c.Int, c.Dom, With(3, "/coverage/events", J.N(1)))));
        var allEvents = Schema.Long(JEdit.Get(checks[0], "/coverage/events"));
        Assert.Equal(EvidenceCodes.PzlMismatch, Code(() => EvidenceHash.Phase(c.Cal, c.Int, c.Dom, With(9, "/coverage/events", J.N(allEvents)))));
        Assert.Equal(EvidenceCodes.PzlMismatch, Code(() => EvidenceHash.Phase(c.Cal, c.Int, c.Dom, With(5, "/coverage/completed_games", J.N(21)))));
        // R-9: a missing test_evidence, a PASS #12b
        Assert.Equal(EvidenceCodes.SchemaInvalid, Code(() => EvidenceHash.Phase(c.Cal, c.Int, c.Dom, With(6, "/test_evidence", JNode.Null))));
        Assert.Equal(EvidenceCodes.SchemaInvalid, Code(() => EvidenceHash.Phase(c.Cal, c.Int, c.Dom, With(12, "/status", J.S("PASS")))));
    }

    [Fact]
    public void N36b_a_phase_check_whose_scope_observed_nothing_is_not_refused_for_its_zero_coverage()
    {
        var c = Children();
        var zeroUnits = (Func<EvidencePayload, EvidencePayload>)(p => Child(p.Kind, (JObject)p.Payload["result"],
            Schema.Items(p.Payload["pzl_units"]).Select(u => (JObject)JEdit.Set(u, "/checks/0/coverage", Coverage(0, 0, 0, 0))), p.Payload["pzl6"] as JObject));
        var z = (zeroUnits(c.Cal), zeroUnits(c.Int), zeroUnits(c.Dom));
        var checks = Checks(z);
        Assert.Equal(0, Schema.Long(JEdit.Get(checks[0], "/coverage/events")));
        Assert.Equal("PHASE", EvidenceHash.Phase(z.Item1, z.Item2, z.Item3, checks).Kind);
    }
}
