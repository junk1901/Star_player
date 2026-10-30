using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using Microsoft.Win32.SafeHandles;
using StarPlayer.Domain.Common;
using System.Security.Cryptography;
using System.Xml.Linq;
using StarPlayer.SimHarness;
using StarPlayer.SimHarness.Evidence;
using static StarPlayer.SimHarness.Evidence.Schema;

namespace StarPlayer.EvidenceVerifier;

/// <summary>One executed test case as the TRX reported it: <c>{project}|{xUnit display name}</c> and the outcome.</summary>
public sealed record TestCaseResult(string CaseId, string Outcome);

/// <summary>
/// G4-T2 (F-01, F-02): the child <c>dotnet test</c> is started from an absolute <see cref="Dotnet"/> path (never a name Windows would search for in the parent's image directory, the current directory or
/// PATH) in a fixed <see cref="WorkingDirectory"/> (the build root, whose <c>global.json</c> pins the SDK), with the OWNER's extra start-environment prefixes removed as well (G4-T8).
/// </summary>
public sealed record TestRunner(string Dotnet, string WorkingDirectory, IReadOnlyList<string> ExtraForbiddenPrefixes)
{
    /// <summary>Outside an official session: the <c>dotnet.exe</c> of the runtime hosting this process (<c>dotnet\shared\Microsoft.NETCore.App\v\</c> three levels up).</summary>
    public static TestRunner FromRuntime(string workingDirectory) =>
        new(Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", "dotnet.exe")), workingDirectory, []);

    /// <summary>
    /// G4-04: the work root, the results directory and the TRX may be written only by the TCB or this account (an official session always sets this; R-WORK's real ACL is the G5 measurement). Without it
    /// the tree is still pinned by handle and the TRX read once, but a shared work tree (a developer's %TEMP%) is accepted.
    /// </summary>
    public bool RequireProtectedWorkTree { get; init; }

    /// <summary>The longest a child run may take before it is killed (<c>TestProfile.timeout</c>).</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>Tests only: arguments placed before <c>test</c> (a stand-in child that plays dotnet test).</summary>
    internal IReadOnlyList<string> LeadingArguments { get; init; } = [];

    /// <summary>Tests only: called with the pinned work root and results directory just before the child's temporary folder is made and the child starts.</summary>
    internal Action<string, string>? BeforeChild { get; init; }

    /// <summary>Tests only: called with the TRX path after it is opened and checked, before its bytes are read.</summary>
    internal Action<string>? AfterTrxOpen { get; init; }
}

/// <summary>G4-04: what the verifier asks of an object it reads a judgement input from (its own work tree): its owner and every writer are the TCB or this account.</summary>
[SupportedOSPlatform("windows")]
internal static partial class WorkTree
{
    // NSC-4 TCB (SYSTEM, BUILTIN\Administrators, NT SERVICE\TrustedInstaller); CREATOR OWNER resolves to the creator, who must already be a writer.
    private static readonly string[] Tcb = ["S-1-5-18", "S-1-5-32-544", "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464"];
    private const string CreatorOwner = "S-1-3-0";
    // FILE_WRITE_DATA/ADD_FILE, FILE_APPEND_DATA/ADD_SUBDIRECTORY, FILE_WRITE_EA, FILE_DELETE_CHILD, FILE_WRITE_ATTRIBUTES, DELETE, WRITE_DAC, WRITE_OWNER, GENERIC_ALL, GENERIC_WRITE
    private const int WriteMask = 0x2 | 0x4 | 0x10 | 0x40 | 0x100 | 0x10000 | 0x40000 | 0x80000 | 0x10000000 | 0x40000000;

    /// <summary>The first reason a descriptor lets someone other than the TCB or this account change the object or (inherited) its children, or null. Deny entries never relax anything.</summary>
    public static string? WriterProblem(byte[] descriptor)
    {
        var self = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
        var sd = new RawSecurityDescriptor(descriptor, 0);
        var owner = sd.Owner?.Value;
        if (owner is null || (!Tcb.Contains(owner) && owner != self))
            return $"소유자 {owner ?? "(없음)"}가 TCB나 이 계정이 아닙니다.";
        if (sd.DiscretionaryAcl is null)
            return "DACL이 없습니다(모두 쓰기 가능).";
        foreach (var ace in sd.DiscretionaryAcl)
        {
            if (ace is not CommonAce c)
                return $"해석하지 않는 ACE 형식 {ace.AceType}가 있습니다.";
            var sid = c.SecurityIdentifier.Value;
            if (c.AceQualifier == AceQualifier.AccessAllowed && (c.AccessMask & WriteMask) != 0 && !Tcb.Contains(sid) && sid != self && sid != CreatorOwner)
                return $"{sid}에게 쓰기·삭제·권한 변경 권한이 있습니다.";
        }

        return null;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", SetLastError = true)]
    private static unsafe partial uint GetFinalPathNameByHandle(SafeFileHandle file, ushort* path, uint length, uint flags);

    /// <summary>The real path of an open object (no <c>\\?\</c> prefix), or null.</summary>
    public static unsafe string? FinalPath(SafeFileHandle handle)
    {
        const int capacity = 32768;
        var buffer = new ushort[capacity];
        uint n;
        fixed (ushort* b = buffer)
            n = GetFinalPathNameByHandle(handle, b, capacity, 0);
        if (n == 0 || n >= capacity)
            return null;
        var path = new string(buffer.Take((int)n).Select(c => (char)c).ToArray());
        return path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path[4..] : path;
    }
}

/// <summary>
/// T06b §12: the PZL Test Profile. <see cref="Required"/> is the §12.2 table with each abbreviated name written out as the test assemblies list it; <see cref="Run"/> executes the profile's cases from the
/// test assemblies (SHA-checked against Build Provenance first) in a child <c>dotnet test</c> whose environment has no <c>DOTNET_</c> / <c>COMPlus_</c> variable (E4) and reads its own TRX; <see cref="Evidence"/>
/// turns the result into each check's <c>test_evidence</c>. A missing, extra, duplicated, skipped or not-executed case is <c>Evidence.test_profile_mismatch</c>; a failure is a FAIL case, never hidden.
/// </summary>
public static class TestProfiles
{
    private const string Infra = "StarPlayer.Infrastructure.Tests", Sim = "StarPlayer.Simulation.Tests", Harn = "StarPlayer.SimHarness.Tests", Dom = "StarPlayer.Domain.Tests";

    private static string C(string project, string ns, string name) => $"{project}|{ns}.{name}";

    /// <summary>§12.2 required cases per check (contract order of checks).</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Required { get; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["pzl-4"] =
        [
            C(Infra, Infra, "Ef1CorpusPinTests.Every_receipt_of_the_hundred_phase_2_games_is_byte_identical_to_the_one_before_ef2_existed"),
            C(Infra, Infra, "Ef1CorpusPinTests.The_legacy_event_writer_produces_the_same_bytes_for_the_hundred_phase_2_games_as_before_event_format_2_existed"),
            C(Infra, Infra, "EventJsonV2Tests.Receipts_of_both_fingerprint_versions_are_kept_as_written"),
            C(Sim, Sim, "PitchLocationEventTests.An_event_with_a_location_is_ef2_and_every_other_event_stays_ef1"),
            C(Sim, Sim, "PitchLocationEventTests.The_ef2_text_is_the_whole_ef1_content_plus_the_four_location_fields_in_a_fixed_order"),
        ],
        ["pzl-6"] =
        [
            C(Infra, Infra, "EventJsonV2Tests.A_version_2_event_list_round_trips_with_every_location_and_the_same_receipts"),
            C(Infra, Infra, "EventJsonV2Tests.A_version_2_game_replays_to_the_same_state_and_mixed_receipts_survive_their_own_json"),
            C(Infra, Infra, "EventJsonV2Tests.Only_format_version_2_is_read_as_version_2_and_a_legacy_array_is_never_promoted"),
            C(Infra, Infra, "EventJsonV2Tests.The_legacy_reader_reads_bare_arrays_gives_them_no_location_and_refuses_both_a_location_member_and_a_version_2_envelope"),
            C(Infra, Infra, "PzlJsonRoundTripReplayTests.A_located_engine_game_cannot_be_written_in_the_legacy_format_and_its_version_2_json_is_never_read_as_legacy"),
            C(Infra, Infra, "PzlJsonRoundTripReplayTests.Ten_engine_games_of_each_mode_round_trip_through_the_version_2_json_and_replay_to_the_same_receipts_and_final_state(rulesProfile: \"test-pzl-automated\", key: \"fc2-dry-cal-va\", firstSeed: 900101)"),
            C(Infra, Infra, "PzlJsonRoundTripReplayTests.Ten_engine_games_of_each_mode_round_trip_through_the_version_2_json_and_replay_to_the_same_receipts_and_final_state(rulesProfile: \"test-pzl-non-automated\", key: \"fc2-dry-cal-vn\", firstSeed: 900151)"),
        ],
        ["pzl-7"] =
        [
            .. new[] { "p1", "p2", "i1", "t1" }.Select(id => C(Infra, Infra, $"EventJsonV2Tests.A_version_2_pitch_event_without_a_location_is_an_error(id: \"{id}\")")),
            .. new[] { "p1", "i1", "t1" }.Select(id => C(Infra, Infra, $"EventJsonV2Tests.A_version_2_pitch_event_with_a_null_location_is_an_error(id: \"{id}\")")),
            C(Infra, Infra, "EventJsonV2Tests.An_unsupported_zone_model_an_unknown_zone_a_bad_side_and_a_wrong_class_are_each_rejected_without_correction"),
            C(Infra, Infra, "EventJsonV2Tests.A_location_member_that_is_missing_or_unknown_is_a_json_error"),
            C(Infra, Infra, "EventJsonV2Tests.A_non_pitch_event_neither_needs_nor_accepts_a_location"),
            C(Infra, Infra, "EventJsonV2Tests.The_legacy_writer_refuses_an_event_with_a_location_instead_of_dropping_it_and_the_version_2_writer_refuses_a_pitch_without_one"),
            C(Sim, Sim, "PitchLocationEventTests.A_location_cannot_be_built_with_an_unknown_model_a_wrong_class_an_unknown_zone_or_an_invalid_side"),
        ],
        ["pzl-8"] =
        [
            C(Infra, Infra, "PitchBranchTendencyTests.No_tendency_and_the_explicit_neutral_vector_give_the_same_draft_and_the_same_distributions"),
            C(Infra, Infra, "PitchBranchTendencyTests.A_neutral_tendency_moves_no_probability_in_any_stage"),
            C(Infra, Infra, "PitchBranchTendencyTests.A_tendency_consumes_no_random_number_and_never_changes_a_roll"),
        ],
        ["pzl-9"] =
        [
            C(Infra, Infra, "PitchBranchTendencyTests.Swing_result_and_batted_ball_shift_the_favourable_mass_by_exactly_value_times_cap_over_100_and_no_other_stage_moves"),
            C(Infra, Infra, "PitchBranchTendencyTests.The_shift_is_monotonic_in_the_tendency_value_for_the_same_pitch"),
        ],
        ["pzl-10"] =
        [
            C(Infra, Infra, "FramingScopeMatrixTests.The_oracle_covers_every_zone_mode_and_rating_of_the_approved_matrix"),
            C(Infra, Infra, "FramingScopeMatrixTests.Every_oracle_case_equals_the_production_distribution_the_applied_flag_and_the_effective_factor"),
            C(Infra, Infra, "FramingScopeMatrixTests.Automated_never_applies_framing_at_any_rating_in_any_zone"),
            C(Infra, Infra, "FramingScopeMatrixTests.Non_automated_clear_zones_never_apply_framing_and_borderline_zones_do"),
            C(Infra, Infra, "FramingScopeMatrixTests.Neutral_framing_and_no_framing_input_give_the_baseline_in_every_condition_and_the_same_trace_class"),
            C(Harn, Harn, "PzlCheckTests.Pzl10_an_automated_run_has_no_non_neutral_framing_factor_while_the_non_automated_control_shows_the_counter_working"),
            C(Harn, Harn, "PzlCheckTests.Pzl10_a_non_neutral_factor_fails_an_automated_run_and_no_called_pitch_is_not_evaluated"),
            C(Dom, Dom, "GameRulesCalledPitchModeTests.Automated_mode_means_framing_cannot_change_a_call_and_non_automated_allows_it_within_later_caps"),
        ],
        ["pzl-11"] =
        [
            C(Dom, Dom, "GameRulesCalledPitchModeTests.The_legacy_content_hash_is_the_value_computed_before_the_pitch_zone_fields_existed"),
            C(Dom, Dom, "GameRulesCalledPitchModeTests.A_legacy_snapshot_does_not_declare_a_called_pitch_mode_or_zone_model_and_says_so"),
            C(Dom, Dom, "GameRulesCalledPitchModeTests.Adding_the_new_fields_leaves_every_other_rule_value_identical_to_the_legacy_snapshot"),
            C(Infra, Infra, "GameRulesV2ProfileTests.Every_legacy_profile_keeps_its_independently_computed_content_hash"),
            C(Infra, Infra, "GameRulesV2ProfileTests.Legacy_profiles_declare_no_called_pitch_mode_including_the_kbo_oriented_one_and_are_schema_v1"),
            C(Infra, Infra, "Phase3B2CompatibilityTests.The_legacy_gamerules_profiles_still_declare_no_called_pitch_mode_and_keep_seven_hashes"),
        ],
        ["pzl-12a"] =
        [
            C(Harn, Harn, "PzlCheckTests.Pzl12a_the_registered_streams_are_exactly_the_pinned_game_set_and_real_runs_draw_only_from_them"),
            C(Harn, Harn, "PzlCheckTests.Pzl12a_a_presentation_stream_in_the_registry_or_in_a_game_fails_and_12b_is_always_deferred_never_pass"),
            C(Harn, Harn, "PzlCheckTests.Pzl12a_the_harness_and_its_result_hashes_depend_on_no_presentation_or_infrastructure_assembly"),
        ],
    };

    private static EvidenceException Mismatch(string cause, string message) => EvidenceCodes.Fail(EvidenceCodes.TestProfileMismatch, cause, "", "test_profile", message);

    /// <summary>The profile's cases must include every §12.2 required case of each check.</summary>
    public static void CheckRequired(JObject profile)
    {
        var checks = Items(profile["checks"]).Select(Obj).ToDictionary(c => Text(c["check_id"]), c => Items(c["cases"]).Select(Text).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var (check, cases) in Required)
        {
            var have = checks.GetValueOrDefault(check) ?? [];
            if (cases.FirstOrDefault(c => !have.Contains(c)) is { } missing)
                throw Mismatch("TestProfile.required_case_missing", $"{check}: 필수 Case '{missing}'가 Profile에 없습니다(§12.2).");
        }
    }

    /// <summary>
    /// §12.1 fixture set: every file tracked under <c>tests/Fixtures/**</c> and <c>content/**</c> at <paramref name="commit"/>, path ordinal order, SHA-256 of the <c>git cat-file blob</c> bytes. An official
    /// session passes its pinned <paramref name="git"/> (G4-T2, F-09).
    /// </summary>
    public static IReadOnlyList<JObject> Fixtures(string repositoryRoot, string commit, string git = "git")
    {
        var listed = GitIntegrity.Run(repositoryRoot, ["ls-tree", "-r", "--name-only", commit, "--", "tests/Fixtures", "content"], git);
        if (listed.ExitCode != 0)
            throw Mismatch("TestProfile.git_tree", $"git ls-tree {commit} 실패: {listed.Stderr}");
        var paths = System.Text.Encoding.UTF8.GetString(listed.Stdout).Split('\n', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal).ToList();
        return paths.Select(p =>
        {
            var blob = GitIntegrity.Run(repositoryRoot, ["cat-file", "blob", $"{commit}:{p}"], git);
            if (blob.ExitCode != 0)
                throw Mismatch("TestProfile.git_blob", $"git cat-file {p} 실패");
            return J.O(("path", J.S(p)), ("git_blob_sha256", J.S(Convert.ToHexString(SHA256.HashData(blob.Stdout)).ToLowerInvariant())), ("byte_length", J.N(blob.Stdout.Length)));
        }).ToList();
    }

    /// <summary>§12.1: the profile's fixture list must be exactly the tree's.</summary>
    public static void CheckFixtures(JObject profile, string repositoryRoot, string git = "git")
    {
        var expected = Fixtures(repositoryRoot, Text(profile["source_git_sha"]), git);
        if (!Jcs.Bytes(J.A(expected)).AsSpan().SequenceEqual(Jcs.Bytes(profile["fixtures"])))
            throw Mismatch("TestProfile.fixtures", "Profile의 fixtures가 source_git_sha 트리의 tests/Fixtures/**·content/** 추적 파일 집합·Blob SHA와 다릅니다.");
    }

    /// <summary>The TEST_ASSEMBLY artifact of the Build Provenance must have exactly these bytes before it is run (§12.3).</summary>
    public static void CheckAssembly(JObject buildProvenance, string relativePath, byte[] bytes)
    {
        var a = Items(buildProvenance["artifacts"]).Select(Obj).FirstOrDefault(x => Text(x["role"]) == "TEST_ASSEMBLY" && Text(x["relative_path"]) == relativePath)
            ?? throw Mismatch("TestProfile.assembly_unknown", $"'{relativePath}'는 Build Provenance의 TEST_ASSEMBLY가 아닙니다.");
        if (Text(a["raw_sha256"]) != Archive.Sha(bytes) || Long(a["byte_length"]) != bytes.Length)
            throw Mismatch("TestProfile.assembly_changed", $"'{relativePath}'의 바이트가 Build Provenance와 다릅니다.");
    }

    /// <summary>The xUnit method a case id names (display name up to the parameter list).</summary>
    public static string Method(string caseId) => caseId[(caseId.IndexOf('|') + 1)..] is var name && name.IndexOf('(') is var p and >= 0 ? name[..p] : caseId[(caseId.IndexOf('|') + 1)..];

    private static PinnedDirectory Pinned(Result<PinnedDirectory> r, string what) =>
        r.IsSuccess ? r.Value : throw Mismatch("TestProfile.results_directory", $"{what}를 핸들로 고정할 수 없습니다(Reparse·교체·이미 있음, G4-04): {r.Error!.Value.Message}");

    [SupportedOSPlatform("windows")]
    private static void Writers(string path, byte[] descriptor)
    {
        if (WorkTree.WriterProblem(descriptor) is { } why)
            throw Mismatch("TestProfile.results_acl", $"'{path}': {why} (G4-04, 운영 R-WORK ACL은 G5)");
    }

    /// <summary>
    /// Runs the given cases of ONE test assembly in a child <c>dotnet test</c> (forbidden prefixes removed from its environment, E4) and returns every result of the methods they name (so a case the profile
    /// does not list but the assembly now has is seen, not dropped).
    /// <list type="bullet">
    /// <item>G4-04 (F-17): the work root and a NEW results directory are pinned by handle from the volume root (no link followed, no rename or delete while held) and, when
    /// <see cref="TestRunner.RequireProtectedWorkTree"/>, may be written only by the TCB or this account; the TRX is opened once without write or delete sharing, its real path and writers are checked through that handle, it must be the only TRX, and its bytes are read from that same handle.</item>
    /// <item>G4-TEMP: the child's TEMP and TMP are a NEW <c>temp</c> folder pinned inside the results directory the same way (and checked the same way), never the parent's TEMP: <c>dotnet test</c> looks
    /// there for a post-processing folder before it exits. It is made before the child starts; if it cannot be made or checked, no child starts. A TRX dropped into it is a second TRX.</item>
    /// <item>G4-02: exit code 0 means every case ran and none failed; 1 means real case failures, which the TRX must show (they stay FAIL cases, never hidden); anything else, a 0 with a failed case, a 1
    /// without one, a missing or extra TRX, a start failure, a timeout or a cancellation is an infrastructure failure (<c>Evidence.test_profile_mismatch</c>), never a result.</item>
    /// <item>G4-FIX-01: the TRX's run-level state (<see cref="RunSummary"/>) must be clean and agree with the exit code; a run-level error outranks case failures.</item>
    /// <item>G4-FIX-02: the time limit and <paramref name="cancel"/> cover the child's exit and the end of its stdout and stderr together; on either the process tree is killed.</item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<TestCaseResult> Run(string project, string assemblyPath, IEnumerable<string> caseIds, string workDirectory, TestRunner runner, CancellationToken cancel = default)
    {
        var methods = caseIds.Where(c => c.StartsWith(project + "|", StringComparison.Ordinal)).Select(Method).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (methods.Count == 0)
            return [];
        if (!OperatingSystem.IsWindows())
            throw Mismatch("TestProfile.platform", "Test Profile 실행은 Windows 핸들 고정이 필요합니다.");
        if (!Path.IsPathFullyQualified(runner.Dotnet) || !File.Exists(runner.Dotnet) || !Path.IsPathFullyQualified(runner.WorkingDirectory) || !Directory.Exists(runner.WorkingDirectory))
            throw Mismatch("TestProfile.runner", $"시험 실행 파일 '{runner.Dotnet}'·작업 디렉터리 '{runner.WorkingDirectory}'가 존재하는 절대경로가 아닙니다(G4-T2).");
        using var root = Pinned(PinnedDirectory.Open(workDirectory, [], create: true, createRoot: true), "작업 Root");
        if (runner.RequireProtectedWorkTree)
            Writers(root.FullPath, new DirectoryInfo(root.FullPath).GetAccessControl().GetSecurityDescriptorBinaryForm());       // the path resolves to the pinned object
        using var results = Pinned(root.CreateSubdirectory($"{project}-{Guid.NewGuid():N}"), "결과 디렉터리");
        if (runner.RequireProtectedWorkTree)
            Writers(results.FullPath, new DirectoryInfo(results.FullPath).GetAccessControl().GetSecurityDescriptorBinaryForm());
        runner.BeforeChild?.Invoke(root.FullPath, results.FullPath);
        using var temp = Pinned(results.CreateSubdirectory("temp"), "자식 임시 디렉터리");
        if (runner.RequireProtectedWorkTree)
            Writers(temp.FullPath, new DirectoryInfo(temp.FullPath).GetAccessControl().GetSecurityDescriptorBinaryForm());

        var trx = Path.Combine(results.FullPath, $"{project}.trx");
        var filter = string.Join('|', methods.Select(m => $"FullyQualifiedName={m}"));
        var start = new ProcessStartInfo(runner.Dotnet) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = runner.WorkingDirectory };
        foreach (var arg in runner.LeadingArguments.Concat(["test", assemblyPath, "--filter", filter, "--logger", $"trx;LogFileName={Path.GetFileName(trx)}", "--results-directory", results.FullPath]))
            start.ArgumentList.Add(arg);
        foreach (var name in ExecutionEnvironment.ForbiddenVariables(Environment.GetEnvironmentVariables()))
            start.Environment.Remove(name);
        foreach (var name in start.Environment.Keys.Where(k => runner.ExtraForbiddenPrefixes.Any(x => k.StartsWith(x, StringComparison.OrdinalIgnoreCase))).ToList())
            start.Environment.Remove(name);
        start.Environment["TEMP"] = temp.FullPath;                                                   // set last, so no prefix removal can drop them (names are case-insensitive on Windows)
        start.Environment["TMP"] = temp.FullPath;

        Process? started;
        try
        {
            started = Process.Start(start);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw Mismatch("TestProfile.start_failed", $"{project} 시험 프로세스를 시작하지 못했습니다: {e.Message}");
        }

        int exit;
        using (var p = started ?? throw Mismatch("TestProfile.start_failed", $"{project} 시험 프로세스를 시작하지 못했습니다."))
        using (var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel))
        {
            // G4-FIX-02: one budget covers the exit AND the end of both pipes (a process the child started may hold them open after the child exits); nothing below waits outside it.
            limit.CancelAfter(runner.Timeout);
            var finished = false;
            try
            {
                Task.WhenAll(p.WaitForExitAsync(limit.Token), p.StandardOutput.ReadToEndAsync(limit.Token), p.StandardError.ReadToEndAsync(limit.Token)).WaitAsync(limit.Token).GetAwaiter().GetResult();
                finished = true;
            }
            catch (OperationCanceledException)
            {
                throw cancel.IsCancellationRequested
                    ? Mismatch("TestProfile.cancelled", $"{project} 시험 실행이 취소되었습니다.")
                    : Mismatch("TestProfile.timeout", $"{project} 시험 실행(종료와 stdout·stderr 끝)이 {runner.Timeout} 안에 끝나지 않았습니다.");
            }
            finally
            {
                if (!finished)
                    Stop(p);
                p.StandardOutput.Dispose();
                p.StandardError.Dispose();
            }

            exit = p.ExitCode;
        }

        if (exit is not (0 or 1))
            throw Mismatch("TestProfile.exit_code", $"{project} 시험 프로세스 종료 코드 {exit}는 실행 인프라 실패입니다(TRX와 무관하게 결과로 쓰지 않음, G4-02).");
        byte[] bytes;
        FileStream s;
        try
        {
            s = new FileStream(trx, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw Mismatch("TestProfile.trx", $"{project}: 기대한 TRX를 열 수 없습니다(없음·잠김, 종료 코드 {exit}): {e.Message}");
        }

        using (s)
        {
            if (WorkTree.FinalPath(s.SafeFileHandle) is not { } final || !string.Equals(final, trx, StringComparison.OrdinalIgnoreCase))
                throw Mismatch("TestProfile.trx", $"{project}: 연 TRX의 실제 경로가 기대 경로가 아닙니다(Reparse·교체, G4-04).");
            if (runner.RequireProtectedWorkTree)
                Writers(trx, s.GetAccessControl().GetSecurityDescriptorBinaryForm());
            if (Directory.GetFiles(results.FullPath, "*.trx", SearchOption.AllDirectories) is not [var only] || !string.Equals(only, trx, StringComparison.OrdinalIgnoreCase))
                throw Mismatch("TestProfile.trx", $"{project}: 결과 디렉터리에 기대한 TRX 하나만 있지 않습니다(G4-T7).");
            runner.AfterTrxOpen?.Invoke(trx);
            using var m = new MemoryStream();
            s.CopyTo(m);
            bytes = m.ToArray();
        }

        var parsed = Trx(project, bytes);
        var outcome = RunSummary(project, bytes, parsed);
        if (exit == 0 && parsed.Any(r => r.Outcome == "Failed"))
            throw Mismatch("TestProfile.exit_inconsistent", $"{project}: 종료 코드 0인데 TRX에 실패 Case가 있습니다(G4-02).");
        if (exit == 1 && !parsed.Any(r => r.Outcome == "Failed"))
            throw Mismatch("TestProfile.exit_code", $"{project}: 종료 코드 1인데 TRX에 실패 Case가 없습니다(인프라 실패를 성공으로 보지 않음, G4-02).");
        if (outcome != (exit == 0 ? "Completed" : "Failed"))
            throw Mismatch("TestProfile.exit_inconsistent", $"{project}: 종료 코드 {exit}와 TRX ResultSummary outcome '{outcome}'가 맞지 않습니다(G4-FIX-01).");
        return parsed;
    }

    /// <summary>G4-FIX-02: best-effort kill of the child and everything it started (the pipes close with them); the run is reported as failed either way.</summary>
    private static void Stop(Process p)
    {
        try
        {
            p.Kill(entireProcessTree: true);
            p.WaitForExit(TimeSpan.FromSeconds(10));
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or AggregateException)
        {
        }
    }

    private static readonly XNamespace TrxNs = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    // Counters that only a run-level problem (testhost crash, abort, adapter error, disconnection) makes non-zero.
    private static readonly string[] RunErrorCounters = ["error", "timeout", "aborted", "passedButRunAborted", "notRunnable", "disconnected"];

    private static XElement TrxRoot(string project, byte[] trx)
    {
        XDocument doc;
        try
        {
            using var reader = System.Xml.XmlReader.Create(new MemoryStream(trx), new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null });
            doc = XDocument.Load(reader);
        }
        catch (System.Xml.XmlException e)
        {
            throw Mismatch("TestProfile.trx", $"{project}: TRX를 XML로 읽을 수 없습니다: {e.Message}");
        }

        return doc.Root is { } root && root.Name == TrxNs + "TestRun" ? root : throw Mismatch("TestProfile.trx", $"{project}: TRX의 뿌리가 VSTest 2010 TestRun이 아닙니다(형식·버전 불일치).");
    }

    /// <summary>The results of a TRX file (VSTest 2010 schema): <c>UnitTestResult/@testName</c> and <c>@outcome</c>.</summary>
    public static IReadOnlyList<TestCaseResult> Trx(string project, byte[] trx) =>
        TrxRoot(project, trx).Descendants(TrxNs + "UnitTestResult").Select(r => (string?)r.Attribute("testName") is { } name && (string?)r.Attribute("outcome") is { } outcome
            ? new TestCaseResult($"{project}|{name}", outcome)
            : throw Mismatch("TestProfile.trx", $"{project}: testName·outcome이 없는 UnitTestResult가 있습니다.")).ToList();

    /// <summary>
    /// G4-FIX-01: the run as a whole, not only its cases. Exactly one <c>ResultSummary</c> with one <c>Counters</c> whose totals equal the <paramref name="cases"/>, or <c>TestProfile.trx</c> (fail closed on any
    /// other shape). Any <c>RunInfo</c> (VSTest's run-level error or warning: testhost crash, abort, adapter error, no matching test; a normal run writes none), a non-zero run-error counter or an outcome other
    /// than <c>Completed</c>/<c>Failed</c> is <c>TestProfile.run_error</c>, an infrastructure failure even when cases failed too. Returns the summary outcome.
    /// </summary>
    private static string RunSummary(string project, byte[] trx, IReadOnlyList<TestCaseResult> cases)
    {
        EvidenceException Shape(string what) => Mismatch("TestProfile.trx", $"{project}: TRX {what}(G4-FIX-01).");
        EvidenceException RunError(string what) => Mismatch("TestProfile.run_error", $"{project}: TRX에 실행 수준 오류가 있습니다: {what}(케이스 실패와 무관하게 인프라 실패, G4-FIX-01).");
        var root = TrxRoot(project, trx);
        if (root.Elements(TrxNs + "ResultSummary").ToList() is not [var summary])
            throw Shape("ResultSummary가 정확히 하나가 아닙니다");
        if (summary.Elements(TrxNs + "Counters").ToList() is not [var counters])
            throw Shape("ResultSummary/Counters가 정확히 하나가 아닙니다");
        int Count(string name) => int.TryParse((string?)counters.Attribute(name), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n)
            ? n : throw Shape($"Counters/@{name}가 0 이상의 정수가 아닙니다");
        var outcome = (string?)summary.Attribute("outcome") ?? throw Shape("ResultSummary/@outcome이 없습니다");

        if (root.Descendants(TrxNs + "RunInfo").FirstOrDefault() is { } info)
            throw RunError($"RunInfo {(string?)info.Attribute("outcome")}: {info.Value.Trim()}");
        if (RunErrorCounters.FirstOrDefault(c => Count(c) != 0) is { } counter)
            throw RunError($"Counters/@{counter} = {Count(counter)}");
        if (outcome is not ("Completed" or "Failed"))
            throw RunError($"ResultSummary outcome '{outcome}'");
        if (Count("total") != cases.Count || Count("passed") != cases.Count(c => c.Outcome == "Passed") || Count("failed") != cases.Count(c => c.Outcome == "Failed"))
            throw Shape("Counters(total·passed·failed)가 UnitTestResult와 다릅니다");
        return outcome;
    }

    /// <summary>
    /// §12.3: the TestEvidence of each profile check from the verifier's own results. Within the methods a check names, the executed set must be exactly the check's cases, each once, Passed or Failed.
    /// </summary>
    public static IReadOnlyDictionary<string, JObject> Evidence(JObject profile, string profileSha256, IReadOnlyList<TestCaseResult> results)
    {
        var byId = results.GroupBy(r => r.CaseId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var evidence = new Dictionary<string, JObject>(StringComparer.Ordinal);
        foreach (var check in Items(profile["checks"]).Select(Obj))
        {
            var id = Text(check["check_id"]);
            var cases = Items(check["cases"]).Select(Text).ToList();
            var methods = cases.Select(Method).ToHashSet(StringComparer.Ordinal);
            var executed = results.Where(r => methods.Contains(Method(r.CaseId))).Select(r => r.CaseId).Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
            if (!executed.SetEquals(cases))
                throw Mismatch("TestProfile.case_set", $"{id}: 실행된 Case 집합이 Profile과 다릅니다(누락 {string.Join(", ", cases.Except(executed))}; 추가 {string.Join(", ", executed.Except(cases))}).");
            var rows = new List<JNode>();
            foreach (var c in cases.Order(StringComparer.Ordinal))
            {
                var r = byId[c];
                if (r.Count != 1)
                    throw Mismatch("TestProfile.case_duplicate", $"{id}: '{c}'가 {r.Count}번 실행되었습니다.");
                var status = r[0].Outcome switch
                {
                    "Passed" => "PASS",
                    "Failed" => "FAIL",
                    _ => throw Mismatch("TestProfile.case_not_executed", $"{id}: '{c}'의 결과가 {r[0].Outcome}입니다(Skipped·NotExecuted 불인정)."),
                };
                rows.Add(J.O(("id", J.S(c)), ("status", J.S(status))));
            }

            evidence[id] = J.O(("profile_id", profile["profile_id"]), ("profile_sha256", J.S(profileSha256)), ("source_git_sha", profile["source_git_sha"]), ("cases", J.A(rows)));
        }

        return evidence;
    }

    /// <summary>True when every case of the evidence passed.</summary>
    public static bool AllPass(JObject testEvidence) => Items(testEvidence["cases"]).Select(Obj).All(c => Text(c["status"]) == "PASS");

    public const string RegressionProofFormat = "simharness-regression-proof-v1";

    /// <summary>
    /// B1 T11 R2 (§21 REGRESSION_PROOF "승인 검사 프로파일별 1세트", §12.3 2): the TestEvidence of every check of the approved profile, in the profile's check order, as JCS bytes
    /// <c>{format, checks: [{check_id, test_evidence}]}</c>. A submitted proof must be exactly these bytes of the verifier's own run.
    /// </summary>
    public static byte[] RegressionProof(TestProfileRun run) => Jcs.Bytes(J.O(("format", J.S(RegressionProofFormat)),
        ("checks", J.A(ApprovalFormats.TestProfileChecks.Select(c => (JNode)J.O(("check_id", J.S(c)), ("test_evidence", run.Evidence[c]))).ToArray()))));
}
