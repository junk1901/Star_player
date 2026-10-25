using StarPlayer.EvidenceVerifier;
using StarPlayer.EvidenceVerifier.Tests;
using StarPlayer.Infrastructure.Fixtures;
using StarPlayer.SimHarness;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// B1-T05 / T06b: official PZL #6 with the real event-format-2 JSON codec on the TEST_ONLY world's Integration run — the fixed 20 games (first 10 per mode, never replaced) round-trip, rewrite, replay
/// and re-run identically; the whole Integration evidence (units, bundles, #6, document) then verifies end to end and its payload is the A+ of the verified documents, with the injected internal codec and with
/// the verifier's own binding of the public entry point (OWNER decision 2).
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class OfficialPzl6VerificationTests(TestOnlyWorld w) : IClassFixture<TestOnlyWorld>
{
    private static readonly EventJsonCodec Codec = new(e => ScriptedGameJson.WriteEventsV2(e), ScriptedGameJson.ReadEventsV2);

    [Fact]
    public void The_fixed_twenty_game_sample_round_trips_and_the_integration_evidence_verifies_end_to_end()
    {
        Assert.Null(Pzl6Verifier.CoverageGap());                                         // every GameState member is compared
        var (reference, doc, pzl6) = w.ProduceIntegration(Codec);
        Assert.Equal("PASS", Schema.Text(((JObject)pzl6["check"])["status"]));
        Assert.All(((JArray)pzl6["samples"]).Items.Cast<JObject>(), s => Assert.All(new[] { "event_sequence_equal", "locations_equal", "receipts_equal", "rewrite_bytes_equal", "final_state_equal",
            "event_hash_equal", "event_replay_match", "seed_replay_match" }, f => Assert.True(((JBool)s[f]).Value, f)));
        Assert.Equal(20, Schema.Long(((JObject)((JObject)pzl6["check"])["coverage"])["completed_games"]));

        var v = Verifier.VerifyFamily(w.FamilyRequest("fc3-int", reference, Codec));
        Assert.True(v.TestOnly);
        Assert.Equal(EvidenceHash.Family(doc, v.PzlUnits, pzl6).Hash, v.Payload.Hash);
        Assert.Equal((GateText.Of(w.Integration.Evaluation!.Gate), "COMPLETED"), (v.StatisticalStatus ?? "NOT_EVALUATED", v.TechnicalStatus ?? "COMPLETED"));

        if (Environment.GetEnvironmentVariable("B1_GOLDEN_EXPORT") is { Length: > 0 } dir)   // one-off freeze of the independent Golden inputs
        {
            Directory.CreateDirectory(Path.Combine(dir, "int"));
            File.WriteAllBytes(Path.Combine(dir, "int", "document.json"), Jcs.Bytes(v.Document));
            File.WriteAllBytes(Path.Combine(dir, "int", "pzl6.json"), Jcs.Bytes(v.Pzl6!));
            for (var i = 0; i < v.PzlUnits.Count; i++)
                File.WriteAllBytes(Path.Combine(dir, "int", $"unit-{i:D2}.json"), Jcs.Bytes(v.PzlUnits[i]));
        }

        // OWNER decision 2: the verifier binds the same codec itself through EventJsonV2Codec — the same A+ without an injected codec, and the public entry point writes the internal writer's bytes
        Assert.Equal(v.Payload.Hash, Verifier.VerifyFamily(w.FamilyRequest("fc3-int", reference)).Payload.Hash);
        var events = new List<StarPlayer.Domain.Baseball.Game.GameEvent>();
        GameExecutor.Play(w.Integration.Units[0].Run.Config, w.Integration.Units[0].Run.Specs[0], default, c => events.Add(c.Event));
        var json = ScriptedGameJson.WriteEventsV2(events);
        Assert.Equal(json, EventJsonV2Codec.Write(events));
        Assert.Equal(EventJsonV2Codec.Write(ScriptedGameJson.ReadEventsV2(json).Value), EventJsonV2Codec.Write(EventJsonV2Codec.Read(json).Value));

        // ---- the Phase root through the production boundary (B1-T11-01/02): a session (trust bootstrap + E2-1/E2-4/E2-5), the three families verified in it, its OWN Test Profile run (child dotnet
        //      test, TRX read back), the Family receipts it can issue, and the Phase refused at the child binding
        var trust = new TestOnlyTrust(w, insufficientSampleAllowed: true);                              // this world's SEEDS_EXHAUSTED Calibration is then certifiable as TEST_ONLY (§19)
        using var registry = trust.Open(RegistryRole.VerifierService, TestOnlyTrust.Verifier);
        var production = w.ProducerFor(trust);
        CertificationSession session;
        Certification.VariablesSeam = () => new System.Collections.Hashtable();                     // a dotnet test host carries DOTNET_ variables; E2-1 itself: CertificationBoundaryTests
        try
        {
            session = Certification.Open(trust.Anchor, registry, trust.Approval.Id, trust.Approval.RawSha256, "auth-test-1", w.Root, production.ArchiveRoot);
        }
        finally
        {
            Certification.VariablesSeam = null;
        }

        Assert.True(session.TestOnly);
        var cal = Certification.VerifyFamily(session, "fc3-cal", production.Family(w.Calibration, "clause-cal-test", "calibration-result.json").Reference);
        var integration = Certification.VerifyFamily(session, "fc3-int", production.Integration(Codec).Reference, Codec);
        var dom = Certification.VerifyFamily(session, "fc3-dom", production.Dominance().Reference);
        var run = Certification.RunTestProfile(session, TestOnlyWorld.RepositoryRoot, Path.Combine(w.Root, "test-profile"));
        Assert.All(run.Evidence.Values, e => Assert.True(TestProfiles.AllPass(e)));                     // every §12.2 case Passed in the verifier's own run
        var phase = PhaseVerifier.Payload(cal, integration, dom, run.Evidence);
        var checks = Schema.Items(phase.Payload["checks"]).Cast<JObject>().ToList();
        Assert.Equal(EvidenceHash.PhaseCheckIds, checks.Select(c => Schema.Text(c["id"])));
        Assert.All(checks.Take(12), c => Assert.Equal("PASS", Schema.Text(c["status"])));
        Assert.Equal("DEFERRED", Schema.Text(checks[12]["status"]));
        Assert.Equal(EvidenceHash.APlus(EvidenceHash.PhasePrefix, phase.Canonical), phase.Hash);

        // §19: every family is certifiable here — Calibration INSUFFICIENT_SAMPLE with the allowance, Integration PASS, Dominance PASS (seed base TestOnlyWorld.DominanceSeedBase) — as TEST_ONLY receipts
        var calReceipt = Certification.IssueFamily(session, cal);
        var intReceipt = Certification.IssueFamily(session, integration);
        var domReceipt = Certification.IssueFamily(session, dom);
        Assert.All(new[] { calReceipt, intReceipt, domReceipt }, r => Assert.Equal(ReceiptState.TestOnlyRegistered, r.State));

        // B1 T11 R2: the submitted REGRESSION_PROOF must be the verifier's own run, byte for byte (§12.3 2)
        var proof = Certification.VerifyRegressionProof(session, run, production.RegressionProof(run));
        var forgedProof = Archive.WriteNew(production.ArchiveRoot, $"trust/forged-proof-{Guid.NewGuid():N}.json", [.. TestProfiles.RegressionProof(run).SkipLast(1), (byte)' '], "forged-proof", "REGRESSION_PROOF");
        Assert.Equal("TestProfile.regression_proof_differs", Assert.Throws<EvidenceException>(() => Certification.VerifyRegressionProof(session, run, forgedProof)).Error.Cause);

        // A28 at the Phase boundary: no children, the same receipt three times, or roles out of order → not certifiable
        string Phase(IReadOnlyList<IssuedReceipt> children) => Assert.Throws<EvidenceException>(() => Certification.IssuePhase(session, cal, integration, dom, children, proof)).Error.Cause;
        Assert.Equal("Phase.children_missing", Phase([]));
        Assert.Equal("Phase.children_duplicate", Phase([intReceipt, intReceipt, intReceipt]));
        Assert.Equal("Phase.child_role", Phase([intReceipt, calReceipt, domReceipt]));

        // B1 T11 R3 positive path: the three verified families, their three receipts and the verified proof issue the Phase receipt
        var phaseReceipt = Certification.IssuePhase(session, cal, integration, dom, [calReceipt, intReceipt, domReceipt], proof);
        Assert.Equal(ReceiptState.TestOnlyRegistered, phaseReceipt.State);
        Assert.Equal(phaseReceipt.Bytes, registry.Get("VERIFICATION_RECEIPT", phaseReceipt.ReceiptId)!.RawBytes);
        var r = Receipts.Validate(phaseReceipt.Bytes);
        var targets = Schema.Items(r["targets"]).Cast<JObject>().ToList();
        Assert.Equal(["CALIBRATION", "INTEGRATION", "DOMINANCE", "PHASE"], targets.Select(t => Schema.Text(t["kind"])));
        Assert.Equal(phase.Hash, Schema.Text(targets[3]["evidence_hash"]));                                          // A+ of the verified Phase payload
        Assert.Equal([calReceipt.ReceiptId, intReceipt.ReceiptId, domReceipt.ReceiptId], Schema.Items(r["child_receipt_ids"]).Select(Schema.Text));
        // every family's every original is in the Phase source files (none lost to an id collision), plus the proof; the shared validation-set originals once each
        var originals = Schema.Items(r["source_files"]).Cast<JObject>().Where(f => Schema.Text(f["origin"]) == "ORIGINAL").ToList();
        var ids = originals.Select(f => Schema.Text(f["artifact_id"])).ToList();
        Assert.All(new[] { cal, integration, dom }.SelectMany(f => f.SourceFiles), s => Assert.Contains(Jcs.Text(s), originals.Select(o => Jcs.Text(o))));
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(1, originals.Count(f => Schema.Text(f["role"]) == "REGRESSION_PROOF"));
        // B1 T11 R2-A: four consumed inputs (two profiles, two embedded data files) shared once; R2-B: the preflight records of all three families (Family 1 + one per unit each: 5 + 3 + 49)
        Assert.Equal((1, 1, 4), (originals.Count(f => Schema.Text(f["role"]) == "APPROVAL_RECORD"), originals.Count(f => Schema.Text(f["role"]) == "BUILD_PROVENANCE"),
            originals.Count(f => Schema.Text(f["role"]) == "INPUT_BUNDLE")));
        Assert.Equal(OfficialFamilyPlan.Calibration.Units.Count + OfficialFamilyPlan.Integration.Units.Count + DominanceMatrix.Cells.Count + 3, originals.Count(f => Schema.Text(f["role"]) == "PREFLIGHT"));
        Assert.Equal(3, originals.Count(f => Schema.Text(f["role"]) is "FAMILY_RESULT" or "DOMINANCE_RESULT"));
        Receipts.CheckInventory(r, production.ArchiveRoot);                                                          // its RAW_INVENTORY is exactly these originals

        // the Phase boundary refuses an ORIGINAL list missing a family's document, a consumed input or a family's preflight records (against the session's trust and the three plans)
        var proofSha = Schema.Text(proof.Source["raw_sha256"]);
        string[] families = ["fc3-cal", "fc3-int", "fc3-dom"];
        Certification.RequireOriginals(session, originals, proofSha, families);
        string Without(Func<JObject, bool> drop) => Assert.Throws<EvidenceException>(() => Certification.RequireOriginals(session, originals.Where(f => !drop(f)).ToList(), proofSha, families)).Error.Cause;
        Assert.Equal("Certification.required_original", Without(f => Schema.Text(f["artifact_id"]) == "input-virtual-tuning-v4"));
        Assert.Equal("Certification.required_original", Without(f => Schema.Text(f["role"]) == "PREFLIGHT" && f["unit_id"] is JString { Value: "automated" }));
        Assert.Equal("Certification.required_original", Without(f => Schema.Text(f["role"]) == "PREFLIGHT" && f["unit_id"] is JNull && Schema.Text(f["artifact_id"]) == "preflight-run-dom-test"));
        Assert.Equal("Certification.required_original", Assert.Throws<EvidenceException>(() => Certification.RequireOriginals(session, originals, proofSha, ["fc3-cal", "fc3-int"])).Error.Cause);
        Assert.Equal("Phase.artifact_id_conflict", Assert.Throws<EvidenceException>(() => Certification.PhaseOriginals([cal.SourceFiles,
            [.. integration.SourceFiles.Select(f => Schema.Text(f["artifact_id"]) == "input-virtual-tuning-v4" ? JEditMembers.With(f, "raw_sha256", J.S(new string('9', 64))) : f)], dom.SourceFiles])).Error.Cause);
    }
}
