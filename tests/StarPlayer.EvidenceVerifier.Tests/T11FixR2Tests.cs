using StarPlayer.SimHarness;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.EvidenceVerifier.Tests;

/// <summary>Codex B1 T11 second FIX_REQUIRED (R1-R3) on the shared TEST_ONLY world: Red before the fix, Green after. R4 is in <c>ArchiveTests</c>.</summary>
public sealed partial class T11CertificationTests
{
    /// <summary>Whether <paramref name="mutate"/> changed something in place (false: the collection refused the change).</summary>
    private static bool Mutated(Action mutate)
    {
        try
        {
            mutate();
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
    }

    // ---------------------------------------------------------------- R1

    [Fact]
    public void R1_a_verified_result_cannot_be_changed_in_place_and_issuance_uses_exactly_the_verification_snapshot()
    {
        var (o, _, _, _, _) = Certified();
        var calRef = o.Production.Family(w.Calibration, "clause-cal-test", "calibration-result.json").Reference;
        var v = Certification.VerifyFamily(o.Session, "fc3-cal", calRef);
        var canonical = v.Payload.Canonical.ToArray();
        var sources = Jcs.Bytes(J.A(v.SourceFiles.Cast<JNode>().ToArray()));
        var document = Jcs.Bytes(v.Document);

        // the collections of a verified result refuse every in-place change (SourceFiles add / remove, a source file's raw SHA, a nested document array)
        Assert.False(Mutated(() => ((IList<JObject>)v.SourceFiles).Add(v.SourceFiles[0])), "SourceFiles add");
        Assert.False(Mutated(() => ((IList<JObject>)v.SourceFiles).RemoveAt(0)), "SourceFiles remove");
        var sha = v.SourceFiles[0].Members.Select((m, i) => (m, i)).Single(x => x.m.Key == "raw_sha256").i;
        Assert.False(Mutated(() => ((IList<KeyValuePair<string, JNode>>)v.SourceFiles[0].Members)[sha] = new("raw_sha256", J.S(new string('0', 64)))), "raw_sha256");
        Assert.False(Mutated(() => ((IList<JNode>)((JArray)v.Document["units"]).Items)[0] = J.N(1)), "nested JArray");
        Assert.False(Mutated(() => ((IList<KeyValuePair<string, JNode>>)((JObject)v.Document["execution"]).Members)[0] = new("format", J.S("x"))), "nested JObject");
        Assert.Equal(sources, Jcs.Bytes(J.A(v.SourceFiles.Cast<JNode>().ToArray())));
        Assert.Equal(document, Jcs.Bytes(v.Document));

        // the canonical bytes are an array: a change after verification is refused at issuance, never issued with the verified A+
        v.Payload.Canonical[0] ^= 0x01;
        Assert.Equal(("Evidence.not_certifiable", "Certification.result_mutated"), (Refused(() => Certification.IssueFamily(o.Session, v)).Code, Refused(() => Certification.IssueFamily(o.Session, v)).Cause));

        // an untouched result: the receipt carries exactly the snapshot (CANONICAL_PAYLOAD bytes, A+ of those bytes, source files)
        var clean = Certification.VerifyFamily(o.Session, "fc3-cal", calRef);
        var receipt = Receipts.Validate(Certification.IssueFamily(o.Session, clean).Bytes);
        var files = ((JArray)receipt["source_files"]).Items.Cast<JObject>().ToList();
        var payload = files.Single(f => Schema.Text(f["role"]) == "CANONICAL_PAYLOAD");
        Assert.Equal(canonical, Archive.TryRead(o.Production.ArchiveRoot, Schema.Text(payload["relative_path"])));
        Assert.Equal(EvidenceHash.APlus(EvidenceHash.FamilyPrefix, canonical), Schema.Text(((JObject)((JArray)receipt["targets"]).Items.Single())["evidence_hash"]));
        Assert.Equal(Jcs.Bytes(J.A(clean.SourceFiles.Cast<JNode>().ToArray())),
            Jcs.Bytes(J.A(files.Where(f => Schema.Text(f["origin"]) == "ORIGINAL").Cast<JNode>().ToArray())));
    }

    // ---------------------------------------------------------------- R2

    /// <summary>The session archive's Calibration document re-written with its references edited (a new archive file; nothing else changes).</summary>
    private JObject CalibrationWith(Opened o, Func<List<JObject>, IEnumerable<JObject>> edit)
    {
        var (reference, doc) = o.Production.Family(w.Calibration, "clause-cal-test", "calibration-result.json");
        var refs = ((JArray)((JObject)doc["additional_evidence"])["artifacts"]).Items.Cast<JObject>().ToList();
        var edited = edit(refs).OrderBy(x => Schema.Text(x["artifact_id"]), StringComparer.Ordinal).Cast<JNode>().ToArray();
        _ = reference;
        return EvidenceWriter.WriteDocument(o.Production.ArchiveRoot, (JObject)JEditT.Set(doc, "/additional_evidence/artifacts", J.A(edited)), "FAMILY_RESULT", $"r2-{Guid.NewGuid():N}.json");
    }

    /// <summary>Verified and issued in the session: the cause of the refusal at either step.</summary>
    private string Certify(Opened o, JObject reference) => Refused(() => Certification.IssueFamily(o.Session, Certification.VerifyFamily(o.Session, "fc3-cal", reference))).Cause;

    [Fact]
    public void R2_the_approval_and_build_originals_the_session_used_are_required_even_when_removed_from_the_document_and_the_inventory_together()
    {
        var (o, _, _, _, _) = Certified();
        // removed from the document's references, hence from the snapshot, the ORIGINAL source files and the RAW_INVENTORY at once: the two lists still agree, the receipt must still be refused
        Assert.Equal("Archive.required_original", Certify(o, CalibrationWith(o, r => r.Where(x => Schema.Text(x["role"]) != "APPROVAL_RECORD"))));
        Assert.Equal("Archive.required_original", Certify(o, CalibrationWith(o, r => r.Where(x => Schema.Text(x["role"]) != "BUILD_PROVENANCE"))));
        Assert.Equal("Archive.required_original", Certify(o, CalibrationWith(o, r => r.Where(x => Schema.Text(x["role"]) != "INPUT_BUNDLE"))));

        // an input profile replaced by another file, an extra PREFLIGHT file or a REGRESSION_PROOF named by a family document
        var other = Archive.WriteNew(o.Production.ArchiveRoot, $"r2/{Guid.NewGuid():N}.json", [0x7b, 0x7d], "r2-other-input", "INPUT_BUNDLE");
        Assert.Equal("Archive.required_original", Certify(o, CalibrationWith(o, r => r.Where(x => Schema.Text(x["artifact_id"]) != "input-pzl-test-profile").Append(other))));
        Assert.Equal("Archive.required_original", Certify(o, CalibrationWith(o, r => r.Append(Archive.WriteNew(o.Production.ArchiveRoot, $"r2/{Guid.NewGuid():N}.json", [1], "r2-preflight", "PREFLIGHT")))));
        Assert.Equal("Archive.required_original", Certify(o, CalibrationWith(o, r => r.Append(Archive.WriteNew(o.Production.ArchiveRoot, $"r2/{Guid.NewGuid():N}.json", [1], "r2-proof", "REGRESSION_PROOF")))));

        // positive: the normal document keeps every required original, and its receipt's ORIGINAL list holds exactly the session's own approval / build / input SHAs (R2-A: the two profiles and the
        // two embedded data files the run read)
        var (_, cal, _, calR, _) = Certified();
        var originals = ((JArray)Receipts.Validate(calR.Bytes)["source_files"]).Items.Cast<JObject>().Where(f => Schema.Text(f["origin"]) == "ORIGINAL").ToList();
        Assert.Equal(cal.SourceFiles.Select(f => Jcs.Text(f)), originals.Select(f => Jcs.Text(f)));
        string Sha(string role) => string.Join(",", originals.Where(f => Schema.Text(f["role"]) == role).Select(f => Schema.Text(f["raw_sha256"])).Order(StringComparer.Ordinal));
        Assert.Equal((o.Session.Trust.Approval.RawSha256, o.Session.Trust.Build.Sha), (Sha("APPROVAL_RECORD"), Sha("BUILD_PROVENANCE")));
        Assert.Equal(string.Join(",", new[] { o.Session.Trust.Environment.Sha, o.Session.Trust.TestProfile.Sha }
            .Concat(HarnessData.Inputs.Select(i => Archive.Sha(HarnessData.ResourceBytes(i.Name)))).Order(StringComparer.Ordinal)), Sha("INPUT_BUNDLE"));
        Certification.RequireOriginals(o.Session, originals, null, ["fc3-cal"]);

        // the issuance boundary checks the same against the SESSION's trust, whatever list it is given (defence behind the verifier's own rule)
        string Require(IEnumerable<JObject> list, string? proof = null) => Refused(() => Certification.RequireOriginals(o.Session, list.ToList(), proof, ["fc3-cal"])).Cause;
        Assert.Equal("Certification.required_original", Require(originals.Where(f => Schema.Text(f["role"]) != "APPROVAL_RECORD")));
        Assert.Equal("Certification.required_original", Require(originals.Where(f => Schema.Text(f["role"]) != "BUILD_PROVENANCE")));
        Assert.Equal("Certification.required_original", Require(originals.Select(f => Schema.Text(f["role"]) == "BUILD_PROVENANCE" ? (JObject)JEditT.Set(f, "/raw_sha256", J.S(new string('b', 64))) : f)));
        Assert.Equal("Certification.required_original", Require(originals.Where(f => Schema.Text(f["artifact_id"]) != "input-environment-profile")));
        Assert.Equal("Certification.required_original", Require(originals, new string('c', 64)));        // a Phase without its regression proof
        Assert.Equal("Certification.required_original", Require(originals.Where(f => Schema.Text(f["artifact_id"]) != "input-virtual-tuning-v4")));   // R2-A: a consumed input left out
        Assert.Equal("Certification.required_original", Require(originals.Where(f => Schema.Text(f["role"]) != "PREFLIGHT")));                       // R2-B: no preflight record
        Assert.Equal("Certification.required_original", Require(originals.Where(f => !(Schema.Text(f["role"]) == "PREFLIGHT" && f["unit_id"] is JString { Value: var u } && u == CalibrationStrata.All[0].Id))));   // one unit's record left out
        Assert.Equal("Certification.required_original", Refused(() => Certification.RequireOriginals(o.Session, originals, null, ["fc3-cal", "fc3-int"])).Cause);   // another family's records missing
    }

    [Fact]
    public void R2_a_regression_proof_must_be_the_verifier_s_own_test_profile_run_byte_for_byte()
    {
        var (o, _, _, _, _) = Certified();
        var evidence = ApprovalFormats.TestProfileChecks.ToDictionary(c => c, c => J.O(("profile_id", J.S("pzl-profile-test-1")), ("cases", J.A())), StringComparer.Ordinal);
        var run = new TestProfileRun(o.Session, evidence);
        var proof = Archive.WriteNew(o.Production.ArchiveRoot, $"r2/{Guid.NewGuid():N}.json", TestProfiles.RegressionProof(run), "regression-proof", "REGRESSION_PROOF");
        var verified = Certification.VerifyRegressionProof(o.Session, run, proof);
        Assert.Equal(Jcs.Text(JEditMembers.With(proof, "origin", J.S("ORIGINAL"))), Jcs.Text(verified.Source));
        var otherRun = new TestProfileRun(o.Session, evidence.ToDictionary(e => e.Key, e => e.Key == "pzl-6" ? J.O(("profile_id", J.S("pzl-profile-test-1")), ("cases", J.A(J.S("x")))) : e.Value));
        Assert.Equal("TestProfile.regression_proof_differs", Refused(() => Certification.VerifyRegressionProof(o.Session, otherRun, proof)).Cause);   // another run's result
        Assert.Equal("Archive.required_original", Refused(() => Certification.VerifyRegressionProof(o.Session, run, (JObject)JEditT.Set(proof, "/role", J.S("DIAGNOSTICS")))).Cause);
        Assert.Equal("Archive.raw_mismatch", Refused(() => Certification.VerifyRegressionProof(o.Session, run, (JObject)JEditT.Set(proof, "/raw_sha256", J.S(new string('d', 64))))).Cause);
        Assert.Equal("Certification.test_profile", Refused(() => Certification.VerifyRegressionProof(Open().Session, run, proof)).Cause);   // another session's run
    }

    // ---------------------------------------------------------------- R3

    [Fact]
    public void R3_two_families_of_one_archive_never_name_different_originals_with_one_artifact_id()
    {
        var (_, cal, integration, _, _) = Certified();
        var shared = cal.SourceFiles.Join(integration.SourceFiles, x => Schema.Text(x["artifact_id"]), x => Schema.Text(x["artifact_id"]), (a, b) => (a, b), StringComparer.Ordinal).ToList();
        Assert.All(shared, p =>
        {
            Assert.Equal(Jcs.Text(p.a), Jcs.Text(p.b));                                                  // the same id is the same original (7 fields)
            Assert.Contains(Schema.Text(p.a["role"]), new[] { "APPROVAL_RECORD", "BUILD_PROVENANCE", "INPUT_BUNDLE" });   // of the validation set, never a family's own file
        });
        Assert.Equal(6, shared.Count);   // exactly the validation-set originals are shared: approval, build, four inputs (R2-A: two profiles + two embedded data files)
    }

    [Fact]
    public void R3_the_phase_union_keeps_every_family_s_originals_and_refuses_one_id_for_two_originals()
    {
        static JObject Ref(string id, string role, string path, byte b) => JEditMembers.With(Archive.Reference(id, role, null, null, path, [b]), "origin", J.S("ORIGINAL"));
        var approval = Ref("approval-record", "APPROVAL_RECORD", "trust/approval-record.json", 1);
        var cal = new[] { approval, Ref("calibration-result", "FAMILY_RESULT", "calibration-result.json", 2) };
        var integration = new[] { approval, Ref("integration-result", "FAMILY_RESULT", "integration-result.json", 3) };
        var dom = new[] { approval, Ref("dominance-result", "DOMINANCE_RESULT", "dominance-result.json", 4) };
        string Union(params JObject[][] families) => Refused(() => Certification.PhaseOriginals(families)).Cause;

        // normal: the union of the three, the shared validation-set original once
        Assert.Equal(["approval-record", "calibration-result", "dominance-result", "integration-result"], Certification.PhaseOriginals([cal, integration, dom]).Select(x => Schema.Text(x["artifact_id"])));
        // the Codex path: Calibration and Integration documents with one id (the old role-based id) — one would have been dropped silently
        Assert.Equal("Phase.artifact_id_conflict", Union(cal, [approval, Ref("calibration-result", "FAMILY_RESULT", "integration-result.json", 3)], dom));
        Assert.Equal("Phase.artifact_id_conflict", Union(cal, [approval, Ref("calibration-result", "FAMILY_RESULT", "calibration-result.json", 9)], dom));   // same id and path, another SHA
        Assert.Equal("Phase.artifact_id_conflict", Union(cal, [Ref("approval-record", "APPROVAL_RECORD", "trust/other-approval.json", 1), integration[1]], dom));   // same id, another path
        Assert.Equal("Phase.artifact_id_conflict", Union(cal, [approval, cal[1]], dom));                    // a family's own file named by another family is never merged
        Assert.Equal("Phase.path_conflict", Union(cal, [approval, Ref("integration-result", "FAMILY_RESULT", "calibration-result.json", 2)], dom));   // two ids, one file
    }
}
