using StarPlayer.SimHarness;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.EvidenceVerifier.Tests;

/// <summary>B1-T10 direct counterexamples on the TEST_ONLY world: A04 (a self-consistent rewrite of seed, GameId, ConfigHash and attestation), N20 (a pin mismatch refuses execution and verification, cause kept), N22-b (AMPV's classification table on a real report).</summary>
public sealed class VerifierCounterexampleTests(TestOnlyWorld w) : IClassFixture<TestOnlyWorld>
{
    private static EvidenceError Refused(Action a) => Assert.Throws<EvidenceException>(a).Error;

    private JObject Archived(JObject doc) => EvidenceWriter.WriteDocument(w.Archive, doc, "DOMINANCE_RESULT", $"tampered-{Guid.NewGuid():N}.json");

    [Fact]
    public void A04_seed_game_id_config_hash_and_attestation_rewritten_together_are_refused_against_the_external_plan()
    {
        var doc = w.DominanceDocument;
        var seed = J.S("123456789");
        var gameId = J.S("game:fc3-dom-sa-nb:neutral-v1-999999");
        var config = J.S(new string('c', 64));
        foreach (var (p, v) in new (string, JNode)[]
                 {
                     ("/units/0/inputs/games/0/seed", seed), ("/units/0/inputs/games/0/game_id", gameId), ("/units/0/technical/games/0/seed", seed), ("/units/0/technical/games/0/game_id", gameId),
                     ("/units/0/inputs/attestation/config_hash", config), ("/units/0/technical/config_hash", config), ("/units/0/inputs/attestation_hash", J.S(new string('d', 64))),
                 })
            doc = (JObject)JEditT.Set(doc, p, v);
        var e = Refused(() => Verifier.VerifyFamily(w.DominanceRequest(reference: Archived(doc))));
        Assert.Equal(EvidenceCodes.ApprovalMismatch, e.Code);
    }

    [Fact]
    public void A12_A13_a_snapshot_changed_only_outside_the_payload_keeps_the_a_plus_but_not_the_raw_list_so_an_old_receipt_does_not_fit()
    {
        var first = Verifier.VerifyFamily(w.DominanceRequest());
        var copy = EvidenceWriter.WriteDocument(w.Archive, w.DominanceDocument, "DOMINANCE_RESULT", $"dominance-result-{Guid.NewGuid():N}.json");   // same bytes, another archive file
        var second = Verifier.VerifyFamily(w.DominanceRequest(reference: copy));
        Assert.Equal(first.Payload.Hash, second.Payload.Hash);                                             // A12: A+ unchanged
        // ... the Raw list differs, so the receipts differ and the first receipt's source_files do not describe the second snapshot (A13)
        Assert.NotEqual(Jcs.Text(J.A(first.SourceFiles)), Jcs.Text(J.A(second.SourceFiles)));
    }

    [Fact]
    public void N20_a_pin_mismatch_refuses_both_execution_and_verification_and_keeps_the_seed_cause()
    {
        var changed = (JObject)JEditT.Set(w.Approval.Record, "/families/2/units/0/seed_manifest_sha256", J.S(new string('3', 64)));
        var a = new ApprovalRecordV1(changed, Jcs.Bytes(changed), Archive.Sha(Jcs.Bytes(changed)));
        var verify = Refused(() => Verifier.VerifyFamily(w.DominanceRequest(approval: a)));
        Assert.Equal((EvidenceCodes.ApprovalMismatch, "Seed.file_hash_mismatch"), (verify.Code, verify.Cause));
        // the producer side with the same pins (even a READY test instance) refuses before any game
        var ready = ApprovalFormats.ToOfficialApprovals(a, executionPathReady: true);
        var pre = DominancePreflight.Run(new DominanceOfficialRequest(w.Root, $"run-n20-{Guid.NewGuid():N}", null), ready, GitIntegrityPolicy.Evaluate([], TestOnlyWorld.Head));
        Assert.False(pre.Ready);
        Assert.Contains(pre.Checks, c => c.Code == "Seed.file_hash_mismatch");
    }

    [Fact]
    public void N22b_ampv_passes_when_only_execution_only_checks_are_not_passing_and_fails_on_any_evaluated_check()
    {
        var path = Schema.Text(w.Approval.Units("fc3-dom")[0]["seed_manifest_path"]);
        var a = ApprovalFormats.ToOfficialApprovals(w.Approval);
        var report = OfficialPreflight.Run(new OfficialRequest(w.Root, path, "run-dom-test", null), a, GitIntegrityPolicy.Evaluate([], w.Approval.GitCommitSha));
        string[] executionOnly = ["cli", "git_integrity", "git_sha", "output_directory"];
        Assert.Contains(report.Checks, c => executionOnly.Contains(c.Id) && c.Status != CheckStatus.Pass);   // the run directory already exists: output_directory does not pass
        Assert.Contains(report.Checks, c => c.Id == "execution_path" && c.Status == CheckStatus.NotReady);    // instance A is never ready
        Assert.Null(Verifier.Ampv(w.Approval, "fc3-dom", w.Root));                                            // ... and AMPV passes
        var changed = (JObject)JEditT.Set(w.Approval.Record, "/families/2/units/0/seed_manifest_sha256", J.S(new string('3', 64)));
        Assert.NotNull(Verifier.Ampv(new ApprovalRecordV1(changed, Jcs.Bytes(changed), Archive.Sha(Jcs.Bytes(changed))), "fc3-dom", w.Root));   // an evaluated check fails → AMPV fails
    }
}
