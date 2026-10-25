using System.Diagnostics;
using System.Security.Cryptography;
using System.Xml.Linq;
using StarPlayer.SimHarness;
using StarPlayer.SimHarness.Evidence;
using static StarPlayer.SimHarness.Evidence.Schema;

namespace StarPlayer.EvidenceVerifier;

/// <summary>One executed test case as the TRX reported it: <c>{project}|{xUnit display name}</c> and the outcome.</summary>
public sealed record TestCaseResult(string CaseId, string Outcome);

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

    /// <summary>§12.1 fixture set: every file tracked under <c>tests/Fixtures/**</c> and <c>content/**</c> at <paramref name="commit"/>, path ordinal order, SHA-256 of the <c>git cat-file blob</c> bytes.</summary>
    public static IReadOnlyList<JObject> Fixtures(string repositoryRoot, string commit)
    {
        var listed = GitIntegrity.Run(repositoryRoot, ["ls-tree", "-r", "--name-only", commit, "--", "tests/Fixtures", "content"]);
        if (listed.ExitCode != 0)
            throw Mismatch("TestProfile.git_tree", $"git ls-tree {commit} 실패: {listed.Stderr}");
        var paths = System.Text.Encoding.UTF8.GetString(listed.Stdout).Split('\n', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal).ToList();
        return paths.Select(p =>
        {
            var blob = GitIntegrity.Run(repositoryRoot, ["cat-file", "blob", $"{commit}:{p}"]);
            if (blob.ExitCode != 0)
                throw Mismatch("TestProfile.git_blob", $"git cat-file {p} 실패");
            return J.O(("path", J.S(p)), ("git_blob_sha256", J.S(Convert.ToHexString(SHA256.HashData(blob.Stdout)).ToLowerInvariant())), ("byte_length", J.N(blob.Stdout.Length)));
        }).ToList();
    }

    /// <summary>§12.1: the profile's fixture list must be exactly the tree's.</summary>
    public static void CheckFixtures(JObject profile, string repositoryRoot)
    {
        var expected = Fixtures(repositoryRoot, Text(profile["source_git_sha"]));
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

    /// <summary>
    /// Runs the given cases of ONE test assembly in a child <c>dotnet test</c> (forbidden prefixes removed from its environment, E4) and returns every result of the methods they name (so a case the profile
    /// does not list but the assembly now has is seen, not dropped).
    /// </summary>
    public static IReadOnlyList<TestCaseResult> Run(string project, string assemblyPath, IEnumerable<string> caseIds, string workDirectory)
    {
        var methods = caseIds.Where(c => c.StartsWith(project + "|", StringComparison.Ordinal)).Select(Method).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (methods.Count == 0)
            return [];
        Directory.CreateDirectory(workDirectory);
        var trx = $"{project}-{Guid.NewGuid():N}.trx";
        var filter = string.Join('|', methods.Select(m => $"FullyQualifiedName={m}"));
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var arg in new[] { "test", assemblyPath, "--filter", filter, "--logger", $"trx;LogFileName={trx}", "--results-directory", workDirectory })
            start.ArgumentList.Add(arg);
        foreach (var name in ExecutionEnvironment.ForbiddenVariables(Environment.GetEnvironmentVariables()))
            start.Environment.Remove(name);
        using (var p = Process.Start(start)!)
        {
            var stdout = p.StandardOutput.ReadToEndAsync();
            var stderr = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(30 * 60 * 1000))
            {
                p.Kill(entireProcessTree: true);
                throw Mismatch("TestProfile.timeout", $"{project} 시험 실행이 30분 안에 끝나지 않았습니다.");
            }

            _ = stdout.Result + stderr.Result;
        }

        return Trx(project, File.ReadAllBytes(Path.Combine(workDirectory, trx)));
    }

    /// <summary>The results of a TRX file (VSTest 2010 schema): <c>UnitTestResult/@testName</c> and <c>@outcome</c>.</summary>
    public static IReadOnlyList<TestCaseResult> Trx(string project, byte[] trx)
    {
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        using var m = new MemoryStream(trx);
        var doc = XDocument.Load(m);
        return doc.Descendants(ns + "UnitTestResult").Select(r => new TestCaseResult($"{project}|{(string)r.Attribute("testName")!}", (string)r.Attribute("outcome")!)).ToList();
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
