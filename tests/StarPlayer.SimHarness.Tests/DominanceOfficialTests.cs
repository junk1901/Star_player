using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StarPlayer.Domain.Common;
using StarPlayer.SimHarness;

namespace StarPlayer.SimHarness.Tests;

/// <summary>
/// A TEST-ONLY official world for the 48-cell integration: a TEMP root holding 48 synthetic <c>fc3-dom</c> seed files (fictional seeds 990001 / 990002, 2 games per cell) and approvals that pin them,
/// freeze 2 games per cell and mark the path ready. None of it is an official seed, pin, sample size or approval; the real <see cref="OfficialApprovals.Current"/> is untouched. The 48 cells are
/// executed ONCE here (96 games) and the tests copy the result directory before tampering with it.
/// </summary>
public sealed class DominanceOfficialWorld : IDisposable
{
    public const string Sha = "f1c2f1c2f1c2f1c2f1c2f1c2f1c2f1c2f1c2f1c2";

    public string Root { get; } = Path.Combine(Path.GetTempPath(), "simharness-tests", "dom-official-" + Guid.NewGuid().ToString("N"));

    public OfficialApprovals Approvals { get; }

    public DominancePreflightReport Preflight { get; }

    public DominanceOfficialResult Result { get; }

    public DominanceOfficialWorld()
    {
        Directory.CreateDirectory(Path.Combine(Root, "seeds", "dom"));
        var pins = DominanceMatrix.Cells.ToDictionary(c => SeedPath(c), c => Put(Root, SeedPath(c), SeedFile(c)), StringComparer.Ordinal);
        Approvals = new OfficialApprovals(Sha, OfficialProfile.Id, OfficialProfile.Hash(), pins,
            new Dictionary<string, string> { ["neutral-v1"] = NeutralCohort.Create().InputsHash, ["neutral-opp-v1"] = NeutralCohort.CreateOpposite().InputsHash }, true, 2);
        Preflight = DominancePreflight.Run(new DominanceOfficialRequest(Root, "dom-fixture", null), Approvals, Git());
        Result = DominanceOfficial.Execute(Preflight, "dom-fixture", Approvals, Root);
    }

    public void Dispose() => Directory.Delete(Root, true);

    public string ResultDirectory => Preflight.OutputDirectory!;

    public static GitIntegrityReport Git(string? head = Sha) => GitIntegrityPolicy.Evaluate([], head);

    public static string SeedPath(DominanceCell c) => $"seeds/dom/{c.Id.ToLowerInvariant()}.json";

    /// <param name="seedBase">Another fictional seed base (seed = base + index); null keeps the fixture's 990000.</param>
    public static JsonObject SeedFile(DominanceCell c, int games = 2, long? seedBase = null)
    {
        var m = SeedManifestTests.Manifest("fc3-dom", c.CompetitionKey, games, games, games, cohort: c.CohortId);
        m["policy_id"] = c.PolicyId;
        m["rules_profile_id"] = c.RulesProfileId;
        if (seedBase is { } b)
            foreach (var s in (JsonArray)m["seeds"]!)
                s!["seed"] = b + s["index"]!.GetValue<int>();
        return m;
    }

    public static string Put(string root, string rel, JsonObject content)
    {
        var bytes = Encoding.UTF8.GetBytes(content.ToJsonString());
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, rel))!);
        File.WriteAllBytes(Path.Combine(root, rel), bytes);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}

/// <summary>
/// F-C2 official integration: the 48-cell preflight against the approvals' pins (fail-closed, blocked with the real approvals), the execution entered only from a READY preflight, the stored result
/// (existing manifest / envelope / attestation contracts per cell plus a DRAFT matrix index) and the replay that re-derives everything from trusted inputs.
/// </summary>
public sealed partial class DominanceOfficialTests : IClassFixture<DominanceOfficialWorld>, IDisposable
{
    private readonly DominanceOfficialWorld _w;
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "simharness-tests", "dom-official-scratch-" + Guid.NewGuid().ToString("N"));

    public DominanceOfficialTests(DominanceOfficialWorld world)
    {
        _w = world;
        Directory.CreateDirectory(_scratch);
    }

    /// <summary>Removes the scratch tree (junctions are removed as links, never followed); a transient delete error is retried briefly.</summary>
    public void Dispose()
    {
        for (var attempt = 0; ; attempt++)
            try
            {
                if (Directory.Exists(_scratch))
                    Directory.Delete(_scratch, true);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException && attempt < 10)
            {
                Thread.Sleep(200);
            }
    }

    private static PreflightCheck Check(DominancePreflightReport r, string id) => r.Checks.Single(c => c.Id == id);

    private DominancePreflightReport Preflight(OfficialApprovals a, GitIntegrityReport? git = null, string runId = "dom-pre", string? root = null) =>
        DominancePreflight.Run(new DominanceOfficialRequest(root ?? _w.Root, runId, null), a, git ?? DominanceOfficialWorld.Git());

    private static void Refused(DominancePreflightReport r, string id, CheckStatus status, string code)
    {
        Assert.False(r.Ready);
        Assert.Equal((status, code), (Check(r, id).Status, Check(r, id).Code));
    }

    /// <summary>A private copy of the world's seed files (for tests that change a file on disk).</summary>
    private string CopyRoot()
    {
        var root = Path.Combine(_scratch, "root-" + Guid.NewGuid().ToString("N")[..8]);
        foreach (var file in Directory.GetFiles(Path.Combine(_w.Root, "seeds"), "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(root, Path.GetRelativePath(_w.Root, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        return root;
    }

    /// <summary>A private copy of the executed result directory.</summary>
    private string CopyResult()
    {
        var dir = Path.Combine(_scratch, "result-" + Guid.NewGuid().ToString("N")[..8]);
        foreach (var file in Directory.GetFiles(_w.ResultDirectory, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(dir, Path.GetRelativePath(_w.ResultDirectory, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        return dir;
    }

    private static JsonObject ReadNode(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    private static void WriteNode(string path, JsonNode node) => File.WriteAllText(path, node.ToJsonString(), new UTF8Encoding(false));

    private static string FileSha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    /// <summary>Edits one cell manifest and (consistently) the index's record of its file hash, so only deeper checks can notice.</summary>
    private static void EditCell(string dir, int cellIndex, Action<JsonObject> edit)
    {
        var cell = DominanceMatrix.Cells[cellIndex];
        var path = Path.Combine(dir, "cells", cell.Id.ToLowerInvariant(), "manifest.json");
        var m = ReadNode(path);
        edit(m);
        WriteNode(path, m);
        EditIndex(dir, d => d["cells"]![cellIndex]!["manifest_sha256"] = FileSha(path));
    }

    private static void EditIndex(string dir, Action<JsonObject> edit)
    {
        var path = Path.Combine(dir, DominanceOfficial.ResultFile);
        var d = ReadNode(path);
        edit(d);
        WriteNode(path, d);
    }

    private DominanceReplayOutcome Replay(string dir, OfficialApprovals? a = null, string? root = null) => DominanceOfficialReplay.Verify(dir, a ?? _w.Approvals, root ?? _w.Root);

    private static void RefusedBeforeGames(DominanceReplayOutcome o, string code)
    {
        Assert.Equal(code, o.Error?.Code);
        Assert.False(o.GamesExecuted);
    }

    // ---------------------------------------------------------------- the real approvals: blocked

    [Fact]
    public void With_the_real_approvals_the_48_cell_preflight_is_blocked_and_nothing_can_execute()
    {
        var r = Preflight(OfficialApprovals.Current);

        Assert.False(r.Ready);
        Assert.Empty(r.Cells);
        Assert.Null(r.GamesPerCell);
        Refused(r, "seed_manifests", CheckStatus.NotReady, "Official.dominance_seed_manifests_not_frozen");
        Refused(r, "games_per_cell", CheckStatus.NotReady, "Official.dominance_games_per_cell_not_frozen");
        Refused(r, "git_sha", CheckStatus.NotReady, "Official.git_sha_not_approved");
        Refused(r, "execution_policy", CheckStatus.NotReady, "Official.execution_policy_not_frozen");
        Refused(r, "execution_path", CheckStatus.NotReady, "Official.not_ready");
        Refused(r, "cell_preflight", CheckStatus.NotReady, "Official.input_unavailable");
        Assert.Equal("Official.not_ready", r.ErrorCode);
        Assert.Equal(CheckStatus.Pass, Check(r, "model_tuning").Status);
        Assert.Throws<InvalidOperationException>(() => DominanceOfficial.Execute(r, "dom-pre", OfficialApprovals.Current, _w.Root));   // never enters execution
        Assert.False(Directory.Exists(Path.Combine(_w.Root, "build", "simharness", "official", "dom-pre")));

        // the real approvals still freeze nothing; the fixture is a separate instance
        var current = OfficialApprovals.Current;
        Assert.Equal((null, false, 0, (int?)null), (current.GitCommitSha, current.ExecutionPathReady, current.SeedManifestPins.Count, current.DominanceGamesPerCell));
        Assert.NotSame(current, _w.Approvals);
    }

    // ---------------------------------------------------------------- preflight with test approvals

    [Fact]
    public void Fully_pinned_test_approvals_make_the_preflight_ready_with_one_attestation_per_cell_in_matrix_order()
    {
        var r = _w.Preflight;
        Assert.True(r.Ready, string.Join("; ", r.Checks.Where(c => c.Status != CheckStatus.Pass).Select(c => $"{c.Id} {c.Code} {c.Message}")));
        Assert.Equal(DominanceMatrix.Cells.Select(c => c.Id), r.Cells.Select(c => c.Pinned.Cell.Id));
        Assert.All(r.Cells, c =>
        {
            var a = c.Report.Attestation!;
            Assert.Equal((DominanceOfficialWorld.SeedPath(c.Pinned.Cell), _w.Approvals.SeedManifestPins[a.SeedManifestPath]), (a.SeedManifestPath, a.SeedManifestSha256));
            Assert.Equal((c.Pinned.Cell.CompetitionKey, c.Pinned.Cell.CohortId, c.Pinned.Cell.PolicyId, c.Pinned.Cell.RulesProfileId, "fc3-dom"), (a.CompetitionKey, a.CohortId, a.PolicyId, a.RulesProfileId, a.SeedFamily));
        });
        Assert.Equal(48, r.Cells.Select(c => c.Report.Attestation!.Hash()).Distinct().Count());
    }

    [Fact]
    public void Each_unmet_48_cell_condition_fails_closed_on_its_own_check()
    {
        var ok = _w.Approvals;
        var first = DominanceMatrix.Cells[0];
        var firstPath = DominanceOfficialWorld.SeedPath(first);
        Dictionary<string, string> Pins() => new(ok.SeedManifestPins, StringComparer.Ordinal);

        Refused(Preflight(ok with { DominanceGamesPerCell = null }), "games_per_cell", CheckStatus.NotReady, "Official.dominance_games_per_cell_not_frozen");   // sample size not frozen: never PASS
        Refused(Preflight(ok with { DominanceGamesPerCell = 1 }), "games_per_cell", CheckStatus.Fail, "Official.dominance_games_per_cell_invalid");
        Refused(Preflight(ok with { DominanceGamesPerCell = 3 }), "games_per_cell", CheckStatus.Fail, "Official.dominance_plan_size_mismatch");
        var missing = Pins();
        missing.Remove(firstPath);
        Refused(Preflight(ok with { SeedManifestPins = missing }), "seed_manifests", CheckStatus.Fail, "Official.dominance_cells_missing");          // 47 cells: no partial matrix
        var wrongPin = Pins();
        wrongPin[firstPath] = new string('0', 64);
        Refused(Preflight(ok with { SeedManifestPins = wrongPin }), "seed_manifests", CheckStatus.Fail, "Seed.file_hash_mismatch");
        var absent = Pins();
        absent["seeds/dom/absent.json"] = new string('1', 64);
        Refused(Preflight(ok with { SeedManifestPins = absent }), "seed_manifests", CheckStatus.Fail, "Seed.file_missing");

        var extra = Pins();                                                                   // a pinned fc3-dom file that is not a matrix cell
        var foreign = SeedManifestTests.Manifest("fc3-dom", "fc3-dom-x", 2, 2, 2);
        extra["seeds/dom/x.json"] = DominanceOfficialWorld.Put(_w.Root, "seeds/dom/x.json", foreign);
        Refused(Preflight(ok with { SeedManifestPins = extra }), "seed_manifests", CheckStatus.Fail, "Dominance.cell_unknown");
        var twice = Pins();                                                                   // two pinned files for the same cell
        twice["seeds/dom/again.json"] = DominanceOfficialWorld.Put(_w.Root, "seeds/dom/again.json", DominanceOfficialWorld.SeedFile(first));
        Refused(Preflight(ok with { SeedManifestPins = twice }), "seed_manifests", CheckStatus.Fail, "Seed.competition_key_reused");
        var bound = Pins();                                                                   // the first cell's key with another cell's policy
        var misbound = DominanceOfficialWorld.SeedFile(first);
        misbound["policy_id"] = DominanceMatrix.Cells[1].PolicyId;
        bound.Remove(firstPath);
        bound["seeds/dom/misbound.json"] = DominanceOfficialWorld.Put(_w.Root, "seeds/dom/misbound.json", misbound);
        Refused(Preflight(ok with { SeedManifestPins = bound }), "seed_manifests", CheckStatus.Fail, "Dominance.cell_binding_mismatch");

        Refused(Preflight(ok with { CohortInputsHashes = new Dictionary<string, string> { ["neutral-v1"] = ok.CohortInputsHashes["neutral-v1"] } }), "cell_preflight", CheckStatus.NotReady, "Official.cohort_hash_not_frozen");
        Refused(Preflight(ok with { CohortInputsHashes = new Dictionary<string, string> { ["neutral-v1"] = ok.CohortInputsHashes["neutral-v1"], ["neutral-opp-v1"] = new string('9', 64) } }), "cell_preflight", CheckStatus.Fail, "Official.cohort_hash_mismatch");
        Refused(Preflight(ok, GitIntegrityPolicy.Evaluate(H.Ok(GitStatusParser.Parse(" M src/a.cs\0")), DominanceOfficialWorld.Sha)), "git_integrity", CheckStatus.Fail, "Git.dirty");
        Refused(Preflight(ok, GitIntegrityPolicy.Evaluate(H.Ok(GitStatusParser.Parse("!! src/P/bin/p.dll\0")), DominanceOfficialWorld.Sha)), "git_integrity", CheckStatus.NotReady, "Git.build_output_unverified");
        Refused(Preflight(ok, DominanceOfficialWorld.Git(new string('1', 40))), "git_sha", CheckStatus.Fail, "Official.git_sha_mismatch");
        Refused(Preflight(ok with { ExecutionPolicyHash = new string('e', 64) }), "execution_policy", CheckStatus.Fail, "Official.execution_policy_mismatch");
        Refused(Preflight(ok with { ExecutionPathReady = false }), "execution_path", CheckStatus.NotReady, "Official.not_ready");
        Refused(Preflight(ok, runId: "dom-fixture"), "output_directory", CheckStatus.Fail, "Official.output_exists");               // never over an existing result

        var cal = Pins();                                                                     // an approved file of another family does not join the matrix
        var calFile = SeedManifestTests.Manifest("fc3-cal", "fc3-cal-na", 7, 7, 7, 100_000, "neutral-mixed-v1");
        cal["seeds/cal.json"] = DominanceOfficialWorld.Put(_w.Root, "seeds/cal.json", calFile);
        Assert.True(Preflight(ok with { SeedManifestPins = cal }).Ready);
    }

    // ---------------------------------------------------------------- execution and stored result

    [Fact]
    public void A_ready_preflight_runs_all_48_cells_as_full_runs_and_stores_existing_contract_manifests_with_a_draft_index()
    {
        var r = _w.Result;
        Assert.True(r.Complete, $"{r.Abort?.Code}: {r.Abort?.Message}");
        Assert.Equal(DominanceRunTermination.Completed, r.Termination);
        Assert.Equal(DominanceMatrix.Cells.Select(c => c.Id), r.Cells.Select(c => c.Plan.Pinned.Cell.Id));
        Assert.All(r.Cells, c => Assert.Equal((false, 2, 2, "official"), (c.Run.StopAware, c.Run.Games.Count, c.Run.Requested, c.Run.Config.Mode)));   // fc3-dom: no stop rule (SR-D5)
        Assert.Equal(DominanceEvaluator.Evaluate(r.Cells.Select(c => c.Run), 2).Canonical(), r.Evaluation!.Canonical());
        Assert.Equal(r.RunStatus == RunReport.StatusCompleted ? 0 : r.RunStatus == RunReport.StatusMetricsError ? 3 : 1, r.ExitCode);

        foreach (var c in r.Cells)                                                            // every cell manifest is a normal official manifest the EXISTING replay check accepts
        {
            var path = Path.Combine(_w.ResultDirectory, "cells", c.Plan.Pinned.Cell.Id.ToLowerInvariant(), "manifest.json");
            Assert.Equal(c.ManifestSha256, FileSha(path));
            using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
            var plan = H.Ok(ReplayCheck.Validate(doc.RootElement, _w.Approvals, _w.Root));
            Assert.Equal((c.Run.ConfigHash, c.Run.RunResultHash), (plan.ConfigHash, plan.RunResultHash));
            Assert.Equal(OfficialAttestation.EnvelopeVersion, doc.RootElement.GetProperty("official").GetProperty("schema_version").GetString());
            Assert.Equal(RunResultText.V1, c.Run.ResultVersion);
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(path)!, "summary.json")));
        }

        var index = ReadNode(Path.Combine(_w.ResultDirectory, DominanceOfficial.ResultFile));
        Assert.Equal((DominanceOfficial.ResultSchema, DominanceOfficial.SchemaStatus, false, true), (index["schema_version"]!.GetValue<string>(), index["schema_status"]!.GetValue<string>(),
            index["official_evidence"]!.GetValue<bool>(), index["complete"]!.GetValue<bool>()));   // a draft format: never Official Evidence
        Assert.Equal(48, index["cells"]!.AsArray().Count);
        Assert.Equal(DominanceEvaluation.Text(r.Evaluation.Gate), index["dominance_gate"]!.GetValue<string>());
    }

    [Fact]
    public void The_stored_result_replays_to_a_match_from_trusted_inputs()
    {
        var o = Replay(_w.ResultDirectory);
        Assert.Null(o.Error);
        Assert.True(o.GamesExecuted);
        Assert.Equal(_w.Result.Evaluation!.Canonical(), o.Evaluation!.Canonical());
    }

    [Fact]
    public void G5T01_the_byte_replay_asks_for_each_file_once_and_matches_the_stored_result()
    {
        var asked = new List<string>();
        byte[]? Read(string path)
        {
            asked.Add(path);
            var file = Path.Combine([_w.ResultDirectory, .. path.Split('/')]);
            return File.Exists(file) ? File.ReadAllBytes(file) : null;
        }

        var o = DominanceOfficialReplay.Verify(Read, _w.Approvals, _w.Root);
        Assert.Null(o.Error);
        Assert.Equal(_w.Result.Evaluation!.Canonical(), o.Evaluation!.Canonical());
        Assert.Equal(_w.Result.Cells.Select(c => c.Run.RunResultHash), o.Reruns!.Select(r => r.RunResultHash));
        Assert.Equal([DominanceOfficial.ResultFile, .. DominanceMatrix.Cells.Select(c => DominancePreflight.CellDirectory(c) + "/manifest.json")], asked);
    }

    [Fact]
    public void Replay_refuses_changes_to_the_index_before_any_game()
    {
        string Edited(Action<JsonObject> edit)
        {
            var dir = CopyResult();
            EditIndex(dir, edit);
            return dir;
        }

        RefusedBeforeGames(Replay(Edited(d => d["complete"] = false)), "DomOfficial.run_not_complete");
        RefusedBeforeGames(Replay(Edited(d => d["run_status"] = RunReport.StatusCancelled)), "DomOfficial.run_not_complete");
        RefusedBeforeGames(Replay(Edited(d => d["schema_version"] = "simharness-dominance-result-v1")), "DomOfficial.result_schema_unsupported");   // no self-promotion to an unapproved version
        RefusedBeforeGames(Replay(Edited(d => d["games_per_cell"] = 3)), "DomOfficial.games_per_cell_mismatch");
        RefusedBeforeGames(Replay(Edited(d => d["git_commit_sha"] = new string('2', 40))), "DomOfficial.approval_record_mismatch");
        RefusedBeforeGames(Replay(Edited(d => d["cells"]!.AsArray().RemoveAt(47))), "DomOfficial.cell_set_mismatch");          // a missing cell
        RefusedBeforeGames(Replay(Edited(d =>
        {
            var cells = d["cells"]!.AsArray();
            var a = cells[0]!.DeepClone();
            cells[0] = cells[1]!.DeepClone();
            cells[1] = a;
        })), "DomOfficial.cell_order_mismatch");                                              // swapped order
        RefusedBeforeGames(Replay(Edited(d => d["cells"]![5]!["manifest_path"] = "cells/dom-sa-nb/manifest.json")), "DomOfficial.cell_path_mismatch");
        RefusedBeforeGames(Replay(Edited(d => d["cells"]![5]!["manifest_sha256"] = new string('a', 64))), "DomOfficial.cell_manifest_hash_mismatch");
        RefusedBeforeGames(Replay(Edited(d => d["cells"]![5]!["run_result_hash"] = new string('b', 64))), "DomOfficial.cell_record_mismatch");
        RefusedBeforeGames(Replay(Edited(d => d.Remove("evaluation_canonical"))), "DomOfficial.result_invalid");
        var gone = CopyResult();
        File.Delete(Path.Combine(gone, DominanceOfficial.ResultFile));
        RefusedBeforeGames(Replay(gone), "DomOfficial.result_missing");
        var plain = CopyResult();                                                            // a cell manifest edited without updating the index
        var manifest = Path.Combine(plain, "cells", "dom-sa-nb", "manifest.json");
        File.AppendAllText(manifest, " ");
        RefusedBeforeGames(Replay(plain), "DomOfficial.cell_manifest_hash_mismatch");
    }

    [Fact]
    public void Replay_refuses_untrusted_or_mixed_approval_inputs()
    {
        var a = _w.Approvals;
        var first = DominanceOfficialWorld.SeedPath(DominanceMatrix.Cells[0]);
        RefusedBeforeGames(Replay(_w.ResultDirectory, a with { DominanceGamesPerCell = null }), "Official.dominance_games_per_cell_not_frozen");
        RefusedBeforeGames(Replay(_w.ResultDirectory, OfficialApprovals.Current), "Official.dominance_games_per_cell_not_frozen");   // the real approvals accept nothing
        var pins = new Dictionary<string, string>(a.SeedManifestPins, StringComparer.Ordinal) { [first] = new string('0', 64) };
        RefusedBeforeGames(Replay(_w.ResultDirectory, a with { SeedManifestPins = pins }), "Seed.file_hash_mismatch");   // external pin mismatch
        RefusedBeforeGames(Replay(_w.ResultDirectory, a with { GitCommitSha = new string('3', 40) }), "DomOfficial.approval_record_mismatch");
        RefusedBeforeGames(Replay(_w.ResultDirectory, a with { ExecutionPathReady = false }), "Official.not_ready");

        // an approved seed file replaced by another valid file for the same cell (re-pinned): the recorded attestation no longer matches the approval
        var root = CopyRoot();
        var replaced = DominanceOfficialWorld.SeedFile(DominanceMatrix.Cells[0]);
        replaced["seeds"]![0]!["seed"] = 123456;
        var swapped = new Dictionary<string, string>(a.SeedManifestPins, StringComparer.Ordinal) { [first] = DominanceOfficialWorld.Put(root, first, replaced) };
        RefusedBeforeGames(Replay(_w.ResultDirectory, a with { SeedManifestPins = swapped }, root), "Replay.seed_manifest_hash_mismatch");
    }

    [Fact]
    public void Replay_refuses_tampered_cell_manifests()
    {
        var mixed = CopyResult();                                                             // a cell from another run id (results of different runs mixed)
        EditCell(mixed, 3, m => m["run"]!["run_id"] = "other-run");
        RefusedBeforeGames(Replay(mixed), "DomOfficial.run_id_mismatch");
        var seed = CopyResult();                                                              // a seed changed in the recorded plan
        EditCell(seed, 3, m => m["games"]![0]!["seed"] = 1);
        RefusedBeforeGames(Replay(seed), "Replay.seed_games_mismatch");
        var game = CopyResult();                                                              // one game's recorded result changed
        EditCell(game, 0, m => m["results"]!["games"]![1]!["result_hash"] = new string('c', 64));
        var o = Replay(game);
        Assert.Equal(("DomOfficial.result_games_mismatch", true), (o.Error?.Code, o.GamesExecuted));
    }

    [Fact]
    public void Replay_recomputes_the_metrics_state_and_the_gate_instead_of_trusting_them()
    {
        var state = CopyResult();
        EditIndex(state, d => d["cells"]![47]!["cell_state"] = "InsufficientSample");
        var o = Replay(state);
        Assert.Equal(("DomOfficial.cell_record_mismatch", true), (o.Error?.Code, o.GamesExecuted));

        var gate = CopyResult();
        var other = _w.Result.Evaluation!.Gate == DominanceGate.Pass ? "FAIL" : "PASS";
        EditIndex(gate, d =>
        {
            d["dominance_gate"] = other;
            d["evaluation_canonical"] = d["evaluation_canonical"]!.GetValue<string>().Replace($"|{DominanceEvaluation.Text(_w.Result.Evaluation.Gate)}|", $"|{other}|");
        });
        o = Replay(gate);
        Assert.Equal(("DomOfficial.evaluation_mismatch", true), (o.Error?.Code, o.GamesExecuted));
    }

    [Fact]
    public void An_integrity_break_or_a_cancellation_never_yields_a_complete_or_replayable_result()
    {
        var aborted = DominanceOfficial.Execute(Preflight(_w.Approvals, runId: "dom-abort"), "dom-abort", _w.Approvals, _w.Root, integrity: n => n == 3 ? new Error("Test.pin_broken", "x") : null);
        Assert.Equal((DominanceRunTermination.AbortedIntegrity, 3, false, RunReport.StatusAborted, 2), (aborted.Termination, aborted.Cells.Count, aborted.Complete, aborted.RunStatus, aborted.ExitCode));
        Assert.Null(aborted.Evaluation);
        var abortedDir = Path.Combine(_w.Root, "build", "simharness", "official", "dom-abort");
        var index = ReadNode(Path.Combine(abortedDir, DominanceOfficial.ResultFile));
        Assert.Equal((false, "NOT_EVALUATED", "Test.pin_broken"), (index["complete"]!.GetValue<bool>(), index["dominance_gate"]!.GetValue<string>(), index["abort_code"]!.GetValue<string>()));
        Assert.Equal(3, Directory.GetDirectories(Path.Combine(abortedDir, "cells")).Length);   // executed cells stay as evidence
        RefusedBeforeGames(Replay(abortedDir), "DomOfficial.run_not_complete");

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var stopped = DominanceOfficial.Execute(Preflight(_w.Approvals, runId: "dom-cancel"), "dom-cancel", _w.Approvals, _w.Root, cancelled.Token);
        Assert.Equal((DominanceRunTermination.Cancelled, 1, false, RunReport.StatusCancelled, 1), (stopped.Termination, stopped.Cells.Count, stopped.Complete, stopped.RunStatus, stopped.ExitCode));
        RefusedBeforeGames(Replay(Path.Combine(_w.Root, "build", "simharness", "official", "dom-cancel")), "DomOfficial.run_not_complete");

        // a pinned seed file changed after the preflight: the default re-verification aborts before the first game
        var root = CopyRoot();
        var report = Preflight(_w.Approvals, root: root, runId: "dom-changed");
        Assert.True(report.Ready);
        File.AppendAllText(Path.Combine(root, DominanceOfficialWorld.SeedPath(DominanceMatrix.Cells[10]).Replace('/', Path.DirectorySeparatorChar)), " ");
        var changed = DominanceOfficial.Execute(report, "dom-changed", _w.Approvals, root);
        Assert.Equal((DominanceRunTermination.AbortedIntegrity, 0, "Seed.file_hash_mismatch"), (changed.Termination, changed.Cells.Count, changed.Abort?.Code));
    }

    // ---------------------------------------------------------------- command line

    [Fact]
    public void The_48_cell_commands_accept_only_their_whitelisted_options()
    {
        Assert.Equal("official-dom", H.Ok(Cli.Parse(["official-dom", "--run-id", "r1"])).Name);
        Assert.Equal("replay-dom", H.Ok(Cli.Parse(["replay-dom", "--result", "x"])).Name);
        foreach (var forbidden in new[] { "--seed-manifest", "--seed-sha256", "--seeds", "--tuning", "--rules", "--advancement", "--event-budget" })
            Assert.Equal("Cli.option_forbidden", Cli.Parse(["official-dom", "--run-id", "r1", forbidden, "x"]).Error!.Value.Code);   // no plan input from the command line
        Assert.Equal("Cli.option_unknown", Cli.Parse(["official-dom", "--run-id", "r1", "--family", "fc3-cal"]).Error!.Value.Code);
        Assert.Equal("Cli.option_unknown", Cli.Parse(["official-dom", "--run-id", "r1", "--games-per-cell", "2"]).Error!.Value.Code);   // no hidden default can stand in for the freeze
        Assert.Equal("Cli.option_required", Cli.Parse(["official-dom"]).Error!.Value.Code);
        Assert.Equal("Cli.option_duplicate", Cli.Parse(["official-dom", "--run-id", "r1", "--run-id", "r1"]).Error!.Value.Code);
        Assert.Equal("Cli.option_required", Cli.Parse(["replay-dom"]).Error!.Value.Code);
        Assert.Equal("Cli.option_unknown", Cli.Parse(["replay-dom", "--result", "x", "--manifest", "y"]).Error!.Value.Code);
    }

    [Fact]
    public void The_official_48_cell_command_refuses_with_the_real_approvals_and_starts_no_game()
    {
        var runId = "fc2-dom-refusal-" + Guid.NewGuid().ToString("N")[..8];
        var child = Spawn("official-dom", "--run-id", runId);

        Assert.Equal(2, child.ExitCode);
        using var doc = JsonDocument.Parse(child.Output);
        var result = doc.RootElement;
        Assert.Equal((DominanceOfficial.ResultSchema, false, "PREFLIGHT_REFUSED", "NOT_EVALUATED", false), (result.GetProperty("schema_version").GetString(),
            result.GetProperty("official_evidence").GetBoolean(), result.GetProperty("run_status").GetString(), result.GetProperty("dominance_gate").GetString(), result.GetProperty("complete").GetBoolean()));
        Assert.False(result.GetProperty("preflight").GetProperty("ready").GetBoolean());
        Assert.Contains(result.GetProperty("preflight").GetProperty("checks").EnumerateArray(), c => c.GetProperty("code").GetString() == "Official.dominance_seed_manifests_not_frozen");
        Assert.Contains(result.GetProperty("preflight").GetProperty("checks").EnumerateArray(), c => c.GetProperty("code").GetString() == "Official.dominance_games_per_cell_not_frozen");
        Assert.False(Directory.Exists(Path.Combine(H.Root, "build", "simharness", "official", runId)));

        Assert.Equal(2, Program.Main(["replay-dom", "--result", _w.ResultDirectory]));       // a fixture result is not accepted against the real approvals
        Assert.Equal(2, Program.Main(["replay-dom", "--result", Path.Combine(_scratch, "nothing")]));
        Assert.Equal(2, Program.Main(["official-dom", "--run-id", runId, "--seed-manifest", "seeds/x.json"]));
    }

    private sealed record Child(int ExitCode, string Output, string Error);

    private static Child Spawn(params string[] arguments)
    {
        var host = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "") == "dotnet" ? Environment.ProcessPath! : "dotnet";
        var start = new ProcessStartInfo(host) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = H.Root };
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        foreach (var a in arguments)
            start.ArgumentList.Add(a);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(120_000), "the harness process did not finish in 120 s");
        return new Child(process.ExitCode, output.Result, error.Result);
    }
}
