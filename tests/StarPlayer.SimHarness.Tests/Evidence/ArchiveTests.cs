using System.Diagnostics;
using System.Text;
using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.SimHarness.Tests.Evidence;

/// <summary>B1-T08: one-read snapshots with raw SHA / length (A18, A19, A20), no overwrite, no link, the downward-only DAG (N10), GAME_EVENTS / SEED_MANIFEST cardinality and RAW_INVENTORY without self reference.</summary>
public sealed class ArchiveTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "simharness-tests", "archive-" + Guid.NewGuid().ToString("N"));

    public ArchiveTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var d in Directory.GetDirectories(_root, "*", SearchOption.AllDirectories).Where(d => File.GetAttributes(d).HasFlag(FileAttributes.ReparsePoint)))
            Directory.Delete(d);
        Directory.Delete(_root, true);
    }

    private static string Code(Action a) => Assert.Throws<EvidenceException>(a).Error.Cause;

    [Fact]
    public void A_written_file_is_read_back_once_and_any_byte_change_or_absence_is_archive_invalid()
    {
        var log = Encoding.UTF8.GetBytes("line1\nline2\n");
        var r = Archive.WriteNew(_root, "logs/run.txt", log, "log-1", "DIAGNOSTICS");
        var snap = Archive.Snapshot(_root, [r]);
        Assert.Equal(log, snap["log-1"].Bytes);
        Assert.Equal("Archive.exists", Code(() => Archive.WriteNew(_root, "logs/run.txt", log, "log-1", "DIAGNOSTICS")));   // never overwritten

        // A18 / A19: LF → CRLF changes only the raw SHA, which the snapshot refuses against the old reference
        File.WriteAllBytes(Path.Combine(_root, "logs", "run.txt"), Encoding.UTF8.GetBytes("line1\r\nline2\r\n"));
        Assert.Equal("Archive.raw_mismatch", Code(() => Archive.Snapshot(_root, [r])));
        var crlf = Archive.Reference("log-1", "DIAGNOSTICS", null, null, "logs/run.txt", File.ReadAllBytes(Path.Combine(_root, "logs", "run.txt")));
        Assert.NotEqual(Schema.Text(r["raw_sha256"]), Schema.Text(crlf["raw_sha256"]));
        // A20: a missing file
        File.Delete(Path.Combine(_root, "logs", "run.txt"));
        Assert.Equal("Archive.missing", Code(() => Archive.Snapshot(_root, [r])));
        Assert.Equal("Archive.path_invalid", Code(() => Archive.WriteNew(_root, "../x.txt", log, "x", "DIAGNOSTICS")));
    }

    [Fact]
    public void A_junction_in_the_archive_path_is_refused_for_reading_and_writing()
    {
        var real = Path.Combine(_root, "real");
        Directory.CreateDirectory(real);
        File.WriteAllBytes(Path.Combine(real, "a.txt"), [1, 2, 3]);
        var junction = Path.Combine(_root, "link");
        using (var p = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junction}\" \"{real}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!)
            p.WaitForExit();
        Assert.True(File.GetAttributes(junction).HasFlag(FileAttributes.ReparsePoint), "junction was not created");
        var r = Archive.Reference("a", "DIAGNOSTICS", null, null, "link/a.txt", [1, 2, 3]);
        Assert.Equal("Archive.reparse_point", Code(() => Archive.Snapshot(_root, [r])));
        Assert.Equal("Archive.reparse_point", Code(() => Archive.WriteNew(_root, "link/b.txt", [4], "b", "DIAGNOSTICS")));
    }

    private static JObject WithRefs(JObject doc, params JObject[] refs) => (JObject)JEdit.Set(doc, "/additional_evidence/artifacts", J.A(refs));

    [Fact]
    public void Documents_reference_only_downwards_and_carry_their_own_bundle_and_seed_pins()
    {
        var (run, games) = UnitPzlEvidenceTests.Unit(H.Ok(RunConfig.Create("b1-arch", "official", NeutralCohort.Create(), H.Rules(true), H.Tuning, H.Ok(HarnessData.NeutralPark()),
            "advancement-policy-v2", 3000, competitionKey: "fc3-int-fx-a")), 1);
        var doc = UnitPzlEvidence.Document(OfficialDocumentTests.Ctx, "fc3-int", "automated", new string('f', 40), run, games, []);
        var bundle = Archive.Reference("ev-a", "GAME_EVENTS", "automated", null, "units/automated/game-events.jsonl", [1]);
        var results = Archive.Reference("gr-a", "GAME_RESULTS", "automated", null, "units/automated/game-results.jsonl", [5]);   // OWNER decision 3 §5
        Archive.CheckReferences(EvidenceKind.UnitPzl, WithRefs(doc, bundle, results));
        Assert.Equal("Archive.game_events_count", Code(() => Archive.CheckReferences(EvidenceKind.UnitPzl, WithRefs(doc, results))));
        Assert.Equal("Archive.game_events_count", Code(() => Archive.CheckReferences(EvidenceKind.UnitPzl,
            WithRefs(doc, bundle, Archive.Reference("ev-b", "GAME_EVENTS", "automated", null, "units/automated/b.jsonl", [2]), results))));
        Assert.Equal("Archive.game_results_count", Code(() => Archive.CheckReferences(EvidenceKind.UnitPzl, WithRefs(doc, bundle))));                       // missing
        Assert.Equal("Archive.game_results_count", Code(() => Archive.CheckReferences(EvidenceKind.UnitPzl,
            WithRefs(doc, bundle, results, Archive.Reference("gr-b", "GAME_RESULTS", "automated", null, "units/automated/b-results.jsonl", [6])))));       // two
        Assert.Equal("Archive.game_results_count", Code(() => Archive.CheckReferences(EvidenceKind.UnitPzl,
            WithRefs(doc, bundle, Archive.Reference("gr-a", "GAME_RESULTS", "automated", "game:x", "units/automated/game-results.jsonl", [5])))));           // a game_id
        Assert.Equal("Archive.game_results_count", Code(() => Archive.CheckReferences(EvidenceKind.UnitPzl,
            WithRefs(doc, bundle, Archive.Reference("gr-a", "GAME_RESULTS", "non-automated", null, "units/automated/game-results.jsonl", [5])))));       // another unit's
        // N10: a Unit PZL naming its parent family result
        var upward = Archive.Reference("fam", "FAMILY_RESULT", null, null, "family-result.json", [3]);
        Assert.Equal(EvidenceCodes.ReferenceInvalid, Assert.Throws<EvidenceException>(() => Archive.CheckReferences(EvidenceKind.UnitPzl, WithRefs(doc, bundle, results, upward))).Error.Code);

        // RAW_INVENTORY: sorted, no self, receipt, registry record or canonical payload
        var inv = Archive.Inventory([bundle, upward]);
        Assert.StartsWith("{\"artifacts\":[{\"artifact_id\":\"ev-a\"", Encoding.UTF8.GetString(inv), StringComparison.Ordinal);
        Assert.Equal("Archive.self_reference", Code(() => Archive.Inventory([bundle, Archive.Reference("inv", "RAW_INVENTORY", null, null, "inventory.json", [4])])));
    }

    // ---------------------------------------------------------------- B1-T11-04: no window between the path check and the use

    private static void Junction(string link, string target)
    {
        using var p = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
        p.WaitForExit();
    }

    /// <summary>Swaps <paramref name="directory"/> for a junction to <paramref name="elsewhere"/>; returns whether the swap happened (it must not while the archive holds the path).</summary>
    private static bool Swap(string directory, string elsewhere)
    {
        try
        {
            Directory.Move(directory, directory + ".moved");
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        Junction(directory, elsewhere);
        return true;
    }

    [Fact]
    public void T11_04_a_parent_swapped_for_a_junction_between_the_check_and_the_write_cannot_redirect_a_new_file_outside_the_root()
    {
        var outside = Path.Combine(_root, "..", "archive-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        var archive = Path.Combine(_root, "a");
        Directory.CreateDirectory(Path.Combine(archive, "units", "u1"));
        bool? swapped = null;
        Archive.BetweenCheckAndUse = _ => swapped ??= Swap(Path.Combine(archive, "units", "u1"), outside);
        try
        {
            try
            {
                Archive.WriteNew(archive, "units/u1/x.json", [1, 2, 3], "x", "DIAGNOSTICS");
            }
            catch (EvidenceException)
            {
            }
        }
        finally
        {
            Archive.BetweenCheckAndUse = null;
        }

        Assert.False(File.Exists(Path.Combine(outside, "x.json")), "the new file was created outside the archive root through a swapped parent");
        Assert.False(swapped, "the parent directory could be swapped while the write was in progress");
        Directory.Delete(outside, true);
    }

    [Fact]
    public void T11_04_a_parent_swapped_between_the_check_and_the_read_cannot_make_the_snapshot_read_another_file()
    {
        var outside = Path.Combine(_root, "..", "archive-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        var archive = Path.Combine(_root, "b");
        var r = Archive.WriteNew(archive, "units/u1/x.json", [1, 2, 3], "x", "DIAGNOSTICS");
        File.WriteAllBytes(Path.Combine(outside, "x.json"), [1, 2, 3]);                                // the same bytes outside: only the path differs
        bool? swapped = null;
        Archive.BetweenCheckAndUse = _ => swapped ??= Swap(Path.Combine(archive, "units", "u1"), outside);
        try
        {
            try
            {
                Archive.Snapshot(archive, [r]);
            }
            catch (EvidenceException)
            {
            }
        }
        finally
        {
            Archive.BetweenCheckAndUse = null;
        }

        Assert.False(swapped, "the parent directory could be swapped while the snapshot read was in progress");
        Directory.Delete(outside, true);
    }

    [Fact]
    public void T11_04_the_receipt_retry_read_goes_through_the_same_pinned_path()
    {
        var archive = Path.Combine(_root, "c");
        Archive.WriteNew(archive, "receipts/r.json", [7], "r", "DIAGNOSTICS");
        Assert.Equal([7], Archive.TryRead(archive, "receipts/r.json"));
        Assert.Null(Archive.TryRead(archive, "receipts/none.json"));
        var real = Path.Combine(_root, "c-real");
        Directory.CreateDirectory(real);
        File.WriteAllBytes(Path.Combine(real, "r.json"), [7]);
        Directory.Move(Path.Combine(archive, "receipts"), Path.Combine(archive, "receipts.old"));
        Junction(Path.Combine(archive, "receipts"), real);
        Assert.Equal("Archive.reparse_point", Code(() => Archive.TryRead(archive, "receipts/r.json")));
    }

    /// <summary>B1 T11 R4: a new archive root is created only through the pinned chain — a junction among its parents is refused BEFORE anything exists on the other side of it.</summary>
    [Fact]
    public void R4_a_new_archive_root_below_a_junction_is_refused_without_creating_anything_outside()
    {
        var outside = Path.Combine(_root, "r4-outside");
        Directory.CreateDirectory(outside);
        Junction(Path.Combine(_root, "r4-link"), outside);
        Assert.Equal("Archive.reparse_point", Code(() => Archive.WriteNew(Path.Combine(_root, "r4-link", "archive", "deep"), "units/a.json", [1], "a", "DIAGNOSTICS")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(outside));                                  // no root directory made through the link

        var fresh = Path.Combine(_root, "r4-new", "archive");                                         // a normal new root: created component by component, then used
        var r = Archive.WriteNew(fresh, "units/a.json", [1], "a", "DIAGNOSTICS");
        Assert.Equal([1], Archive.Snapshot(fresh, [r])["a"].Bytes);
        Archive.WriteNew(fresh, "units/b.json", [2], "b", "DIAGNOSTICS");                           // an existing root is used as it is
    }
}
