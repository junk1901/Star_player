using System.Diagnostics;
using System.Text.Json.Nodes;
using StarPlayer.Domain.Common;
using StarPlayer.SimHarness;

namespace StarPlayer.SimHarness.Tests;

/// <summary>
/// F-C2 official integration FIX1 regressions. OI-01: the official output must never be written outside the output root, even when a parent is swapped for a Windows junction after the preflight
/// or while the run writes, and two runs with the same run id must never share or overwrite one output. OI-02: replay must apply the preflight's approved-plan check (per-cell seed count = the
/// approved size) before re-executing anything. Junctions are made with <c>mklink /J</c> (no privilege needed) only inside this class's TEMP scratch directory, which is removed afterwards.
/// </summary>
public sealed partial class DominanceOfficialTests
{
    private static void Junction(string link, string target)
    {
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var a in new[] { "/c", "mklink", "/J", link, target })
            start.ArgumentList.Add(a);
        using var p = Process.Start(start)!;
        p.WaitForExit();
        Assert.True(p.ExitCode == 0 && File.GetAttributes(link).HasFlag(FileAttributes.ReparsePoint), $"junction not created: {p.StandardError.ReadToEnd()}");
    }

    private static string[] FilesUnder(string dir) => Directory.Exists(dir) ? Directory.GetFileSystemEntries(dir, "*", SearchOption.AllDirectories) : [];

    // ---------------------------------------------------------------- OI-01

    [Theory]
    [InlineData("build")]
    [InlineData("build/simharness/official")]
    public void A_parent_swapped_for_a_junction_after_the_preflight_never_redirects_the_output(string swapped)
    {
        var root = CopyRoot();
        var outside = Path.Combine(_scratch, "outside-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(outside);
        var report = Preflight(_w.Approvals, root: root, runId: "dom-junction");
        Assert.True(report.Ready);

        var link = Path.Combine(root, swapped.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        Junction(link, outside);                                                             // the parent now points outside the root
        var result = DominanceOfficial.Execute(report, "dom-junction", _w.Approvals, root);

        Assert.Empty(FilesUnder(outside));                                                   // nothing was created outside the root
        Assert.Equal((DominanceRunTermination.AbortedIntegrity, 0, false), (result.Termination, result.Cells.Count, result.Complete));
        Assert.Equal("Official.output_reparse_point", result.Abort?.Code);
        Directory.Delete(link);                                                              // removes the junction only
    }

    [Fact]
    public void The_pinned_output_chain_cannot_be_moved_or_replaced_while_the_run_writes()
    {
        var root = CopyRoot();
        var outside = Path.Combine(_scratch, "outside-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(outside);
        var report = Preflight(_w.Approvals, root: root, runId: "dom-swap");
        Assert.True(report.Ready);
        var official = Path.Combine(root, "build", "simharness", "official");
        var run = report.OutputDirectory!;
        var attempts = new List<string>();

        Error? Attack(int cellsDone)
        {
            if (cellsDone == 0)                                                               // after the output exists, before the first cell is written
            {
                foreach (var (from, to) in new[] { (official, official + "-moved"), (run, run + "-moved"), (Path.Combine(root, "build"), Path.Combine(root, "build-moved")) })
                    try
                    {
                        Directory.Move(from, to);
                        attempts.Add($"MOVED {from}");
                        if (!Directory.Exists(from))
                            Junction(from, outside);                                          // a successful move would let the attacker put a junction in its place
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        attempts.Add($"blocked {from}");
                    }
            }

            if (cellsDone == 1)                                                               // "cells" now has no open child: only the pin's missing FILE_SHARE_DELETE keeps it in place
            {
                var cells = Path.Combine(run, "cells");
                try
                {
                    Directory.Move(cells, Path.Combine(outside, "cells-moved"));                  // moving it outside the root would carry the next cell's writes with it
                    attempts.Add($"MOVED {cells}");
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    attempts.Add($"blocked {cells}");
                }
            }

            return cellsDone == 2 ? new Error("Test.stop", "stop after two cells") : null;
        }

        var result = DominanceOfficial.Execute(report, "dom-swap", _w.Approvals, root, integrity: Attack);

        Assert.All(attempts, a => Assert.StartsWith("blocked", a));                          // the run holds every directory of its output chain
        Assert.Equal(4, attempts.Count);
        Assert.Empty(FilesUnder(outside));
        Assert.Equal((DominanceRunTermination.AbortedIntegrity, 2, "Test.stop"), (result.Termination, result.Cells.Count, result.Abort?.Code));
        Assert.True(File.Exists(Path.Combine(run, DominanceOfficial.ResultFile)));            // the evidence is where the preflight said it would be
        Assert.Equal(2, Directory.GetDirectories(Path.Combine(run, "cells")).Length);
    }

    [Fact]
    public void A_held_output_directory_turned_into_a_mount_point_mid_run_aborts_without_writing_outside()
    {
        var root = CopyRoot();
        var outside = Path.Combine(_scratch, "outside-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(outside);
        var report = Preflight(_w.Approvals, root: root, runId: "dom-mount");
        Assert.True(report.Ready);
        string? staged = "not attempted";

        var result = DominanceOfficial.Execute(report, "dom-mount", _w.Approvals, root,
            integrity: n => { if (n == 0) staged = PinnedDirectoryTests.MountPoint(Path.Combine(report.OutputDirectory!, "cells"), outside); return null; });

        Assert.Null(staged);                                                                 // the held, still empty "cells" directory really became a mount point
        Assert.Empty(FilesUnder(outside));
        Assert.Equal((DominanceRunTermination.AbortedIntegrity, 0, false, "Official.output_reparse_point"), (result.Termination, result.Cells.Count, result.Complete, result.Abort?.Code));
        var index = ReadNode(Path.Combine(report.OutputDirectory!, DominanceOfficial.ResultFile));   // the incomplete record stays in the pinned run directory
        Assert.Equal((false, "NOT_EVALUATED"), (index["complete"]!.GetValue<bool>(), index["dominance_gate"]!.GetValue<string>()));
        Directory.Delete(Path.Combine(report.OutputDirectory!, "cells"));                     // removes the mount point only
    }

    [Fact]
    public async Task Concurrent_runs_with_the_same_run_id_never_share_or_overwrite_one_output()
    {
        var root = CopyRoot();
        var a = Preflight(_w.Approvals, root: root, runId: "dom-same");
        var b = Preflight(_w.Approvals, root: root, runId: "dom-same");
        Assert.True(a.Ready && b.Ready);                                                      // both checked before either created anything
        using var start = new Barrier(2);
        DominanceOfficialResult Go(DominancePreflightReport r, int stopAt, string code)
        {
            start.SignalAndWait();
            return DominanceOfficial.Execute(r, "dom-same", _w.Approvals, root, integrity: n => n == stopAt ? new Error(code, code) : null);
        }

        var ta = Task.Run(() => Go(a, 1, "Test.stop_a"));
        var tb = Task.Run(() => Go(b, 2, "Test.stop_b"));
        var results = await Task.WhenAll(ta, tb);

        var loser = Assert.Single(results, r => r.Abort?.Code == "Official.output_exists");
        var winner = Assert.Single(results, r => r != loser);
        Assert.Empty(loser.Cells);                                                            // the loser ran no game and wrote nothing
        var index = ReadNode(Path.Combine(a.OutputDirectory!, DominanceOfficial.ResultFile));
        Assert.Equal(winner.Abort!.Value.Code, index["abort_code"]!.GetValue<string>());           // the one result belongs to the winner only
        Assert.Equal(winner.Cells.Count, index["cells"]!.AsArray().Count);
        Assert.Equal(winner.Cells.Count, Directory.GetDirectories(Path.Combine(a.OutputDirectory!, "cells")).Length);
    }

    [Fact]
    public void An_existing_or_partial_output_is_never_reused_or_overwritten()
    {
        var root = CopyRoot();
        var report = Preflight(_w.Approvals, root: root, runId: "dom-partial");
        Assert.True(report.Ready);
        Directory.CreateDirectory(report.OutputDirectory!);                                   // appears after the preflight: an earlier, partial attempt
        var kept = Path.Combine(report.OutputDirectory!, DominanceOfficial.ResultFile);
        File.WriteAllText(kept, "earlier evidence");

        var result = DominanceOfficial.Execute(report, "dom-partial", _w.Approvals, root);

        Assert.Equal(("Official.output_exists", 0, false), (result.Abort?.Code, result.Cells.Count, result.Complete));
        Assert.Equal("earlier evidence", File.ReadAllText(kept));
        Assert.Single(FilesUnder(report.OutputDirectory!));
    }

    [Fact]
    public void An_output_that_cannot_be_created_fails_closed_without_a_result()
    {
        var root = CopyRoot();
        var report = Preflight(_w.Approvals, root: root, runId: "dom-blocked");
        Assert.True(report.Ready);
        File.WriteAllText(Path.Combine(root, "build"), "not a directory");                    // the output chain cannot be built

        var result = DominanceOfficial.Execute(report, "dom-blocked", _w.Approvals, root);

        Assert.Equal((DominanceRunTermination.AbortedIntegrity, 0, false), (result.Termination, result.Cells.Count, result.Complete));
        Assert.Equal("Official.output_unpinnable", result.Abort?.Code);
        Assert.Equal(2, result.ExitCode);
        Assert.Equal("not a directory", File.ReadAllText(Path.Combine(root, "build")));
    }

    // ---------------------------------------------------------------- OI-02

    private (DominanceReplayOutcome Outcome, int Reruns) CountedReplay(string dir, OfficialApprovals a, string? root = null)
    {
        var reruns = 0;
        var outcome = DominanceOfficialReplay.Verify(dir, a, root ?? _w.Root, default, (config, games, token) =>
        {
            Interlocked.Increment(ref reruns);
            return SimRunner.RunSpecs(config, games, null, token);
        });
        return (outcome, reruns);
    }

    [Fact]
    public void A_correct_result_replays_every_cell_once()
    {
        var (o, reruns) = CountedReplay(_w.ResultDirectory, _w.Approvals);
        Assert.Null(o.Error);
        Assert.Equal(48, reruns);
    }

    [Fact]
    public void Replay_refuses_a_plan_whose_seed_count_is_not_the_approved_size_even_with_a_consistently_forged_index()
    {
        var approved3 = _w.Approvals with { DominanceGamesPerCell = 3 };                    // the approved plans have 2 games per cell
        Refused(Preflight(approved3), "games_per_cell", CheckStatus.Fail, "Official.dominance_plan_size_mismatch");

        var forged = CopyResult();                                                            // an index re-derived for n = 3, consistent with itself
        var e3 = DominanceEvaluator.Evaluate(_w.Result.Cells.Select(c => c.Run), 3);
        EditIndex(forged, d =>
        {
            d["games_per_cell"] = 3;
            d["evaluation_canonical"] = e3.Canonical();
            d["dominance_gate"] = DominanceEvaluation.Text(e3.Gate);
            d["dominance_gate_reason"] = e3.GateReason;
            d["batter_verdict"] = SideResult.Text(e3.Batter.Verdict);
            d["pitcher_verdict"] = SideResult.Text(e3.Pitcher.Verdict);
            d["run_status"] = e3.Cells.Any(c => c.MetricsFailures > 0) ? RunReport.StatusMetricsError : RunReport.StatusCompleted;
            for (var i = 0; i < 48; i++)
            {
                d["cells"]![i]!["cell_state"] = e3.Cells[i].State.ToString();
                d["cells"]![i]!["cell_reason"] = e3.Cells[i].Reason;
            }
        });

        var (o, reruns) = CountedReplay(forged, approved3);
        Assert.Equal("Official.dominance_plan_size_mismatch", o.Error?.Code);
        Assert.Equal((false, 0), (o.GamesExecuted, reruns));                                  // refused before any game
    }

    [Fact]
    public void Replay_refuses_when_a_single_approved_plan_is_short()
    {
        var root = CopyRoot();
        var cell = DominanceMatrix.Cells[17];
        var shortPath = "seeds/dom/short.json";
        var pins = new Dictionary<string, string>(_w.Approvals.SeedManifestPins, StringComparer.Ordinal);
        pins.Remove(DominanceOfficialWorld.SeedPath(cell));
        pins[shortPath] = DominanceOfficialWorld.Put(root, shortPath, DominanceOfficialWorld.SeedFile(cell, games: 1));
        var a = _w.Approvals with { SeedManifestPins = pins };
        Refused(Preflight(a, root: root), "games_per_cell", CheckStatus.Fail, "Official.dominance_plan_size_mismatch");

        var (o, reruns) = CountedReplay(_w.ResultDirectory, a, root);
        Assert.Equal(("Official.dominance_plan_size_mismatch", false, 0), (o.Error?.Code, o.GamesExecuted, reruns));
    }

    [Fact]
    public void Every_approved_plan_the_preflight_refuses_is_refused_by_replay_before_any_game_with_the_same_reason()
    {
        var ok = _w.Approvals;
        var first = DominanceOfficialWorld.SeedPath(DominanceMatrix.Cells[0]);
        var missing = new Dictionary<string, string>(ok.SeedManifestPins, StringComparer.Ordinal);
        missing.Remove(first);
        var wrongPin = new Dictionary<string, string>(ok.SeedManifestPins, StringComparer.Ordinal) { [first] = new string('0', 64) };
        var variants = new (string Name, OfficialApprovals Approvals, string PlanCheck)[]
        {
            ("size not frozen", ok with { DominanceGamesPerCell = null }, "games_per_cell"),
            ("size invalid", ok with { DominanceGamesPerCell = 1 }, "games_per_cell"),
            ("size mismatch", ok with { DominanceGamesPerCell = 3 }, "games_per_cell"),
            ("cell missing", ok with { SeedManifestPins = missing }, "seed_manifests"),
            ("pin mismatch", ok with { SeedManifestPins = wrongPin }, "seed_manifests"),
            ("nothing pinned", ok with { SeedManifestPins = new Dictionary<string, string>() }, "seed_manifests"),
        };
        foreach (var (name, a, planCheck) in variants)
        {
            var pre = Preflight(a);
            Assert.False(pre.Ready, name);
            var (o, reruns) = CountedReplay(_w.ResultDirectory, a);
            Assert.True(o.Error is not null && !o.GamesExecuted && reruns == 0, $"{name}: {o.Error?.Code}");
            Assert.True(pre.Checks.Single(c => c.Id == planCheck).Code == o.Error!.Value.Code, $"{name}: preflight {pre.Checks.Single(c => c.Id == planCheck).Code} vs replay {o.Error.Value.Code}");
        }
    }
}
