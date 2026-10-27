using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.EvidenceVerifier.Tests;

/// <summary>
/// B1-T11-01 / -02 / -03 on one TEST_ONLY world (shared, it is costly): the certification boundary, the Phase child binding and the whole-archive snapshot. Written after the fix (no Red run): the
/// boundary did not exist before, so each test targets the path Codex traced (caller-built receipt, recheck=null, [c, c, c], an unopened DIAGNOSTICS reference, a re-read result directory).
/// </summary>
[Collection(CertificationSeams.Name)]                                                                     // G4: shares the static Certification seams with G4OfficialEntryTests
public sealed partial class T11CertificationTests(TestOnlyWorld w) : IClassFixture<TestOnlyWorld>
{
    private static EvidenceError Refused(Action a) => Assert.Throws<EvidenceException>(a).Error;

    /// <summary>A TEST_ONLY session, its local registry and its own archive whose documents are produced under the session's (re-pointed) approval record.</summary>
    private sealed record Opened(TestOnlyTrust Trust, SqliteRegistry Registry, Production Production, CertificationSession Session);

    /// <param name="allowance">The record's INSUFFICIENT_SAMPLE allowance (true makes this world's SEEDS_EXHAUSTED Calibration certifiable as TEST_ONLY).</param>
    private Opened Open(bool allowance = true, Func<EnvironmentSnapshot>? capture = null, string? freezeId = null)
    {
        var trust = new TestOnlyTrust(w, insufficientSampleAllowed: allowance, freezeId: freezeId);
        var registry = w.Own(trust.Open(RegistryRole.VerifierService, TestOnlyTrust.Verifier));
        var production = w.ProducerFor(trust);
        Certification.VariablesSeam = () => new System.Collections.Hashtable();                      // a test host carries DOTNET_ variables; E2-1 has its own test below
        Certification.CaptureSeam = capture;
        try
        {
            return new Opened(trust, registry, production, Certification.Open(trust.Anchor, registry, trust.Approval.Id, trust.Approval.RawSha256, "auth-test-1", w.Root, production.ArchiveRoot));
        }
        finally
        {
            Certification.VariablesSeam = null;
            Certification.CaptureSeam = null;
        }
    }

    private (Opened O, VerifiedFamily Cal, VerifiedFamily Int, IssuedReceipt CalR, IssuedReceipt IntR) Certified() => w.Shared("t11-certified", () =>
    {
        var o = Open();
        var cal = Certification.VerifyFamily(o.Session, "fc3-cal", o.Production.Family(w.Calibration, "clause-cal-test", "calibration-result.json").Reference);
        var integration = Certification.VerifyFamily(o.Session, "fc3-int", o.Production.Integration().Reference);
        return (o, cal, integration, Certification.IssueFamily(o.Session, cal), Certification.IssueFamily(o.Session, integration));
    });

    // ---------------------------------------------------------------- B1-T11-01

    [Fact]
    public void T11_01_no_public_api_accepts_a_caller_built_receipt_or_an_environment_recheck()
    {
        Assert.Null(typeof(Receipts).GetMethod("Issue", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static));
        var publicMethods = typeof(Certification).Assembly.GetExportedTypes()
            .SelectMany(t => t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance)).ToList();
        Assert.DoesNotContain(publicMethods, m => m.ReturnType == typeof(IssuedReceipt) && m.DeclaringType != typeof(Certification) && m.DeclaringType != typeof(IssuedReceipt));   // the record's own Clone
        Assert.DoesNotContain(publicMethods, m => m.GetParameters().Any(p => p.Name is "recheck"));
        // nothing public takes a caller-built receipt and issues (returns an IssuedReceipt) from it; Receipts.CheckInventory only checks one
        Assert.DoesNotContain(publicMethods, m => m.ReturnType == typeof(IssuedReceipt) && m.GetParameters().Any(p => p.ParameterType == typeof(JObject)));
        // the pipe clients' public identity / access seams exist, but a seamed connection is never Trusted, so it can never certify (A17, PipeRegistryTests)
        Assert.Null(typeof(Certification).GetProperty("VariablesSeam", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static));   // seams are internal
        Assert.Empty(typeof(CertificationSession).GetConstructors());                                                                                     // no public way to make a session
        Assert.Empty(typeof(TestProfileRun).GetConstructors());
    }

    [Fact]
    public void T11_01_a_receipt_is_built_only_from_a_result_this_session_verified_and_carries_its_values()
    {
        var (o, cal, _, calR, _) = Certified();
        var r = Receipts.Validate(calR.Bytes);
        var t = (JObject)((JArray)r["targets"]).Items.Single();
        Assert.Equal(("CALIBRATION", cal.Payload.Hash, "INSUFFICIENT_SAMPLE", true), (Schema.Text(t["kind"]), Schema.Text(t["evidence_hash"]), Schema.Text(t["statistical_status"]), ((JBool)r["test_only"]).Value));
        Assert.Equal(ReceiptState.TestOnlyRegistered, calR.State);                                                       // a TEST_ONLY session never certifies (A17)
        Assert.Equal(calR.Bytes, o.Registry.Get("VERIFICATION_RECEIPT", calR.ReceiptId)!.RawBytes);
        var sources = ((JArray)r["source_files"]).Items.Cast<JObject>().ToList();
        var roles = sources.Select(f => (Schema.Text(f["role"]), Schema.Text(f["origin"]))).ToHashSet();
        Assert.Contains(("CANONICAL_PAYLOAD", "VERIFIER_DERIVED"), roles);                                              // §20.2 4: payload and inventory bound
        Assert.Contains(("RAW_INVENTORY", "VERIFIER_DERIVED"), roles);
        Assert.Contains(("UNIT_MANIFEST", "ORIGINAL"), roles);                                                           // the replayed bytes are in the source list

        // forged or changed results, results verified outside the session, results of another session
        Assert.Equal("Certification.unverified_result", Refused(() => Certification.IssueFamily(o.Session, cal with { StatisticalStatus = "PASS" })).Cause);
        Assert.Equal("Certification.unverified_result", Refused(() => Certification.IssueFamily(o.Session, cal with { TestOnly = false })).Cause);
        var docRef = new JObject(sources.Single(f => Schema.Text(f["role"]) == "FAMILY_RESULT").Members.Where(m => m.Key != "origin").ToList());
        var outside = Verifier.VerifyFamily(new FamilyVerificationRequest(o.Trust.Approval, o.Session.Trust.Authorization, "fc3-cal", w.Root, o.Production.ArchiveRoot, docRef));
        Assert.Equal("Certification.unverified_result", Refused(() => Certification.IssueFamily(o.Session, outside)).Cause);
        var other = Open();
        Assert.Equal("Certification.unverified_result", Refused(() => Certification.IssueFamily(other.Session, cal)).Cause);
    }

    [Fact]
    public void T11_01_the_session_enforces_e2_1_and_e2_4_issuance_enforces_e2_7_and_an_ineligible_or_pzl_failing_result_gets_no_receipt()
    {
        var trust = new TestOnlyTrust(w, insufficientSampleAllowed: true);
        using (var registry = trust.Open(RegistryRole.VerifierService, TestOnlyTrust.Verifier))   // disposed here
        {
            Certification.VariablesSeam = () => new System.Collections.Hashtable { ["DOTNET_TieredPGO"] = "0" };
            try
            {
                Assert.Equal(EvidenceCodes.EnvironmentMismatch, Refused(() => Certification.Open(trust.Anchor, registry, trust.Approval.Id, trust.Approval.RawSha256, "auth-test-1", w.Root, w.Archive)).Code);   // E2-1
            }
            finally
            {
                Certification.VariablesSeam = null;
            }
        }

        var real = ExecutionEnvironment.Capture();
        Assert.Equal("Environment.profile_mismatch", Refused(() => Open(capture: () => real with { OsVersion = "0.0" })).Cause);                                         // E2-4: C0 against the profile
        var calls = 0;
        Assert.Equal("Environment.c1_differs", Refused(() => Open(capture: () => ++calls == 1 ? real : real with { EnvironmentDigest = new string('0', 64) })).Cause);   // E2-4: C1 == C0

        // E2-7 at issuance: C2 differs → nothing written or registered
        var (o, _, integration, _, _) = Certified();
        Certification.CaptureSeam = () => real with { EnvironmentDigest = new string('1', 64) };
        try
        {
            Assert.Equal(EvidenceCodes.EnvironmentMismatch, Refused(() => Certification.IssueFamily(o.Session, integration)).Code);
        }
        finally
        {
            Certification.CaptureSeam = null;
        }

        // §19: a result the verifier found ineligible gets no receipt; the PZL axis refuses FAIL and NOT_EVALUATED
        var strict = Open(allowance: false);
        var calStrict = Certification.VerifyFamily(strict.Session, "fc3-cal", strict.Production.Family(w.Calibration, "clause-cal-test", "calibration-result.json").Reference);
        Assert.Equal(EvidenceCodes.NotCertifiable, Refused(() => Certification.IssueFamily(strict.Session, calStrict)).Code);
        var unit = integration.PzlUnits[0];
        Receipts.PzlEligibility([unit], integration.Pzl6);
        foreach (var status in new[] { "FAIL", "NOT_EVALUATED" })
            Assert.Equal("Certification.pzl", Refused(() => Receipts.PzlEligibility([(JObject)JEditT.Set(unit, "/checks/0/status", J.S(status))], integration.Pzl6)).Cause);
        Assert.Equal("Certification.pzl", Refused(() => Receipts.PzlEligibility([unit], (JObject)JEditT.Set(integration.Pzl6!, "/check/status", J.S("FAIL")))).Cause);
    }

    // ---------------------------------------------------------------- B1-T11-02

    [Fact]
    public void T11_02_the_phase_binds_its_exact_three_children_at_the_issuance_boundary()
    {
        var (o, cal, integration, calR, intR) = Certified();
        var dom = Certification.VerifyFamily(o.Session, "fc3-dom", o.Production.Dominance().Reference);
        var run = new VerifiedRegressionProof(new TestProfileRun(o.Session, new Dictionary<string, JObject>()), J.O());   // never reached: the children are bound first
        string Phase(params IssuedReceipt[] children) => Refused(() => Certification.IssuePhase(o.Session, cal, integration, dom, children, run)).Cause;
        string NewId() => Guid.NewGuid().ToString("D");

        // a receipt with forged content, registered in this registry (the registry stores bytes; it does not vouch for them)
        IssuedReceipt Forge(IssuedReceipt from, string pointer, JNode value)
        {
            var forged = (JObject)JEditT.Set(JEditT.Set(Receipts.Validate(from.Bytes), "/receipt_id", J.S(NewId())), pointer, value);
            return Receipts.Issue(forged, o.Production.ArchiveRoot, o.Registry, () => null);
        }

        Assert.Equal("Phase.children_missing", Phase(calR, intR));
        Assert.Equal("Phase.children_duplicate", Phase(calR, calR, calR));                                             // the Codex path [c, c, c]
        Assert.Equal("Phase.child_role", Phase(intR, calR, intR with { ReceiptId = NewId() }));                         // roles swapped
        Assert.Equal("Phase.child_not_registered", Phase(calR with { Bytes = intR.Bytes }, intR, calR with { ReceiptId = NewId() }));
        Assert.Equal("Phase.child_target", Phase(Forge(calR, "/targets/0/evidence_hash", J.S("sha256:" + new string('0', 64))), intR, intR with { ReceiptId = NewId() }));   // A+
        Assert.Equal("Phase.child_target", Phase(Forge(calR, "/targets/0/run_id", J.S("run-other")), intR, intR with { ReceiptId = NewId() }));
        Assert.Equal("Phase.child_test_only", Phase(Forge(calR, "/test_only", J.B(false)), intR, intR with { ReceiptId = NewId() }));
        // a normal receipt of another validation run (another approval record) mixed in
        var other = Open(freezeId: "freeze-test-other");
        var otherCal = Certification.VerifyFamily(other.Session, "fc3-cal", other.Production.Family(w.Calibration, "clause-cal-test", "calibration-result.json").Reference);
        var mixed = Certification.IssueFamily(other.Session, otherCal);
        o.Registry.Register("VERIFICATION_RECEIPT", mixed.ReceiptId, mixed.Bytes);
        Assert.Equal("Phase.child_approval", Phase(mixed, intR, intR with { ReceiptId = NewId() }));
        // the first two children bound; the Dominance position holds a receipt id that was never registered (A28). The issued Phase receipt itself: R3_* (B1 T11 second round; this world's Dominance
        // gate is PASS since its seed base changed, so the position is no longer refused as Certification.statistical)
        Assert.Equal("Phase.child_not_registered", Phase(calR, intR, intR with { ReceiptId = NewId() }));
    }

    // ---------------------------------------------------------------- B1-T11-03

    /// <summary>The integration document with one Unit PZL replaced by <paramref name="edit"/> of it (both re-written as new archive files, references re-hashed).</summary>
    private JObject WithUnit(JObject doc, Func<JObject, JObject> edit)
    {
        var refs = ((JArray)((JObject)doc["additional_evidence"])["artifacts"]).Items.Cast<JObject>().ToList();
        var unitRef = refs.First(x => Schema.Text(x["role"]) == "UNIT_PZL");
        var unit = (JObject)StrictJson.Parse(Archive.TryRead(w.Archive, Schema.Text(unitRef["relative_path"]))!);
        var tag = Guid.NewGuid().ToString("N");
        var newUnit = Archive.WriteNew(w.Archive, $"tampered/{tag}/unit-pzl.json", Jcs.Bytes(edit(unit)), Schema.Text(unitRef["artifact_id"]), "UNIT_PZL", Schema.Text(unitRef["unit_id"]));
        var parent = (JObject)JEditT.Set(doc, "/additional_evidence/artifacts", J.A(refs.Select(x => ReferenceEquals(x, unitRef) ? newUnit : x).Cast<JNode>().ToArray()));
        return EvidenceWriter.WriteDocument(w.Archive, parent, "FAMILY_RESULT", $"tampered-{tag}.json");
    }

    private static JObject AddArtifact(JObject doc, JObject artifact)
    {
        var list = ((JArray)((JObject)doc["additional_evidence"])["artifacts"]).Items.Cast<JObject>().Append(artifact).OrderBy(x => Schema.Text(x["artifact_id"]), StringComparer.Ordinal);
        return (JObject)JEditT.Set(doc, "/additional_evidence/artifacts", J.A(list.Cast<JNode>().ToArray()));
    }

    private (JObject Reference, JObject Document, JObject Pzl6) Integration() => w.ProduceIntegration();                  // made once per world by the producer

    [Fact]
    public void T11_03_every_reference_below_the_document_is_opened_and_one_artifact_id_has_one_reference()
    {
        var (reference, doc, _) = Integration();
        Verifier.VerifyFamily(w.FamilyRequest("fc3-int", reference));                                                    // the untampered evidence verifies
        // the Codex path: a DIAGNOSTICS reference in a Unit PZL whose file does not exist
        var ghost = Archive.Reference("ghost-diagnostics", "DIAGNOSTICS", null, null, "units/ghost/diagnostics.json", [1, 2, 3]);
        var e = Refused(() => Verifier.VerifyFamily(w.FamilyRequest("fc3-int", WithUnit(doc, u => AddArtifact(u, ghost)))));
        Assert.Equal((EvidenceCodes.ArchiveInvalid, "Archive.missing"), (e.Code, e.Cause));
        // the same artifact id naming another file than the parent's reference
        var seed = ((JArray)((JObject)doc["additional_evidence"])["artifacts"]).Items.Cast<JObject>().First(x => Schema.Text(x["role"]) == "SEED_MANIFEST");
        var clash = Archive.WriteNew(w.Archive, $"tampered/{Guid.NewGuid():N}/clash.json", [9], Schema.Text(seed["artifact_id"]), "SEED_MANIFEST", Schema.Text(seed["unit_id"]));
        Assert.Equal("Archive.artifact_id_conflict", Refused(() => Verifier.VerifyFamily(w.FamilyRequest("fc3-int", WithUnit(doc, u => AddArtifact(u, clash))))).Cause);
    }

    [Fact]
    public void T11_03_the_replay_reads_only_the_archived_run_files_and_the_pzl6_keeps_its_twenty_json_files()
    {
        var (reference, doc, pzl6) = Integration();
        // the result directory changes after archiving: the verification no longer reads it
        var manifest = Path.Combine(w.Root, "build", "simharness", "official", "run-int-test", "units", "automated", "manifest.json");
        var original = File.ReadAllBytes(manifest);
        File.WriteAllBytes(manifest, [.. original, (byte)' ']);
        try
        {
            Verifier.VerifyFamily(w.FamilyRequest("fc3-int", reference));
        }
        finally
        {
            File.WriteAllBytes(manifest, original);
        }

        // a document without the run index or a unit manifest is refused before any game
        var refs = ((JArray)((JObject)doc["additional_evidence"])["artifacts"]).Items.Cast<JObject>().ToList();
        // (a unit manifest is first missed by the §21 whole-snapshot count, the run index by the replay-input check; both archive_invalid)
        foreach (var (role, cause) in new[] { ("UNIT_MANIFEST", "Archive.required_count"), ("DIAGNOSTICS", "Archive.run_file_missing") })
        {
            var drop = refs.FindIndex(r => Schema.Text(r["role"]) == role);
            var cut = (JObject)JEditT.Set(doc, "/additional_evidence/artifacts", J.A(refs.Where((_, i) => i != drop).Cast<JNode>().ToArray()));
            var e = Refused(() => Verifier.VerifyFamily(w.FamilyRequest("fc3-int", EvidenceWriter.WriteDocument(w.Archive, cut, "FAMILY_RESULT", $"cut-{Guid.NewGuid():N}.json"))));
            Assert.Equal((EvidenceCodes.ArchiveInvalid, cause), (e.Code, e.Cause));
        }

        // PZL6: twenty v2 JSON files, one per fixed sample in sample order, archived and referenced by the #6 document
        var json = ((JArray)((JObject)pzl6["additional_evidence"])["artifacts"]).Items.Cast<JObject>().Where(x => Schema.Text(x["role"]) == "PZL6" && x["game_id"] is JString).ToList();
        Assert.Equal(20, json.Count);
        Assert.Equal(((JArray)pzl6["samples"]).Items.Cast<JObject>().Select(x => Schema.Text(x["game_id"])).Order(StringComparer.Ordinal), json.Select(x => Schema.Text(x["game_id"])).Order(StringComparer.Ordinal));
        Assert.All(json, x => Assert.NotNull(Archive.TryRead(w.Archive, Schema.Text(x["relative_path"]))));
    }
}
