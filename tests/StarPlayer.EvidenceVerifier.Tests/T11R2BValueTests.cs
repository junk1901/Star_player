using System.Text.Json;
using System.Text.Json.Nodes;
using StarPlayer.SimHarness;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.EvidenceVerifier.Tests;

/// <summary>
/// Codex B1 T11 R2-B MINOR (value binding): a preflight record whose deterministic field was changed and whose raw SHA, byte length and ArtifactRef were re-made consistently is refused by the CONTENT
/// comparison with the verifier's own re-run (not by a hash mismatch); the untouched copy passes.
/// </summary>
public sealed partial class T11CertificationTests
{
    /// <summary>A session of this trust on another archive root (the copied evidence).</summary>
    private CertificationSession SessionOn(Opened o, string archive)
    {
        Certification.VariablesSeam = () => new System.Collections.Hashtable();
        try
        {
            return Certification.Open(o.Trust.Anchor, o.Registry, o.Trust.Approval.Id, o.Trust.Approval.RawSha256, "auth-test-1", w.Root, archive);
        }
        finally
        {
            Certification.VariablesSeam = null;
        }
    }

    /// <summary>
    /// The family document at <paramref name="documentReference"/> and every file below it copied into a NEW archive unchanged, except the PREFLIGHT record <paramref name="target"/>, whose bytes are
    /// <paramref name="edit"/>ed (re-serialized as the producer does) and whose raw SHA, byte length and reference are re-made from the new bytes; the document is re-written with that reference.
    /// </summary>
    private (string Archive, JObject Reference) Copy(Opened o, JObject documentReference, Func<JObject, bool> target, Action<JsonNode>? edit)
    {
        var source = o.Production.ArchiveRoot;
        var archive = Path.Combine(w.Root, "r2b-" + Guid.NewGuid().ToString("N"));
        JObject Refs(JObject d) => (JObject)d["additional_evidence"];
        var doc = (JObject)StrictJson.Parse(Archive.TryRead(source, Schema.Text(documentReference["relative_path"]))!);
        var copied = new HashSet<string>(StringComparer.Ordinal);
        JObject Put(JObject r, byte[]? bytes = null)
        {
            var path = Schema.Text(r["relative_path"]);
            var b = bytes ?? Archive.TryRead(source, path)!;
            copied.Add(path);
            return Archive.WriteNew(archive, path, b, Schema.Text(r["artifact_id"]), Schema.Text(r["role"]), r["unit_id"] is JString u ? u.Value : null, r["game_id"] is JString g ? g.Value : null);
        }

        void Below(JObject r)
        {
            if (copied.Contains(Schema.Text(r["relative_path"])))
                return;
            Put(r);
            if (Schema.Text(r["role"]) is "UNIT_PZL" or "PZL6" && r["game_id"] is JNull)
                foreach (var x in ((JArray)Refs((JObject)StrictJson.Parse(Archive.TryRead(source, Schema.Text(r["relative_path"]))!))["artifacts"]).Items.Cast<JObject>())
                    Below(x);
        }

        var refs = new List<JNode>();
        foreach (var r in ((JArray)Refs(doc)["artifacts"]).Items.Cast<JObject>())
        {
            if (target(r))
            {
                var node = JsonNode.Parse(Archive.TryRead(source, Schema.Text(r["relative_path"]))!)!;
                edit?.Invoke(node);
                refs.Add(Put(r, Utf8(JsonSerializer.Serialize(node, RunReport.Json))));
            }
            else
            {
                Below(r);
                refs.Add(r);
            }
        }

        var rewritten = (JObject)JEditT.Set(doc, "/additional_evidence/artifacts", J.A(refs.ToArray()));
        return (archive, EvidenceWriter.WriteDocument(archive, rewritten, Schema.Text(documentReference["role"]), $"copied-{Guid.NewGuid():N}.json"));
    }

    private static byte[] Utf8(string text) => new System.Text.UTF8Encoding(false).GetBytes(text);

    private static bool FamilyPreflight(JObject r) => Is(r, "PREFLIGHT") && r["unit_id"] is JNull;

    /// <summary>Verified and issued in a session on the copy: the refusal cause (null when the receipt is issued).</summary>
    private string? CertifyCopy(Opened o, string family, JObject reference, Func<JObject, bool> target, Action<JsonNode>? edit)
    {
        var (archive, docRef) = Copy(o, reference, target, edit);
        var session = SessionOn(o, archive);
        try
        {
            Certification.IssueFamily(session, Certification.VerifyFamily(session, family, docRef));
            return null;
        }
        catch (EvidenceException e)
        {
            return e.Error.Cause;
        }
    }

    [Fact]
    public void R2B_value_a_changed_deterministic_calibration_preflight_field_with_a_re_made_sha_is_refused_by_the_content_comparison()
    {
        var (o, _, _, _, _) = Certified();
        var reference = o.Production.Family(w.Calibration, "clause-cal-test", "calibration-result.json").Reference;
        string? Cause(Action<JsonNode>? edit, Func<JObject, bool>? target = null) => CertifyCopy(o, "fc3-cal", reference, target ?? FamilyPreflight, edit);

        Assert.Null(Cause(null));                                                                           // the untouched copy (re-serialized as produced) verifies and issues
        foreach (var (what, edit) in new (string, Action<JsonNode>)[]
                 {
                     ("schema_status", n => n["schema_status"] = "APPROVED"),
                     ("official_evidence true", n => n["official_evidence"] = true),
                     ("official_evidence as a string", n => n["official_evidence"] = "false"),
                     ("official_evidence_note", n => n["official_evidence_note"] = "변경된 설명"),
                     ("complete true", n => n["complete"] = true),
                     ("complete as a number", n => n["complete"] = 0),
                     ("family_gate", n => n["family_gate"] = "PASS"),
                     ("family_gate_reason", n => n["family_gate_reason"] = "변경"),
                     ("nested unit seed path", n => n["preflight"]!["units"]![0]!["seed_manifest_path"] = "seeds/other.json"),
                     ("a member added", n => n["extra"] = 1),
                     ("a member removed", n => ((JsonObject)n).Remove("complete")),
                 })
            Assert.True(Cause(edit) == "Preflight.record_differs", $"{what}: {Cause(edit)}");

        // a unit record's nested deterministic value (a check message the re-run produces exactly)
        var unit = OfficialFamilyPlan.Calibration.Units[0].Id;
        Assert.Equal("Preflight.record_differs", Cause(n => n["checks"]![2]!["message"] = "HEAD = 다른 SHA", r => Is(r, "PREFLIGHT") && OfUnit(r, unit)));
    }

    [Fact]
    public void R2B_value_a_changed_dominance_games_per_cell_or_gate_with_a_re_made_sha_is_refused_by_the_content_comparison()
    {
        var (o, _, _, _, _) = Certified();
        var reference = o.Production.Dominance().Reference;
        string? Cause(Action<JsonNode>? edit) => CertifyCopy(o, "fc3-dom", reference, FamilyPreflight, edit);
        Assert.Equal("Preflight.record_differs", Cause(n => n["preflight"]!["games_per_cell"] = 3));
        Assert.Equal("Preflight.record_differs", Cause(n => n["preflight"]!["games_per_cell"] = "2"));      // the number as a string
        Assert.Equal("Preflight.record_differs", Cause(n => n["dominance_gate"] = "PASS"));                 // NOT_EVALUATED → PASS
        Assert.Equal("Preflight.record_differs", Cause(n => n["dominance_gate_reason"] = "변경"));
    }
}
