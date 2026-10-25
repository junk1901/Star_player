using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.EvidenceVerifier.Tests;

/// <summary>
/// B1-T05 with OWNER decision 2: the verifier alone (this project references no Infrastructure internals) binds the real event-format-2 codec through <c>EventJsonV2Codec</c> and verifies the Integration
/// evidence with its #6 document; a #6 document produced with a broken codec, or a verification run with one, is a <c>pzl_mismatch</c>.
/// </summary>
public sealed class Pzl6StandaloneTests(TestOnlyWorld w) : IClassFixture<TestOnlyWorld>
{
    /// <summary>A codec whose reader silently drops the last event (what a copied, drifting codec could do).</summary>
    private static readonly EventJsonCodec Dropping = new(EventJsonCodec.Infrastructure.Write, json =>
    {
        var r = EventJsonCodec.Infrastructure.Read(json);
        return r.IsSuccess ? Result<IReadOnlyList<GameEvent>>.Success(r.Value.Take(r.Value.Count - 1).ToList()) : r;
    });

    [Fact]
    public void The_verifier_binds_the_real_codec_itself_and_the_integration_evidence_with_its_pzl6_verifies()
    {
        var (reference, doc, pzl6) = w.ProduceIntegration();
        Assert.Equal("PASS", Schema.Text(((JObject)pzl6["check"])["status"]));
        var v = Verifier.VerifyFamily(w.FamilyRequest("fc3-int", reference));                              // no codec passed: the verifier's own binding
        Assert.Equal(EvidenceHash.Family(doc, v.PzlUnits, pzl6).Hash, v.Payload.Hash);
        Assert.Equal(Jcs.Bytes(pzl6), Jcs.Bytes(v.Pzl6!));
    }

    [Fact]
    public void A_pzl6_document_made_with_a_broken_codec_or_a_verification_with_one_is_a_pzl_mismatch()
    {
        // the producer used a codec that loses an event: its own #6 says FAIL, the verifier's real codec recomputes PASS → the documents differ
        var (forged, _, bad) = w.ProduceIntegration(Dropping, "integration-result-dropping.json");
        Assert.Equal("FAIL", Schema.Text(((JObject)bad["check"])["status"]));
        var e = Assert.Throws<EvidenceException>(() => Verifier.VerifyFamily(w.FamilyRequest("fc3-int", forged))).Error;
        Assert.Equal((EvidenceCodes.PzlMismatch, "Evidence.pzl6_differs"), (e.Code, e.Cause));
        // a correct document checked with a broken codec does not pass either: #6 is recomputed, never taken from the document
        var (reference, _, _) = w.ProduceIntegration(fileName: "integration-result-good.json");
        var e2 = Assert.Throws<EvidenceException>(() => Verifier.VerifyFamily(w.FamilyRequest("fc3-int", reference, Dropping))).Error;
        Assert.Equal((EvidenceCodes.PzlMismatch, "Evidence.pzl6_differs"), (e2.Code, e2.Cause));
    }
}
