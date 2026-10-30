using System.Collections;
using System.Security.AccessControl;
using System.Security.Principal;
using StarPlayer.SimHarness.Evidence;
using static StarPlayer.EvidenceVerifier.Tests.G4Fixtures;

namespace StarPlayer.EvidenceVerifier.Tests;

/// <summary>
/// G4 official entry point (B-4 OWNER decision draft §4, T1-T8) and the Codex G4 FIX_REQUIRED corrections G4-01..04: the installation settings, the process binding (start environment, host, pins, running
/// verifier, SimHarness copy and the SimHarness in use, every loaded file pinned), the held folders, the child runner (exit code with TRX), the pinned results tree and the CLI order (E2-1 first). Each guard
/// has a passing case and a counterexample that must refuse; loads that cannot be undone run in an isolated child process. All values are TEST_ONLY; none is an OWNER value.
/// </summary>
[Collection(CertificationSeams.Name)]
public sealed class G4OfficialEntryTests(TestOnlyWorld w) : IClassFixture<TestOnlyWorld>
{
    private static EvidenceError Refused(Action a) => Assert.Throws<EvidenceException>(a).Error;

    private static readonly Func<string, string?> AclOk = _ => null;

    private static readonly Func<bool> NotElevated = () => false;

    private static string Host => Environment.ProcessPath!;

    private string Temp(string name)
    {
        var d = Path.Combine(w.Root, $"g4-{name}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(d);
        return d;
    }

    private static readonly SecurityIdentifier Self = WindowsIdentity.GetCurrent().User!;

    /// <summary>A directory whose DACL no longer inherits and names only this account, SYSTEM and Administrators (what G5 asks of R-WORK; the developer's %TEMP% grants more).</summary>
    private string Protected(string name)
    {
        var d = new DirectoryInfo(Temp(name));
        var acl = new DirectorySecurity();
        acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (var sid in new[] { Self, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null) })
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        d.SetAccessControl(acl);
        return d.FullName;
    }

    /// <summary>A config for this test process: its host as "dotnet", a scratch file as "git", no execution pin, no dynamic exception.</summary>
    private InstallConfig Config(IReadOnlyList<string>? prefixes = null, string? dotnetSha = null)
    {
        var git = Path.Combine(Temp("git"), "git.exe");
        File.WriteAllBytes(git, [1, 2, 3]);
        return G4Fixtures.Config(w.Root, [], [], prefixes, dotnetSha) with { Git = new ToolPin(git, Sha(git)) };
    }

    // ---------------------------------------------------------------- T1 installation settings

    private string WriteConfig(Func<JObject, JObject>? edit = null)
    {
        var dir = Temp("config");
        var o = J.O(("format", J.S(InstallConfigReader.Format)), ("anchor_path", J.S(Path.Combine(dir, "anchor.json"))), ("build_root", J.S(BuildRoot)),
            ("archive_root", J.S(Path.Combine(dir, "archive"))), ("work_root", J.S(Path.Combine(dir, "work"))), ("dotnet", J.O(("path", J.S(Host)), ("raw_sha256", J.S(Sha(Host))))),
            ("git", J.O(("path", J.S(Host)), ("raw_sha256", J.S(Sha(Host))))), ("execution_pins", J.A()), ("startup_forbidden_prefixes", J.A(J.S("CORECLR_"), J.S("VSTEST_"))),
            ("allowed_dynamic_assemblies", J.A()), ("unpinned_output_files", J.A()));
        var path = Path.Combine(dir, "install-config.json");
        File.WriteAllBytes(path, Jcs.Bytes(edit?.Invoke(o) ?? o));
        return path;
    }

    [Fact]
    public void A_well_formed_installation_config_is_read_once_with_canonical_roots_and_its_bytes_hashed()
    {
        var path = WriteConfig();
        var c = InstallConfigReader.Read(path, AclOk, NotElevated);
        Assert.Equal(BuildRoot, c.BuildRoot);
        Assert.Equal(["CORECLR_", "VSTEST_"], c.StartupForbiddenPrefixes);
        Assert.Empty(c.AllowedDynamicAssemblies);
        Assert.Equal(Sha(path), c.RawSha256);
    }

    [Fact]
    public void A_missing_elevated_writable_unknown_field_unsorted_root_internal_or_root_trusting_config_is_refused()
    {
        Assert.Equal("Trust.install_config_missing", Refused(() => InstallConfigReader.Read(Path.Combine(w.Root, "none.json"), AclOk, NotElevated)).Cause);
        Assert.Equal("Trust.install_config_untrusted", Refused(() => InstallConfigReader.Read(WriteConfig(), AclOk, () => true)).Cause);
        Assert.Equal("Trust.install_config_untrusted", Refused(() => InstallConfigReader.Read(WriteConfig(), _ => "Users: write", NotElevated)).Cause);
        Assert.Equal(EvidenceCodes.SchemaInvalid, Refused(() => InstallConfigReader.Read(WriteConfig(o => JEditMembers.With(o, "extra", J.S("x"))), AclOk, NotElevated)).Code);
        Assert.Equal(EvidenceCodes.SchemaInvalid, Refused(() => InstallConfigReader.Read(WriteConfig(o => JEditMembers.With(o, "startup_forbidden_prefixes", J.A(J.S("VSTEST_"), J.S("CORECLR_")))), AclOk, NotElevated)).Code);
        Assert.Equal(EvidenceCodes.SchemaInvalid, Refused(() => InstallConfigReader.Read(WriteConfig(o => JEditMembers.With(o, "build_root", J.S("relative\\root"))), AclOk, NotElevated)).Code);
        Assert.Equal("Trust.install_config_untrusted", Refused(() => InstallConfigReader.Read(WriteConfig(o => JEditMembers.With(o, "anchor_path", J.S(Path.Combine(BuildRoot, "anchor.json")))), AclOk, NotElevated)).Cause);
        // G4-01: a root that trusts every file under it is no longer part of the settings
        Assert.Equal(EvidenceCodes.SchemaInvalid, Refused(() => InstallConfigReader.Read(WriteConfig(o => JEditMembers.With(o, "allowed_module_roots", J.A(J.S(Environment.GetFolderPath(Environment.SpecialFolder.Windows))))), AclOk, NotElevated)).Code);
    }

    // ---------------------------------------------------------------- T8 start environment, host and pins

    [Fact]
    public void The_binding_holds_the_pinned_host_and_tools_so_they_cannot_be_rewritten_until_it_is_disposed()
    {
        var c = Config();
        using (var b = ExecutionBinding.Establish(c, new Hashtable()))
            Assert.Throws<IOException>(() => File.WriteAllBytes(c.Git.Path, [9]));
        File.WriteAllBytes(c.Git.Path, [9]);                                                              // released after the process binding ends
    }

    [Fact]
    public void A_forbidden_start_variable_another_host_or_a_changed_pin_refuses_the_binding()
    {
        Assert.Equal(("Evidence.environment_mismatch", "Environment.startup_policy"),
            (Refused(() => ExecutionBinding.Establish(Config(), new Hashtable { ["CoreClr_Enable_Profiling"] = "1" })).Code, Refused(() => ExecutionBinding.Establish(Config(), new Hashtable { ["CORECLR_PROFILER_PATH"] = "x" })).Cause));
        Assert.Equal("Execution.host_unpinned", Refused(() => ExecutionBinding.Establish(Config(), new Hashtable(), processPath: Path.Combine(BuildRoot, "tools", "StarPlayer.EvidenceVerifier", "bin", "Release", "net8.0", "StarPlayer.EvidenceVerifier.exe"))).Cause);
        Assert.Equal("Execution.pin_mismatch", Refused(() => ExecutionBinding.Establish(Config(dotnetSha: new string('0', 64)), new Hashtable())).Cause);
    }

    // ---------------------------------------------------------------- T3 / T4 / G4-01 running verifier, SimHarness, loaded files

    [Fact]
    public void A_moved_verifier_a_foreign_simharness_copy_or_in_use_assembly_no_matching_entry_an_unpinned_folder_file_or_an_unpinned_runtime_load_refuses()
    {
        string Bind(JObject build, string? verifierSha = null, System.Reflection.Assembly? simHarness = null)
        {
            using var b = ExecutionBinding.Establish(Config(), new Hashtable());
            return Refused(() => b.BindBuild(build, Verifiers(verifierSha ?? Sha(VerifierDll)), BuildRoot, simHarness)).Cause;
        }

        Assert.Equal("Execution.verifier_location", Bind(Provenance(verifierRel: "tools/StarPlayer.EvidenceVerifier/bin/Release/net8.0/StarPlayer.EvidenceVerifier.dll")));
        Assert.Equal("Execution.simharness_copy", Bind(Provenance(simHarnessSha: new string('a', 64))));
        Assert.Equal("Execution.simharness_load", Bind(Provenance(), simHarness: typeof(Assert).Assembly));                        // the copy has the bytes, but another assembly is in use
        Assert.Equal("Execution.verifier_entry", Bind(Provenance(), verifierSha: new string('b', 64)));
        Assert.Equal("Execution.unpinned_file", Bind(Provenance(skip: rel => rel.EndsWith("/testhost.exe", StringComparison.Ordinal))));
        Assert.Equal("Execution.unpinned_load", Bind(Provenance()));                                                               // the shared framework is not pinned: no root is trusted (G4-01)
    }

    [Fact]
    public void In_an_isolated_process_everything_loaded_and_pinned_binds_and_an_unpinned_windows_or_runtime_module_a_byte_load_another_context_or_a_duplicate_refuses()
    {
        var baseline = Child("g4-binding", "baseline");
        Assert.True(baseline.Exit == 0, baseline.Output);
        foreach (var (scenario, text) in new[] { ("windows-module", "Pin되지 않은 모듈 'C:\\WINDOWS"), ("runtime-module", "Pin되지 않은 모듈 'C:\\Program Files\\dotnet"), ("byte-load", "Assembly.Load(byte[]"), ("alc", "'g4-other'"),
            ("duplicate", "이름이 같은 어셈블리 'StarPlayer.SimHarness'") })
        {
            var (exit, output) = Child("g4-binding", scenario);
            Assert.True(exit == 3 && output.Contains(text, StringComparison.Ordinal), $"{scenario}: exit {exit}: {output}");
        }
    }

    [Fact]
    public void A_held_folder_refuses_an_extra_changed_or_missing_file_accepts_a_listed_unpinned_output_and_blocks_writes_while_held()
    {
        var root = Temp("tree");
        var dir = Path.Combine(root, "out");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "a.dll"), [1]);
        File.WriteAllBytes(Path.Combine(dir, "a.pdb"), [2]);
        JObject Row(string rel, byte[] b) => J.O(("role", J.S("DEPENDENCY")), ("relative_path", J.S(rel)), ("raw_sha256", J.S(Archive.Sha(b))), ("byte_length", J.N(b.Length)));
        var build = J.O(("artifacts", J.A(Row("out/a.dll", [1]))));
        Assert.Equal("Execution.unpinned_file", Refused(() => ExecutionBinding.HoldTree(dir, build, root, [], "DEPENDENCY")).Cause);
        Assert.Equal("Execution.artifact_missing", Refused(() => ExecutionBinding.HoldTree(dir, J.O(("artifacts", J.A(Row("out/a.dll", [1]), Row("out/sub/b.dll", [3])))), root, ["out/a.pdb"], "DEPENDENCY")).Cause);
        var held = ExecutionBinding.HoldTree(dir, build, root, ["out/a.pdb"], "DEPENDENCY");
        Assert.Throws<IOException>(() => File.WriteAllBytes(Path.Combine(dir, "a.dll"), [7]));
        foreach (var h in held)
            h.Handle.Dispose();
        File.WriteAllBytes(Path.Combine(dir, "a.dll"), [7]);
        Assert.Equal("Execution.artifact_changed", Refused(() => ExecutionBinding.HoldTree(dir, build, root, ["out/a.pdb"], "DEPENDENCY")).Cause);
    }

    // ---------------------------------------------------------------- G4-03 E2-1 first, one C0

    private (TestOnlyTrust Trust, SqliteRegistry Registry, Production Production) World()
    {
        var trust = new TestOnlyTrust(w);
        return (trust, w.Own(trust.Open(RegistryRole.VerifierService, TestOnlyTrust.Verifier)), w.ProducerFor(trust));
    }

    [Fact]
    public void A_session_keeps_the_initial_c0_refuses_a_change_during_bootstrap_and_a_binding_needs_both_the_initial_c0_and_the_provenance_verifier()
    {
        var (trust, registry, production) = World();
        var c0 = ExecutionEnvironment.Capture();
        CertificationSession Open(ExecutionBinding? b = null, EnvironmentSnapshot? initial = null, string? archive = null) =>
            Certification.Open(trust.Anchor, registry, trust.Approval.Id, trust.Approval.RawSha256, "auth-test-1", w.Root, archive ?? production.ArchiveRoot, b, initial);
        Certification.VariablesSeam = () => new Hashtable();
        try
        {
            Assert.Same(c0, Open(initial: c0).C0);                                                                    // the entry point's C0 is the session's C0 (no new baseline)
            Certification.CaptureSeam = () => c0 with { EnvironmentDigest = new string('9', 64) };                   // the environment changed after E2-1
            Assert.Equal("Environment.c1_differs", Refused(() => Open(initial: c0)).Cause);
            Certification.CaptureSeam = null;
            var config = Config() with { BuildRoot = w.Root, ArchiveRoot = production.ArchiveRoot };
            using var b = ExecutionBinding.Establish(config, new Hashtable());
            Assert.Equal("Execution.initial_capture_missing", Refused(() => Open(b)).Cause);
            Assert.Equal("Execution.archive_root", Refused(() => Open(b, c0, Path.Combine(w.Root, "elsewhere"))).Cause);
            Assert.Equal("Execution.verifier_location", Refused(() => Open(b, c0)).Cause);
        }
        finally
        {
            Certification.VariablesSeam = null;
            Certification.CaptureSeam = null;
        }
    }

    // ---------------------------------------------------------------- T2 / G4-02 child runner and exit code

    private const string Project = "StarPlayer.Domain.Tests";
    private static readonly string[] Cases = [Project + "|" + Project + ".X.Y"];

    private TestRunner Fake(string scenario) => TestRunner.FromRuntime(BuildRoot) with { LeadingArguments = Exec("g4-fake-dotnet", scenario) };

    private string Run(TestRunner runner, CancellationToken cancel = default) => Refused(() => TestProfiles.Run(Project, "x.dll", Cases, Temp("work"), runner, cancel)).Cause;

    [Fact]
    public void The_child_runner_is_an_absolute_dotnet_in_the_build_root_and_a_name_or_a_start_failure_is_refused()
    {
        var runner = TestRunner.FromRuntime(BuildRoot);
        Assert.True(Path.IsPathFullyQualified(runner.Dotnet) && File.Exists(runner.Dotnet));
        Assert.Equal("dotnet.exe", Path.GetFileName(runner.Dotnet), ignoreCase: true);
        Assert.Equal("TestProfile.runner", Run(runner with { Dotnet = "dotnet" }));
        var notAnExe = Path.Combine(Temp("exe"), "dotnet.txt");
        File.WriteAllText(notAnExe, "x");
        Assert.Equal("TestProfile.start_failed", Run(runner with { Dotnet = notAnExe }));
    }

    [Fact]
    public void Only_exit_0_with_a_passing_trx_or_exit_1_with_failed_cases_is_a_result_and_every_other_combination_is_an_infrastructure_failure()
    {
        Assert.Equal([new TestCaseResult(Cases[0], "Passed")], TestProfiles.Run(Project, "x.dll", Cases, Temp("work"), Fake("pass")));
        Assert.Equal([new TestCaseResult(Cases[0], "Failed")], TestProfiles.Run(Project, "x.dll", Cases, Temp("work"), Fake("fail")));     // a real case failure stays a FAIL case
        Assert.Equal("TestProfile.exit_code", Run(Fake("exit1-passed")));
        Assert.Equal("TestProfile.trx", Run(Fake("exit1-none")));
        Assert.Equal("TestProfile.trx", Run(Fake("exit0-none")));
        Assert.Equal("TestProfile.exit_inconsistent", Run(Fake("exit0-failed")));
        Assert.Equal("TestProfile.exit_code", Run(Fake("exit3-passed")));
        Assert.Equal("TestProfile.trx", Run(Fake("two-trx")));
    }

    [Fact]
    public void A_run_past_its_time_limit_is_a_timeout_and_a_cancelled_run_is_a_cancellation()
    {
        Assert.Equal("TestProfile.timeout", Run(Fake("sleep") with { Timeout = TimeSpan.FromSeconds(2) }));
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        Assert.Equal("TestProfile.cancelled", Run(Fake("sleep"), cancel.Token));
    }

    // ---------------------------------------------------------------- G4-FIX-01 TRX run-level state

    [Fact]
    public void A_run_level_error_in_the_trx_is_an_infrastructure_failure_even_with_failed_cases_and_a_plain_case_failure_stays_a_fail_result()
    {
        // a normal case failure is still a result with a FAIL case, and the Evidence keeps it as FAIL
        var failed = TestProfiles.Run(Project, "x.dll", Cases, Temp("work"), Fake("fail"));
        Assert.Equal([new TestCaseResult(Cases[0], "Failed")], failed);
        var profile = J.O(("profile_id", J.S("p")), ("source_git_sha", J.S("s")), ("checks", J.A(J.O(("check_id", J.S("c")), ("cases", J.A(J.S(Cases[0])))))));
        Assert.False(TestProfiles.AllPass(TestProfiles.Evidence(profile, "sha", failed)["c"]));

        Assert.Equal("TestProfile.run_error", Run(Fake("fail-run-error")));      // failed cases + run error: infrastructure, not a FAIL result
        Assert.Equal("TestProfile.run_error", Run(Fake("pass-run-error")));      // exit 1, only passed cases, run error
        Assert.Equal("TestProfile.run_error", Run(Fake("exit0-run-error")));     // exit 0 + run error
        Assert.Equal("TestProfile.run_error", Run(Fake("run-warning")));         // any RunInfo (a normal run writes none)
        Assert.Equal("TestProfile.run_error", Run(Fake("error-counter")));
        Assert.Equal("TestProfile.run_error", Run(Fake("aborted")));
        Assert.Equal("TestProfile.exit_inconsistent", Run(Fake("exit0-outcome-failed")));
        foreach (var shape in new[] { "no-summary", "two-summaries", "no-counters", "bad-counter", "counter-mismatch", "no-outcome", "other-version", "not-xml", "case-no-outcome" })
            Assert.True(Run(Fake(shape)) == "TestProfile.trx", shape);
    }

    // ---------------------------------------------------------------- G4-FIX-02 one time budget for the exit and both pipes

    /// <summary>Runs a pipe-holding scenario; returns the refusal cause, the elapsed time and whether the holder is gone afterwards (it is killed here if not, so no test leaves it behind).</summary>
    private (string Cause, TimeSpan Elapsed, bool HolderGone) Held(string scenario, TimeSpan limit, CancellationToken cancel = default)
    {
        string? pidFile = null;
        var runner = Fake(scenario) with { Timeout = limit, BeforeChild = (_, results) => pidFile = Path.Combine(results, "holder.pid") };
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var cause = Run(runner, cancel);
        clock.Stop();
        Assert.True(File.Exists(pidFile), $"{scenario}: the holder never started");
        var pid = int.Parse(File.ReadAllText(pidFile!), System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            using var holder = System.Diagnostics.Process.GetProcessById(pid);
            if (holder.WaitForExit(10_000))
                return (cause, clock.Elapsed, true);
            holder.Kill();
            return (cause, clock.Elapsed, false);
        }
        catch (ArgumentException)
        {
            return (cause, clock.Elapsed, true);     // no such process any more
        }
    }

    [Fact]
    public void A_process_left_holding_stdout_or_stderr_after_the_child_exits_is_a_timeout_and_is_killed_with_the_tree()
    {
        foreach (var scenario in new[] { "hold-both", "hold-stdout", "hold-stderr" })
        {
            var (cause, elapsed, gone) = Held(scenario, TimeSpan.FromSeconds(5));
            Assert.True(cause == "TestProfile.timeout", $"{scenario}: {cause}");     // a valid passing TRX and exit 0 are not enough while a pipe is open
            Assert.True(elapsed < TimeSpan.FromSeconds(60), $"{scenario}: {elapsed}");
            Assert.True(gone, $"{scenario}: the holder survived");
        }
    }

    [Fact]
    public void A_cancellation_while_a_pipe_is_held_is_a_cancellation_and_kills_the_holder()
    {
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (cause, elapsed, gone) = Held("hold-both", TimeSpan.FromMinutes(30), cancel.Token);
        Assert.Equal("TestProfile.cancelled", cause);
        Assert.True(elapsed < TimeSpan.FromSeconds(60), elapsed.ToString());
        Assert.True(gone);
    }

    [Fact]
    public void Large_interleaved_stdout_and_stderr_are_drained_without_blocking_the_child()
    {
        // ~8 MB on each pipe: a sequential reader would leave the child blocked on the other pipe until the limit
        Assert.Equal([new TestCaseResult(Cases[0], "Passed")], TestProfiles.Run(Project, "x.dll", Cases, Temp("work"), Fake("loud") with { Timeout = TimeSpan.FromMinutes(2) }));
    }

    [Fact]
    public void A_real_child_test_run_passes_while_its_whole_folder_is_held_against_replacement()
    {
        const string caseId = Project + "|" + Project + ".GameRulesCalledPitchModeTests.The_legacy_content_hash_is_the_value_computed_before_the_pitch_zone_fields_existed";
        var assembly = Path.Combine(BuildRoot, "tests", Project, "bin", "Release", "net8.0", Project + ".dll");
        var dir = Path.GetDirectoryName(assembly)!;
        var rows = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Select(f =>
        {
            var bytes = File.ReadAllBytes(f);
            return (JNode)J.O(("role", J.S(f == assembly ? "TEST_ASSEMBLY" : "DEPENDENCY")), ("relative_path", J.S(Path.GetRelativePath(BuildRoot, f).Replace('\\', '/'))),
                ("raw_sha256", J.S(Archive.Sha(bytes))), ("byte_length", J.N(bytes.Length)));
        }).ToList();
        var held = ExecutionBinding.HoldTree(dir, J.O(("artifacts", J.A(rows))), BuildRoot, [], "TEST_ASSEMBLY", "DEPENDENCY");
        try
        {
            Assert.Equal([new TestCaseResult(caseId, "Passed")], TestProfiles.Run(Project, assembly, [caseId], Temp("work"), TestRunner.FromRuntime(BuildRoot)));
            Assert.Throws<IOException>(() => File.OpenWrite(assembly).Dispose());
        }
        finally
        {
            foreach (var h in held)
                h.Handle.Dispose();
        }
    }

    // ---------------------------------------------------------------- G4-04 pinned results tree and the one TRX

    [Fact]
    public void The_trx_read_is_the_object_checked_it_cannot_be_rewritten_moved_or_deleted_after_the_check_and_the_tree_cannot_be_moved_during_the_run()
    {
        var replaced = false;
        var runner = Fake("pass") with
        {
            BeforeChild = (root, results) =>
            {
                Assert.ThrowsAny<Exception>(() => Directory.Move(results, results + "-moved"));
                Assert.ThrowsAny<Exception>(() => Directory.Move(root, root + "-moved"));                             // a parent cannot be replaced while pinned
                Assert.True(Directory.Exists(results) && !Directory.Exists(root + "-moved"));
            },
            AfterTrxOpen = trx =>
            {
                Assert.ThrowsAny<IOException>(() => File.WriteAllText(trx, "<forged/>"));
                Assert.ThrowsAny<Exception>(() => File.Move(trx, trx + ".old"));
                Assert.ThrowsAny<Exception>(() => File.Delete(trx));
                replaced = true;
            },
        };
        Assert.Equal([new TestCaseResult(Cases[0], "Passed")], TestProfiles.Run(Project, "x.dll", Cases, Temp("work"), runner));
        Assert.True(replaced);
    }

    [Fact]
    public void A_work_root_that_is_a_junction_or_that_another_account_can_write_and_a_trx_another_account_can_write_are_refused_while_a_protected_tree_is_read()
    {
        var target = Temp("target");
        var link = Path.Combine(w.Root, $"g4-junction-{Guid.NewGuid():N}");
        using (var mk = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"") { UseShellExecute = false, CreateNoWindow = true })!)
            mk.WaitForExit();
        try
        {
            Assert.True(Directory.Exists(link));
            Assert.Equal("TestProfile.results_directory", Refused(() => TestProfiles.Run(Project, "x.dll", Cases, link, Fake("pass"))).Cause);
        }
        finally
        {
            Directory.Delete(link);                                                                               // the junction itself, not its target
        }

        var guarded = Fake("pass") with { RequireProtectedWorkTree = true };
        Assert.Equal([new TestCaseResult(Cases[0], "Passed")], TestProfiles.Run(Project, "x.dll", Cases, Protected("protected"), guarded));
        Assert.Equal("TestProfile.results_acl", Refused(() => TestProfiles.Run(Project, "x.dll", Cases, Temp("shared"), guarded)).Cause);     // this PC's %TEMP% tree grants other writers
        Assert.Equal("TestProfile.results_acl", Refused(() => TestProfiles.Run(Project, "x.dll", Cases, Protected("trx"), Fake("trx-everyone") with { RequireProtectedWorkTree = true })).Cause);

        var open = Protected("open");
        var info = new DirectoryInfo(open);
        var acl = info.GetAccessControl();
        acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.Write, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
        info.SetAccessControl(acl);
        Assert.Equal("TestProfile.results_acl", Refused(() => TestProfiles.Run(Project, "x.dll", Cases, open, guarded)).Cause);
    }

    // ---------------------------------------------------------------- T1 / G4-03 CLI order

    [Fact]
    public void The_cli_takes_no_anchor_root_or_pin_and_needs_every_evidence_option_exactly_once()
    {
        string?[] Error(params string[] a) => [OfficialEntry.Parse("certify-family", a).Error];
        string[] ok = ["--family", "fc3-cal", "--approval-record-id", "a", "--approval-record-sha256", "h", "--authorization-id", "x", "--document", "{}"];
        Assert.Null(OfficialEntry.Parse("certify-family", ok).Error);
        Assert.NotNull(Error([.. ok, "--anchor", "C:\\a.json"])[0]);
        Assert.NotNull(Error([.. ok, "--family", "fc3-int"])[0]);
        Assert.NotNull(Error(ok[..^2])[0]);
        Assert.NotNull(Error(["--family", "fc3-x", .. ok[2..]])[0]);
        Assert.Equal(OfficialEntry.Usage, OfficialEntry.Run("certify-phase", ["--calibration", "{}"], TextWriter.Null, new StringWriter()));
    }

    [Fact]
    public void The_cli_refuses_in_order_e2_1_then_config_then_start_environment_then_anchor_and_never_reaches_the_registry()
    {
        string[] args = ["--family", "fc3-cal", "--approval-record-id", "a", "--approval-record-sha256", new string('0', 64), "--authorization-id", "x", "--document", "{}"];
        IEvidenceRegistry Never(TrustAnchorV1 _) => throw new InvalidOperationException("registry reached");
        string Cli(OfficialSeams s)
        {
            var e = new StringWriter();
            Assert.Equal(OfficialEntry.Refused, OfficialEntry.Run("certify-family", args, TextWriter.Null, e, s with { Acl = AclOk, Elevated = NotElevated, Connect = Never }));
            return e.ToString();
        }

        var config = WriteConfig();                                                                                  // its anchor does not exist
        Assert.Contains("Environment.forbidden_variable", Cli(new OfficialSeams(ConfigPath: Path.Combine(w.Root, "absent.json"), Variables: new Hashtable { ["DOTNET_NOLOGO"] = "1" })));
        Assert.Contains("Environment.forbidden_variable", Cli(new OfficialSeams(ConfigPath: config, Variables: new Hashtable { ["DOTNET_NOLOGO"] = "1" })));   // E2-1 before the anchor error
        Assert.Contains("Trust.install_config_missing", Cli(new OfficialSeams(ConfigPath: Path.Combine(w.Root, "absent.json"), Variables: new Hashtable())));
        Assert.Contains("Environment.startup_policy", Cli(new OfficialSeams(ConfigPath: config, Variables: new Hashtable { ["VSTEST_HOST_DEBUG"] = "1" })));
        Assert.Contains("Trust.anchor_missing", Cli(new OfficialSeams(ConfigPath: config, Variables: new Hashtable())));
    }

    // ---------------------------------------------------------------- G4-ENV-01: VSTEST_ is a required start prefix (OWNER 2026-10-31)

    private string WithPrefixes(string csv) => WriteConfig(o => JEditMembers.With(o, "startup_forbidden_prefixes", J.A(csv.Length == 0 ? [] : csv.Split(',').Select(p => (JNode)J.S(p)))));

    [Theory]
    [InlineData("VSTEST_")]
    [InlineData("CORECLR_,VSTEST_")]
    [InlineData("CORECLR_,OWNER_EXTRA_,VSTEST_")]
    public void A_config_naming_the_exact_vstest_prefix_is_read_with_its_prefixes_unchanged(string csv)
    {
        var c = InstallConfigReader.Read(WithPrefixes(csv), AclOk, NotElevated);
        Assert.Equal(csv.Split(','), c.StartupForbiddenPrefixes);                                    // nothing added, removed or rewritten
    }

    [Theory]
    [InlineData("")]
    [InlineData("CORECLR_")]
    [InlineData("CORECLR_,VSTEST_CONSOLE_PATH")]                                                     // one variable name does not stand in for the prefix
    [InlineData("CORECLR_,vstest_")]                                                                 // non-normative spelling
    [InlineData("VSTEST")]
    [InlineData("VSTEST_,vstest_")]
    [InlineData("VSTEST_,VsTest_")]
    public void A_config_without_the_exact_vstest_prefix_is_refused_as_an_approval_mismatch(string csv)
    {
        var e = Refused(() => InstallConfigReader.Read(WithPrefixes(csv), AclOk, NotElevated));
        Assert.Equal((EvidenceCodes.ApprovalMismatch, "Trust.install_config_policy"), (e.Code, e.Cause));
    }

    [Theory]
    [InlineData("VSTEST_,VSTEST_")]
    [InlineData("VSTEST_,CORECLR_")]
    [InlineData("CORECLR-,VSTEST_")]
    public void The_prefix_list_keeps_its_schema_order_and_duplicate_rules_before_the_vstest_rule(string csv) =>
        Assert.Equal(EvidenceCodes.SchemaInvalid, Refused(() => InstallConfigReader.Read(WithPrefixes(csv), AclOk, NotElevated)).Code);

    [Theory]
    [InlineData("VSTEST_CONSOLE_PATH")]
    [InlineData("vStEsT_ConSoLe_PaTh")]
    [InlineData("VSTEST_HOST_DEBUG")]
    [InlineData("VSTEST_DISABLE_ARTIFACTS_POSTPROCESSING")]
    public void A_vstest_variable_in_any_case_refuses_the_start_of_a_binding_built_from_the_read_config(string name)
    {
        var config = InstallConfigReader.Read(WriteConfig(), AclOk, NotElevated);
        var e = Refused(() => ExecutionBinding.Establish(config, new Hashtable { [name] = "x" }));
        Assert.Equal((EvidenceCodes.EnvironmentMismatch, "Environment.startup_policy"), (e.Code, e.Cause));
        using var b = ExecutionBinding.Establish(config, new Hashtable { ["G4_ENV_KEEP"] = "1" });            // no forbidden variable: the binding is made
    }

    [Theory]
    [InlineData("certify-family")]
    [InlineData("certify-phase")]
    public void A_config_missing_vstest_or_a_vstest_start_variable_stops_the_cli_before_the_anchor_registry_and_any_test_profile_child(string command)
    {
        string[] args = command == "certify-family"
            ? ["--family", "fc3-cal", "--approval-record-id", "a", "--approval-record-sha256", new string('0', 64), "--authorization-id", "x", "--document", "{}"]
            : ["--approval-record-id", "a", "--approval-record-sha256", new string('0', 64), "--authorization-id", "x", "--calibration", "{}", "--integration", "{}", "--dominance", "{}", "--regression-proof", "{}"];
        IEvidenceRegistry Never(TrustAnchorV1 _) => throw new InvalidOperationException("registry reached");
        string Cli(string config, Hashtable variables)
        {
            var e = new StringWriter();
            Assert.Equal(OfficialEntry.Refused, OfficialEntry.Run(command, args, TextWriter.Null, e, new OfficialSeams(ConfigPath: config, Variables: variables) with { Acl = AclOk, Elevated = NotElevated, Connect = Never }));
            return e.ToString();
        }

        var missing = WithPrefixes("CORECLR_");
        Assert.Contains("Trust.install_config_policy", Cli(missing, new Hashtable()));
        Assert.Contains("Trust.install_config_policy", Cli(missing, new Hashtable { ["VSTEST_CONSOLE_PATH"] = "C:\\x\\vstest.console.dll" }));   // the config is refused first
        var good = WriteConfig();
        Assert.Contains("Environment.startup_policy", Cli(good, new Hashtable { ["vStEsT_ConSoLe_PaTh"] = "C:\\x\\vstest.console.dll" }));
        Assert.Contains("Trust.anchor_missing", Cli(good, new Hashtable()));                                                          // control: the same config passes both checks
        foreach (var config in new[] { missing, good })
            Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(config)!, "work")));                                     // no Test Profile results directory was ever made
    }

    [Fact]
    public void The_parents_vstest_variables_never_reach_the_test_profile_child_when_the_config_names_vstest()
    {
        var parent = new Dictionary<string, string> { ["vStEsT_ConSoLe_PaTh"] = "C:\\x\\vstest.console.dll", ["VSTEST_HOST_DEBUG"] = "1", ["VsTest_Diag"] = "x", ["G4_ENV_KEEP"] = "1" };
        List<string> Names(string prefixes)
        {
            var work = Protected("env-work");
            var (exit, output) = Child(parent, "g4-env-isolation", work, prefixes);
            Assert.True(exit == 0, output);
            Assert.Contains("result=StarPlayer.Domain.Tests|StarPlayer.Domain.Tests.X.Y:Passed", output);                           // the PASS result is unchanged
            var results = Assert.Single(Directory.GetDirectories(work));
            var lines = File.ReadAllLines(Path.Combine(results, "env.txt"));
            Assert.Contains($"TEMP={Path.Combine(results, "temp")}", lines, StringComparer.OrdinalIgnoreCase);                         // G4-TEMP protection kept
            return lines.Where(l => l.StartsWith("name=", StringComparison.Ordinal)).Select(l => l["name=".Length..]).ToList();
        }

        bool Vstest(string name) => name.StartsWith("VSTEST_", StringComparison.OrdinalIgnoreCase);
        var with = Names("CORECLR_,VSTEST_");
        Assert.DoesNotContain(with, Vstest);
        Assert.Contains("G4_ENV_KEEP", with);
        var without = Names("CORECLR_");                                                                                              // control: without the prefix all three are inherited
        foreach (var name in parent.Keys.Where(Vstest))
            Assert.Contains(name, without, StringComparer.OrdinalIgnoreCase);
    }
}
