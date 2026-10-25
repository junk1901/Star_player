using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StarPlayer.SimHarness;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.EvidenceVerifier.Tests;

/// <summary>Codex B1 T11 R2 residual (R2-A INPUT_BUNDLE = every consumed input, R2-B PREFLIGHT = the Family and Unit preflight records) on the shared TEST_ONLY world: Red before the fix, Green after.</summary>
public sealed partial class T11CertificationTests
{
    private static readonly string[] ResourceInputs = ["input-virtual-tuning-v4", "input-game-rules-profiles-v2"];

    private static bool Is(JObject x, string role) => Schema.Text(x["role"]) == role;

    private static bool OfUnit(JObject x, string unit) => x["unit_id"] is JString { Value: var u } && u == unit;

    // ---------------------------------------------------------------- R2-A

    [Fact]
    public void R2A_the_two_profiles_without_the_consumed_tuning_and_rules_inputs_are_refused()
    {
        var (o, _, _, _, _) = Certified();
        Assert.Equal("Archive.required_original", Certify(o, CalibrationWith(o, r => r.Where(x => !ResourceInputs.Contains(Schema.Text(x["artifact_id"]))))));
    }

    [Fact]
    public void R2A_every_consumed_input_is_required_with_the_bytes_the_run_read_and_nothing_else_is_accepted()
    {
        var (o, cal, _, calR, _) = Certified();
        var archive = o.Production.ArchiveRoot;
        // positive: the four inputs (two profiles, two embedded data files) are the document's, the receipt's and its inventory's
        var inputs = cal.SourceFiles.Where(x => Is(x, "INPUT_BUNDLE")).Select(x => Schema.Text(x["artifact_id"])).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["input-environment-profile", "input-game-rules-profiles-v2", "input-pzl-test-profile", "input-virtual-tuning-v4"], inputs);
        var receipt = Receipts.Validate(calR.Bytes);
        Receipts.CheckInventory(receipt, archive);
        Assert.Equal(4, ((JArray)receipt["source_files"]).Items.Cast<JObject>().Count(x => Is(x, "INPUT_BUNDLE") && Schema.Text(x["origin"]) == "ORIGINAL"));

        // one consumed input missing (removed from the document, the snapshot and the inventory at once)
        Assert.Equal("Archive.required_original", Certify(o, CalibrationWith(o, r => r.Where(x => Schema.Text(x["artifact_id"]) != "input-game-rules-profiles-v2"))));
        // its raw SHA changed in the reference
        Assert.Equal("Archive.raw_mismatch", Certify(o, CalibrationWith(o, r => r.Select(x => Schema.Text(x["artifact_id"]) == "input-virtual-tuning-v4" ? (JObject)JEditT.Set(x, "/raw_sha256", J.S(new string('e', 64))) : x))));
        // the same identifier naming other bytes (the Git blob of the tuning file: LF, not the CRLF bytes the run read)
        var blob = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(HarnessData.ResourceBytes(HarnessData.TuningResource)).Replace("\r\n", "\n"));
        Assert.NotEqual(blob, HarnessData.ResourceBytes(HarnessData.TuningResource));
        var other = Archive.WriteNew(archive, $"r2a/{Guid.NewGuid():N}.json", blob, "input-virtual-tuning-v4", "INPUT_BUNDLE");
        Assert.Equal("Archive.required_original", Certify(o, CalibrationWith(o, r => r.Where(x => Schema.Text(x["artifact_id"]) != "input-virtual-tuning-v4").Append(other))));
        // an input outside the derived set added
        var extra = Archive.WriteNew(archive, $"r2a/{Guid.NewGuid():N}.json", Encoding.UTF8.GetBytes("{}"), "input-extra", "INPUT_BUNDLE");
        Assert.Equal("Archive.required_original", Certify(o, CalibrationWith(o, r => r.Append(extra))));
        // the same input twice under two ids
        var twice = Archive.WriteNew(archive, $"r2a/{Guid.NewGuid():N}.json", HarnessData.ResourceBytes(HarnessData.RulesResource), "input-rules-again", "INPUT_BUNDLE");
        Assert.Equal("Archive.required_original", Certify(o, CalibrationWith(o, r => r.Append(twice))));
    }

    [Fact]
    public void R2A_an_embedded_input_must_be_its_repository_original_byte_for_byte()
    {
        ConsumedInputs.CheckSources(w.Root, "fc3-cal");                                                    // this world's root holds the originals
        string Cause(string root, Func<string, byte[]>? resource = null) => Refused(() => ConsumedInputs.CheckSources(root, "fc3-cal", resource)).Cause;
        Assert.Equal("Input.embedded_differs", Cause(w.Root, n => n == HarnessData.RulesResource ? [.. HarnessData.ResourceBytes(n), (byte)'\n'] : HarnessData.ResourceBytes(n)));   // the resource differs
        var root = Path.Combine(w.Root, "r2a-root-" + Guid.NewGuid().ToString("N"));
        foreach (var (_, path) in HarnessData.Inputs)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, path))!);
            File.Copy(Path.Combine(w.Root, path), Path.Combine(root, path));
        }

        ConsumedInputs.CheckSources(root, "fc3-cal");
        var tuning = Path.Combine([root, .. HarnessData.Inputs.Single(i => i.Name == HarnessData.TuningResource).RepositoryPath.Split('/')]);
        File.WriteAllText(tuning, File.ReadAllText(tuning).Replace("\r\n", "\n"));                          // the original file differs from what was embedded
        Assert.Equal("Input.embedded_differs", Cause(root));
        File.Delete(tuning);
        Assert.Equal("Input.embedded_differs", Cause(root));                                                // the original is missing
    }

    // ---------------------------------------------------------------- R2-B

    [Fact]
    public void R2B_a_family_without_its_preflight_records_is_refused()
    {
        var (o, _, _, _, _) = Certified();
        Assert.Equal("Archive.required_original", Certify(o, CalibrationWith(o, r => r.Where(x => Schema.Text(x["role"]) != "PREFLIGHT"))));
    }

    /// <summary>The verifier's preflight comparison over this session's archived Calibration records, with the unit records (and the Family record) edited first.</summary>
    private void Preflight(Opened o, VerifiedFamily cal, Func<List<ArchivedFile>, List<ArchivedFile>>? units = null, Func<ArchivedFile, ArchivedFile>? familyRecord = null)
    {
        ArchivedFile Read(JObject x)
        {
            var plain = new JObject(x.Members.Where(m => m.Key != "origin").ToList());
            return new ArchivedFile(plain, Archive.TryRead(o.Production.ArchiveRoot, Schema.Text(plain["relative_path"]))!);
        }

        var ids = OfficialFamilyPlan.Calibration.Units.Select(u => u.Id).ToList();
        var records = ids.Select(id => Read(cal.SourceFiles.Single(x => Is(x, "PREFLIGHT") && OfUnit(x, id)))).ToList();
        var attestations = ids.Select(id =>
        {
            using var m = JsonDocument.Parse(Read(cal.SourceFiles.Single(x => Is(x, "UNIT_MANIFEST") && OfUnit(x, id))).Bytes);
            return OfficialAttestation.FromJson(m.RootElement.GetProperty("official").GetProperty("attestation")).Value;
        }).ToList();
        var family = Read(cal.SourceFiles.Single(x => Is(x, "PREFLIGHT") && x["unit_id"] is JNull));
        PreflightRecords.Verify(o.Session.Trust.Approval, "fc3-cal", OfficialFamilyPlan.Calibration, "run-cal-test", w.Root, familyRecord?.Invoke(family) ?? family, units?.Invoke(records) ?? records,
            attestations, ids);
    }

    private static ArchivedFile Edited(ArchivedFile f, Action<JsonNode> edit)
    {
        var node = JsonNode.Parse(f.Bytes)!;
        edit(node);
        return f with { Bytes = Encoding.UTF8.GetBytes(node.ToJsonString()) };
    }

    [Fact]
    public void R2B_the_family_and_unit_preflight_records_are_kept_bound_and_compared_with_the_verifier_s_own_preflight()
    {
        var (o, cal, _, calR, _) = Certified();
        var units = OfficialFamilyPlan.Calibration.Units.Select(u => u.Id).ToList();
        // positive: one Family record and one per unit in the document, the receipt and its inventory; the comparison passes
        Assert.Equal(units.Count + 1, cal.SourceFiles.Count(x => Is(x, "PREFLIGHT")));
        Assert.All(units, u => Assert.Single(cal.SourceFiles, x => Is(x, "PREFLIGHT") && OfUnit(x, u)));
        var receipt = Receipts.Validate(calR.Bytes);
        Receipts.CheckInventory(receipt, o.Production.ArchiveRoot);
        Assert.Equal(units.Count + 1, ((JArray)receipt["source_files"]).Items.Cast<JObject>().Count(x => Is(x, "PREFLIGHT") && Schema.Text(x["origin"]) == "ORIGINAL"));
        Preflight(o, cal);

        // archive level: one unit's record missing, an attestation alone (no record at all), the DIAGNOSTICS index alone (records relabelled), a SHA changed
        Assert.Equal("Archive.required_original", Certify(o, CalibrationWith(o, r => r.Where(x => !(Is(x, "PREFLIGHT") && OfUnit(x, units[0]))))));
        Assert.Equal("Archive.required_original", Certify(o, CalibrationWith(o, r => r.Where(x => !Is(x, "PREFLIGHT")))));   // only the UNIT_MANIFEST attestations remain
        Assert.Equal("Archive.required_original", Certify(o, CalibrationWith(o, r => r.Select(x => Is(x, "PREFLIGHT") ? (JObject)JEditT.Set(x, "/role", J.S("DIAGNOSTICS")) : x))));
        Assert.Equal("Archive.raw_mismatch", Certify(o, CalibrationWith(o, r => r.Select(x => Is(x, "PREFLIGHT") && OfUnit(x, units[1]) ? (JObject)JEditT.Set(x, "/raw_sha256", J.S(new string('f', 64))) : x))));
        // another unit's record in a unit's place (by reference: the run path does not match)
        Assert.Equal("Archive.run_file_missing", Certify(o, CalibrationWith(o, r =>
        {
            var second = r.Single(x => Is(x, "PREFLIGHT") && OfUnit(x, units[1]));
            return r.Select(x => Is(x, "PREFLIGHT") && OfUnit(x, units[0]) ? (JObject)JEditT.Set(JEditT.Set(second, "/artifact_id", x["artifact_id"]), "/unit_id", J.S(units[0])) : x);
        })));

        // content level (the comparison itself): another unit's bytes, a changed evaluated check, a NOT_READY check, another attestation, a member added, the Family unit list or status changed
        string Content(Func<List<ArchivedFile>, List<ArchivedFile>>? edit = null, Func<ArchivedFile, ArchivedFile>? family = null) => Refused(() => Preflight(o, cal, edit, family)).Cause;
        Assert.Equal("Preflight.record_differs", Content(r => [r[1] with { Reference = r[0].Reference }, .. r.Skip(1)]));
        Assert.Equal("Preflight.record_differs", Content(r => [Edited(r[0], n => n["checks"]![3]!["message"] = "변조"), .. r.Skip(1)]));
        Assert.Equal("Preflight.record_differs", Content(r => [Edited(r[0], n => n["checks"]![1]!["status"] = "NOT_READY"), .. r.Skip(1)]));
        Assert.Equal("Preflight.record_differs", Content(r => [Edited(r[0], n => n["attestation_hash"] = new string('a', 64)), .. r.Skip(1)]));
        Assert.Equal("Preflight.record_differs", Content(r => [Edited(r[0], n => n["extra"] = 1), .. r.Skip(1)]));
        Assert.Equal("Preflight.record_differs", Content(r => [Edited(r[0], n => ((JsonArray)n["checks"]!).RemoveAt(0)), .. r.Skip(1)]));
        Assert.Equal("Preflight.record_differs", Content(family: f => Edited(f, n => n["run_status"] = "PREFLIGHT_REFUSED")));
        Assert.Equal("Preflight.record_differs", Content(family: f => Edited(f, n => ((JsonArray)n["preflight"]!["units"]!).RemoveAt(0))));
        Assert.Equal("Preflight.record_differs", Content(family: f => Edited(f, n => n["run_id"] = "run-other")));
    }
}
