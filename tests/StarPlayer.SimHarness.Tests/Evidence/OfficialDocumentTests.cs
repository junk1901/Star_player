using System.Security.Cryptography;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.SimHarness.Tests.Evidence;

/// <summary>
/// B1-T03: the closed Family / Dominance documents projected from COMPLETE official results (real-engine Integration and 48-cell runs of the test worlds, and a calibration run of schema-valid synthetic
/// games), refused for an incomplete run (N04), leaving the DRAFT index bytes alone, and every projected document passing the T01 schema; then single-field tampering of a valid document.
/// </summary>
public sealed class OfficialDocumentTests(FamilyOfficialWorld fam, DominanceOfficialWorld dom) : IClassFixture<FamilyOfficialWorld>, IClassFixture<DominanceOfficialWorld>
{
    internal static readonly EvidenceContext Ctx = new("vs-b1-test", "apr-b1-test", new string('a', 64), "freeze-b1-test", "bp-b1-test", "clause-b1-test");

    private static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static string Hex(string seed) => Hashing.Sha256(seed);

    /// <summary>A synthetic COMPLETED game that satisfies the state table: real-format hashes and a passing 15-item cross-check (10,000 PA, as in the family tests).</summary>
    internal static GameRunResult ValidFake(GameSpec s)
    {
        var c = FamilyOfficialWorld.Good();
        c.EventHomeRuns = c.HomeRuns;
        c.EventHits = c.Hits;
        long[] values = [c.PlateAppearances, c.AtBats, c.Hits, c.Singles, c.Doubles, c.Triples, c.HomeRuns, c.Walks, c.HitByPitch, c.Strikeouts, c.SacrificeFlies, c.Pitches, c.EventHomeRuns, c.EventHits, c.HitByPitchPitches];
        var cross = new CrossCheckResult(true, EvidenceShapes.CrossCheckNames.Zip(values).Select(p => new CrossCheckItem(p.First, p.Second, p.Second)).ToList());
        return new GameRunResult(s, GameRunStatus.Completed, null, GameOutcome.HomeWin, GameEndKind.Regulation, 0, 1, 9, InningHalf.Bottom, 1, c, cross, Hex($"e{s.GameId}"), Hex($"r{s.GameId}"),
            Hex($"g{s.GameId}"), 0);
    }

    private FamilyOfficialResult Calibration(string runId) =>
        FamilyOfficial.Execute(FamilyPreflight.Run(OfficialFamilyPlan.Calibration, new DominanceOfficialRequest(fam.Root, runId, null), fam.Approvals, FamilyOfficialWorld.Git()), runId, fam.Approvals,
            fam.Root, play: (_, s) => ValidFake(s));

    internal static JObject Validate(EvidenceKind kind, JObject doc) => EvidenceRules.Validate(kind, Jcs.Bytes(doc));

    private static EvidenceError Refused(EvidenceKind kind, JNode doc)
    {
        var e = EvidenceRules.Check(kind, Jcs.Bytes(doc));
        Assert.NotNull(e);
        return e!;
    }

    [Fact]
    public void A_complete_integration_calibration_and_dominance_result_project_to_documents_the_closed_schema_accepts()
    {
        var integration = OfficialDocuments.Family(fam.IntResult, Ctx, []);
        Validate(EvidenceKind.Family, integration);
        var cal = Calibration("b1-t03-cal");
        Assert.True(cal.Complete);
        var calibration = OfficialDocuments.Family(cal, Ctx, []);
        Validate(EvidenceKind.Family, calibration);
        Assert.True(dom.Result.Complete);
        Validate(EvidenceKind.Dominance, OfficialDocuments.Dominance(dom.Result, Ctx, []));

        // R-5: calibration keeps the policy id/hash and official metrics; integration has none; the gates are the family evaluation's own
        Assert.Equal(EvaluationPolicy.FcEvaluationV1.Hash(), ((JString)JEdit.Get(calibration, "/units/0/evaluation/policy_hash")).Value);
        Assert.Equal(["F1", "F6", "F7", "F8"], ((JArray)JEdit.Get(calibration, "/units/0/evaluation/metrics")).Items.Select(m => ((JString)((JObject)m)["f_id"]).Value));
        Assert.IsType<JNull>(JEdit.Get(integration, "/units/0/evaluation/policy_id"));
        Assert.Equal(GateText.Of(fam.IntResult.Evaluation!.Gate), ((JString)JEdit.Get(integration, "/aggregate_evaluation/status")).Value);
        Assert.Equal(fam.IntResult.Units[0].Run.RunResultHash, ((JString)JEdit.Get(integration, "/units/0/technical/run_result_hash")).Value);
        Assert.Equal(fam.IntResult.Units[0].Plan.Report.Attestation!.Hash(), ((JString)JEdit.Get(integration, "/units/0/inputs/attestation_hash")).Value);
    }

    [Fact]
    public void An_incomplete_result_has_no_official_document_and_projecting_changes_no_draft_index_byte()
    {
        var cancelled = fam.CalResult with { Termination = DominanceRunTermination.Cancelled, Evaluation = null };
        var e = Assert.Throws<EvidenceException>(() => OfficialDocuments.Family(cancelled, Ctx, []));
        Assert.Equal(EvidenceCodes.NotCertifiable, e.Error.Code);
        Assert.Throws<EvidenceException>(() => OfficialDocuments.Dominance(dom.Result with { Termination = DominanceRunTermination.AbortedIntegrity }, Ctx, []));

        var index = Path.Combine(fam.IntPreflight.OutputDirectory!, FamilyOfficial.ResultFile);
        var before = Sha(index);
        var files = Directory.GetFiles(fam.IntPreflight.OutputDirectory!, "*", SearchOption.AllDirectories).Length;
        OfficialDocuments.Family(fam.IntResult, Ctx, []);
        Assert.Equal(before, Sha(index));
        Assert.Equal(files, Directory.GetFiles(fam.IntPreflight.OutputDirectory!, "*", SearchOption.AllDirectories).Length);
        Assert.Contains("simharness-family-result-draft", File.ReadAllText(index), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/schema/contracts/family", "int1", "Evidence.schema_invalid")]   // N06 / N23: integer 1
    [InlineData("/schema/contracts/family", "01", "Evidence.schema_invalid")]     // "01"
    [InlineData("/schema/contracts/hash", "2", "Evidence.contract_not_approved")]  // a valid VER that is not the approved combination
    [InlineData("/schema/id", "simharness-family-result-draft", "Evidence.schema_invalid")]   // A11 / S1
    [InlineData("/execution/evidence_id", "vs-b1-test.fc3-int.other", "Evidence.reference_invalid")]   // N08 / R-12
    [InlineData("/units/0/inputs/unit_run_id", "other-run", "Evidence.schema_invalid")]   // R-14 run_id
    [InlineData("/units/0/inputs/games/0/seed", "01", "Evidence.schema_invalid")]   // A21
    [InlineData("/units/0/technical/games/0/rng_hash", "null", "Evidence.schema_invalid")]   // row A needs an RNG hash
    [InlineData("/units/0/technical/games/0/end_kind", "None", "Evidence.schema_invalid")]   // A06: a completed game's end kind
    [InlineData("/units/0/technical/games/0/regulation_error_code", "Dominance.regulation_incomplete", "Evidence.schema_invalid")]   // Family projects Regulation as null
    [InlineData("/units/0/technical/committed_events", "n1", "Evidence.schema_invalid")]   // A06: the technical aggregate
    [InlineData("/additional_evidence/pzl6_id", "null", "Evidence.reference_invalid")]   // integration needs its #6 id
    public void One_changed_field_of_a_valid_integration_document_is_refused_with_its_code(string pointer, string value, string code)
    {
        var doc = OfficialDocuments.Family(fam.IntResult, Ctx, []);
        JNode v = value switch
        {
            "null" => JNode.Null,
            "int1" => J.N(1),

            "n1" => new JNumber((long.Parse(((JNumber)JEdit.Get(doc, pointer)).Raw) + 1).ToString()),
            _ => J.S(value),
        };
        Assert.Equal(code, Refused(EvidenceKind.Family, JEdit.Set(doc, pointer, v)).Code);
    }

    [Fact]
    public void An_extra_member_a_missing_member_a_sixth_contract_and_a_draft_key_are_schema_errors()
    {
        var doc = OfficialDocuments.Family(fam.IntResult, Ctx, []);
        Assert.Equal("Schema.unknown_field", Refused(EvidenceKind.Family, JEdit.Add(doc, "", "official_evidence", J.B(true))).Cause);
        Assert.Equal("Schema.unknown_field", Refused(EvidenceKind.Family, JEdit.Add(doc, "", "schema_version", J.S("simharness-family-result-draft"))).Cause);
        Assert.Equal("Schema.unknown_field", Refused(EvidenceKind.Family, JEdit.Add(doc, "/schema/contracts", "approval", J.S("1"))).Cause);
        Assert.Equal("Schema.missing_field", Refused(EvidenceKind.Family, JEdit.Remove(doc, "/schema/contracts/pzl6")).Cause);
        Assert.Equal("Schema.missing_field", Refused(EvidenceKind.Family, JEdit.Remove(doc, "/units/0/technical/stop")).Cause);   // present-and-null differs from absent
        Assert.Equal("Json.duplicate_key", EvidenceRules.Check(EvidenceKind.Family, "{\"schema\":1,\"schema\":2}"u8)!.Cause);
    }

    [Fact]
    public void A22_an_integer_of_two_to_the_53_is_refused_in_the_raw_bytes()
    {
        var doc = OfficialDocuments.Family(fam.IntResult, Ctx, []);
        var text = Jcs.Text(JEdit.Set(doc, "/units/0/technical/counters/duplicates_ignored", new JNumber("9007199254740991")));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, "9007199254740991"));
        var e = EvidenceRules.Check(EvidenceKind.Family, System.Text.Encoding.UTF8.GetBytes(text.Replace("9007199254740991", "9007199254740992")));
        Assert.Equal(("Evidence.schema_invalid", "Schema.integer_range"), (e!.Code, e.Cause));
    }

    private static string? Row(JObject game)
    {
        try
        {
            EvidenceRules.GameRow(game, "/g", dominance: false);
            return null;
        }
        catch (EvidenceException e)
        {
            return e.Error.Cause;
        }
    }

    [Fact]
    public void A_failed_game_keeps_its_state_table_row_crosscheck_only_on_a_crosscheck_failure()
    {
        // N03 (§3.6 row B): an engine-final failure carries a cross-check only with stage crosscheck, and then passed=false; rows A / C / D as projected stay valid
        var doc = OfficialDocuments.Family(fam.IntResult, Ctx, []);
        var game = (JObject)JEdit.Get(doc, "/units/0/technical/games/0");
        Assert.Null(Row(game));
        var failure = J.O(("code", J.S("Harness.crosscheck_mismatch")), ("stage", J.S("crosscheck")), ("event_sequence", J.N(1)), ("revision", J.N(1)),
            ("rng_hash", game["rng_hash"]), ("last_receipts", J.A()));
        var failed = (JObject)JEdit.Set(JEdit.Set(JEdit.Set(game, "/status", J.S("Failed")), "/error_code", J.S("Harness.crosscheck_mismatch")), "/failure", failure);
        Assert.Equal("Schema.state_table", Row(failed));                                                   // passed=true on a crosscheck failure
        Assert.Equal("Schema.crosscheck_passed", Row((JObject)JEdit.Set(failed, "/box_score_cross_check/passed", J.B(false))));   // passed must equal the items
        var items = (JArray)((JObject)failed["box_score_cross_check"])["items"];
        var mismatched = JEdit.Set(JEdit.Set(failed, "/box_score_cross_check/passed", J.B(false)), "/box_score_cross_check/items/0/from_events",
            J.N(long.Parse(((JNumber)((JObject)items.Items[0])["from_events"]).Raw) + 1));
        Assert.Null(Row((JObject)mismatched));                                                              // a real crosscheck failure is a legal row B
        Assert.Equal("Schema.state_table", Row((JObject)JEdit.Set(mismatched, "/failure/stage", J.S("metrics"))));   // no cross-check outside stage crosscheck
        Assert.Equal("Schema.state_table", Row((JObject)JEdit.Set(mismatched, "/error_code", J.S("Harness.other"))));   // error_code == failure.code
        Assert.Equal("Schema.state_table", Row((JObject)JEdit.Set(game, "/failure", failure)));             // a completed game has no failure
    }

    [Fact]
    public void Dominance_documents_refuse_a_catalogue_change_an_unfrozen_size_and_a_non_canonical_f64()
    {
        var doc = OfficialDocuments.Dominance(dom.Result, Ctx, []);
        Assert.Equal(EvidenceCodes.ReferenceInvalid, Refused(EvidenceKind.Dominance, JEdit.Set(doc, "/units/0/cell/batter_strategy", J.S("Patient"))).Code);
        Assert.Equal("Schema.r7", Refused(EvidenceKind.Dominance, JEdit.Set(doc, "/aggregate_evaluation/frozen_games_per_cell", J.N(99))).Cause);
        var ok = ((JArray)JEdit.Get(doc, "/aggregate_evaluation/cells")).Items.Select((c, i) => (c, i)).First(x => ((JObject)x.c)["sample"] is JObject).i;
        foreach (var bad in new[] { "1E+21", "NaN", "-0", "Infinity", "1.0" })   // A24
            Assert.Equal(EvidenceCodes.SchemaInvalid, Refused(EvidenceKind.Dominance, JEdit.Set(doc, $"/aggregate_evaluation/cells/{ok}/sample/mean/value", J.S(bad))).Code);
        Assert.Equal("Schema.r7", Refused(EvidenceKind.Dominance, JEdit.Set(doc, $"/aggregate_evaluation/cells/{ok}/sample", JNode.Null)).Cause);
        Assert.Equal("Schema.f64_undefined", Refused(EvidenceKind.Dominance, JEdit.Set(doc, $"/aggregate_evaluation/cells/{ok}/sample/mean",
            J.O(("state", J.S("UNDEFINED")), ("value", JNode.Null), ("reason", J.S("HOLM_STOPPED"))))).Cause);
    }
}
