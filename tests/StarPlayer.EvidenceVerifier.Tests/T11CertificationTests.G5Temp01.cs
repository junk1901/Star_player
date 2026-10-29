using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using StarPlayer.SimHarness;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.EvidenceVerifier.Tests;

/// <summary>
/// G5-TEMP-01 (C-3): the replay consumes the verification snapshot's own bytes of the run index and of every cell manifest, never a copy it could re-read from a path. Each case uses its own copy of a
/// Dominance archive. Acting "between the snapshot and the replay" uses the archive's check/use seam on this thread only (the verifier runs on the calling thread): before C-3 it fired for every
/// replay copy the verifier wrote to %TEMP%, after C-3 only for the snapshot's own archive reads.
/// T-2/T-3/T-5 a cell manifest that is not the run index's is refused (every cell, not only the first); T-4/T-6a bytes changed in a replay copy never reach the replay; T-6b an archive file changed
/// after its snapshot read is never read again; T-7 a failed binding registers no receipt.
/// </summary>
public sealed partial class T11CertificationTests
{
    private const string DomRun = "run-dom-test";

    private sealed class Undo(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }

    /// <summary>Sets the harness's internal <c>Archive.BetweenCheckAndUse</c> seam for this thread only, so parallel classes are never affected.</summary>
    private static IDisposable OnThisThread(Action<string> hook)
    {
        var seam = typeof(Archive).GetProperty("BetweenCheckAndUse", BindingFlags.Static | BindingFlags.NonPublic)!;
        var thread = Environment.CurrentManagedThreadId;
        seam.SetValue(null, (Action<string>)(path =>
        {
            if (Environment.CurrentManagedThreadId == thread)
                hook(path);
        }));
        return new Undo(() => seam.SetValue(null, null));
    }

    private static string CellManifest(int index) => EvidenceWriter.RunPath(DomRun, DominancePreflight.CellDirectory(DominanceMatrix.Cells[index]) + "/manifest.json");

    private string CopyArchive(string from)
    {
        var to = Path.Combine(w.Root, "g5t01-" + Guid.NewGuid().ToString("N"));
        foreach (var f in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(f).StartsWith("tampered-", StringComparison.Ordinal))   // other tests' documents, never referenced here
                continue;
            var target = Path.Combine(to, Path.GetRelativePath(from, f));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(f, target);
        }

        return to;
    }

    /// <summary>The manifest with one recorded game's result hash changed (same length, still valid JSON; the attestation is untouched).</summary>
    private static byte[] ClaimChanged(byte[] manifest)
    {
        var text = Encoding.UTF8.GetString(manifest);
        var hash = Regex.Match(text, "\"result_hash\"\\s*:\\s*\"([0-9a-f]{64})\"").Groups[1];
        Assert.True(hash.Success);
        return Encoding.UTF8.GetBytes(text[..hash.Index] + (hash.Value[0] == '0' ? '1' : '0') + text[(hash.Index + 1)..]);
    }

    /// <summary>
    /// A copy of <paramref name="archive"/> and a new document reference in it. With <paramref name="cell"/>, that cell's archived manifest carries a changed claim; with <paramref name="rehash"/> the
    /// document's reference to it is re-hashed (a self-consistent archive whose manifest is not the one the run index names).
    /// </summary>
    private (string Archive, JObject Reference, byte[] Good) Copy(string archive, JObject document, int? cell = null, bool rehash = false)
    {
        var copy = CopyArchive(archive);
        byte[] good = [];
        if (cell is { } c)
        {
            var file = Path.Combine([copy, .. CellManifest(c).Split('/')]);
            good = File.ReadAllBytes(file);
            var bad = ClaimChanged(good);
            File.WriteAllBytes(file, bad);
            if (rehash)
            {
                var i = Schema.Items(((JObject)document["additional_evidence"])["artifacts"]).Select(x => Schema.Text(((JObject)x)["relative_path"])).ToList().IndexOf(CellManifest(c));
                Assert.True(i >= 0);
                document = (JObject)JEditT.Set(JEditT.Set(document, $"/additional_evidence/artifacts/{i}/raw_sha256", J.S(Archive.Sha(bad))), $"/additional_evidence/artifacts/{i}/byte_length", J.N(bad.Length));
            }
        }

        return (copy, EvidenceWriter.WriteDocument(copy, document, "DOMINANCE_RESULT", $"dominance-result-{Guid.NewGuid():N}.json"), good);
    }

    private FamilyVerificationRequest Request(string archive, JObject reference) => new(w.Approval, w.Authorization, DominanceMatrix.Family, w.Root, archive, reference);

    private static readonly string ReplayCopies = Path.Combine(Path.GetTempPath(), "starplayer-verifier");

    /// <summary>The verifier's own replay copy written since <paramref name="before"/> (exactly one, or the case is ambiguous and stops).</summary>
    private static string NewReplayCopy(HashSet<string> before)
    {
        var made = Directory.Exists(ReplayCopies) ? Directory.GetDirectories(ReplayCopies).Where(d => !before.Contains(d)).ToList() : [];
        Assert.True(made.Count == 1, $"replay copy is ambiguous ({made.Count})");
        return made[0];
    }

    private static HashSet<string> ExistingReplayCopies() => Directory.Exists(ReplayCopies) ? [.. Directory.GetDirectories(ReplayCopies)] : [];

    /// <summary>
    /// The attack of G5-TEMP-01 as a hook: when the run index is about to be copied for the replay (the cell manifests are already there), the cell's archived (bad) manifest in the replay copy is
    /// replaced by <paramref name="good"/>, the one the index names. Before C-3 the replay then reads a consistent pair the snapshot never had. Returns whether the swap happened.
    /// </summary>
    private static IDisposable SwapBackInReplayCopy(int cell, byte[] good, Action swapped)
    {
        var before = ExistingReplayCopies();
        return OnThisThread(path =>
        {
            if (path != DominanceOfficial.ResultFile)
                return;
            File.WriteAllBytes(Path.Combine([NewReplayCopy(before), .. (DominancePreflight.CellDirectory(DominanceMatrix.Cells[cell]) + "/manifest.json").Split('/')]), good);
            swapped();
        });
    }

    // ---------------------------------------------------------------- T-2 / T-3 / T-5: the archived manifest of every cell is the one the run index names

    [Fact]
    public void G5T01_T2_a_rehashed_cell_manifest_that_is_not_the_index_s_is_refused_before_any_game()
    {
        var t = Copy(w.Archive, w.DominanceDocument, cell: 0, rehash: true);
        var e = Refused(() => Verifier.VerifyFamily(Request(t.Archive, t.Reference)));
        Assert.Equal((EvidenceCodes.HashMismatch, "DomOfficial.cell_manifest_hash_mismatch"), (e.Code, e.Cause));
    }

    [Fact]
    public void G5T01_T3_a_changed_cell_manifest_under_the_original_reference_is_refused_by_the_snapshot()
    {
        var t = Copy(w.Archive, w.DominanceDocument, cell: 0, rehash: false);
        Assert.Equal(EvidenceCodes.ArchiveInvalid, Refused(() => Verifier.VerifyFamily(Request(t.Archive, t.Reference))).Code);
    }

    [Theory]
    [InlineData(23)]
    [InlineData(47)]
    public void G5T01_T5_a_middle_or_last_cell_is_bound_too(int cell)
    {
        var t = Copy(w.Archive, w.DominanceDocument, cell: cell, rehash: true);
        var e = Refused(() => Verifier.VerifyFamily(Request(t.Archive, t.Reference)));
        Assert.Equal((EvidenceCodes.HashMismatch, "DomOfficial.cell_manifest_hash_mismatch"), (e.Code, e.Cause));
        Assert.Contains(DominanceMatrix.Cells[cell].Id, e.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- T-4 / T-6: only the snapshot's bytes reach the replay

    [Fact]
    public void G5T01_T4_a_consistent_pair_swapped_into_a_replay_copy_never_replaces_the_snapshot()
    {
        var t = Copy(w.Archive, w.DominanceDocument, cell: 0, rehash: true);
        var swapped = false;
        EvidenceError e;
        using (SwapBackInReplayCopy(0, t.Good, () => swapped = true))
            e = Refused(() => Verifier.VerifyFamily(Request(t.Archive, t.Reference)));
        Assert.Equal((EvidenceCodes.HashMismatch, "DomOfficial.cell_manifest_hash_mismatch"), (e.Code, e.Cause));
        Assert.False(swapped);                                                                            // there is no replay copy to act on
    }

    [Fact]
    public void G5T01_T6a_a_pristine_snapshot_verifies_whatever_happens_to_a_replay_copy_and_nothing_is_written_for_the_replay()
    {
        var t = Copy(w.Archive, w.DominanceDocument);
        var seen = new List<string>();
        var before = ExistingReplayCopies();
        VerifiedFamily v;
        using (OnThisThread(path =>
        {
            seen.Add(path);
            if (path == DominanceOfficial.ResultFile)                                                    // a replay copy is being completed: corrupt one of its manifests
                File.WriteAllBytes(Path.Combine([NewReplayCopy(before), .. (DominancePreflight.CellDirectory(DominanceMatrix.Cells[5]) + "/manifest.json").Split('/')]), [(byte)'{', (byte)'}']);
        }))
            v = Verifier.VerifyFamily(Request(t.Archive, t.Reference));
        Assert.Equal(EvidenceHash.Dominance(w.DominanceDocument, v.PzlUnits).Hash, v.Payload.Hash);       // T-1: the same payload as the shared world's evidence
        Assert.NotEmpty(seen);                                                                            // the seam saw the snapshot reads
        Assert.All(seen, p => Assert.False(p == DominanceOfficial.ResultFile || p.StartsWith("cells/", StringComparison.Ordinal), $"replay copy written: {p}"));
    }

    [Fact]
    public void G5T01_T6b_an_archive_file_changed_after_its_snapshot_read_is_never_read_again()
    {
        var t = Copy(w.Archive, w.DominanceDocument);
        var (read, changed) = (false, false);
        using (OnThisThread(path =>
        {
            if (read && !changed)                                                                        // the snapshot read of cell 7's manifest has finished: change the archive file
            {
                File.WriteAllBytes(Path.Combine([t.Archive, .. CellManifest(7).Split('/')]), [(byte)'{', (byte)'}']);
                changed = true;
            }

            read |= path == CellManifest(7);                                                             // the seam runs just before each read
        }))
        {
            var v = Verifier.VerifyFamily(Request(t.Archive, t.Reference));
            Assert.Equal(EvidenceHash.Dominance(w.DominanceDocument, v.PzlUnits).Hash, v.Payload.Hash);
        }

        Assert.True(changed);
    }

    // ---------------------------------------------------------------- T-7: no receipt without the binding

    private static long ReceiptCount(string database)
    {
        using var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={database};Mode=ReadOnly;Pooling=False");
        c.Open();
        using var q = c.CreateCommand();
        q.CommandText = "SELECT COUNT(*) FROM registry_entry WHERE entry_kind = 'VERIFICATION_RECEIPT'";
        return (long)q.ExecuteScalar()!;
    }

    [Fact]
    public void G5T01_T7_a_failed_binding_registers_no_receipt_even_when_a_replay_copy_is_swapped_back()
    {
        var trust = new TestOnlyTrust(w);
        var registry = w.Own(trust.Open(RegistryRole.VerifierService, TestOnlyTrust.Verifier));
        var production = w.ProducerFor(trust);
        var (_, document) = production.Dominance();
        var t = Copy(production.ArchiveRoot, document, cell: 0, rehash: true);
        Certification.VariablesSeam = () => new System.Collections.Hashtable();
        CertificationSession session;
        try
        {
            session = Certification.Open(trust.Anchor, registry, trust.Approval.Id, trust.Approval.RawSha256, "auth-test-1", w.Root, t.Archive);
        }
        finally
        {
            Certification.VariablesSeam = null;
        }

        string? Certify()
        {
            try
            {
                Certification.IssueFamily(session, Certification.VerifyFamily(session, "fc3-dom", t.Reference));
                return null;
            }
            catch (EvidenceException e)
            {
                return e.Error.Code;
            }
        }

        var plain = Certify();                                                                            // the plain mismatch
        Assert.Equal((EvidenceCodes.HashMismatch, 0L), (plain, ReceiptCount(trust.DatabasePath)));
        string? swapped;
        using (SwapBackInReplayCopy(0, t.Good, () => { }))
            swapped = Certify();                                                                          // and the G5-TEMP-01 swap
        Assert.Equal((EvidenceCodes.HashMismatch, 0L), (swapped, ReceiptCount(trust.DatabasePath)));
    }
}
